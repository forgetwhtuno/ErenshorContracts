using System;
using System.Collections.Generic;
using ErenshorContracts;

internal static class ContractCombatPolicyTests
{
    private static int _assertions;

    internal static int RunAll()
    {
        _assertions = 0;
        TestEnemyEligibilityExclusions();
        TestLevelAppropriateness();
        TestLocalGenerationUsesCurrentZoneOnly();
        TestLocalGenerationPersistsPerZoneWithinRevision();
        TestEmptyGenerationRetriesUntilEvidenceExists();
        TestGlobalGenerationUsesOtherObservedZones();
        TestGeneratedLocationCountsAndPriority();
        TestAbundancePreference();
        TestTargetQualityAndEvidenceCaps();
        TestLegacyGeneratedOfferQualityNormalization();
        TestCountdownFormattingAndTiming();
        TestKillCreditWrongZoneAndExactlyOnce();
        TestKillLineParsing();
        TestIncompleteClaimRejectedAndClaimExactlyOnce();
        TestPayoutUnavailableFailsClosedPolicy();
        return _assertions;
    }

    private static void TestEnemyEligibilityExclusions()
    {
        True(Eligible(), "ordinary active hostile semantic flags admitted");
        False(Eligible(simBacked: true), "Sim/player-like actor rejected");
        False(Eligible(neverAggro: true), "never-aggro scenery/friendly rejected");
        False(Eligible(miningNode: true), "resource node rejected");
        False(Eligible(treasureChest: true), "treasure chest rejected");
        False(Eligible(summonedByPlayer: true), "player summon rejected");
        False(Eligible(temporaryPvpProxy: true), "PvP proxy rejected");
        False(Eligible(ownedActor: true), "pet/owned actor rejected");
        False(Eligible(invulnerable: true), "invulnerable actor rejected");
        False(Eligible(vendor: true), "vendor rejected");
        False(Eligible(knownFriendlyFaction: true), "known friendly/debug faction rejected");
        False(Eligible(bossRewardActor: true), "boss-reward actor rejected from repeatable grind pool");
        False(Eligible(forbiddenPetIdentity: true), "pet/minion identity rejected");
        False(Eligible(alive: false), "dead actor rejected for discovery scan");
        True(Eligible(alive: false, requireAlive: false), "DoDeath candidate may bypass discovery Alive requirement");
    }

    private static void TestLevelAppropriateness()
    {
        True(ContractCombatPolicy.IsLevelAppropriate(10, 8, 12), "inside enemy range appropriate");
        True(ContractCombatPolicy.IsLevelAppropriate(10, 15, 15), "five levels above allowed");
        False(ContractCombatPolicy.IsLevelAppropriate(10, 16, 16), "six levels above rejected");
        Equal(0, ContractCombatPolicy.LevelDistance(10, 8, 12), "range distance zero");
        Equal(4, ContractCombatPolicy.LevelDistance(10, 14, 16), "range distance measured");
    }

    private static void TestLocalGenerationUsesCurrentZoneOnly()
    {
        ContractDocument doc = NewDoc();
        List<ContractEnemyObservation> scan = new List<ContractEnemyObservation>();
        scan.Add(Obs("Hidden Hills", "Brittle Skeleton", 9, 11, 3));
        scan.Add(Obs("Hidden Hills", "Young Wolf", 8, 9, 4));
        scan.Add(Obs("Bonepits", "Bone Guard", 10, 12, 2));
        scan.Add(Obs("Hidden Hills", "Raid Boss", 30, 30, 1));

        True(ContractCombatPolicy.EnsureLocalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 3, scan),
            "first local revision generated");
        Equal(2, CountGenerated(doc, ContractCategory.Local), "only current-zone level-appropriate types selected");
        List<ContractTemplate> templates = ContractCombatPolicy.BuildGeneratedTemplates(doc);
        for (int i = 0; i < templates.Count; i++)
        {
            if (!string.Equals(templates[i].Category, ContractCategory.Local, StringComparison.Ordinal)) continue;
            Equal("Hidden Hills", templates[i].TargetZone, "local target location explicit");
            True(templates[i].Description.IndexOf("in Hidden Hills", StringComparison.OrdinalIgnoreCase) >= 0,
                "local objective names zone");
        }

        scan.Add(Obs("Hidden Hills", "New Spawn", 10, 10, 1));
        False(ContractCombatPolicy.EnsureLocalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 3, scan),
            "same revision frozen against live scan churn");
        Equal(2, CountGenerated(doc, ContractCategory.Local), "same revision target set unchanged");
    }

    private static void TestLocalGenerationPersistsPerZoneWithinRevision()
    {
        ContractDocument doc = NewDoc();
        List<ContractEnemyObservation> a = new List<ContractEnemyObservation>();
        a.Add(Obs("Hidden Hills", "Brittle Skeleton", 10, 10, 4));
        List<ContractEnemyObservation> b = new List<ContractEnemyObservation>();
        b.Add(Obs("Faerie's Brake", "Forest Spider", 10, 10, 5));

        True(ContractCombatPolicy.EnsureLocalCombatBoard(doc, 2, "Hidden Hills", "p1", 10, 3, a), "A local generated");
        string aKey = FirstGeneratedKey(doc, "Hidden Hills", 2);
        True(aKey.Length > 0, "A generated target captured");
        True(ContractCombatPolicy.EnsureLocalCombatBoard(doc, 2, "Faerie's Brake", "p1", 10, 3, b), "B local generated same revision");
        string bKey = FirstGeneratedKey(doc, "Faerie's Brake", 2);
        True(bKey.Length > 0, "B generated target captured");
        False(string.Equals(aKey, bKey, StringComparison.OrdinalIgnoreCase), "A and B use independent zone target sets");
        False(ContractCombatPolicy.EnsureLocalCombatBoard(doc, 2, "Hidden Hills", "p1", 10, 3, a),
            "returning to A reuses frozen A set rather than rerolling");
        Equal(aKey, FirstGeneratedKey(doc, "Hidden Hills", 2), "A to B to A returns original generated A target");
        Equal(2, CountGenerated(doc, ContractCategory.Local), "same revision retains both per-zone generated sets");

        True(ContractCombatPolicy.EnsureLocalCombatBoard(doc, 3, "Hidden Hills", "p1", 10, 3, a), "new revision generates fresh A board");
        Equal(1, CountGenerated(doc, ContractCategory.Local), "new revision prunes prior local revision sets");
    }

    private static void TestEmptyGenerationRetriesUntilEvidenceExists()
    {
        ContractDocument doc = NewDoc();
        False(ContractCombatPolicy.EnsureLocalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 3,
            new List<ContractEnemyObservation>()), "empty first local scan leaves no fake persisted board state");
        Equal(-1, doc.LocalCombatGenerationRevision, "empty local scan does not freeze revision");
        List<ContractEnemyObservation> later = new List<ContractEnemyObservation>();
        later.Add(Obs("Hidden Hills", "Brittle Skeleton", 10, 10, 3));
        True(ContractCombatPolicy.EnsureLocalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 3, later),
            "later authoritative local evidence can populate same board revision");
        Equal(0, doc.LocalCombatGenerationRevision, "local revision freezes only after target exists");

        ContractDocument globals = NewDoc();
        False(ContractCombatPolicy.EnsureGlobalCombatBoard(globals, 0, "Hidden Hills", "p1", 10, 2),
            "empty new global scan has no persisted state to change");
        Equal(-1, globals.GlobalCombatGenerationRevision, "empty global scan remains unfrozen");
        ContractCombatPolicy.MergeObservations(globals, new List<ContractEnemyObservation> {
            Obs("Bonepits", "Bone Guard", 10, 11, 2)
        }, 10);
        True(ContractCombatPolicy.EnsureGlobalCombatBoard(globals, 0, "Hidden Hills", "p1", 10, 2),
            "newly observed other-zone evidence can populate same global revision");
        Equal(0, globals.GlobalCombatGenerationRevision, "global revision freezes only after target exists");
    }

    private static void TestGlobalGenerationUsesOtherObservedZones()
    {
        ContractDocument doc = NewDoc();
        List<ContractEnemyObservation> hidden = new List<ContractEnemyObservation>();
        hidden.Add(Obs("Hidden Hills", "Brittle Skeleton", 10, 10, 3));
        List<ContractEnemyObservation> bone = new List<ContractEnemyObservation>();
        bone.Add(Obs("Bonepits", "Bone Guard", 11, 12, 3));
        List<ContractEnemyObservation> coast = new List<ContractEnemyObservation>();
        coast.Add(Obs("Stowaway's Step", "Shore Crab", 9, 10, 3));
        ContractCombatPolicy.MergeObservations(doc, hidden, 10);
        ContractCombatPolicy.MergeObservations(doc, bone, 20);
        ContractCombatPolicy.MergeObservations(doc, coast, 30);

        True(ContractCombatPolicy.EnsureGlobalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 2),
            "first global revision generated");
        Equal(2, CountGenerated(doc, ContractCategory.Global), "two observed other-zone globals");
        for (int i = 0; i < doc.GeneratedCombatOffers.Count; i++)
        {
            ContractGeneratedCombatOffer offer = doc.GeneratedCombatOffers[i];
            if (offer == null || offer.Category != ContractCategory.Global) continue;
            False(string.Equals("Hidden Hills", offer.TargetZone, StringComparison.OrdinalIgnoreCase),
                "global never targets current zone at generation");
            True(!string.IsNullOrWhiteSpace(offer.TargetZone), "global target location always explicit");
            True(offer.TargetCount >= 1 && offer.TargetCount <= 12, "global count is bounded by deterministic range and observed population");
        }

        ContractCombatPolicy.MergeObservations(doc, new List<ContractEnemyObservation> {
            Obs("Port Azure", "Impossible Guard", 30, 30, 1)
        }, 40);
        False(ContractCombatPolicy.EnsureGlobalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 2),
            "global same revision frozen");
    }

    private static void TestGeneratedLocationCountsAndPriority()
    {
        ContractDocument doc = NewDoc();
        List<ContractEnemyObservation> scan = new List<ContractEnemyObservation>();
        scan.Add(Obs("Hidden Hills", "Brittle Skeleton", 10, 10, 3));
        ContractCombatPolicy.EnsureLocalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 3, scan);
        ContractTemplate t = ContractCombatPolicy.BuildGeneratedTemplates(doc)[0];
        True(t.Target >= 1 && t.Target <= 6, "local count capped by observed population evidence");
        Equal("Hidden Hills", ContractCore.LocationText(t, "Wrong"), "template location authoritative");
        Equal(1000, t.Priority, "generated combat priority dominates low-priority fallback");
        List<ContractTemplate> boardMix = new List<ContractTemplate>();
        boardMix.Add(ContractCore.BuildPatrolTemplate(5));
        boardMix.Add(t);
        List<ContractOffer> mixedOffers = ContractCore.BuildOffers(ContractCategory.Local, 0, "Hidden Hills", "p1", boardMix, NewDoc(), 1);
        Equal(ContractCombatPolicy.NativeKillProviderId, mixedOffers[0].Template.ProviderId, "combat fills local board before patrol fallback");
        ContractDocument acceptedDoc = NewDoc();
        List<ContractTemplate> one = new List<ContractTemplate>(); one.Add(t);
        ContractOffer offer = ContractCore.BuildOffers(ContractCategory.Local, 0, "Hidden Hills", "p1", one, acceptedDoc, 1)[0];
        ContractInstance active = ContractCore.Accept(acceptedDoc, offer, "Hidden Hills", DateTime.UtcNow);
        Equal("Hidden Hills", active.TargetZone, "accepted location persists");
        True(ContractCore.ProgressText(active).IndexOf("Brittle Skeleton", StringComparison.OrdinalIgnoreCase) >= 0,
            "kill progress names target");
    }

    private static void TestAbundancePreference()
    {
        ContractDocument doc = NewDoc();
        List<ContractEnemyObservation> scan = new List<ContractEnemyObservation>();
        scan.Add(Obs("Hidden Hills", "Sparse Enemy", 10, 10, 1));
        scan.Add(Obs("Hidden Hills", "Common Enemy", 10, 10, 7));
        ContractCombatPolicy.EnsureLocalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 1, scan);
        List<ContractTemplate> generated = ContractCombatPolicy.BuildGeneratedTemplates(doc);
        Equal(1, generated.Count, "one slot yields one grind target");
        Equal("Common Enemy", generated[0].ContextFilter, "equal-level generation prefers more plentiful enemy type");
    }

    private static void TestTargetQualityAndEvidenceCaps()
    {
        False(ContractEnemyTargetPolicy.IsLikelyExactNamedTarget("Brittle Skeleton", 3), "skeleton type remains repeatable/generic");
        False(ContractEnemyTargetPolicy.IsLikelyExactNamedTarget("Young Wolf", 4), "wolf type remains repeatable/generic");
        True(ContractEnemyTargetPolicy.IsLikelyExactNamedTarget("Trevor Ulchand", 1), "one-off proper identity becomes exact bounty-style target");
        True(ContractEnemyTargetPolicy.IsLikelyExactNamedTarget("Trevor Ulchand", 3), "proper personal-name shape stays exact even if duplicate actors are observed");
        Equal(1, ContractEnemyTargetPolicy.ResolveTargetCount(ContractCategory.Local, "named", "Trevor Ulchand", 7),
            "named target can never become Kill 10 proper name");

        int local = ContractEnemyTargetPolicy.ResolveTargetCount(ContractCategory.Local, "ordinary", "Brittle Skeleton", 3);
        True(local >= 1 && local <= 6, "ordinary local target capped to twice observed population");
        int global = ContractEnemyTargetPolicy.ResolveTargetCount(ContractCategory.Global, "ordinary-global", "Young Wolf", 2);
        True(global >= 1 && global <= 6, "ordinary global target capped to three times observed population");
        Equal("Brittle Skeletons", ContractEnemyTargetPolicy.BuildDisplayTarget("Brittle Skeleton", 4, 3),
            "ordinary repeatable objective uses generic/plural presentation");
        Equal("Young Wolves", ContractEnemyTargetPolicy.BuildDisplayTarget("Young Wolf", 4, 4),
            "wolf generic presentation pluralizes naturally");
        Equal("Trevor Ulchand", ContractEnemyTargetPolicy.BuildDisplayTarget("Trevor Ulchand", 1, 1),
            "exact bounty presentation keeps exact native display identity");

        ContractDocument doc = NewDoc();
        List<ContractEnemyObservation> scan = new List<ContractEnemyObservation>();
        scan.Add(Obs("Hidden Hills", "Trevor Ulchand", 10, 10, 1));
        scan.Add(Obs("Hidden Hills", "Brittle Skeleton", 10, 10, 4));
        ContractCombatPolicy.EnsureLocalCombatBoard(doc, 0, "Hidden Hills", "p1", 10, 2, scan);
        List<ContractTemplate> generated = ContractCombatPolicy.BuildGeneratedTemplates(doc);
        Equal(2, generated.Count, "eligible present targets generate bounded local offers");
        Equal("Brittle Skeleton", generated[0].ContextFilter, "ordinary repeated mob is preferred ahead of named target");
        for (int i = 0; i < generated.Count; i++)
        {
            Equal("Hidden Hills", generated[i].TargetZone, "generated target is present in intended zone");
            if (string.Equals(generated[i].ContextFilter, "Trevor Ulchand", StringComparison.OrdinalIgnoreCase))
            {
                Equal(1, generated[i].Target, "named generated target has bounty-like count one");
                True(generated[i].Title.IndexOf("Bounty", StringComparison.OrdinalIgnoreCase) >= 0, "named target uses bounty presentation");
            }
        }
    }

    private static void TestLegacyGeneratedOfferQualityNormalization()
    {
        ContractDocument doc = NewDoc();
        ContractEnemyRecord enemy = new ContractEnemyRecord();
        enemy.Zone = "Hidden Hills"; enemy.EnemyName = "Trevor Ulchand"; enemy.MinLevel = 10; enemy.MaxLevel = 10;
        enemy.ObservedCount = 1; doc.EnemyCatalog.Add(enemy);
        ContractGeneratedCombatOffer old = new ContractGeneratedCombatOffer();
        old.Category = ContractCategory.Local; old.BoardRevision = 0; old.BoardZone = "Hidden Hills";
        old.TargetZone = "Hidden Hills"; old.EnemyName = "Trevor Ulchand"; old.TargetCount = 10;
        True(ContractCombatPolicy.NormalizeGeneratedOfferForCurrentEvidence(doc, old), "legacy unaccepted generated target can be narrowed safely");
        Equal(1, old.TargetCount, "legacy proper-name offer normalized to one target");

        // Accepted instances are a separate persisted model and are not passed through generated-offer normalization.
        ContractInstance accepted = new ContractInstance();
        accepted.OriginZone = "Hidden Hills"; accepted.TargetZone = "Hidden Hills"; accepted.ContextFilter = "Trevor Ulchand";
        accepted.Target = 10; accepted.Progress = 2;
        Equal(10, accepted.Target, "old accepted contract remains self-contained and is not rewritten");
    }

    private static void TestCountdownFormattingAndTiming()
    {
        ContractDocument doc = NewDoc();
        Equal("00:45:00", ContractCore.FormatRefreshCountdown(ContractCore.SecondsUntilLocalRefresh(doc)),
            "initial local countdown seconds");
        Equal("02:00:00", ContractCore.FormatRefreshCountdown(ContractCore.SecondsUntilGlobalRefresh(doc)),
            "initial global countdown seconds");
        ContractCore.AdvanceActivePlay(doc, 19, 45, 120);
        Equal("00:44:41", ContractCore.FormatRefreshCountdown(ContractCore.SecondsUntilLocalRefresh(doc)),
            "countdown ticks exact active seconds");
        ContractRefreshResult refresh = ContractCore.AdvanceActivePlay(doc, 2681, 45, 120);
        True(refresh.LocalRefreshed, "refresh exactly at zero boundary");
        Equal("00:45:00", ContractCore.FormatRefreshCountdown(ContractCore.SecondsUntilLocalRefresh(doc)),
            "next revision countdown resets to full active cadence");
    }

    private static void TestKillCreditWrongZoneAndExactlyOnce()
    {
        ContractDocument doc = NewDoc();
        ContractInstance active = new ContractInstance();
        active.OccurrenceId = "kill";
        active.Category = ContractCategory.Local;
        active.ProgressKey = ContractCombatPolicy.NativeKillProgressKey;
        active.ContextFilter = "Brittle Skeleton";
        active.TargetZone = "Hidden Hills";
        active.Target = 2;
        doc.Active.Add(active);

        Equal(0, ContractCombatPolicy.RecordQualifyingKill(doc, "Bonepits", "Brittle Skeleton"), "wrong zone rejected");
        Equal(0, active.Progress, "wrong zone no progress");
        Equal(1, ContractCombatPolicy.RecordQualifyingKill(doc, "Hidden Hills", "Brittle Skeleton"), "qualifying kill increments one active contract");
        Equal(1, active.Progress, "one credit");
        Equal(0, ContractCombatPolicy.RecordQualifyingKill(doc, "Hidden Hills", "Young Wolf"), "wrong enemy rejected");
        Equal(1, active.Progress, "wrong enemy no progress");
        Equal(1, ContractCombatPolicy.RecordQualifyingKill(doc, "Hidden Hills", "A Brittle Skeleton!"), "normalized enemy credits");
        Equal(2, active.Progress, "target reached");
        Equal(0, ContractCombatPolicy.RecordQualifyingKill(doc, "Hidden Hills", "Brittle Skeleton"), "complete contract cannot over-credit");
        Equal(2, active.Progress, "complete remains exact");
    }

    private static void TestKillLineParsing()
    {
        string enemy, killer; bool local;
        True(ContractKillCreditPolicy.TryParseKillLine("You have slain A Young Wolf!", out enemy, out killer, out local),
            "local kill line recognized");
        Equal("Young Wolf", enemy, "local enemy normalized");
        True(local, "local attribution explicit");
        True(ContractKillCreditPolicy.TryParseKillLine("Brittle Skeleton has been slain by Phanty!", out enemy, out killer, out local),
            "party kill line recognized");
        Equal("Phanty", killer, "party killer captured");
        False(local, "party form not falsely local");
        False(ContractKillCreditPolicy.TryParseKillLine("Brittle Skeleton disappears.", out enemy, out killer, out local),
            "despawn text is not kill credit");
    }

    private static void TestIncompleteClaimRejectedAndClaimExactlyOnce()
    {
        ContractDocument doc = NewDoc();
        ContractInstance active = new ContractInstance();
        active.OccurrenceId = "claim-once";
        active.Category = ContractCategory.Local;
        active.Target = 2;
        active.Progress = 1;
        doc.Active.Add(active);
        True(ContractCore.FindClaimable(doc, active.OccurrenceId) == null, "incomplete claim rejected");
        active.Progress = 2;
        ContractInstance first = ContractCore.ClaimRecordOnly(doc, active.OccurrenceId);
        True(first != null, "complete record-only claim succeeds");
        True(ContractCore.ClaimRecordOnly(doc, active.OccurrenceId) == null, "second claim rejected exactly once");
    }

    private static void TestPayoutUnavailableFailsClosedPolicy()
    {
        False(ContractRewardAuthorityPolicy.CanAttemptXp(false, true), "XP disabled gate blocks proven symbol");
        False(ContractRewardAuthorityPolicy.CanAttemptXp(true, false), "missing XP symbol blocks enabled gate");
        True(ContractRewardAuthorityPolicy.CanAttemptXp(true, true), "XP only admitted when both evidence gates true");
        False(ContractRewardAuthorityPolicy.CanAttemptGold(false), "gold unavailable without exact native authority");
    }

    private static bool Eligible(
        bool active = true, bool simBacked = false, bool neverAggro = false, bool miningNode = false,
        bool treasureChest = false, bool summonedByPlayer = false, bool temporaryPvpProxy = false,
        bool hasCharacter = true, bool ownedActor = false, bool invulnerable = false, bool vendor = false,
        bool knownFriendlyFaction = false, bool bossRewardActor = false, bool requireAlive = true,
        bool alive = true, bool forbiddenPetIdentity = false)
    {
        return ContractEnemyEligibilityPolicy.IsEligible(active, simBacked, neverAggro, miningNode, treasureChest,
            summonedByPlayer, temporaryPvpProxy, hasCharacter, ownedActor, invulnerable, vendor, knownFriendlyFaction,
            bossRewardActor, requireAlive, alive, forbiddenPetIdentity);
    }

    private static ContractEnemyObservation Obs(string zone, string name, int min, int max, int count)
    {
        ContractEnemyObservation value = new ContractEnemyObservation();
        value.Zone = zone; value.EnemyName = name; value.MinLevel = min; value.MaxLevel = max; value.Count = count;
        return value;
    }

    private static string FirstGeneratedKey(ContractDocument doc, string zone, int revision)
    {
        for (int i = 0; i < doc.GeneratedCombatOffers.Count; i++)
        {
            ContractGeneratedCombatOffer value = doc.GeneratedCombatOffers[i];
            if (value == null || value.BoardRevision != revision) continue;
            if (!string.Equals(value.BoardZone, zone, StringComparison.OrdinalIgnoreCase)) continue;
            return value.TargetZone + "|" + value.EnemyName + "|" + value.TargetCount.ToString();
        }
        return string.Empty;
    }

    private static int CountGenerated(ContractDocument doc, string category)
    {
        int count = 0;
        for (int i = 0; i < doc.GeneratedCombatOffers.Count; i++)
            if (doc.GeneratedCombatOffers[i] != null &&
                string.Equals(ContractCategory.Normalize(doc.GeneratedCombatOffers[i].Category), category, StringComparison.Ordinal))
                count++;
        return count;
    }

    private static ContractDocument NewDoc()
    {
        ContractDocument doc = new ContractDocument();
        ContractCore.AdvanceActivePlay(doc, 0, 45, 120);
        return doc;
    }

    private static void True(bool value, string label) { _assertions++; if (!value) throw new Exception(label); }
    private static void False(bool value, string label) { True(!value, label); }
    private static void Equal<T>(T expected, T actual, string label)
    {
        _assertions++;
        if (!object.Equals(expected, actual)) throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }
}
