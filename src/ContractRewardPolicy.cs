using System;
using System.Globalization;

namespace ErenshorContracts
{
    // Central reward balance for built-in Contracts. XP scales from the player's current native
    // level threshold instead of using a flat number, so low and high level characters receive
    // comparable fractions of progression. Gold is a fixed, deterministic amount scaled by the
    // objective tier; it intentionally does not scale with level so it cannot become a late-game
    // money exploit.
    internal static class ContractRewardPolicy
    {
        internal const int LocalRoadCheckXpBasisPoints = 300;          // 3%
        internal const int LocalPerimeterXpBasisPoints = 400;          // 4%
        internal const int LocalWayfarerXpBasisPoints = 500;           // 5%
        internal const int LocalCircuitXpBasisPoints = 500;            // 5%
        internal const int LocalPatrolXpBasisPoints = 600;             // 6%

        internal const int GlobalLocalCompletionsXpBasisPoints = 1200; // 12%
        internal const int GlobalPatrolXpBasisPoints = 1500;           // 15%
        internal const int GlobalExpeditionXpBasisPoints = 1700;       // 17%
        internal const int GlobalWayfarerXpBasisPoints = 1800;         // 18%

        internal static int ResolveXpBasisPoints(string category, string templateId)
        {
            string id = templateId ?? string.Empty;
            if (string.Equals(ContractCategory.Normalize(category), ContractCategory.Global, StringComparison.Ordinal))
            {
                if (string.Equals(id, "global_patrol", StringComparison.OrdinalIgnoreCase)) return GlobalPatrolXpBasisPoints;
                if (string.Equals(id, "global_wayfarer", StringComparison.OrdinalIgnoreCase)) return GlobalWayfarerXpBasisPoints;
                if (string.Equals(id, "global_local_completions", StringComparison.OrdinalIgnoreCase)) return GlobalLocalCompletionsXpBasisPoints;
                if (string.Equals(id, "global_expedition", StringComparison.OrdinalIgnoreCase)) return GlobalExpeditionXpBasisPoints;
                return 0;
            }

            if (string.Equals(id, "local_patrol", StringComparison.OrdinalIgnoreCase)) return LocalPatrolXpBasisPoints;
            if (string.Equals(id, "road_check", StringComparison.OrdinalIgnoreCase)) return LocalRoadCheckXpBasisPoints;
            if (string.Equals(id, "local_perimeter", StringComparison.OrdinalIgnoreCase)) return LocalPerimeterXpBasisPoints;
            if (string.Equals(id, "wayfarer", StringComparison.OrdinalIgnoreCase)) return LocalWayfarerXpBasisPoints;
            if (string.Equals(id, "local_circuit", StringComparison.OrdinalIgnoreCase)) return LocalCircuitXpBasisPoints;
            return 0;
        }

        internal static string DescribeXpPolicy(int basisPoints)
        {
            int safe = Math.Max(0, Math.Min(5000, basisPoints));
            if (safe <= 0) return "Completion recorded locally; no native reward configured.";
            decimal percent = safe / 100m;
            return "XP: " + percent.ToString("0.##", CultureInfo.InvariantCulture) + "% of current level XP threshold";
        }

        internal static int CalculateXpAmount(int levelThreshold, int basisPoints)
        {
            int threshold = Math.Max(1, levelThreshold);
            int safe = Math.Max(0, Math.Min(5000, basisPoints));
            if (safe <= 0) return 0;
            long scaled = ((long)threshold * (long)safe) / 10000L;
            if (scaled <= 0L) return 1;
            return scaled > int.MaxValue ? int.MaxValue : (int)scaled;
        }

        internal static int ResolveGoldAmount(string category, string templateId, int target)
        {
            string id = templateId ?? string.Empty;
            bool global = string.Equals(ContractCategory.Normalize(category), ContractCategory.Global, StringComparison.Ordinal);
            if (global)
            {
                if (string.Equals(id, "global_local_completions", StringComparison.OrdinalIgnoreCase)) return 90;
                if (string.Equals(id, "global_patrol", StringComparison.OrdinalIgnoreCase)) return 120;
                if (string.Equals(id, "global_wayfarer", StringComparison.OrdinalIgnoreCase)) return 145;
                if (string.Equals(id, "global_expedition", StringComparison.OrdinalIgnoreCase)) return 165;
                return 0;
            }
            if (string.Equals(id, "road_check", StringComparison.OrdinalIgnoreCase)) return 22;
            if (string.Equals(id, "local_perimeter", StringComparison.OrdinalIgnoreCase)) return 28;
            if (string.Equals(id, "wayfarer", StringComparison.OrdinalIgnoreCase)) return 34;
            if (string.Equals(id, "local_circuit", StringComparison.OrdinalIgnoreCase)) return 38;
            if (string.Equals(id, "local_patrol", StringComparison.OrdinalIgnoreCase)) return Math.Max(28, Math.Min(48, Math.Max(1, target) / 60 * 6));
            return 0;
        }

        internal static int ResolveCombatGoldAmount(string category, int enemyLevel, int targetCount)
        {
            int level = Math.Max(1, Math.Min(100, enemyLevel));
            int count = Math.Max(1, Math.Min(25, targetCount));
            int baseAmount = string.Equals(ContractCategory.Normalize(category), ContractCategory.Global, StringComparison.Ordinal) ? 28 : 14;
            long amount = baseAmount + (long)level * 2L + (long)count * 3L;
            return (int)Math.Max(1L, Math.Min(300L, amount));
        }

        internal static string DescribeReward(int gold, int basisPoints)
        {
            return "Reward: " + Math.Max(0, gold).ToString(CultureInfo.InvariantCulture) + " Gold + " + DescribeXpPolicy(basisPoints).Replace("XP: ", string.Empty) + " XP";
        }
    }
}
