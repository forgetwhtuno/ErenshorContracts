using System;

namespace ErenshorContracts
{
    // Contract kill work is a grind board against ordinary world mobs. Unique story actors, quest
    // NPCs, achievement/raid actors and rare named spawns stay off the board even though they are
    // hostile, killable and level-appropriate. The decision has two halves because generation runs
    // against both live scans and persisted catalog records:
    //   1. structural evidence read from the live actor (dialog, quest wiring, achievement hooks,
    //      raid ownership, rare-spawn origin) - authoritative, applied at discovery time;
    //   2. a conservative display-name shape test - the only signal a persisted record still has,
    //      so it also retires named entries captured by older builds.
    internal static class ContractMobTargetPolicy
    {
        // Individual-identity markers used by Erenshor's named actors ("Grum the Vile",
        // "Karthus of the Deep"). Ordinary mob display names do not carry them.
        private static readonly string[] IndividualNameMarkers = new string[] { " the ", " of ", "," };

        internal static bool IsNamedIndividualActor(
            bool hasDialog,
            bool assignsQuest,
            bool completesQuestOnDeath,
            bool achievementActor,
            bool raidManaged,
            bool rareSpawnVariant)
        {
            return hasDialog || assignsQuest || completesQuestOnDeath || achievementActor || raidManaged || rareSpawnVariant;
        }

        internal static bool HasIndividualNameShape(string enemyName)
        {
            string name = ContractCombatPolicy.NormalizeEnemyName(enemyName);
            if (name.Length == 0) return false;
            for (int i = 0; i < IndividualNameMarkers.Length; i++)
                if (name.IndexOf(IndividualNameMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Population evidence outranks name shape: a display name observed on two or more live
        // actors at once is a spawn type, not an individual, whatever it is called.
        internal static bool IsMobTarget(string enemyName, int observedCount)
        {
            string name = ContractCombatPolicy.NormalizeEnemyName(enemyName);
            if (name.Length == 0) return false;
            if (HasIndividualNameShape(name)) return false;
            if (Math.Max(1, observedCount) >= 2) return true;
            return !ContractEnemyTargetPolicy.IsLikelyExactNamedTarget(name, 1);
        }
    }
}
