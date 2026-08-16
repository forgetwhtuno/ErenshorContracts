using System;
using System.IO;
using ErenshorContracts;

internal static class ContractStoreTests
{
    private static int _assertions;

    internal static int RunAll()
    {
        _assertions = 0;
        TestFirstCharacterClaimsLegacyData();
        TestSecondCharacterStartsFresh();
        TestExistingCharacterDataIsNeverOverwritten();
        TestNoLegacyDataMeansNoClaim();
        TestAlreadyClaimedMeansNoSecondClaimEvenAfterDelete();
        TestV2GameplayAndRewardLedgerRoundTrip();
        TestV2ApplyingBecomesOutcomeUnknownAfterRestart();
        TestV2PreparedRemainsSafeToRetry();
        TestV2TruncatedLedgerRecoversFromBackup();
        TestV1PendingMigratesFailClosed();
        TestV1NoPendingMigratesNotStarted();
        TestLegacyRecordOnlyActiveRemainsRecordOnly();
        TestBackupRecovery();
        TestMissingPrimaryRecoversBackup();
        TestMissingPrimaryRecoversCompleteTemp();
        TestMalformedActiveRecordsAreBoundedAndDeduplicated();
        TestMalformedAppliedStatusFailsClosed();
        TestInvalidRequiredRewardStatusFailsClosed();
        TestPreparedXpWithoutLockedPlanFailsClosed();
        TestUnstartedXpWithHiddenPlanFailsClosed();
        TestActiveRecordCap();
        TestClaimedOccurrenceSuppressesActiveRecord();
        return _assertions;
    }

    private static void TestFirstCharacterClaimsLegacyData()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            File.WriteAllText(legacy, "legacy-data");
            string dest = CharacterPath(root, "slot0_aldric");
            True(ContractStore.TryClaimLegacyData(legacy, marker, dest, "slot0_aldric"), "first character claims legacy data");
            True(File.Exists(dest), "legacy copied to character path");
            True(File.Exists(marker), "claim marker written");
            True(File.Exists(legacy), "legacy original retained");
            Equal("legacy-data", File.ReadAllText(dest), "copy matches legacy");
        }
        finally { Cleanup(root); }
    }

    private static void TestSecondCharacterStartsFresh()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            File.WriteAllText(legacy, "legacy-data");
            string a = CharacterPath(root, "slot0_aldric");
            string b = CharacterPath(root, "slot1_branwen");
            ContractStore.TryClaimLegacyData(legacy, marker, a, "slot0_aldric");
            False(ContractStore.TryClaimLegacyData(legacy, marker, b, "slot1_branwen"), "second character cannot claim same legacy data");
            False(File.Exists(b), "second character starts fresh");
        }
        finally { Cleanup(root); }
    }

    private static void TestExistingCharacterDataIsNeverOverwritten()
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
            False(ContractStore.TryClaimLegacyData(legacy, marker, dest, "slot0_aldric"), "existing character file never overwritten");
            Equal("own-data", File.ReadAllText(dest), "existing file preserved");
            True(File.Exists(marker), "legacy marked claimed once existing scoped data is seen");
        }
        finally { Cleanup(root); }
    }

    private static void TestNoLegacyDataMeansNoClaim()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            False(ContractStore.TryClaimLegacyData(legacy, marker, CharacterPath(root, "slot0_aldric"), "slot0_aldric"), "missing legacy is no-op");
            False(File.Exists(marker), "missing legacy does not create marker");
        }
        finally { Cleanup(root); }
    }

    private static void TestAlreadyClaimedMeansNoSecondClaimEvenAfterDelete()
    {
        string root = NewTempDir();
        try
        {
            string legacy = Path.Combine(root, "contracts.dat");
            string marker = legacy + ".claimed";
            File.WriteAllText(legacy, "legacy-data");
            string a = CharacterPath(root, "slot0_aldric");
            ContractStore.TryClaimLegacyData(legacy, marker, a, "slot0_aldric");
            File.Delete(a);
            False(ContractStore.TryClaimLegacyData(legacy, marker, CharacterPath(root, "slot1_branwen"), "slot1_branwen"), "claim marker permanently blocks second import");
        }
        finally { Cleanup(root); }
    }

    private static void TestV2GameplayAndRewardLedgerRoundTrip()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractStore store = new ContractStore(path);
            ContractDocument doc = new ContractDocument();
            doc.TotalCompleted = 7; doc.TotalLocalCompleted = 5; doc.TotalGlobalCompleted = 2;
            doc.ActivePlaySeconds = 4321; doc.LocalBoardRevision = 3; doc.GlobalBoardRevision = 1;
            doc.NextLocalRefreshAtSeconds = 5400; doc.NextGlobalRefreshAtSeconds = 7200;
            doc.LocalBoardZone = "Stowaway's Step";
            doc.LocalCombatGenerationRevision = 3;
            doc.LocalCombatGenerationZone = "Stowaway's Step";
            doc.GlobalCombatGenerationRevision = 1;
            ContractEnemyRecord knownEnemy = new ContractEnemyRecord();
            knownEnemy.Zone = "Bonepits"; knownEnemy.EnemyName = "Bone Guard";
            knownEnemy.MinLevel = 11; knownEnemy.MaxLevel = 13; knownEnemy.ObservedCount = 4; knownEnemy.LastSeenActiveSeconds = 4200;
            doc.EnemyCatalog.Add(knownEnemy);
            ContractGeneratedCombatOffer generated = new ContractGeneratedCombatOffer();
            generated.Category = ContractCategory.Global; generated.BoardRevision = 1;
            generated.TargetZone = "Bonepits"; generated.EnemyName = "Bone Guard";
            generated.EnemyLevel = 12; generated.TargetCount = 12; generated.RewardXpBasisPoints = 1300;
            doc.GeneratedCombatOffers.Add(generated);
            doc.Claimed.Add("claimed-id");

            ContractInstance active = NewActive("global|1|p1|builtin|global_patrol");
            active.RewardXpBasisPoints = 1200;
            active.RewardGoldAmount = 38;
            active.RewardItemId = "ore_common";
            active.RewardItemName = "Common Ore";
            active.RewardItemQuantity = 2;
            active.XpRewardStatus = RewardComponentStatus.Applied;
            active.AppliedXpAmount = 420;
            active.GoldRewardStatus = RewardComponentStatus.FailedRetryable;
            active.ItemRewardStatus = RewardComponentStatus.Applied;
            active.TargetZone = "Bonepits";
            active.AppliedItemCount = 2;
            active.AppliedItemSummary = "Common Ore";
            doc.Active.Add(active);

            store.Save(doc);
            True(File.ReadAllText(path).StartsWith("ERENSHOR_CONTRACTS_V3", StringComparison.Ordinal), "save uses V3 header");
            string warning;
            ContractDocument loaded = store.Load(out warning);
            Equal(string.Empty, warning, "V3 round-trip warning");
            Equal(7, loaded.TotalCompleted, "completed persisted");
            Equal(4321L, loaded.ActivePlaySeconds, "active play persisted");
            Equal(3, loaded.LocalBoardRevision, "local revision persisted");
            Equal("Stowaway's Step", loaded.LocalBoardZone, "local board origin persisted");
            Equal(3, loaded.LocalCombatGenerationRevision, "local combat generation revision persisted");
            Equal(1, loaded.GlobalCombatGenerationRevision, "global combat generation revision persisted");
            Equal(1, loaded.EnemyCatalog.Count, "enemy catalog persisted");
            Equal("Bonepits", loaded.EnemyCatalog[0].Zone, "enemy catalog zone persisted");
            Equal(4, loaded.EnemyCatalog[0].ObservedCount, "enemy abundance persisted");
            Equal(1, loaded.GeneratedCombatOffers.Count, "generated combat board persisted");
            Equal("Bone Guard", loaded.GeneratedCombatOffers[0].EnemyName, "generated target persisted");
            Equal(1, loaded.Active.Count, "active row persisted");
            ContractInstance got = loaded.Active[0];
            Equal(1200, got.RewardXpBasisPoints, "XP policy persisted");
            Equal(38, got.RewardGoldAmount, "gold definition persisted");
            Equal("ore_common", got.RewardItemId, "item id persisted");
            Equal("Bonepits", got.TargetZone, "active target zone persisted");
            Equal(RewardComponentStatus.Applied, got.XpRewardStatus, "XP applied status persisted");
            Equal(420, got.AppliedXpAmount, "actual XP amount persisted");
            Equal(420, got.PlannedXpAmount, "planned XP amount reconstructed/persisted");
            Equal(RewardComponentStatus.FailedRetryable, got.GoldRewardStatus, "retryable gold status persisted");
            Equal(RewardComponentStatus.Applied, got.ItemRewardStatus, "item applied status persisted");
            Equal(2, got.AppliedItemCount, "actual item count persisted");
            True(loaded.Claimed.Contains("claimed-id"), "claimed set persisted");
        }
        finally { Cleanup(root); }
    }

    private static void TestV2ApplyingBecomesOutcomeUnknownAfterRestart()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument();
            ContractInstance active = NewActive("xp-applying");
            active.RewardXpBasisPoints = 500;
            active.XpRewardStatus = RewardComponentStatus.Applying;
            doc.Active.Add(active);
            new ContractStore(path).Save(doc);
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(RewardComponentStatus.OutcomeUnknown, loaded.Active[0].XpRewardStatus, "restart converts in-flight Applying to fail-closed unknown");
            True(ContractCore.HasUnknownRewardOutcome(loaded.Active[0]), "unknown state blocks duplicate reward");
        }
        finally { Cleanup(root); }
    }

    private static void TestV2PreparedRemainsSafeToRetry()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument();
            ContractInstance active = NewActive("xp-prepared");
            active.RewardXpBasisPoints = 500;
            active.XpRewardStatus = RewardComponentStatus.Prepared;
            active.PlannedXpAmount = 321;
            doc.Active.Add(active);
            new ContractStore(path).Save(doc);
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(RewardComponentStatus.Prepared, loaded.Active[0].XpRewardStatus, "prepared state survives restart as safe retry");
            Equal(321, loaded.Active[0].PlannedXpAmount, "prepared XP amount survives restart");
            True(ContractCore.PrepareRewardComponent(loaded, "xp-prepared", RewardComponentKind.Xp, 321), "prepared component can deliberately retry same pre-invocation plan");
        }
        finally { Cleanup(root); }
    }

    private static void TestV2TruncatedLedgerRecoversFromBackup()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractStore store = new ContractStore(path);
            ContractDocument backupDoc = new ContractDocument();
            ContractInstance safe = NewActive("safe-backup");
            safe.RewardXpBasisPoints = 500;
            safe.XpRewardStatus = RewardComponentStatus.Applied;
            safe.PlannedXpAmount = 50;
            safe.AppliedXpAmount = 50;
            backupDoc.Active.Add(safe);
            store.Save(backupDoc);
            File.Copy(path, path + ".bak", true);

            string valid = File.ReadAllLines(path)[3];
            string[] fields = valid.Split('|');
            string truncated = string.Join("|", fields, 0, 24);
            File.WriteAllLines(path, new string[] { "ERENSHOR_CONTRACTS_V2", "M|0|0|0", "R|0|0|0|2700|7200", truncated });

            string warning;
            ContractDocument loaded = store.Load(out warning);
            Equal(1, loaded.Active.Count, "truncated V2 uses valid backup rather than resetting reward ledger");
            Equal("safe-backup", loaded.Active[0].OccurrenceId, "backup occurrence recovered");
            Equal(RewardComponentStatus.Applied, loaded.Active[0].XpRewardStatus, "applied reward remains applied after recovery");
            True(warning.IndexOf("recovered", StringComparison.OrdinalIgnoreCase) >= 0, "truncated V2 recovery reported");
        }
        finally { Cleanup(root); }
    }

    private static void TestV1PendingMigratesFailClosed()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractInstance active = NewActive("legacy-pending");
            active.RewardXpBasisPoints = 500;
            string row = V1ActiveRow(active);
            File.WriteAllLines(path, new string[] { "ERENSHOR_CONTRACTS_V1", "P|" + B64(active.OccurrenceId), row });
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(RewardComponentStatus.OutcomeUnknown, loaded.Active[0].XpRewardStatus, "legacy pending guard migrates to unknown rather than retrying XP");
        }
        finally { Cleanup(root); }
    }

    private static void TestV1NoPendingMigratesNotStarted()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractInstance active = NewActive("legacy-clean");
            active.RewardXpBasisPoints = 500;
            File.WriteAllLines(path, new string[] { "ERENSHOR_CONTRACTS_V1", V1ActiveRow(active) });
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(RewardComponentStatus.NotStarted, loaded.Active[0].XpRewardStatus, "legacy completed XP contract without pending marker remains unattempted");
        }
        finally { Cleanup(root); }
    }

    private static void TestLegacyRecordOnlyActiveRemainsRecordOnly()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractInstance active = NewActive("legacy-record-only");
            active.RewardXpBasisPoints = 0;
            File.WriteAllLines(path, new string[] { "ERENSHOR_CONTRACTS_V1", V1ActiveRow(active) });
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(0, loaded.Active[0].RewardXpBasisPoints, "old blank/zero reward remains record-only");
            Equal(RewardComponentStatus.NotStarted, loaded.Active[0].XpRewardStatus, "record-only has no synthetic reward transaction");
        }
        finally { Cleanup(root); }
    }

    private static void TestBackupRecovery()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractStore store = new ContractStore(path);
            ContractDocument doc = new ContractDocument(); doc.TotalCompleted = 9; doc.ActivePlaySeconds = 1234;
            store.Save(doc);
            File.Copy(path, path + ".bak", true);
            File.WriteAllText(path, "not-a-contract-file");
            string warning;
            ContractDocument loaded = store.Load(out warning);
            Equal(9, loaded.TotalCompleted, "backup restores completed count");
            Equal(1234L, loaded.ActivePlaySeconds, "backup restores active play");
            True(warning.IndexOf("recovered", StringComparison.OrdinalIgnoreCase) >= 0, "backup recovery reported");
            True(Directory.GetFiles(root, "contracts.dat.corrupt-*").Length >= 1, "bad primary preserved for diagnosis");
        }
        finally { Cleanup(root); }
    }

    private static void TestMissingPrimaryRecoversBackup()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractStore store = new ContractStore(path);
            ContractDocument doc = new ContractDocument(); doc.TotalCompleted = 11; doc.ActivePlaySeconds = 2222;
            store.Save(doc);
            File.Copy(path, path + ".bak", true);
            File.Delete(path);
            string warning;
            ContractDocument loaded = store.Load(out warning);
            Equal(11, loaded.TotalCompleted, "missing primary recovers backup completed count");
            Equal(2222L, loaded.ActivePlaySeconds, "missing primary recovers backup timer");
            True(warning.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 warning.IndexOf(".bak", StringComparison.OrdinalIgnoreCase) >= 0, "missing-primary backup recovery reported");
        }
        finally { Cleanup(root); }
    }

    private static void TestMissingPrimaryRecoversCompleteTemp()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument(); doc.TotalCompleted = 12; doc.ActivePlaySeconds = 3333;
            new ContractStore(path + ".tmp").Save(doc);
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(12, loaded.TotalCompleted, "missing primary recovers complete temp completed count");
            Equal(3333L, loaded.ActivePlaySeconds, "missing primary recovers complete temp timer");
            True(warning.IndexOf(".tmp", StringComparison.OrdinalIgnoreCase) >= 0, "missing-primary temp recovery reported");
        }
        finally { Cleanup(root); }
    }

    private static void TestMalformedActiveRecordsAreBoundedAndDeduplicated()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            string id = "malformed";
            string[] fields = new string[]
            {
                "A", B64(id), B64("builtin"), B64("local_patrol"), B64("Home"), B64(new string('T', 200)),
                B64(new string('D', 500)), B64("builtin"), B64("zone_seconds"), B64(""), B64("reward"),
                "-50", "999999", B64(""), long.MaxValue.ToString(), B64(ContractCategory.Local), "99999"
            };
            string row = string.Join("|", fields);
            File.WriteAllLines(path, new string[] { "ERENSHOR_CONTRACTS_V1", row, row });
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(string.Empty, warning, "bounded malformed values do not corrupt file");
            Equal(1, loaded.Active.Count, "duplicate occurrence deduplicated");
            Equal(1, loaded.Active[0].Target, "bad target clamped");
            Equal(1, loaded.Active[0].Progress, "progress clamped to target");
            Equal(5000, loaded.Active[0].RewardXpBasisPoints, "XP basis points bounded");
            Equal(120, loaded.Active[0].Title.Length, "title bounded");
            Equal(320, loaded.Active[0].Description.Length, "description bounded");
        }
        finally { Cleanup(root); }
    }

    private static void TestMalformedAppliedStatusFailsClosed()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument();
            ContractInstance active = NewActive("applied-zero"); active.RewardXpBasisPoints = 500;
            active.XpRewardStatus = RewardComponentStatus.Applied; active.AppliedXpAmount = 0;
            doc.Active.Add(active);
            new ContractStore(path).Save(doc);
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(RewardComponentStatus.OutcomeUnknown, loaded.Active[0].XpRewardStatus, "malformed Applied-with-zero amount fails closed");
        }
        finally { Cleanup(root); }
    }

    private static void TestInvalidRequiredRewardStatusFailsClosed()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument();
            ContractInstance active = NewActive("invalid-status");
            active.RewardXpBasisPoints = 500;
            active.XpRewardStatus = RewardComponentStatus.Prepared;
            active.PlannedXpAmount = 50;
            doc.Active.Add(active);
            new ContractStore(path).Save(doc);

            string[] lines = File.ReadAllLines(path);
            string[] fields = lines[3].Split('|');
            fields[21] = "not-an-enum";
            lines[3] = string.Join("|", fields);
            File.WriteAllLines(path, lines);

            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(string.Empty, warning, "invalid status is normalized rather than replayed");
            Equal(RewardComponentStatus.OutcomeUnknown, loaded.Active[0].XpRewardStatus,
                "invalid required reward status fails closed");
        }
        finally { Cleanup(root); }
    }

    private static void TestPreparedXpWithoutLockedPlanFailsClosed()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument();
            ContractInstance active = NewActive("prepared-no-plan");
            active.RewardXpBasisPoints = 500;
            active.XpRewardStatus = RewardComponentStatus.Prepared;
            active.PlannedXpAmount = 0;
            doc.Active.Add(active);
            new ContractStore(path).Save(doc);
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(RewardComponentStatus.OutcomeUnknown, loaded.Active[0].XpRewardStatus,
                "prepared XP without persisted amount cannot be recalculated after reload");
        }
        finally { Cleanup(root); }
    }

    private static void TestUnstartedXpWithHiddenPlanFailsClosed()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument();
            ContractInstance active = NewActive("unstarted-hidden-plan");
            active.RewardXpBasisPoints = 500;
            active.XpRewardStatus = RewardComponentStatus.NotStarted;
            active.PlannedXpAmount = 999999;
            doc.Active.Add(active);
            new ContractStore(path).Save(doc);
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(RewardComponentStatus.OutcomeUnknown, loaded.Active[0].XpRewardStatus,
                "unstarted XP with unexpected persisted plan fails closed");
        }
        finally { Cleanup(root); }
    }

    private static void TestActiveRecordCap()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument();
            for (int i = 0; i < 12; i++) doc.Active.Add(NewActive("active-" + i.ToString()));
            new ContractStore(path).Save(doc);
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(ContractCore.MaxActiveContracts, loaded.Active.Count, "loader caps active rows even if file contains more");
        }
        finally { Cleanup(root); }
    }

    private static void TestClaimedOccurrenceSuppressesActiveRecord()
    {
        string root = NewTempDir();
        try
        {
            string path = Path.Combine(root, "contracts.dat");
            ContractDocument doc = new ContractDocument();
            ContractInstance active = NewActive("same-id"); doc.Active.Add(active); doc.Claimed.Add("same-id");
            new ContractStore(path).Save(doc);
            string warning;
            ContractDocument loaded = new ContractStore(path).Load(out warning);
            Equal(0, loaded.Active.Count, "claimed occurrence cannot reconstruct as active");
            True(loaded.Claimed.Contains("same-id"), "claimed occurrence retained");
        }
        finally { Cleanup(root); }
    }

    private static ContractInstance NewActive(string id)
    {
        ContractInstance active = new ContractInstance();
        active.OccurrenceId = id;
        active.ProviderId = "builtin";
        active.TemplateId = "global_patrol";
        active.Category = ContractCategory.Global;
        active.OriginZone = "Home";
        active.Title = "Long Watch";
        active.Description = "Test";
        active.ProgressChannel = "builtin";
        active.ProgressKey = "global_seconds";
        active.RewardText = "XP test";
        active.Target = 3600;
        active.Progress = 3600;
        active.AcceptedUtc = new DateTime(2026, 8, 14, 1, 0, 0, DateTimeKind.Utc);
        return active;
    }

    private static string V1ActiveRow(ContractInstance active)
    {
        return string.Join("|", new string[]
        {
            "A", B64(active.OccurrenceId), B64(active.ProviderId), B64(active.TemplateId), B64(active.OriginZone),
            B64(active.Title), B64(active.Description), B64(active.ProgressChannel), B64(active.ProgressKey), B64(active.ContextFilter),
            B64(active.RewardText), active.Target.ToString(), active.Progress.ToString(), B64(active.StateToken),
            active.AcceptedUtc.Ticks.ToString(), B64(active.Category), active.RewardXpBasisPoints.ToString()
        });
    }

    private static string B64(string value)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty));
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

    private static void True(bool value, string label) { _assertions++; if (!value) throw new Exception(label); }
    private static void False(bool value, string label) { True(!value, label); }
    private static void Equal<T>(T expected, T actual, string label)
    {
        _assertions++;
        if (!object.Equals(expected, actual)) throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }
}
