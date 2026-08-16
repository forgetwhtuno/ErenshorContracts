using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace ErenshorContracts
{
    // Current-zone native enemy discovery. The field set mirrors same-snapshot Follow/PvP evidence
    // and intentionally rejects anything that is not a normal living hostile world NPC.
    internal static class ContractNativeEnemyRuntime
    {
        private static Type _pvpCloneFactoryType;
        private static MethodInfo _pvpIsTemporaryNpc;
        private static float _nextPvpProbeAt;

        internal static List<ContractEnemyObservation> Scan(string zone)
        {
            Dictionary<string, ContractEnemyObservation> grouped =
                new Dictionary<string, ContractEnemyObservation>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(zone)) return new List<ContractEnemyObservation>();

            NPC[] all;
            try { all = UnityEngine.Object.FindObjectsOfType<NPC>(); }
            catch { return new List<ContractEnemyObservation>(); }

            for (int i = 0; i < all.Length; i++)
            {
                NPC npc = all[i];
                Character character;
                if (!TryGetEligibleEnemy(npc, out character)) continue;
                string name = ReadEnemyName(npc);
                if (string.IsNullOrWhiteSpace(name)) continue;
                int level = 0;
                try { if (character.MyStats != null) level = character.MyStats.Level; } catch { level = 0; }
                if (level <= 0) continue;

                ContractEnemyObservation observation;
                if (!grouped.TryGetValue(name, out observation))
                {
                    observation = new ContractEnemyObservation();
                    observation.Zone = ContractCore.Clean(zone, 128);
                    observation.EnemyName = ContractCore.Clean(name, 120);
                    observation.MinLevel = level;
                    observation.MaxLevel = level;
                    observation.Count = 0;
                    grouped[name] = observation;
                }
                observation.MinLevel = Math.Min(observation.MinLevel, level);
                observation.MaxLevel = Math.Max(observation.MaxLevel, level);
                observation.Count++;
            }

            List<ContractEnemyObservation> result = new List<ContractEnemyObservation>(grouped.Values);
            result.Sort(delegate(ContractEnemyObservation a, ContractEnemyObservation b)
            {
                int byName = string.Compare(a.EnemyName, b.EnemyName, StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
                return a.MinLevel.CompareTo(b.MinLevel);
            });
            return result;
        }

        internal static bool TryGetEligibleEnemy(NPC npc, out Character character)
        {
            return TryGetEligibleEnemy(npc, out character, true);
        }

        internal static bool TryGetEligibleEnemy(NPC npc, out Character character, bool requireAlive)
        {
            character = null;
            try
            {
                if (npc == null || npc.gameObject == null) return false;
                character = npc.GetComponent<Character>();
                bool knownFriendlyFaction = character != null &&
                    (character.MyFaction == Character.Faction.Player || character.MyFaction == Character.Faction.PC ||
                     character.MyFaction == Character.Faction.Villager || character.MyFaction == Character.Faction.DEBUG);
                string identity = ReadEnemyName(npc) + " " + npc.gameObject.name;
                bool forbiddenPetIdentity = ContainsAny(identity, new string[] { "pet", "companion", "familiar", "minion", "summon" });

                // BossXp is a current same-snapshot Character field used by PvP's borrowed-reward
                // suppression. Repeatable culls deliberately exclude boss-reward actors: asking for
                // 6-14 kills of a one-off/named boss is not a sensible grind-board objective.
                bool eligible = ContractEnemyEligibilityPolicy.IsEligible(
                    npc.gameObject.activeInHierarchy,
                    npc.SimPlayer || npc.ThisSim != null,
                    npc.NeverAggro,
                    npc.MiningNode,
                    npc.TreasureChest,
                    npc.SummonedByPlayer,
                    IsTemporaryPvpNpc(npc) || LooksLikeTemporaryPvpProxy(npc),
                    character != null,
                    character != null && character.Master != null,
                    character != null && character.Invulnerable,
                    character != null && character.isVendor,
                    knownFriendlyFaction,
                    character != null && character.BossXp > 0f,
                    requireAlive,
                    character != null && character.Alive,
                    forbiddenPetIdentity);
                if (!eligible) { character = null; return false; }
                return true;
            }
            catch
            {
                character = null;
                return false;
            }
        }

        internal static string ReadEnemyName(NPC npc)
        {
            try
            {
                if (npc == null) return string.Empty;
                if (!string.IsNullOrWhiteSpace(npc.NPCName)) return npc.NPCName.Trim();
                return npc.gameObject == null ? string.Empty : (npc.gameObject.name ?? string.Empty).Trim();
            }
            catch { return string.Empty; }
        }

        private static bool ContainsAny(string value, string[] needles)
        {
            string text = value ?? string.Empty;
            for (int i = 0; i < needles.Length; i++)
                if (text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool IsTemporaryPvpNpc(NPC npc)
        {
            ResolvePvpProbe();
            if (_pvpIsTemporaryNpc == null) return false;
            try
            {
                object raw = _pvpIsTemporaryNpc.Invoke(null, new object[] { npc });
                return raw is bool && (bool)raw;
            }
            catch
            {
                // A hot-unload/reload can invalidate an optional sibling's cached static surface.
                // Drop it and retry later; Contracts never requires PvP to be installed.
                _pvpCloneFactoryType = null;
                _pvpIsTemporaryNpc = null;
                _nextPvpProbeAt = Time.unscaledTime + 5f;
                return false;
            }
        }

        private static bool LooksLikeTemporaryPvpProxy(NPC npc)
        {
            try
            {
                string objectName = npc == null || npc.gameObject == null ? string.Empty : (npc.gameObject.name ?? string.Empty);
                // Exact same-snapshot PvpTemporaryCloneFactory naming contract. This is only a
                // negative safety filter; it does not create a hard sibling dependency.
                return objectName.StartsWith("PvP_TemporaryClone", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void ResolvePvpProbe()
        {
            if (_pvpIsTemporaryNpc != null) return;
            float now = Time.unscaledTime;
            if (now < _nextPvpProbeAt) return;
            _nextPvpProbeAt = now + 10f;
            try
            {
                _pvpCloneFactoryType = null;
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length && _pvpCloneFactoryType == null; i++)
                    _pvpCloneFactoryType = assemblies[i].GetType("ErenshorPvP.PvpTemporaryCloneFactory", false);
                if (_pvpCloneFactoryType == null) return;
                _pvpIsTemporaryNpc = _pvpCloneFactoryType.GetMethod(
                    "IsTemporaryNpc",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new Type[] { typeof(NPC) },
                    null);
            }
            catch
            {
                _pvpCloneFactoryType = null;
                _pvpIsTemporaryNpc = null;
            }
        }
    }

    // Two-signal kill authority:
    //   1. an eligible native Character.DoDeath candidate for a normal hostile NPC;
    //   2. a native visible kill message attributed to the local player or current party.
    // A log line consumes one candidate exactly once. Despawns, duplicate death callbacks and
    // duplicate log overloads cannot produce progress by themselves.
    internal static class ContractKillCreditRuntime
    {
        private sealed class DeathCandidate
        {
            internal int InstanceId;
            internal string EnemyName;
            internal string Zone;
            internal float ExpiresAt;
        }

        private static readonly List<DeathCandidate> Candidates = new List<DeathCandidate>();
        private const float CandidateLifetimeSeconds = 6f;

        internal static void Reset()
        {
            Candidates.Clear();
        }

        internal static void NoteDeath(Character character)
        {
            if (character == null) return;
            NPC npc;
            try { npc = character.GetComponent<NPC>(); }
            catch { return; }
            Character verified;
            if (!ContractNativeEnemyRuntime.TryGetEligibleEnemy(npc, out verified, false) || !object.ReferenceEquals(character, verified)) return;

            string zone = ErenshorContractsPlugin.CurrentZoneForRuntime();
            string name = ContractCombatPolicy.NormalizeEnemyName(ContractNativeEnemyRuntime.ReadEnemyName(npc));
            if (string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(name)) return;

            int instanceId;
            try { instanceId = character.GetInstanceID(); }
            catch { return; }

            PurgeExpired();
            for (int i = 0; i < Candidates.Count; i++)
                if (Candidates[i] != null && Candidates[i].InstanceId == instanceId) return;

            DeathCandidate candidate = new DeathCandidate();
            candidate.InstanceId = instanceId;
            candidate.EnemyName = name;
            candidate.Zone = zone;
            candidate.ExpiresAt = Time.unscaledTime + CandidateLifetimeSeconds;
            Candidates.Add(candidate);
            if (Candidates.Count > 32) Candidates.RemoveAt(0);
        }

        internal static void NoteLog(string raw)
        {
            string enemy;
            string killer;
            bool localPlayer;
            if (!ContractKillCreditPolicy.TryParseKillLine(raw, out enemy, out killer, out localPlayer)) return;
            if (!localPlayer && !IsCurrentPartyKiller(killer)) return;

            PurgeExpired();
            string normalizedEnemy = ContractCombatPolicy.NormalizeEnemyName(enemy);
            string zone = ErenshorContractsPlugin.CurrentZoneForRuntime();
            for (int i = Candidates.Count - 1; i >= 0; i--)
            {
                DeathCandidate candidate = Candidates[i];
                if (candidate == null) continue;
                if (!string.Equals(candidate.Zone, zone, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(candidate.EnemyName, normalizedEnemy, StringComparison.OrdinalIgnoreCase)) continue;
                Candidates.RemoveAt(i);
                ErenshorContractsPlugin instance = ErenshorContractsPlugin.Instance;
                if (instance != null) instance.NoteQualifyingNativeKill(candidate.Zone, candidate.EnemyName);
                return;
            }
        }

        private static bool IsCurrentPartyKiller(string killer)
        {
            string name = ContractKillCreditPolicy.CleanActorName(killer);
            if (string.IsNullOrWhiteSpace(name)) return false;
            try
            {
                string player = GameData.PlayerControl == null || GameData.PlayerControl.Myself == null ||
                    GameData.PlayerControl.Myself.MyStats == null
                    ? string.Empty : (GameData.PlayerControl.Myself.MyStats.MyName ?? string.Empty).Trim();
                if (string.Equals(name, "you", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "player", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(player) && string.Equals(name, player, StringComparison.OrdinalIgnoreCase)))
                    return true;

                SimPlayerTracking[] members = GameData.GroupMembers;
                if (members == null) return false;
                for (int i = 0; i < members.Length; i++)
                {
                    SimPlayerTracking member = members[i];
                    if (member != null && !string.IsNullOrWhiteSpace(member.SimName) &&
                        string.Equals(member.SimName.Trim(), name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static void PurgeExpired()
        {
            float now = Time.unscaledTime;
            for (int i = Candidates.Count - 1; i >= 0; i--)
                if (Candidates[i] == null || Candidates[i].ExpiresAt < now) Candidates.RemoveAt(i);
        }
    }

    [HarmonyPatch(typeof(Character), "DoDeath")]
    internal static class ContractsNativeDeathPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Character __instance)
        {
            try { ContractKillCreditRuntime.NoteDeath(__instance); } catch { }
        }
    }

    [HarmonyPatch(typeof(UpdateSocialLog), "LogAdd", new Type[] { typeof(string), typeof(string) })]
    internal static class ContractsKillLogTwoArgPatch
    {
        [HarmonyPostfix]
        private static void Postfix(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length > 0) ContractKillCreditRuntime.NoteLog(__args[0] as string);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(UpdateSocialLog), "LogAdd", new Type[] { typeof(string) })]
    internal static class ContractsKillLogOneArgPatch
    {
        [HarmonyPostfix]
        private static void Postfix(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length > 0) ContractKillCreditRuntime.NoteLog(__args[0] as string);
            }
            catch { }
        }
    }
}
