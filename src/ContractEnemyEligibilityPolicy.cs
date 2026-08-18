namespace ErenshorContracts
{
    // Unity-free admission gate mirroring the runtime's normal-hostile NPC classification.
    // Runtime code is responsible only for reading native fields and translating them into these
    // semantic facts; this policy makes the exclusion matrix deterministic and testable.
    internal static class ContractEnemyEligibilityPolicy
    {
        internal static bool IsEligible(
            bool active,
            bool simBacked,
            bool neverAggro,
            bool miningNode,
            bool treasureChest,
            bool summonedByPlayer,
            bool temporaryPvpProxy,
            bool hasCharacter,
            bool ownedActor,
            bool invulnerable,
            bool vendor,
            bool knownFriendlyFaction,
            bool bossRewardActor,
            bool requireAlive,
            bool alive,
            bool forbiddenPetIdentity,
            bool namedIndividualActor)
        {
            if (!active || simBacked || neverAggro || miningNode || treasureChest || summonedByPlayer || temporaryPvpProxy)
                return false;
            if (!hasCharacter || ownedActor || invulnerable || vendor || knownFriendlyFaction || bossRewardActor || forbiddenPetIdentity)
                return false;
            if (namedIndividualActor) return false;
            if (requireAlive && !alive) return false;
            return true;
        }
    }
}
