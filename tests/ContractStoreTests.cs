using System;
using System.IO;
using ErenshorContracts;

// Covers the one-time "first character to load may claim the legacy global save once" policy
// (ContractStore.TryClaimLegacyData). Uses real temp-directory file I/O; no Unity instance
// required. The legacy file itself must never be deleted or truncated by this policy.
internal static class ContractStoreTests
{
    internal static int RunAll()
    {
        int assertions = 0;
        assertions += TestFirstCharacterClaimsLegacyData();
        assertions += TestSecondCharacterStartsFresh();
        assertions += TestExistingCharacterDataIsNeverOverwritten();
        assertions += TestNoLegacyDataMeansNoClaim();
        assertions += TestAlreadyClaimedMeansNoSecondClaimEvenAfterDelete();
        return assertions;
    }

    private static int TestFirstCharacterClaimsLegacyData()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            File.WriteAllText(legacy, "legacy-data");
            string dest = CharacterPath(root, "slot0_aldric");

            bool claimed = ContractStore.TryClaimLegacyData(legacy, marker, dest, "slot0_aldric");

            True(claimed, "first character claims legacy data");
            True(File.Exists(dest), "legacy data copied to the character's own path");
            True(File.Exists(marker), "claim marker written");
            True(File.Exists(legacy), "legacy file is left untouched");
            Equal("legacy-data", File.ReadAllText(legacy), "legacy file content is unmodified");
            Equal("legacy-data", File.ReadAllText(dest), "copied content matches legacy");
            return 6;
        }
        finally { Cleanup(root); }
    }

    private static int TestSecondCharacterStartsFresh()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            File.WriteAllText(legacy, "legacy-data");
            string destA = CharacterPath(root, "slot0_aldric");
            string destB = CharacterPath(root, "slot1_branwen");

            ContractStore.TryClaimLegacyData(legacy, marker, destA, "slot0_aldric");
            bool claimedSecond = ContractStore.TryClaimLegacyData(legacy, marker, destB, "slot1_branwen");

            False(claimedSecond, "second character does not also claim legacy data");
            False(File.Exists(destB), "second character has no imported file, i.e. starts fresh");
            return 2;
        }
        finally { Cleanup(root); }
    }

    private static int TestExistingCharacterDataIsNeverOverwritten()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            File.WriteAllText(legacy, "legacy-data");
            string dest = CharacterPath(root, "slot0_aldric");
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.WriteAllText(dest, "own-data");

            bool claimed = ContractStore.TryClaimLegacyData(legacy, marker, dest, "slot0_aldric");

            False(claimed, "a character with its own data is never overwritten by the legacy import");
            Equal("own-data", File.ReadAllText(dest), "character's own data is preserved as-is");
            True(File.Exists(marker), "claim marker is still written so nobody else imports later");
            return 3;
        }
        finally { Cleanup(root); }
    }

    private static int TestNoLegacyDataMeansNoClaim()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            string dest = CharacterPath(root, "slot0_aldric");

            bool claimed = ContractStore.TryClaimLegacyData(legacy, marker, dest, "slot0_aldric");

            False(claimed, "no legacy file means there is nothing to claim");
            False(File.Exists(marker), "no marker is written when there was nothing to claim");
            return 2;
        }
        finally { Cleanup(root); }
    }

    private static int TestAlreadyClaimedMeansNoSecondClaimEvenAfterDelete()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            File.WriteAllText(legacy, "legacy-data");
            string destA = CharacterPath(root, "slot0_aldric");
            ContractStore.TryClaimLegacyData(legacy, marker, destA, "slot0_aldric");

            // Even if the first character's own file later disappears (corruption recovery,
            // manual cleanup, etc.), the marker must still block a second import.
            File.Delete(destA);
            string destB = CharacterPath(root, "slot1_branwen");
            bool claimedSecond = ContractStore.TryClaimLegacyData(legacy, marker, destB, "slot1_branwen");

            False(claimedSecond, "claim marker blocks import permanently, independent of the first claimer's file state");
            return 1;
        }
        finally { Cleanup(root); }
    }

    private static string CharacterPath(string root, string key)
    {
        return Path.Combine(Path.Combine(Path.Combine(root, "Characters"), key), "contracts.dat");
    }

    private static string NewTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "ErenshorContractsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private static void True(bool value, string label) { if (!value) throw new Exception(label); }
    private static void False(bool value, string label) { if (value) throw new Exception(label); }

    private static void Equal(string expected, string actual, string label)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }
}
