using System;
using System.Collections.Generic;
using System.Globalization;

namespace ErenshorContracts
{
    // Unity-free policy for native-enemy discovery/catalog retention and deterministic kill-board
    // generation. Runtime scanning lives in ContractNativeEnemyRuntime; this file remains fully
    // testable without Erenshor assemblies.
    internal static class ContractCombatPolicy
    {
        internal const string NativeKillProgressKey = "kill_native";
        internal const string NativeKillProviderId = "builtin_combat";
        internal const int MaxEnemyCatalogRecords = 512;
        internal const int MaxGeneratedOffers = 128;
        internal const int MaxLevelDistance = 5;

        internal static bool IsUsableObservation(ContractEnemyObservation value)
        {
            return value != null &&
                !string.IsNullOrWhiteSpace(value.Zone) &&
                !string.IsNullOrWhiteSpace(value.EnemyName) &&
                value.MinLevel > 0 &&
                value.MaxLevel >= value.MinLevel &&
                value.Count > 0;
        }

        internal static int LevelDistance(int playerLevel, int minLevel, int maxLevel)
        {
            int player = Math.Max(1, playerLevel);
            int min = Math.Max(1, minLevel);
            int max = Math.Max(min, maxLevel);
            if (player < min) return min - player;
            if (player > max) return player - max;
            return 0;
        }

        internal static bool IsLevelAppropriate(int playerLevel, int minLevel, int maxLevel)
        {
            return LevelDistance(playerLevel, minLevel, maxLevel) <= MaxLevelDistance;
        }

        internal static int RepresentativeLevel(int minLevel, int maxLevel)
        {
            int min = Math.Max(1, minLevel);
            int max = Math.Max(min, maxLevel);
            return min + ((max - min) / 2);
        }

        internal static int ResolveTargetCount(string category, string seed)
        {
            // Compatibility helper for callers that do not carry observation evidence. Production
            // generation always uses ContractEnemyTargetPolicy with the real observed count/name.
            return ContractEnemyTargetPolicy.ResolveTargetCount(category, seed, "Generic Enemy", 4);
        }

        internal static int ResolveXpBasisPoints(string category, string seed)
        {
            uint hash = ContractCore.StableHash("xp|" + (seed ?? string.Empty));
            if (string.Equals(ContractCategory.Normalize(category), ContractCategory.Global, StringComparison.Ordinal))
                return 1200 + (int)(hash % 401u); // 12.00%-16.00%
            return 500 + (int)(hash % 201u);      // 5.00%-7.00%
        }

        internal static string BuildTitle(string category, string enemyName, string zone, bool exactNamedTarget)
        {
            string enemy = ContractCore.Clean(enemyName, 80);
            string targetZone = ContractCore.Clean(zone, 80);
            if (exactNamedTarget)
                return string.Equals(ContractCategory.Normalize(category), ContractCategory.Global, StringComparison.Ordinal) && targetZone.Length > 0
                    ? targetZone + " Bounty" : enemy + " Bounty";
            if (string.Equals(ContractCategory.Normalize(category), ContractCategory.Global, StringComparison.Ordinal))
                return targetZone.Length == 0 ? enemy + " Suppression" : targetZone + " Suppression";
            return enemy + " Cull";
        }

        internal static string BuildObjective(string enemyName, string zone, int targetCount, int observedCount)
        {
            string display = ContractEnemyTargetPolicy.BuildDisplayTarget(enemyName, targetCount, observedCount);
            return "Kill " + Math.Max(1, targetCount).ToString(CultureInfo.InvariantCulture) + " " +
                ContractCore.Clean(display, 80) + " in " + ContractCore.Clean(zone, 80) + ".";
        }

        internal static int MergeObservations(ContractDocument document, IList<ContractEnemyObservation> observations, long activePlaySeconds)
        {
            if (document == null || observations == null) return 0;
            int changed = 0;
            long seenAt = Math.Max(0L, activePlaySeconds);
            for (int i = 0; i < observations.Count; i++)
            {
                ContractEnemyObservation observation = observations[i];
                if (!IsUsableObservation(observation)) continue;
                string zone = ContractCore.Clean(observation.Zone, 128);
                string name = ContractCore.Clean(observation.EnemyName, 120);
                ContractEnemyRecord existing = FindEnemyRecord(document.EnemyCatalog, zone, name);
                if (existing == null)
                {
                    if (document.EnemyCatalog.Count >= MaxEnemyCatalogRecords)
                        EvictOldestEnemyRecord(document.EnemyCatalog);
                    existing = new ContractEnemyRecord();
                    existing.Zone = zone;
                    existing.EnemyName = name;
                    existing.MinLevel = Math.Max(1, observation.MinLevel);
                    existing.MaxLevel = Math.Max(existing.MinLevel, observation.MaxLevel);
                    existing.ObservedCount = Math.Max(1, observation.Count);
                    existing.LastSeenActiveSeconds = seenAt;
                    document.EnemyCatalog.Add(existing);
                    changed++;
                }
                else
                {
                    int min = Math.Max(1, observation.MinLevel);
                    int max = Math.Max(min, observation.MaxLevel);
                    int count = Math.Max(1, observation.Count);
                    bool levelsChanged = existing.MinLevel != min || existing.MaxLevel != max;
                    bool countChanged = existing.ObservedCount != count;
                    bool refreshSeenAt = seenAt >= existing.LastSeenActiveSeconds + 60L;
                    existing.MinLevel = min;
                    existing.MaxLevel = max;
                    existing.ObservedCount = count;
                    if (refreshSeenAt) existing.LastSeenActiveSeconds = seenAt;
                    if (levelsChanged || countChanged || refreshSeenAt) changed++;
                }
            }
            return changed;
        }

        internal static bool EnsureLocalCombatBoard(
            ContractDocument document,
            int revision,
            string boardZone,
            string profileKey,
            int playerLevel,
            int slotCount,
            IList<ContractEnemyObservation> currentObservations)
        {
            if (document == null || string.IsNullOrWhiteSpace(boardZone) || currentObservations == null || playerLevel <= 0) return false;
            int safeRevision = Math.Max(0, revision);
            string zone = ContractCore.Clean(boardZone, 128);

            // Local board identity is revision + playable zone. Preserve each zone's generated set
            // for the life of the revision so A -> B -> A returns the original A targets instead of
            // turning zoning into a reroll surface. Older revisions are bounded/pruned separately.
            bool changed = RemoveGeneratedOutsideRevision(document.GeneratedCombatOffers, ContractCategory.Local, safeRevision);
            if (HasGeneratedBoard(document.GeneratedCombatOffers, ContractCategory.Local, safeRevision, zone))
                return changed;

            List<ContractEnemyRecord> candidates = new List<ContractEnemyRecord>();
            for (int i = 0; i < currentObservations.Count; i++)
            {
                ContractEnemyObservation observation = currentObservations[i];
                if (!IsUsableObservation(observation) ||
                    !string.Equals(observation.Zone, zone, StringComparison.OrdinalIgnoreCase) ||
                    !IsLevelAppropriate(playerLevel, observation.MinLevel, observation.MaxLevel))
                    continue;
                ContractEnemyRecord record = new ContractEnemyRecord();
                record.Zone = zone;
                record.EnemyName = ContractCore.Clean(observation.EnemyName, 120);
                record.MinLevel = observation.MinLevel;
                record.MaxLevel = observation.MaxLevel;
                record.ObservedCount = Math.Max(1, observation.Count);
                candidates.Add(record);
            }
            if (candidates.Count == 0) return changed;

            AddGenerated(document, ContractCategory.Local, safeRevision, zone, zone,
                profileKey, playerLevel, slotCount, candidates);
            document.LocalCombatGenerationRevision = safeRevision; // legacy diagnostic/persistence only
            document.LocalCombatGenerationZone = zone;
            return true;
        }

        internal static bool EnsureGlobalCombatBoard(
            ContractDocument document,
            int revision,
            string currentZone,
            string profileKey,
            int playerLevel,
            int slotCount)
        {
            if (document == null || playerLevel <= 0) return false;
            int safeRevision = Math.Max(0, revision);
            if (document.GlobalCombatGenerationRevision == safeRevision) return false;

            bool hadGlobalGenerated = HasGenerated(document.GeneratedCombatOffers, ContractCategory.Global);
            bool globalGenerationStateChanged = hadGlobalGenerated || document.GlobalCombatGenerationRevision != -1;
            RemoveGenerated(document.GeneratedCombatOffers, ContractCategory.Global);
            document.GlobalCombatGenerationRevision = -1;
            List<ContractEnemyRecord> candidates = new List<ContractEnemyRecord>();
            for (int i = 0; i < document.EnemyCatalog.Count; i++)
            {
                ContractEnemyRecord record = document.EnemyCatalog[i];
                if (record == null || string.IsNullOrWhiteSpace(record.Zone) || string.IsNullOrWhiteSpace(record.EnemyName)) continue;
                if (string.Equals(record.Zone, currentZone ?? string.Empty, StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsLevelAppropriate(playerLevel, record.MinLevel, record.MaxLevel)) continue;
                candidates.Add(record);
            }
            if (candidates.Count == 0) return globalGenerationStateChanged;
            document.GlobalCombatGenerationRevision = safeRevision;
            AddGenerated(document, ContractCategory.Global, safeRevision, string.Empty, currentZone,
                profileKey, playerLevel, slotCount, candidates);
            return true;
        }

        internal static List<ContractTemplate> BuildGeneratedTemplates(ContractDocument document)
        {
            List<ContractTemplate> result = new List<ContractTemplate>();
            if (document == null) return result;
            for (int i = 0; i < document.GeneratedCombatOffers.Count; i++)
            {
                ContractGeneratedCombatOffer generated = document.GeneratedCombatOffers[i];
                if (generated == null || string.IsNullOrWhiteSpace(generated.TargetZone) ||
                    string.IsNullOrWhiteSpace(generated.EnemyName) || generated.TargetCount <= 0)
                    continue;

                string category = ContractCategory.Normalize(generated.Category);
                if (string.Equals(category, ContractCategory.Local, StringComparison.Ordinal) && generated.BoardRevision != document.LocalBoardRevision) continue;
                if (string.Equals(category, ContractCategory.Global, StringComparison.Ordinal) && generated.BoardRevision != document.GlobalBoardRevision) continue;
                int observedCount = FindObservedCount(document, generated.TargetZone, generated.EnemyName);
                bool exactNamedTarget = ContractEnemyTargetPolicy.IsLikelyExactNamedTarget(generated.EnemyName, observedCount);
                string stable = category + "|" + generated.BoardRevision.ToString(CultureInfo.InvariantCulture) + "|" +
                    generated.TargetZone + "|" + generated.EnemyName;
                ContractTemplate template = new ContractTemplate();
                template.ProviderId = NativeKillProviderId;
                template.TemplateId = "kill_" + ContractCore.StableHash(stable).ToString("x8", CultureInfo.InvariantCulture);
                template.Category = category;
                template.ZoneScope = string.Equals(category, ContractCategory.Local, StringComparison.Ordinal)
                    ? generated.TargetZone : "*";
                template.TargetZone = generated.TargetZone;
                template.Title = BuildTitle(category, generated.EnemyName, generated.TargetZone, exactNamedTarget);
                template.Description = BuildObjective(generated.EnemyName, generated.TargetZone, generated.TargetCount, observedCount);
                template.ProgressChannel = "native_combat";
                template.ProgressKey = NativeKillProgressKey;
                template.ContextFilter = generated.EnemyName;
                template.RewardXpBasisPoints = Math.Max(0, Math.Min(5000, generated.RewardXpBasisPoints));
                template.RewardGoldAmount = ContractRewardPolicy.ResolveCombatGoldAmount(category, generated.EnemyLevel, generated.TargetCount);
                template.RewardText = ContractRewardPolicy.DescribeReward(template.RewardGoldAmount, template.RewardXpBasisPoints);
                template.Target = generated.TargetCount;
                template.Priority = 1000;
                result.Add(template);
            }
            return result;
        }

        internal static int RecordQualifyingKill(ContractDocument document, string zone, string enemyName)
        {
            if (document == null || string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(enemyName)) return 0;
            string targetZone = zone.Trim();
            string targetName = NormalizeEnemyName(enemyName);
            if (targetName.Length == 0) return 0;

            int changed = 0;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active == null || active.IsComplete ||
                    !string.Equals(active.ProgressKey, NativeKillProgressKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(active.TargetZone ?? string.Empty, targetZone, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(NormalizeEnemyName(active.ContextFilter), targetName, StringComparison.OrdinalIgnoreCase)) continue;
                int before = active.Progress;
                active.Progress = Math.Min(active.Target, active.Progress + 1);
                if (active.Progress != before) changed++;
            }
            return changed;
        }

        internal static string NormalizeEnemyName(string value)
        {
            string text = ContractCore.Clean(value, 120);
            if (text.Length == 0) return string.Empty;
            if (text.StartsWith("A ", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2).Trim();
            else if (text.StartsWith("An ", StringComparison.OrdinalIgnoreCase)) text = text.Substring(3).Trim();
            else if (text.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) text = text.Substring(4).Trim();
            while (text.EndsWith(".", StringComparison.Ordinal) || text.EndsWith("!", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 1).TrimEnd();
            return text;
        }

        private static void AddGenerated(
            ContractDocument document,
            string category,
            int revision,
            string boardZone,
            string currentZone,
            string profileKey,
            int playerLevel,
            int slotCount,
            List<ContractEnemyRecord> candidates)
        {
            int safeSlots = Math.Max(1, Math.Min(ContractCore.MaxBoardSlots, slotCount));
            string seed = category + "|" + revision.ToString(CultureInfo.InvariantCulture) + "|" +
                (boardZone ?? string.Empty) + "|" + (currentZone ?? string.Empty) + "|" +
                (profileKey ?? "local") + "|" + Math.Max(1, playerLevel).ToString(CultureInfo.InvariantCulture);

            candidates.Sort(delegate(ContractEnemyRecord a, ContractEnemyRecord b)
            {
                bool aExact = ContractEnemyTargetPolicy.IsLikelyExactNamedTarget(a.EnemyName, a.ObservedCount);
                bool bExact = ContractEnemyTargetPolicy.IsLikelyExactNamedTarget(b.EnemyName, b.ObservedCount);
                int byKind = aExact.CompareTo(bExact); // repeatable/generic grind targets first
                if (byKind != 0) return byKind;
                int ad = LevelDistance(playerLevel, a.MinLevel, a.MaxLevel);
                int bd = LevelDistance(playerLevel, b.MinLevel, b.MaxLevel);
                int byDistance = ad.CompareTo(bd);
                if (byDistance != 0) return byDistance;
                // When level fit is equal, prefer enemy types the current/last authoritative
                // scan saw in greater numbers. This makes the board feel like a grind board
                // instead of selecting a rare one-off merely because its hash sorted first.
                int byAbundance = Math.Max(1, b.ObservedCount).CompareTo(Math.Max(1, a.ObservedCount));
                if (byAbundance != 0) return byAbundance;
                uint ah = ContractCore.StableHash(seed + "|" + a.Zone + "|" + a.EnemyName);
                uint bh = ContractCore.StableHash(seed + "|" + b.Zone + "|" + b.EnemyName);
                int byHash = ah.CompareTo(bh);
                if (byHash != 0) return byHash;
                int byZone = string.Compare(a.Zone, b.Zone, StringComparison.OrdinalIgnoreCase);
                if (byZone != 0) return byZone;
                return string.Compare(a.EnemyName, b.EnemyName, StringComparison.OrdinalIgnoreCase);
            });

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int take = Math.Min(safeSlots, candidates.Count);
            for (int i = 0; i < candidates.Count && document.GeneratedCombatOffers.Count < MaxGeneratedOffers && take > 0; i++)
            {
                ContractEnemyRecord candidate = candidates[i];
                string key = candidate.Zone + "|" + candidate.EnemyName;
                if (!seen.Add(key)) continue;
                string offerSeed = seed + "|" + key;
                ContractGeneratedCombatOffer generated = new ContractGeneratedCombatOffer();
                generated.Category = ContractCategory.Normalize(category);
                generated.BoardRevision = revision;
                generated.BoardZone = boardZone ?? string.Empty;
                generated.TargetZone = candidate.Zone;
                generated.EnemyName = candidate.EnemyName;
                generated.EnemyLevel = RepresentativeLevel(candidate.MinLevel, candidate.MaxLevel);
                generated.TargetCount = ContractEnemyTargetPolicy.ResolveTargetCount(category, offerSeed, candidate.EnemyName, candidate.ObservedCount);
                generated.RewardXpBasisPoints = ResolveXpBasisPoints(category, offerSeed);
                document.GeneratedCombatOffers.Add(generated);
                take--;
            }
        }

        internal static int FindObservedCount(ContractDocument document, string zone, string enemyName)
        {
            if (document == null) return 1;
            ContractEnemyRecord record = FindEnemyRecord(document.EnemyCatalog, zone, enemyName);
            return record == null ? 0 : Math.Max(1, record.ObservedCount);
        }

        internal static bool NormalizeGeneratedOfferForCurrentEvidence(ContractDocument document, ContractGeneratedCombatOffer offer)
        {
            if (document == null || offer == null || string.IsNullOrWhiteSpace(offer.TargetZone) || string.IsNullOrWhiteSpace(offer.EnemyName)) return false;
            int observed = FindObservedCount(document, offer.TargetZone, offer.EnemyName);
            if (observed <= 0) return false;
            int capped = ContractEnemyTargetPolicy.CapPersistedTargetCount(offer.Category, offer.EnemyName, observed, offer.TargetCount);
            if (capped == offer.TargetCount) return false;
            offer.TargetCount = capped;
            return true;
        }

        private static bool HasGeneratedBoard(List<ContractGeneratedCombatOffer> offers, string category, int revision, string boardZone)
        {
            string normalized = ContractCategory.Normalize(category);
            for (int i = 0; i < offers.Count; i++)
            {
                ContractGeneratedCombatOffer value = offers[i];
                if (value == null) continue;
                if (!string.Equals(ContractCategory.Normalize(value.Category), normalized, StringComparison.Ordinal)) continue;
                if (value.BoardRevision != revision) continue;
                if (!string.Equals(value.BoardZone ?? string.Empty, boardZone ?? string.Empty, StringComparison.OrdinalIgnoreCase)) continue;
                return true;
            }
            return false;
        }

        private static bool RemoveGeneratedOutsideRevision(List<ContractGeneratedCombatOffer> offers, string category, int revision)
        {
            bool changed = false;
            string normalized = ContractCategory.Normalize(category);
            for (int i = offers.Count - 1; i >= 0; i--)
            {
                ContractGeneratedCombatOffer value = offers[i];
                if (value == null)
                {
                    offers.RemoveAt(i);
                    changed = true;
                    continue;
                }
                if (string.Equals(ContractCategory.Normalize(value.Category), normalized, StringComparison.Ordinal) && value.BoardRevision != revision)
                {
                    offers.RemoveAt(i);
                    changed = true;
                }
            }
            return changed;
        }

        private static ContractEnemyRecord FindEnemyRecord(List<ContractEnemyRecord> records, string zone, string enemyName)
        {
            for (int i = 0; i < records.Count; i++)
            {
                ContractEnemyRecord existing = records[i];
                if (existing != null &&
                    string.Equals(existing.Zone, zone, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.EnemyName, enemyName, StringComparison.OrdinalIgnoreCase))
                    return existing;
            }
            return null;
        }

        private static bool HasGenerated(List<ContractGeneratedCombatOffer> offers, string category)
        {
            string normalized = ContractCategory.Normalize(category);
            for (int i = 0; i < offers.Count; i++)
            {
                ContractGeneratedCombatOffer value = offers[i];
                if (value != null && string.Equals(ContractCategory.Normalize(value.Category), normalized, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void RemoveGenerated(List<ContractGeneratedCombatOffer> offers, string category)
        {
            string normalized = ContractCategory.Normalize(category);
            for (int i = offers.Count - 1; i >= 0; i--)
            {
                ContractGeneratedCombatOffer value = offers[i];
                if (value == null || string.Equals(ContractCategory.Normalize(value.Category), normalized, StringComparison.Ordinal))
                    offers.RemoveAt(i);
            }
        }

        private static void EvictOldestEnemyRecord(List<ContractEnemyRecord> records)
        {
            if (records.Count == 0) return;
            int oldestIndex = 0;
            long oldest = records[0] == null ? long.MinValue : records[0].LastSeenActiveSeconds;
            for (int i = 1; i < records.Count; i++)
            {
                long value = records[i] == null ? long.MinValue : records[i].LastSeenActiveSeconds;
                if (value < oldest)
                {
                    oldest = value;
                    oldestIndex = i;
                }
            }
            records.RemoveAt(oldestIndex);
        }
    }
}
