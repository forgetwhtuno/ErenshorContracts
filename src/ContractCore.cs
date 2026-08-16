using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ErenshorContracts
{
    internal static class ContractCore
    {
        internal const int MaxActiveContracts = 6;
        internal const int MaxBoardSlots = 6;
        internal const int RoadCheckAwaySeconds = 8 * 60;
        internal const int PerimeterSweepHomeSeconds = 10 * 60;
        internal const int LocalCircuitAwayZones = 2;
        internal const int GlobalExpeditionSeconds = 45 * 60;
        internal const int GlobalExpeditionZones = 5;

        internal static ContractTemplate BuildPatrolTemplate(int minutes)
        {
            int safeMinutes = Math.Max(5, Math.Min(60, minutes));
            return NewTemplate(
                "builtin", "local_patrol", ContractCategory.Local, "*",
                "Local Patrol",
                "Spend " + safeMinutes.ToString(CultureInfo.InvariantCulture) + " active minutes adventuring in this zone.",
                "builtin", "zone_seconds", string.Empty,
                safeMinutes * 60, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "local_patrol"));
        }

        internal static ContractTemplate BuildRoadCheckTemplate()
        {
            return NewTemplate(
                "builtin", "road_check", ContractCategory.Local, "*",
                "Road Check",
                "Leave this zone, spend eight active minutes adventuring elsewhere, then return.",
                "builtin", "leave_return", string.Empty,
                1, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "road_check"));
        }

        internal static ContractTemplate BuildPerimeterSweepTemplate()
        {
            return NewTemplate(
                "builtin", "local_perimeter", ContractCategory.Local, "*",
                "Perimeter Sweep",
                "Spend ten active minutes adventuring in this zone, then cross into another playable zone.",
                "builtin", "home_then_depart", string.Empty,
                2, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "local_perimeter"));
        }

        internal static ContractTemplate BuildWayfarerTemplate()
        {
            return NewTemplate(
                "builtin", "wayfarer", ContractCategory.Local, "*",
                "Wayfarer",
                "Enter three different zones away from this contract's origin.",
                "builtin", "visit_unique_zone", string.Empty,
                3, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "wayfarer"));
        }

        internal static ContractTemplate BuildLocalCircuitTemplate()
        {
            return NewTemplate(
                "builtin", "local_circuit", ContractCategory.Local, "*",
                "Local Circuit",
                "Enter two different zones away from this contract's origin, then return to the origin.",
                "builtin", "local_circuit", string.Empty,
                LocalCircuitAwayZones + 1, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "local_circuit"));
        }

        internal static ContractTemplate BuildGlobalPatrolTemplate(int minutes)
        {
            int safeMinutes = Math.Max(30, Math.Min(120, minutes));
            return NewTemplate(
                "builtin", "global_patrol", ContractCategory.Global, "*",
                "Long Watch",
                "Accumulate " + safeMinutes.ToString(CultureInfo.InvariantCulture) + " active minutes of adventuring across playable zones.",
                "builtin", "global_seconds", string.Empty,
                safeMinutes * 60, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Global, "global_patrol"));
        }

        internal static ContractTemplate BuildGlobalWayfarerTemplate()
        {
            return NewTemplate(
                "builtin", "global_wayfarer", ContractCategory.Global, "*",
                "Grand Tour",
                "Enter eight different playable zones after accepting this global contract.",
                "builtin", "global_visit_unique_zone", string.Empty,
                8, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Global, "global_wayfarer"));
        }

        internal static ContractTemplate BuildGlobalLocalCompletionsTemplate()
        {
            return NewTemplate(
                "builtin", "global_local_completions", ContractCategory.Global, "*",
                "Contract Regular",
                "After accepting this contract, successfully claim four Local contracts. With the default three Local slots, this necessarily spans at least one Local refresh.",
                "builtin", "local_completion_claimed", string.Empty,
                4, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Global, "global_local_completions"));
        }

        internal static ContractTemplate BuildGlobalExpeditionTemplate()
        {
            return NewTemplate(
                "builtin", "global_expedition", ContractCategory.Global, "*",
                "Expedition",
                "Accumulate forty-five active minutes of adventuring and enter five different playable zones after accepting.",
                "builtin", "global_expedition", string.Empty,
                2, 0,
                ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Global, "global_expedition"));
        }

        internal static ContractTemplate FromRegistration(ContractTemplateRegistration value)
        {
            if (value == null) return null;
            // Provider API v1 remains local and record-only. Contracts cannot infer a provider
            // objective's effort/reward balance from its registration metadata.
            ContractTemplate template = NewTemplate(
                Clean(value.ProviderId, 64),
                Clean(value.TemplateId, 64),
                ContractCategory.Local,
                string.IsNullOrWhiteSpace(value.ZoneScope) ? "*" : Clean(value.ZoneScope, 96),
                Clean(value.Title, 120),
                Clean(value.Description, 320),
                Clean(value.ProgressChannel, 64),
                Clean(value.ProgressKey, 64),
                Clean(value.ContextFilter, 160),
                Math.Max(1, Math.Min(1000000, value.Target)),
                Math.Max(-1000, Math.Min(1000, value.Priority)),
                0);
            template.RewardText = string.IsNullOrWhiteSpace(value.RewardText)
                ? "Completion recorded locally; provider supplied no native reward."
                : Clean(value.RewardText, 200);
            if (!string.IsNullOrWhiteSpace(template.ZoneScope) && template.ZoneScope != "*")
                template.TargetZone = Clean(template.ZoneScope, 128);
            return template;
        }

        internal static List<ContractOffer> BuildOffers(
            string category,
            int boardRevision,
            string zone,
            string profileKey,
            IEnumerable<ContractTemplate> templates,
            ContractDocument document,
            int slotCount)
        {
            List<ContractOffer> result = new List<ContractOffer>();
            if (document == null || templates == null) return result;

            string normalizedCategory = ContractCategory.Normalize(category);
            if (string.Equals(normalizedCategory, ContractCategory.Local, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(zone))
                return result;

            int safeSlots = Math.Max(1, Math.Min(MaxBoardSlots, slotCount));
            int safeRevision = Math.Max(0, boardRevision);
            string safeProfile = string.IsNullOrWhiteSpace(profileKey) ? "local" : profileKey.Trim();
            string seed = normalizedCategory + "|" + safeRevision.ToString(CultureInfo.InvariantCulture) + "|" +
                          (string.Equals(normalizedCategory, ContractCategory.Local, StringComparison.Ordinal) ? (zone ?? string.Empty) : "global") +
                          "|" + safeProfile;

            Dictionary<string, ContractTemplate> unique = new Dictionary<string, ContractTemplate>(StringComparer.OrdinalIgnoreCase);
            foreach (ContractTemplate raw in templates)
            {
                if (raw == null) continue;
                if (!string.Equals(ContractCategory.Normalize(raw.Category), normalizedCategory, StringComparison.Ordinal)) continue;
                if (string.Equals(normalizedCategory, ContractCategory.Local, StringComparison.Ordinal) && !MatchesZone(raw.ZoneScope, zone)) continue;
                string key = raw.ProviderId + "|" + raw.TemplateId;
                ContractTemplate existing;
                if (!unique.TryGetValue(key, out existing) || raw.Priority > existing.Priority)
                    unique[key] = raw;
            }

            List<ContractTemplate> eligible = new List<ContractTemplate>(unique.Values);
            eligible.Sort(delegate(ContractTemplate a, ContractTemplate b)
            {
                int byPriority = b.Priority.CompareTo(a.Priority);
                if (byPriority != 0) return byPriority;

                uint ah = StableHash(seed + "|" + a.ProviderId + "|" + a.TemplateId);
                uint bh = StableHash(seed + "|" + b.ProviderId + "|" + b.TemplateId);
                int hashCompare = ah.CompareTo(bh);
                if (hashCompare != 0) return hashCompare;
                int providerCompare = string.Compare(a.ProviderId, b.ProviderId, StringComparison.OrdinalIgnoreCase);
                if (providerCompare != 0) return providerCompare;
                return string.Compare(a.TemplateId, b.TemplateId, StringComparison.OrdinalIgnoreCase);
            });

            int count = Math.Min(safeSlots, eligible.Count);
            for (int i = 0; i < count; i++)
            {
                ContractTemplate template = eligible[i];
                ContractOffer offer = new ContractOffer();
                offer.Template = template;
                offer.OccurrenceId = BuildOccurrenceId(normalizedCategory, safeRevision, zone, safeProfile, template.ProviderId, template.TemplateId);
                offer.Active = FindActive(document, offer.OccurrenceId);
                offer.Claimed = document.Claimed.Contains(offer.OccurrenceId);
                offer.RewardLocked = offer.Active != null && HasUnknownRewardOutcome(offer.Active);
                offer.RewardRetryable = offer.Active != null && HasRetryableReward(offer.Active);
                result.Add(offer);
            }
            return result;
        }

        // Compatibility helper for deterministic callers from the older date-based board era. Runtime code now
        // uses persisted active-play board revisions rather than wall-clock dates.
        internal static List<ContractOffer> BuildDailyOffers(
            DateTime localDate,
            string zone,
            string profileKey,
            IEnumerable<ContractTemplate> templates,
            ContractDocument document,
            int slotCount)
        {
            int revision = localDate.Year * 10000 + localDate.Month * 100 + localDate.Day;
            return BuildOffers(ContractCategory.Local, revision, zone, profileKey, templates, document, slotCount);
        }

        internal static ContractInstance Accept(ContractDocument document, ContractOffer offer, string zone, DateTime nowUtc)
        {
            if (document == null || offer == null || offer.Template == null) return null;
            if (offer.Claimed || document.Claimed.Contains(offer.OccurrenceId)) return null;
            if (offer.RewardLocked) return null;
            ContractInstance existing = FindActive(document, offer.OccurrenceId);
            if (existing != null) return existing;
            if (document.Active.Count >= MaxActiveContracts) return null;

            ContractTemplate template = offer.Template;
            ContractInstance value = new ContractInstance();
            value.OccurrenceId = offer.OccurrenceId;
            value.ProviderId = template.ProviderId;
            value.TemplateId = template.TemplateId;
            value.Category = ContractCategory.Normalize(template.Category);
            value.OriginZone = zone == null ? string.Empty : zone;
            value.TargetZone = string.IsNullOrWhiteSpace(template.TargetZone)
                ? DefaultTargetZone(template, value.OriginZone)
                : Clean(template.TargetZone, 128);
            value.Title = template.Title;
            value.Description = template.Description;
            value.ProgressChannel = template.ProgressChannel;
            value.ProgressKey = template.ProgressKey;
            value.ContextFilter = template.ContextFilter;
            value.RewardText = template.RewardText;
            value.RewardXpBasisPoints = Math.Max(0, Math.Min(5000, template.RewardXpBasisPoints));
            value.RewardGoldAmount = Math.Max(0, template.RewardGoldAmount);
            value.RewardItemId = Clean(template.RewardItemId, 128);
            value.RewardItemName = Clean(template.RewardItemName, 120);
            value.RewardItemQuantity = Math.Max(0, Math.Min(1000, template.RewardItemQuantity));
            value.XpRewardStatus = RewardComponentStatus.NotStarted;
            value.GoldRewardStatus = RewardComponentStatus.NotStarted;
            value.ItemRewardStatus = RewardComponentStatus.NotStarted;
            value.Target = template.Target;
            value.Progress = 0;
            value.StateToken = string.Empty;
            value.AcceptedUtc = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : nowUtc.ToUniversalTime();
            document.Active.Add(value);
            return value;
        }

        internal static bool Abandon(ContractDocument document, string occurrenceId)
        {
            if (document == null || string.IsNullOrWhiteSpace(occurrenceId)) return false;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (!string.Equals(active.OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase)) continue;
                // Once any irreversible reward transaction has started, abandoning would discard
                // the component ledger and could turn a later accept/claim into a duplicate grant.
                if (HasRewardTransactionStarted(active)) return false;
                document.Active.RemoveAt(i);
                return true;
            }
            return false;
        }

        internal static ContractInstance FindClaimable(ContractDocument document, string occurrenceId)
        {
            ContractInstance value = FindActive(document, occurrenceId);
            return value != null && value.IsComplete && !document.Claimed.Contains(value.OccurrenceId) ? value : null;
        }

        internal static bool IsRewardComponentRequired(ContractInstance value, RewardComponentKind kind)
        {
            if (value == null) return false;
            if (kind == RewardComponentKind.Xp) return value.RewardXpBasisPoints > 0;
            if (kind == RewardComponentKind.Gold) return value.RewardGoldAmount > 0;
            return value.RewardItemQuantity > 0 && !string.IsNullOrWhiteSpace(value.RewardItemId);
        }

        internal static RewardComponentStatus GetRewardStatus(ContractInstance value, RewardComponentKind kind)
        {
            if (value == null) return RewardComponentStatus.NotStarted;
            if (kind == RewardComponentKind.Xp) return value.XpRewardStatus;
            if (kind == RewardComponentKind.Gold) return value.GoldRewardStatus;
            return value.ItemRewardStatus;
        }

        internal static bool HasRewardTransactionStarted(ContractInstance value)
        {
            if (value == null) return false;
            if (IsRewardComponentRequired(value, RewardComponentKind.Xp) && value.XpRewardStatus != RewardComponentStatus.NotStarted) return true;
            if (IsRewardComponentRequired(value, RewardComponentKind.Gold) && value.GoldRewardStatus != RewardComponentStatus.NotStarted) return true;
            if (IsRewardComponentRequired(value, RewardComponentKind.Item) && value.ItemRewardStatus != RewardComponentStatus.NotStarted) return true;
            return false;
        }

        internal static bool HasUnknownRewardOutcome(ContractInstance value)
        {
            if (value == null) return false;
            if (IsUnknownStatus(value.XpRewardStatus) && IsRewardComponentRequired(value, RewardComponentKind.Xp)) return true;
            if (IsUnknownStatus(value.GoldRewardStatus) && IsRewardComponentRequired(value, RewardComponentKind.Gold)) return true;
            if (IsUnknownStatus(value.ItemRewardStatus) && IsRewardComponentRequired(value, RewardComponentKind.Item)) return true;
            return false;
        }

        internal static bool HasRetryableReward(ContractInstance value)
        {
            if (value == null) return false;
            if (IsRewardComponentRequired(value, RewardComponentKind.Xp) && value.XpRewardStatus == RewardComponentStatus.FailedRetryable) return true;
            if (IsRewardComponentRequired(value, RewardComponentKind.Gold) && value.GoldRewardStatus == RewardComponentStatus.FailedRetryable) return true;
            if (IsRewardComponentRequired(value, RewardComponentKind.Item) && value.ItemRewardStatus == RewardComponentStatus.FailedRetryable) return true;
            return false;
        }

        internal static bool AllConfiguredRewardsApplied(ContractInstance value)
        {
            if (value == null) return false;
            if (IsRewardComponentRequired(value, RewardComponentKind.Xp) &&
                (value.XpRewardStatus != RewardComponentStatus.Applied || value.AppliedXpAmount <= 0 ||
                 (value.PlannedXpAmount > 0 && value.PlannedXpAmount != value.AppliedXpAmount))) return false;
            if (IsRewardComponentRequired(value, RewardComponentKind.Gold) &&
                (value.GoldRewardStatus != RewardComponentStatus.Applied || value.AppliedGoldAmount != value.RewardGoldAmount)) return false;
            if (IsRewardComponentRequired(value, RewardComponentKind.Item) &&
                (value.ItemRewardStatus != RewardComponentStatus.Applied || value.AppliedItemCount != value.RewardItemQuantity)) return false;
            return true;
        }

        internal static bool PrepareRewardComponent(ContractDocument document, string occurrenceId, RewardComponentKind kind)
        {
            return PrepareRewardComponent(document, occurrenceId, kind, 0);
        }

        internal static bool PrepareRewardComponent(ContractDocument document, string occurrenceId, RewardComponentKind kind, int plannedAmount)
        {
            ContractInstance value = FindClaimable(document, occurrenceId);
            if (value == null || !IsRewardComponentRequired(value, kind) || HasUnknownRewardOutcome(value)) return false;
            RewardComponentStatus current = GetRewardStatus(value, kind);
            if (current == RewardComponentStatus.Applied || current == RewardComponentStatus.Applying) return false;
            if (current != RewardComponentStatus.NotStarted && current != RewardComponentStatus.Prepared && current != RewardComponentStatus.FailedRetryable) return false;

            if (kind == RewardComponentKind.Xp && plannedAmount > 0)
            {
                if (value.PlannedXpAmount > 0 && value.PlannedXpAmount != plannedAmount) return false;
                value.PlannedXpAmount = plannedAmount;
            }
            SetRewardStatus(value, kind, RewardComponentStatus.Prepared);
            return true;
        }

        internal static bool MarkRewardComponentApplying(ContractDocument document, string occurrenceId, RewardComponentKind kind)
        {
            ContractInstance value = FindClaimable(document, occurrenceId);
            if (value == null || GetRewardStatus(value, kind) != RewardComponentStatus.Prepared) return false;
            SetRewardStatus(value, kind, RewardComponentStatus.Applying);
            return true;
        }

        internal static bool MarkRewardComponentApplied(ContractDocument document, string occurrenceId, RewardComponentKind kind, int amount, string summary)
        {
            ContractInstance value = FindClaimable(document, occurrenceId);
            if (value == null || GetRewardStatus(value, kind) != RewardComponentStatus.Applying) return false;
            int safeAmount = Math.Max(0, amount);
            if (safeAmount <= 0) return false;
            if (kind == RewardComponentKind.Xp)
            {
                if (value.PlannedXpAmount > 0 && value.PlannedXpAmount != safeAmount) return false;
                if (value.PlannedXpAmount <= 0) value.PlannedXpAmount = safeAmount;
                value.AppliedXpAmount = safeAmount;
            }
            else if (kind == RewardComponentKind.Gold)
            {
                if (value.RewardGoldAmount > 0 && value.RewardGoldAmount != safeAmount) return false;
                value.AppliedGoldAmount = safeAmount;
            }
            else
            {
                if (value.RewardItemQuantity > 0 && value.RewardItemQuantity != safeAmount) return false;
                value.AppliedItemCount = safeAmount;
                value.AppliedItemSummary = Clean(summary, 160);
            }
            SetRewardStatus(value, kind, RewardComponentStatus.Applied);
            return true;
        }

        internal static bool MarkRewardComponentRetryable(ContractDocument document, string occurrenceId, RewardComponentKind kind)
        {
            ContractInstance value = FindClaimable(document, occurrenceId);
            if (value == null) return false;
            RewardComponentStatus current = GetRewardStatus(value, kind);
            if (current != RewardComponentStatus.Prepared && current != RewardComponentStatus.Applying) return false;
            SetRewardStatus(value, kind, RewardComponentStatus.FailedRetryable);
            return true;
        }

        internal static bool MarkRewardComponentUnknown(ContractDocument document, string occurrenceId, RewardComponentKind kind)
        {
            ContractInstance value = FindClaimable(document, occurrenceId);
            if (value == null) return false;
            RewardComponentStatus current = GetRewardStatus(value, kind);
            if (current != RewardComponentStatus.Prepared && current != RewardComponentStatus.Applying) return false;
            SetRewardStatus(value, kind, RewardComponentStatus.OutcomeUnknown);
            return true;
        }

        internal static ContractInstance CommitClaim(ContractDocument document, string occurrenceId)
        {
            if (document == null || string.IsNullOrWhiteSpace(occurrenceId)) return null;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance value = document.Active[i];
                if (!string.Equals(value.OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!value.IsComplete || document.Claimed.Contains(value.OccurrenceId)) return null;
                if (!AllConfiguredRewardsApplied(value)) return null;

                document.Active.RemoveAt(i);
                document.Claimed.Add(value.OccurrenceId);
                if (document.TotalCompleted < int.MaxValue) document.TotalCompleted++;
                if (string.Equals(ContractCategory.Normalize(value.Category), ContractCategory.Global, StringComparison.Ordinal))
                {
                    if (document.TotalGlobalCompleted < int.MaxValue) document.TotalGlobalCompleted++;
                }
                else if (document.TotalLocalCompleted < int.MaxValue) document.TotalLocalCompleted++;
                return value;
            }
            return null;
        }

        internal static ContractInstance ClaimRecordOnly(ContractDocument document, string occurrenceId)
        {
            ContractInstance value = FindClaimable(document, occurrenceId);
            if (value == null) return null;
            if (IsRewardComponentRequired(value, RewardComponentKind.Xp) ||
                IsRewardComponentRequired(value, RewardComponentKind.Gold) ||
                IsRewardComponentRequired(value, RewardComponentKind.Item)) return null;
            return CommitClaim(document, occurrenceId);
        }

        internal static string AppliedRewardSummary(ContractInstance value)
        {
            if (value == null) return string.Empty;
            List<string> parts = new List<string>();
            if (value.XpRewardStatus == RewardComponentStatus.Applied && value.AppliedXpAmount > 0)
                parts.Add("+" + value.AppliedXpAmount.ToString(CultureInfo.InvariantCulture) + " XP");
            if (value.GoldRewardStatus == RewardComponentStatus.Applied && value.AppliedGoldAmount > 0)
                parts.Add("+" + value.AppliedGoldAmount.ToString(CultureInfo.InvariantCulture) + " gold");
            if (value.ItemRewardStatus == RewardComponentStatus.Applied && value.AppliedItemCount > 0)
            {
                string item = string.IsNullOrWhiteSpace(value.AppliedItemSummary) ? value.RewardItemName : value.AppliedItemSummary;
                if (string.IsNullOrWhiteSpace(item)) item = "item";
                parts.Add(value.AppliedItemCount.ToString(CultureInfo.InvariantCulture) + "x " + item);
            }
            return string.Join(", ", parts.ToArray());
        }

        internal static int RecordSuccessfulLocalCompletion(ContractDocument document)
        {
            if (document == null) return 0;
            int changed = 0;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active == null || active.IsComplete) continue;
                if (!string.Equals(ContractCategory.Normalize(active.Category), ContractCategory.Global, StringComparison.Ordinal)) continue;
                if (!string.Equals(active.ProviderId, "builtin", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(active.ProgressKey, "local_completion_claimed", StringComparison.OrdinalIgnoreCase))
                    continue;
                int old = active.Progress;
                active.Progress = Math.Min(active.Target, active.Progress + 1);
                if (active.Progress != old) changed++;
            }
            return changed;
        }

        internal static int ApplyExternalProgress(ContractDocument document, ContractProgressReport report)
        {
            if (document == null || report == null || report.Amount <= 0) return 0;
            int changed = 0;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active.IsComplete) continue;
                if (!string.Equals(active.ProgressChannel, report.Channel, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(active.ProgressKey, report.Key, StringComparison.OrdinalIgnoreCase)) continue;
                if (!ContextMatches(active.ContextFilter, report.Context)) continue;

                int old = active.Progress;
                active.Progress = Math.Min(active.Target, active.Progress + report.Amount);
                if (active.Progress != old) changed++;
            }
            return changed;
        }

        internal static int AddActiveSeconds(ContractDocument document, string currentZone, int seconds)
        {
            if (document == null || !IsProgressZone(currentZone) || seconds <= 0) return 0;
            int changed = 0;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active == null || active.IsComplete) continue;
                if (!string.Equals(active.ProviderId, "builtin", StringComparison.OrdinalIgnoreCase)) continue;

                string category = ContractCategory.Normalize(active.Category);
                bool localCategory = string.Equals(category, ContractCategory.Local, StringComparison.Ordinal);
                bool sameOrigin = string.Equals(active.OriginZone, currentZone, StringComparison.OrdinalIgnoreCase);

                if (localCategory && string.Equals(active.ProgressKey, "zone_seconds", StringComparison.OrdinalIgnoreCase) && sameOrigin)
                {
                    changed += AddBoundedProgress(active, seconds);
                }
                else if (string.Equals(category, ContractCategory.Global, StringComparison.Ordinal) &&
                         string.Equals(active.ProgressKey, "global_seconds", StringComparison.OrdinalIgnoreCase))
                {
                    changed += AddBoundedProgress(active, seconds);
                }
                else if (localCategory && string.Equals(active.ProgressKey, "leave_return", StringComparison.OrdinalIgnoreCase) &&
                         !sameOrigin && IsRoadCheckAway(active.StateToken))
                {
                    int elapsed;
                    bool away;
                    TryGetRoadCheckAwaySeconds(active.StateToken, out elapsed, out away);
                    int next = (int)Math.Min((long)RoadCheckAwaySeconds, (long)elapsed + (long)seconds);
                    if (next != elapsed)
                    {
                        active.StateToken = BuildRoadCheckState(true, next);
                        changed++;
                    }
                }
                else if (localCategory && string.Equals(active.ProgressKey, "home_then_depart", StringComparison.OrdinalIgnoreCase) && sameOrigin)
                {
                    int elapsed = ParseBoundedSeconds(active.StateToken, PerimeterSweepHomeSeconds);
                    int next = (int)Math.Min((long)PerimeterSweepHomeSeconds, (long)elapsed + (long)seconds);
                    if (next != elapsed)
                    {
                        active.StateToken = next.ToString(CultureInfo.InvariantCulture);
                        if (next >= PerimeterSweepHomeSeconds && active.Progress < 1) active.Progress = 1;
                        changed++;
                    }
                }
                else if (string.Equals(category, ContractCategory.Global, StringComparison.Ordinal) &&
                         string.Equals(active.ProgressKey, "global_expedition", StringComparison.OrdinalIgnoreCase))
                {
                    int elapsed;
                    List<string> zones;
                    ParseExpeditionState(active.StateToken, out elapsed, out zones);
                    int next = (int)Math.Min((long)GlobalExpeditionSeconds, (long)elapsed + (long)seconds);
                    if (next != elapsed)
                    {
                        active.StateToken = EncodeExpeditionState(next, zones);
                        active.Progress = ExpeditionCriteria(next, zones.Count);
                        changed++;
                    }
                }
            }
            return changed;
        }

        // Kept as a compatibility alias for existing deterministic callers.
        internal static int AddZoneSeconds(ContractDocument document, string currentZone, int seconds)
        {
            return AddActiveSeconds(document, currentZone, seconds);
        }

        internal static int HandleZoneTransition(ContractDocument document, string oldZone, string newZone)
        {
            if (document == null || !IsProgressZone(oldZone) || !IsProgressZone(newZone)) return 0;
            if (string.Equals(oldZone, newZone, StringComparison.OrdinalIgnoreCase)) return 0;
            int changed = 0;

            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active == null || active.IsComplete) continue;
                if (!string.Equals(active.ProviderId, "builtin", StringComparison.OrdinalIgnoreCase)) continue;

                string category = ContractCategory.Normalize(active.Category);
                if (string.Equals(category, ContractCategory.Local, StringComparison.Ordinal) &&
                    string.Equals(active.ProgressKey, "leave_return", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(newZone, active.OriginZone, StringComparison.OrdinalIgnoreCase))
                    {
                        int elapsed;
                        bool wasAway;
                        if (!TryGetRoadCheckAwaySeconds(active.StateToken, out elapsed, out wasAway)) elapsed = 0;
                        if (!wasAway)
                        {
                            active.StateToken = BuildRoadCheckState(true, elapsed);
                            changed++;
                        }
                    }
                    else if (string.Equals(active.StateToken, "away", StringComparison.Ordinal))
                    {
                        // Grandfather an already-away 0.2.0 Road Check so upgrading does not
                        // silently change an accepted contract's terms mid-trip.
                        active.Progress = active.Target;
                        active.StateToken = "returned";
                        changed++;
                    }
                    else
                    {
                        int elapsed;
                        bool wasAway;
                        if (TryGetRoadCheckAwaySeconds(active.StateToken, out elapsed, out wasAway) && wasAway)
                        {
                            if (elapsed >= RoadCheckAwaySeconds)
                            {
                                active.Progress = active.Target;
                                active.StateToken = "returned";
                            }
                            else active.StateToken = BuildRoadCheckState(false, elapsed);
                            changed++;
                        }
                    }
                }
                else if (string.Equals(category, ContractCategory.Local, StringComparison.Ordinal) &&
                         string.Equals(active.ProgressKey, "home_then_depart", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(newZone, active.OriginZone, StringComparison.OrdinalIgnoreCase) &&
                        ParseBoundedSeconds(active.StateToken, PerimeterSweepHomeSeconds) >= PerimeterSweepHomeSeconds && active.Progress < active.Target)
                    {
                        active.Progress = active.Target;
                        changed++;
                    }
                }
                else if (string.Equals(category, ContractCategory.Local, StringComparison.Ordinal) &&
                         string.Equals(active.ProgressKey, "visit_unique_zone", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(newZone, active.OriginZone, StringComparison.OrdinalIgnoreCase))
                        changed += AddUniqueZone(active, newZone);
                }
                else if (string.Equals(category, ContractCategory.Local, StringComparison.Ordinal) &&
                         string.Equals(active.ProgressKey, "local_circuit", StringComparison.OrdinalIgnoreCase))
                {
                    List<string> visited = ParseVisited(active.StateToken);
                    if (string.Equals(newZone, active.OriginZone, StringComparison.OrdinalIgnoreCase))
                    {
                        if (visited.Count >= LocalCircuitAwayZones)
                        {
                            active.Progress = active.Target;
                            changed++;
                        }
                    }
                    else if (!ContainsIgnoreCase(visited, newZone))
                    {
                        visited.Add(newZone);
                        active.StateToken = EncodeVisited(visited);
                        active.Progress = Math.Min(LocalCircuitAwayZones, visited.Count);
                        changed++;
                    }
                }
                else if (string.Equals(category, ContractCategory.Global, StringComparison.Ordinal) &&
                         string.Equals(active.ProgressKey, "global_visit_unique_zone", StringComparison.OrdinalIgnoreCase))
                {
                    changed += AddUniqueZone(active, newZone);
                }
                else if (string.Equals(category, ContractCategory.Global, StringComparison.Ordinal) &&
                         string.Equals(active.ProgressKey, "global_expedition", StringComparison.OrdinalIgnoreCase))
                {
                    int elapsed;
                    List<string> visited;
                    ParseExpeditionState(active.StateToken, out elapsed, out visited);
                    if (!ContainsIgnoreCase(visited, newZone))
                    {
                        visited.Add(newZone);
                        active.StateToken = EncodeExpeditionState(elapsed, visited);
                        active.Progress = ExpeditionCriteria(elapsed, visited.Count);
                        changed++;
                    }
                }
            }
            return changed;
        }

        internal static bool EnsureLocalBoardZone(ContractDocument document, string currentZone)
        {
            if (document == null) return false;
            if (IsProgressZone(document.LocalBoardZone)) return false;

            // V1 did not persist the board origin. Preserve an in-progress current revision when
            // possible instead of rebinding it to the login zone and creating a one-time reroll.
            string migrated = InferLegacyLocalBoardZone(document);
            if (IsProgressZone(migrated))
            {
                document.LocalBoardZone = Clean(migrated, 128);
                return true;
            }

            if (!IsProgressZone(currentZone)) return false;
            document.LocalBoardZone = Clean(currentZone, 128);
            return true;
        }

        internal static string InferLegacyLocalBoardZone(ContractDocument document)
        {
            if (document == null) return string.Empty;
            string prefix = "local|" + Math.Max(0, document.LocalBoardRevision).ToString(CultureInfo.InvariantCulture) + "|";

            // Active work is the strongest evidence because its origin was persisted independently
            // of the old board timer record.
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active == null || !string.Equals(ContractCategory.Normalize(active.Category), ContractCategory.Local, StringComparison.Ordinal)) continue;
                if (string.IsNullOrWhiteSpace(active.OccurrenceId) || !active.OccurrenceId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (IsProgressZone(active.OriginZone)) return Clean(active.OriginZone, 128);
            }

            // A claimed occurrence still carries the old `local|revision|zone|...` identity. Sort
            // candidates so malformed/multi-zone legacy data migrates deterministically.
            List<string> candidates = new List<string>();
            foreach (string claimed in document.Claimed)
            {
                if (string.IsNullOrWhiteSpace(claimed) || !claimed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string tail = claimed.Substring(prefix.Length);
                int separator = tail.IndexOf('|');
                if (separator <= 0) continue;
                string zone = Clean(tail.Substring(0, separator), 128);
                if (IsProgressZone(zone) && !ContainsIgnoreCase(candidates, zone)) candidates.Add(zone);
            }
            candidates.Sort(StringComparer.OrdinalIgnoreCase);
            return candidates.Count == 0 ? string.Empty : candidates[0];
        }

        internal static ContractRefreshResult AdvanceActivePlay(
            ContractDocument document,
            int seconds,
            int localRefreshMinutes,
            int globalRefreshMinutes)
        {
            ContractRefreshResult result = new ContractRefreshResult();
            if (document == null) return result;

            long localCadence = (long)Math.Max(15, Math.Min(240, localRefreshMinutes)) * 60L;
            long globalCadence = (long)Math.Max(60, Math.Min(480, globalRefreshMinutes)) * 60L;
            if (document.NextLocalRefreshAtSeconds <= 0L)
                document.NextLocalRefreshAtSeconds = SaturatingAdd(document.ActivePlaySeconds, localCadence);
            if (document.NextGlobalRefreshAtSeconds <= 0L)
                document.NextGlobalRefreshAtSeconds = SaturatingAdd(document.ActivePlaySeconds, globalCadence);
            if (seconds <= 0) return result;

            if (document.ActivePlaySeconds > long.MaxValue - seconds) document.ActivePlaySeconds = long.MaxValue;
            else document.ActivePlaySeconds = Math.Max(0L, document.ActivePlaySeconds + seconds);

            while (document.NextLocalRefreshAtSeconds < long.MaxValue &&
                   document.ActivePlaySeconds >= document.NextLocalRefreshAtSeconds)
            {
                if (document.LocalBoardRevision < int.MaxValue) document.LocalBoardRevision++;
                if (document.NextLocalRefreshAtSeconds > long.MaxValue - localCadence)
                {
                    document.NextLocalRefreshAtSeconds = long.MaxValue;
                    document.LocalBoardZone = string.Empty;
                    result.LocalRefreshed = true;
                    break;
                }
                document.NextLocalRefreshAtSeconds += localCadence;
                document.LocalBoardZone = string.Empty;
                result.LocalRefreshed = true;
            }
            while (document.NextGlobalRefreshAtSeconds < long.MaxValue &&
                   document.ActivePlaySeconds >= document.NextGlobalRefreshAtSeconds)
            {
                if (document.GlobalBoardRevision < int.MaxValue) document.GlobalBoardRevision++;
                if (document.NextGlobalRefreshAtSeconds > long.MaxValue - globalCadence)
                {
                    document.NextGlobalRefreshAtSeconds = long.MaxValue;
                    result.GlobalRefreshed = true;
                    break;
                }
                document.NextGlobalRefreshAtSeconds += globalCadence;
                result.GlobalRefreshed = true;
            }
            return result;
        }

        internal static int MinutesUntilLocalRefresh(ContractDocument document)
        {
            return MinutesUntil(document == null ? 0L : document.ActivePlaySeconds,
                document == null ? 0L : document.NextLocalRefreshAtSeconds);
        }

        internal static int MinutesUntilGlobalRefresh(ContractDocument document)
        {
            return MinutesUntil(document == null ? 0L : document.ActivePlaySeconds,
                document == null ? 0L : document.NextGlobalRefreshAtSeconds);
        }

        internal static long SecondsUntilLocalRefresh(ContractDocument document)
        {
            return SecondsUntil(document == null ? 0L : document.ActivePlaySeconds,
                document == null ? 0L : document.NextLocalRefreshAtSeconds);
        }

        internal static long SecondsUntilGlobalRefresh(ContractDocument document)
        {
            return SecondsUntil(document == null ? 0L : document.ActivePlaySeconds,
                document == null ? 0L : document.NextGlobalRefreshAtSeconds);
        }

        internal static string FormatRefreshCountdown(long seconds)
        {
            long safe = Math.Max(0L, seconds);
            long hours = safe / 3600L;
            long minutes = (safe % 3600L) / 60L;
            long remaining = safe % 60L;
            return hours.ToString("00", CultureInfo.InvariantCulture) + ":" +
                minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                remaining.ToString("00", CultureInfo.InvariantCulture);
        }


        internal static bool ShouldAccrueActivePlay(bool gameplayReady, string currentZone, bool applicationFocused, bool simulationRunning)
        {
            return gameplayReady && applicationFocused && simulationRunning && IsProgressZone(currentZone);
        }

        internal static ContractInstance FindActive(ContractDocument document, string occurrenceId)
        {
            if (document == null || string.IsNullOrWhiteSpace(occurrenceId)) return null;
            for (int i = 0; i < document.Active.Count; i++)
                if (string.Equals(document.Active[i].OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase))
                    return document.Active[i];
            return null;
        }

        internal static string TargetText(ContractTemplate value)
        {
            if (value == null) return string.Empty;
            if (string.Equals(value.ProgressKey, ContractCombatPolicy.NativeKillProgressKey, StringComparison.OrdinalIgnoreCase))
                return "Target: " + Math.Max(1, value.Target).ToString(CultureInfo.InvariantCulture) + " " +
                    Clean(value.ContextFilter, 80);
            if (string.Equals(value.ProgressKey, "zone_seconds", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value.ProgressKey, "global_seconds", StringComparison.OrdinalIgnoreCase))
                return "Target: " + Math.Max(1, value.Target / 60).ToString(CultureInfo.InvariantCulture) + " min";
            if (string.Equals(value.ProgressKey, "leave_return", StringComparison.OrdinalIgnoreCase))
                return "Target: 8 min away + return";
            if (string.Equals(value.ProgressKey, "home_then_depart", StringComparison.OrdinalIgnoreCase))
                return "Target: 10 min local + depart";
            if (string.Equals(value.ProgressKey, "local_circuit", StringComparison.OrdinalIgnoreCase))
                return "Target: 2 away zones + return";
            if (string.Equals(value.ProgressKey, "global_expedition", StringComparison.OrdinalIgnoreCase))
                return "Target: 45 min + 5 zones";
            if (string.Equals(value.ProgressKey, "visit_unique_zone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value.ProgressKey, "global_visit_unique_zone", StringComparison.OrdinalIgnoreCase))
                return "Target: " + Math.Max(1, value.Target).ToString(CultureInfo.InvariantCulture) + " zones";
            if (string.Equals(value.ProgressKey, "local_completion_claimed", StringComparison.OrdinalIgnoreCase))
                return "Target: " + Math.Max(1, value.Target).ToString(CultureInfo.InvariantCulture) + " Local claims";
            return "Target: " + Math.Max(1, value.Target).ToString(CultureInfo.InvariantCulture);
        }

        internal static float ProgressFraction(ContractInstance value)
        {
            if (value == null) return 0f;
            if (value.IsComplete) return 1f;
            if (string.Equals(value.ProgressKey, "leave_return", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value.StateToken, "away", StringComparison.Ordinal)) return 0f;
                int elapsed;
                bool away;
                if (!TryGetRoadCheckAwaySeconds(value.StateToken, out elapsed, out away)) return 0f;
                return Math.Max(0f, Math.Min(0.9f, (float)elapsed / (float)RoadCheckAwaySeconds * 0.9f));
            }
            if (string.Equals(value.ProgressKey, "home_then_depart", StringComparison.OrdinalIgnoreCase))
            {
                int elapsed = ParseBoundedSeconds(value.StateToken, PerimeterSweepHomeSeconds);
                return Math.Max(0f, Math.Min(0.9f, (float)elapsed / (float)PerimeterSweepHomeSeconds * 0.9f));
            }
            if (string.Equals(value.ProgressKey, "global_expedition", StringComparison.OrdinalIgnoreCase))
            {
                int elapsed;
                List<string> zones;
                ParseExpeditionState(value.StateToken, out elapsed, out zones);
                float timePart = Math.Min(1f, (float)elapsed / (float)GlobalExpeditionSeconds);
                float zonePart = Math.Min(1f, (float)zones.Count / (float)GlobalExpeditionZones);
                return (timePart + zonePart) * 0.5f;
            }
            if (value.Target <= 0) return 0f;
            return Math.Max(0f, Math.Min(1f, (float)value.Progress / (float)value.Target));
        }

        internal static string ProgressText(ContractInstance value)
        {
            if (value == null) return string.Empty;
            if (string.Equals(value.ProgressKey, ContractCombatPolicy.NativeKillProgressKey, StringComparison.OrdinalIgnoreCase))
                return Math.Min(value.Progress, value.Target).ToString(CultureInfo.InvariantCulture) + " / " +
                    Math.Max(1, value.Target).ToString(CultureInfo.InvariantCulture) + " " + Clean(value.ContextFilter, 70);
            if (string.Equals(value.ProgressKey, "leave_return", StringComparison.OrdinalIgnoreCase) && !value.IsComplete)
            {
                if (string.Equals(value.StateToken, "away", StringComparison.Ordinal)) return "Away · legacy return ready";
                int elapsed;
                bool away;
                if (!TryGetRoadCheckAwaySeconds(value.StateToken, out elapsed, out away)) elapsed = 0;
                return away
                    ? "Away " + FormatClock(elapsed) + " / 8:00 · return after timer"
                    : "Home " + FormatClock(elapsed) + " / 8:00 away time · leave again";
            }
            if (string.Equals(value.ProgressKey, "home_then_depart", StringComparison.OrdinalIgnoreCase) && !value.IsComplete)
            {
                int elapsed = ParseBoundedSeconds(value.StateToken, PerimeterSweepHomeSeconds);
                if (elapsed >= PerimeterSweepHomeSeconds) return "10:00 / 10:00 · cross a zone line";
                return FormatClock(elapsed) + " / 10:00 · stay in origin";
            }
            if (string.Equals(value.ProgressKey, "local_circuit", StringComparison.OrdinalIgnoreCase) && !value.IsComplete)
            {
                List<string> visited = ParseVisited(value.StateToken);
                if (visited.Count >= LocalCircuitAwayZones) return "2 / 2 away zones · return to " + Clean(value.OriginZone, 40);
                return Math.Min(LocalCircuitAwayZones, visited.Count).ToString(CultureInfo.InvariantCulture) + " / 2 away zones · then return";
            }
            if (string.Equals(value.ProgressKey, "global_expedition", StringComparison.OrdinalIgnoreCase) && !value.IsComplete)
            {
                int elapsed;
                List<string> zones;
                ParseExpeditionState(value.StateToken, out elapsed, out zones);
                return FormatClock(elapsed) + " / 45:00 · " + Math.Min(GlobalExpeditionZones, zones.Count).ToString(CultureInfo.InvariantCulture) + " / 5 zones";
            }
            if (string.Equals(value.ProgressKey, "zone_seconds", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value.ProgressKey, "global_seconds", StringComparison.OrdinalIgnoreCase))
                return FormatClock(value.Progress) + " / " + FormatClock(value.Target);

            string suffix = string.Empty;
            if (string.Equals(value.ProgressKey, "visit_unique_zone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value.ProgressKey, "global_visit_unique_zone", StringComparison.OrdinalIgnoreCase)) suffix = " zones";
            else if (string.Equals(value.ProgressKey, "local_completion_claimed", StringComparison.OrdinalIgnoreCase)) suffix = " Local claims";
            return Math.Min(value.Progress, value.Target).ToString(CultureInfo.InvariantCulture) +
                   " / " + value.Target.ToString(CultureInfo.InvariantCulture) + suffix;
        }

        internal static string CompletionSummary(ContractInstance value)
        {
            if (value == null) return string.Empty;
            if (string.Equals(value.ProgressKey, ContractCombatPolicy.NativeKillProgressKey, StringComparison.OrdinalIgnoreCase))
                return "Defeated " + Math.Max(1, value.Target).ToString(CultureInfo.InvariantCulture) + " " +
                    Clean(value.ContextFilter, 80) + " in " + LocationText(value) + ".";
            if (string.Equals(value.ProgressKey, "zone_seconds", StringComparison.OrdinalIgnoreCase))
                return "Adventured for " + Math.Max(1, value.Target / 60).ToString(CultureInfo.InvariantCulture) + " active minutes in " + Clean(value.OriginZone, 60) + ".";
            if (string.Equals(value.ProgressKey, "leave_return", StringComparison.OrdinalIgnoreCase))
                return "Completed an out-and-back route from " + Clean(value.OriginZone, 60) + " after eight active minutes away.";
            if (string.Equals(value.ProgressKey, "home_then_depart", StringComparison.OrdinalIgnoreCase))
                return "Patrolled " + Clean(value.OriginZone, 60) + " for ten active minutes and then departed the zone.";
            if (string.Equals(value.ProgressKey, "visit_unique_zone", StringComparison.OrdinalIgnoreCase))
                return "Entered " + value.Target.ToString(CultureInfo.InvariantCulture) + " different zones away from " + Clean(value.OriginZone, 60) + ".";
            if (string.Equals(value.ProgressKey, "local_circuit", StringComparison.OrdinalIgnoreCase))
                return "Visited two different away zones and returned to " + Clean(value.OriginZone, 60) + ".";
            if (string.Equals(value.ProgressKey, "global_seconds", StringComparison.OrdinalIgnoreCase))
                return "Adventured for " + Math.Max(1, value.Target / 60).ToString(CultureInfo.InvariantCulture) + " active minutes across playable zones.";
            if (string.Equals(value.ProgressKey, "global_visit_unique_zone", StringComparison.OrdinalIgnoreCase))
                return "Entered " + value.Target.ToString(CultureInfo.InvariantCulture) + " different playable zones.";
            if (string.Equals(value.ProgressKey, "local_completion_claimed", StringComparison.OrdinalIgnoreCase))
                return "Successfully claimed " + value.Target.ToString(CultureInfo.InvariantCulture) + " Local contracts.";
            if (string.Equals(value.ProgressKey, "global_expedition", StringComparison.OrdinalIgnoreCase))
                return "Adventured for forty-five active minutes and entered five different playable zones.";
            return "Completed objective (" + Math.Min(value.Progress, value.Target).ToString(CultureInfo.InvariantCulture) + "/" +
                   Math.Max(1, value.Target).ToString(CultureInfo.InvariantCulture) + ").";
        }

        internal static string BuildJournalEntry(ContractInstance value)
        {
            if (value == null) return string.Empty;
            string title = Clean(value.Title, 120);
            if (string.IsNullOrWhiteSpace(title)) title = "Contract";
            string text = "Completed " + ContractCategory.Normalize(value.Category) + " Contract: " + title + ". " + CompletionSummary(value);
            string applied = AppliedRewardSummary(value);
            if (!string.IsNullOrWhiteSpace(applied)) text += " Reward: " + applied + ".";
            return Clean(text, 420);
        }

        internal static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= char.ToLowerInvariant(text[i]);
                    hash *= 16777619u;
                }
                return hash;
            }
        }


        private static int AddBoundedProgress(ContractInstance active, int amount)
        {
            if (active == null || amount <= 0) return 0;
            int old = active.Progress;
            long next = (long)active.Progress + (long)amount;
            active.Progress = (int)Math.Min((long)active.Target, Math.Max(0L, next));
            return active.Progress == old ? 0 : 1;
        }

        private static int ParseBoundedSeconds(string value, int maximum)
        {
            int parsed;
            if (!int.TryParse(value ?? string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return 0;
            return Math.Max(0, Math.Min(maximum, parsed));
        }

        private static string FormatClock(int seconds)
        {
            int safe = Math.Max(0, seconds);
            return (safe / 60).ToString(CultureInfo.InvariantCulture) + ":" + (safe % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static int ExpeditionCriteria(int elapsed, int zoneCount)
        {
            int criteria = 0;
            if (elapsed >= GlobalExpeditionSeconds) criteria++;
            if (zoneCount >= GlobalExpeditionZones) criteria++;
            return criteria;
        }

        private static void ParseExpeditionState(string token, out int elapsed, out List<string> zones)
        {
            elapsed = 0;
            zones = new List<string>();
            string text = token ?? string.Empty;
            int separator = text.IndexOf(';');
            if (separator < 0)
            {
                elapsed = ParseBoundedSeconds(text, GlobalExpeditionSeconds);
                return;
            }
            elapsed = ParseBoundedSeconds(text.Substring(0, separator), GlobalExpeditionSeconds);
            zones = ParseVisited(text.Substring(separator + 1));
        }

        private static string EncodeExpeditionState(int elapsed, List<string> zones)
        {
            return Math.Max(0, Math.Min(GlobalExpeditionSeconds, elapsed)).ToString(CultureInfo.InvariantCulture) + ";" + EncodeVisited(zones);
        }

        private static bool IsUnknownStatus(RewardComponentStatus status)
        {
            return status == RewardComponentStatus.Applying || status == RewardComponentStatus.OutcomeUnknown;
        }

        private static void SetRewardStatus(ContractInstance value, RewardComponentKind kind, RewardComponentStatus status)
        {
            if (value == null) return;
            if (kind == RewardComponentKind.Xp) value.XpRewardStatus = status;
            else if (kind == RewardComponentKind.Gold) value.GoldRewardStatus = status;
            else value.ItemRewardStatus = status;
        }

        private static bool IsRoadCheckAway(string stateToken)
        {
            int elapsed;
            bool away;
            return TryGetRoadCheckAwaySeconds(stateToken, out elapsed, out away) && away;
        }

        private static bool TryGetRoadCheckAwaySeconds(string stateToken, out int elapsed, out bool away)
        {
            elapsed = 0;
            away = false;
            if (string.IsNullOrWhiteSpace(stateToken)) return true;
            if (string.Equals(stateToken, "away", StringComparison.Ordinal)) return false; // legacy marker; handled on return
            if (string.Equals(stateToken, "returned", StringComparison.Ordinal)) return true;
            string[] parts = stateToken.Split('|');
            if (parts.Length != 2) return false;
            if (string.Equals(parts[0], "away", StringComparison.Ordinal)) away = true;
            else if (!string.Equals(parts[0], "home", StringComparison.Ordinal)) return false;
            int parsed;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return false;
            elapsed = Math.Max(0, Math.Min(RoadCheckAwaySeconds, parsed));
            return true;
        }

        private static string BuildRoadCheckState(bool away, int elapsed)
        {
            return (away ? "away|" : "home|") + Math.Max(0, Math.Min(RoadCheckAwaySeconds, elapsed)).ToString(CultureInfo.InvariantCulture);
        }

        // Contract travel progress must never treat login/title/menu scene changes as adventuring.
        // Keep this deliberately conservative and name-based because it is also used by deterministic
        // tests without Unity/game references. Unknown non-empty scenes remain eligible rather than
        // inventing a complete world-zone catalog.
        internal static bool IsProgressZone(string scene)
        {
            if (string.IsNullOrWhiteSpace(scene)) return false;
            string lower = scene.Trim().ToLowerInvariant();
            if (lower.IndexOf("title", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("login", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("characterselect", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("character select", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("charselect", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("mainmenu", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("main menu", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("loading", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("loadscreen", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf("load screen", StringComparison.Ordinal) >= 0) return false;
            return true;
        }

        internal static bool MatchesZone(string scope, string zone)
        {
            if (string.IsNullOrWhiteSpace(zone)) return false;
            if (string.IsNullOrWhiteSpace(scope) || scope == "*") return true;
            return string.Equals(scope.Trim(), zone.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ContextMatches(string filter, string context)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            if (string.IsNullOrWhiteSpace(context)) return false;
            return context.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string Clean(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }

        private static ContractTemplate NewTemplate(
            string providerId, string templateId, string category, string zoneScope,
            string title, string description, string progressChannel, string progressKey,
            string contextFilter, int target, int priority, int rewardXpBasisPoints)
        {
            ContractTemplate value = new ContractTemplate();
            value.ProviderId = providerId;
            value.TemplateId = templateId;
            value.Category = ContractCategory.Normalize(category);
            value.ZoneScope = zoneScope;
            value.TargetZone = !string.IsNullOrWhiteSpace(zoneScope) && zoneScope != "*" ? Clean(zoneScope, 128) : string.Empty;
            value.Title = title;
            value.Description = description;
            value.ProgressChannel = progressChannel;
            value.ProgressKey = progressKey;
            value.ContextFilter = contextFilter;
            value.Target = target;
            value.Priority = priority;
            value.RewardXpBasisPoints = Math.Max(0, Math.Min(5000, rewardXpBasisPoints));
            value.RewardGoldAmount = ContractRewardPolicy.ResolveGoldAmount(value.Category, value.TemplateId, value.Target);
            value.RewardText = ContractRewardPolicy.DescribeReward(value.RewardGoldAmount, value.RewardXpBasisPoints);
            return value;
        }

        private static string BuildOccurrenceId(string category, int revision, string zone, string profile, string provider, string template)
        {
            if (string.Equals(category, ContractCategory.Global, StringComparison.Ordinal))
                return "global|" + revision.ToString(CultureInfo.InvariantCulture) + "|" + profile + "|" + provider + "|" + template;
            return "local|" + revision.ToString(CultureInfo.InvariantCulture) + "|" + (zone ?? string.Empty) + "|" + profile + "|" + provider + "|" + template;
        }

        private static int AddUniqueZone(ContractInstance active, string newZone)
        {
            if (active == null || string.IsNullOrWhiteSpace(newZone)) return 0;
            List<string> visited = ParseVisited(active.StateToken);
            if (ContainsIgnoreCase(visited, newZone)) return 0;
            visited.Add(newZone);
            active.StateToken = EncodeVisited(visited);
            active.Progress = Math.Min(active.Target, visited.Count);
            return 1;
        }


        private static long SaturatingAdd(long value, long amount)
        {
            long safeValue = Math.Max(0L, value);
            long safeAmount = Math.Max(0L, amount);
            if (safeValue > long.MaxValue - safeAmount) return long.MaxValue;
            return safeValue + safeAmount;
        }

        internal static string LocationText(ContractTemplate value, string localFallback)
        {
            if (value == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(value.TargetZone)) return Clean(value.TargetZone, 128);
            if (!string.IsNullOrWhiteSpace(value.ZoneScope) && value.ZoneScope != "*") return Clean(value.ZoneScope, 128);
            if (string.Equals(ContractCategory.Normalize(value.Category), ContractCategory.Local, StringComparison.Ordinal))
                return Clean(localFallback, 128);
            return "Multiple playable zones";
        }

        internal static string LocationText(ContractInstance value)
        {
            if (value == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(value.TargetZone)) return Clean(value.TargetZone, 128);
            if (string.Equals(ContractCategory.Normalize(value.Category), ContractCategory.Local, StringComparison.Ordinal))
                return Clean(value.OriginZone, 128);
            return "Multiple playable zones";
        }

        private static string DefaultTargetZone(ContractTemplate template, string originZone)
        {
            if (template == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(template.TargetZone)) return Clean(template.TargetZone, 128);
            if (!string.IsNullOrWhiteSpace(template.ZoneScope) && template.ZoneScope != "*") return Clean(template.ZoneScope, 128);
            if (string.Equals(ContractCategory.Normalize(template.Category), ContractCategory.Local, StringComparison.Ordinal))
                return Clean(originZone, 128);
            return string.Empty;
        }

        private static long SecondsUntil(long current, long target)
        {
            if (target <= current) return 0L;
            return target - current;
        }

        private static int MinutesUntil(long current, long target)
        {
            if (target <= current) return 0;
            long seconds = target - current;
            long minutes = (seconds + 59L) / 60L;
            return minutes > int.MaxValue ? int.MaxValue : (int)minutes;
        }

        private static List<string> ParseVisited(string token)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(token)) return result;
            string[] parts = token.Split(new char[] { '\u001f' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                if (!ContainsIgnoreCase(result, parts[i])) result.Add(parts[i]);
            return result;
        }

        private static string EncodeVisited(List<string> values)
        {
            if (values == null || values.Count == 0) return string.Empty;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) builder.Append('\u001f');
                builder.Append((values[i] ?? string.Empty).Replace('\u001f', ' '));
            }
            return builder.ToString();
        }

        private static bool ContainsIgnoreCase(List<string> values, string candidate)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
