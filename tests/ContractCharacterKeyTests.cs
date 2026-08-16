using System;
using ErenshorContracts;

// Covers the pure character-key resolution used to scope contract data per character
// (ErenshorContractsPlugin.ResolveCharacterKey / EnsureCharacter). No Unity instance required.
internal static class ContractCharacterKeyTests
{
    internal static int RunAll()
    {
        int assertions = 0;
        assertions += TestSafeKeyLowercasesAndReplaces();
        assertions += TestSafeKeyFallsBackForBlank();
        assertions += TestResolveWithVerifiedSlot();
        assertions += TestResolveFallsBackToNameOnly();
        assertions += TestDistinctCharactersResolveToDistinctKeys();
        assertions += TestSameNameDifferentSlotsAreDistinct();
        assertions += TestAmbiguousSameNameFailsClosedWithoutSlot();
        assertions += TestUnknownNameMultiplicityFailsClosedWithoutSlot();
        assertions += TestUniqueNameMayUseFallbackWithoutSlot();
        assertions += TestSanitizedNameCollisionFailsClosed();
        assertions += TestBlankNameFailsClosed();
        return assertions;
    }

    private static int TestSafeKeyLowercasesAndReplaces()
    {
        Equal("a_b_", ContractCharacterKey.SafeKey("A B!"), "safe key lowercases and replaces non-alphanumerics");
        return 1;
    }

    private static int TestSafeKeyFallsBackForBlank()
    {
        Equal("player", ContractCharacterKey.SafeKey(null), "safe key falls back for null");
        Equal("player", ContractCharacterKey.SafeKey(string.Empty), "safe key falls back for empty");
        return 2;
    }

    private static int TestResolveWithVerifiedSlot()
    {
        Equal("slot2_bramblewick", ContractCharacterKey.Resolve("Bramblewick", 2), "slot-qualified key when slot is verified");
        return 1;
    }

    private static int TestResolveFallsBackToNameOnly()
    {
        Equal("bramblewick", ContractCharacterKey.Resolve("Bramblewick", -1), "name-only key when slot could not be verified");
        return 1;
    }

    private static int TestDistinctCharactersResolveToDistinctKeys()
    {
        string keyA = ContractCharacterKey.Resolve("Aldric", 0);
        string keyB = ContractCharacterKey.Resolve("Branwen", 1);
        NotEqual(keyA, keyB, "two distinct characters resolve to distinct keys");
        return 1;
    }

    private static int TestSameNameDifferentSlotsAreDistinct()
    {
        // Two save slots can legitimately hold the same character name.
        string keyA = ContractCharacterKey.Resolve("Aldric", 0);
        string keyB = ContractCharacterKey.Resolve("Aldric", 1);
        NotEqual(keyA, keyB, "same name in different slots still resolves to distinct keys");
        return 1;
    }

    private static int TestAmbiguousSameNameFailsClosedWithoutSlot()
    {
        Equal(string.Empty, ContractCharacterKey.ResolveStrict("Aldric", -1, 2, 2),
            "proven duplicate name cannot use a shared name-only sidecar");
        return 1;
    }

    private static int TestUnknownNameMultiplicityFailsClosedWithoutSlot()
    {
        Equal(string.Empty, ContractCharacterKey.ResolveStrict("Aldric", -1, 0, 0),
            "unverified save-slot name multiplicity cannot use a name-only sidecar");
        return 1;
    }

    private static int TestUniqueNameMayUseFallbackWithoutSlot()
    {
        Equal("aldric", ContractCharacterKey.ResolveStrict("Aldric", -1, 1, 1),
            "unique save-slot name may use the proven legacy fallback when slot index is unavailable");
        return 1;
    }

    private static int TestSanitizedNameCollisionFailsClosed()
    {
        Equal(string.Empty, ContractCharacterKey.ResolveStrict("A-B", -1, 1, 2),
            "sanitized name collision cannot share a name-only sidecar");
        return 1;
    }

    private static int TestBlankNameFailsClosed()
    {
        Equal(string.Empty, ContractCharacterKey.ResolveStrict("", 0, 0, 0),
            "blank live name cannot create a slot-player sidecar");
        return 1;
    }

    private static void Equal(string expected, string actual, string label)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }

    private static void NotEqual(string left, string right, string label)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
            throw new Exception(label + " values were equal: " + left);
    }
}
