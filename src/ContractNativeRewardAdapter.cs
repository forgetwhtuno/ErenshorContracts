using System;
using System.Reflection;

namespace ErenshorContracts
{
    internal sealed class ContractRewardGrantPlan
    {
        internal int XpAmount;
        internal int XpBasisPoints;
        internal int GoldAmount;
        internal string DisplayText;
    }

    // Current Assembly-CSharp evidence: AddExperience(xp, false) calls the player's EarnedXP in
    // normal play, but routes to GiveRaidXP whenever RaidActive is true. Native loot and quest
    // rewards directly mutate PlayerInv.Gold (Int32); GameManager.SaveGameData serializes it.
    internal static class ContractNativeRewardAdapter
    {
        private static bool _methodResolved;
        private static MethodInfo _addExperience;

        internal static bool TryPreviewXp(int basisPoints, out int xpAmount)
        {
            xpAmount = 0;
            int threshold;
            if (!TryReadExperienceThreshold(out threshold)) return false;
            xpAmount = ContractRewardPolicy.CalculateXpAmount(threshold, basisPoints);
            return xpAmount > 0;
        }

        internal static string DescribeReward(int gold, int basisPoints, string fallback, bool nativeXpEnabled)
        {
            int safeGold = Math.Max(0, gold);
            int amount;
            string xp = TryPreviewXp(basisPoints, out amount) ? amount.ToString() + " XP" : ContractRewardPolicy.DescribeXpPolicy(basisPoints);
            if (!nativeXpEnabled) xp += " (disabled by config)";
            string text = "Reward  |  " + safeGold.ToString() + " Gold + " + xp;
            if (!string.IsNullOrWhiteSpace(fallback) && fallback.IndexOf("Reward:", StringComparison.OrdinalIgnoreCase) < 0)
                text += "  ·  " + fallback;
            return text;
        }

        internal static bool TryPrepare(ContractInstance contract, bool nativeXpEnabled, out ContractRewardGrantPlan plan, out string reason)
        {
            plan = null;
            reason = string.Empty;
            if (contract == null) { reason = "no contract selected"; return false; }
            if (GameData.RaidActive) { reason = "Finish or leave the raid before claiming this contract"; return false; }

            ContractRewardGrantPlan candidate = new ContractRewardGrantPlan();
            if (ContractCore.IsRewardComponentRequired(contract, RewardComponentKind.Xp) && contract.XpRewardStatus != RewardComponentStatus.Applied)
            {
                if (!nativeXpEnabled) { reason = "XP rewards are disabled by config"; return false; }
                if (ResolveAddExperience() == null) { reason = "GameData.AddExperience(int,bool) is unavailable in the current runtime"; return false; }
                int threshold;
                if (!TryReadExperienceThreshold(out threshold)) { reason = "current level XP threshold is unavailable"; return false; }
                int xp = contract.PlannedXpAmount > 0 ? contract.PlannedXpAmount : ContractRewardPolicy.CalculateXpAmount(threshold, contract.RewardXpBasisPoints);
                if (xp <= 0 || xp > threshold) { reason = "calculated XP reward was invalid"; return false; }
                candidate.XpAmount = xp;
                candidate.XpBasisPoints = contract.RewardXpBasisPoints;
            }
            if (ContractCore.IsRewardComponentRequired(contract, RewardComponentKind.Gold) && contract.GoldRewardStatus != RewardComponentStatus.Applied)
            {
                int current;
                if (!TryPrepareGold(contract.RewardGoldAmount, out current, out reason)) return false;
                candidate.GoldAmount = contract.RewardGoldAmount;
            }
            candidate.DisplayText = BuildDisplay(candidate);
            plan = candidate;
            return true;
        }

        internal static bool TryGrantXp(ContractRewardGrantPlan plan, out string outcome, out bool invocationAttempted)
        {
            outcome = string.Empty; invocationAttempted = false;
            if (plan == null || plan.XpAmount <= 0) { outcome = "invalid XP reward plan"; return false; }
            if (GameData.RaidActive) { outcome = "raid began before XP grant"; return false; }
            MethodInfo method = ResolveAddExperience();
            if (method == null) { outcome = "native XP method unavailable"; return false; }
            try { invocationAttempted = true; method.Invoke(null, new object[] { plan.XpAmount, false }); outcome = "+" + plan.XpAmount.ToString() + " XP"; return true; }
            catch (Exception ex) { outcome = "native XP outcome unknown (" + ex.GetType().Name + ")"; return false; }
        }

        internal static bool TryGrantGold(ContractRewardGrantPlan plan, out string outcome, out bool invocationAttempted)
        {
            outcome = string.Empty; invocationAttempted = false;
            if (plan == null || plan.GoldAmount <= 0) { outcome = "invalid Gold reward plan"; return false; }
            int before;
            string reason;
            if (!TryPrepareGold(plan.GoldAmount, out before, out reason)) { outcome = reason; return false; }
            try
            {
                invocationAttempted = true;
                GameData.PlayerInv.Gold = before + plan.GoldAmount;
                if (GameData.PlayerInv.Gold != before + plan.GoldAmount) { outcome = "native Gold postcondition did not hold"; return false; }
                // Native vendor/trade flows update this label directly. UI refresh is best-effort and
                // deliberately outside the irreversible mutation outcome.
                try { if (GameData.PlayerInv.GoldTXT != null) GameData.PlayerInv.GoldTXT.text = GameData.PlayerInv.Gold.ToString(); } catch { }
                outcome = "+" + plan.GoldAmount.ToString() + " Gold";
                return true;
            }
            catch (Exception ex) { outcome = "native Gold outcome unknown (" + ex.GetType().Name + ")"; return false; }
        }

        internal static string CapabilitySummary(bool nativeXpEnabled)
        {
            return ResolveAddExperience() == null ? "Gold rewards enabled; XP rewards unavailable in this runtime" :
                (nativeXpEnabled ? "Gold and direct personal XP rewards enabled outside raids" : "Gold rewards enabled; XP disabled by config");
        }

        private static bool TryPrepareGold(int amount, out int current, out string reason)
        {
            current = 0; reason = string.Empty;
            if (amount <= 0) { reason = "calculated Gold reward was invalid"; return false; }
            try
            {
                if (GameData.PlayerInv == null) { reason = "player inventory is unavailable"; return false; }
                current = GameData.PlayerInv.Gold;
                if (current < 0 || current > int.MaxValue - amount) { reason = "Gold reward would exceed the native currency limit"; return false; }
                return true;
            }
            catch { reason = "player Gold is unavailable"; return false; }
        }

        private static string BuildDisplay(ContractRewardGrantPlan plan)
        {
            string value = string.Empty;
            if (plan.GoldAmount > 0) value = "+" + plan.GoldAmount.ToString() + " Gold";
            if (plan.XpAmount > 0) value += (value.Length == 0 ? string.Empty : " + ") + "+" + plan.XpAmount.ToString() + " XP";
            return value;
        }

        private static MethodInfo ResolveAddExperience()
        {
            if (_methodResolved) return _addExperience;
            _methodResolved = true;
            try { _addExperience = typeof(GameData).GetMethod("AddExperience", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null); }
            catch { _addExperience = null; }
            return _addExperience;
        }

        private static bool TryReadExperienceThreshold(out int threshold)
        {
            threshold = 0;
            try
            {
                if (GameData.PlayerControl == null || GameData.PlayerControl.Myself == null || GameData.PlayerControl.Myself.MyStats == null) return false;
                object stats = GameData.PlayerControl.Myself.MyStats;
                Type type = stats.GetType();
                PropertyInfo property = type.GetProperty("ExperienceToLevelUp", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object raw = property == null ? null : property.GetValue(stats, null);
                if (raw == null) { FieldInfo field = type.GetField("ExperienceToLevelUp", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (field != null) raw = field.GetValue(stats); }
                if (raw == null) return false;
                threshold = Convert.ToInt32(raw); return threshold > 0;
            }
            catch { threshold = 0; return false; }
        }
    }
}
