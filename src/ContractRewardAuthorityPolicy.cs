namespace ErenshorContracts
{
    internal static class ContractRewardAuthorityPolicy
    {
        internal static bool CanAttemptXp(bool compatibilityEnabled, bool exactMethodAvailable)
        {
            return compatibilityEnabled && exactMethodAvailable;
        }

        // No authoritative native gold grant method exists in the supplied evidence. Direct
        // GameData.PlayerInv.Gold mutation is intentionally not accepted as a Contracts payout API.
        internal static bool CanAttemptGold(bool exactNativeAuthorityAvailable)
        {
            return exactNativeAuthorityAvailable;
        }
    }
}
