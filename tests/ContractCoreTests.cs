using System;
using System.Collections.Generic;
using ErenshorContracts;

internal static class ContractCoreTests
{
    private static int _assertions;

    internal static int RunAll()
    {
        _assertions = 0;
        TestCatalogSizeAndClassification();
        TestBoardDeterminismNoDuplicatesAndProviderPriority();
        TestRewardPolicy();
        TestActivePlayRefreshCadenceAndOfflineNeutrality();
        TestLocalBoardUsesRevisionPlusCurrentZone();
        TestActivePlayEligibility();
        TestWholeRevisionRefreshNoImmediateReplacement();
        TestAbandonReacceptWithoutReroll();
        TestLocalPatrolScope();
        TestRoadCheck();
        TestPerimeterSweep();
        TestLocalWayfarerUniqueZones();
        TestLocalCircuit();
        TestGlobalLongWatchAcrossZones();
        TestGlobalGrandTour();
        TestGlobalExpeditionRequiresBothCriteria();
        TestContractRegularSpansLocalRotation();
        TestNonGameplayTransitionsDoNotProgress();
        TestActivePlayOverflowSafety();
        TestPlannedXpAmountLocksAcrossRetry();
        TestRewardComponentLedgerExactlyOnce();
        TestZoneBoardChangeDoesNotChangeAcceptedClaimIdentity();
        TestMixedComponentRetry();
        TestUnknownRewardLocksAbandonAndCommit();
        TestAppliedRewardSummary();
        TestRecordOnlyProviderClaim();
        TestLocalCompletionProgressesGlobal();
        TestPlayerFacingProgressFormatting();
        TestCompletionSummaries();
        TestJournalEntryAndClaimDeduplication();
        TestContextFilter();
        return _assertions;
    }

    private static void TestCatalogSizeAndClassification()
    {
        List<ContractTemplate> templates = Builtins();
        int locals = 0;
        int globals = 0;
        for (int i = 0; i < templates.Count; i++)
        {
            if (string.Equals(ContractCategory.Normalize(templates[i].Category), ContractCategory.Global, StringComparison.Ordinal)) globals++;
            else locals++;
        }
        Equal(5, locals, "five focused local templates");
        Equal(4, globals, "four focused global templates");

        ContractDocument doc = NewDocumentWithSchedule();
        List<ContractOffer> local = ContractCore.BuildOffers(ContractCategory.Local, 0, "Home", "p1", templates, doc, 3);
        List<ContractOffer> global = ContractCore.BuildOffers(ContractCategory.Global, 0, "Home", "p1", templates, doc, 2);
        Equal(3, local.Count, "default board has three local offers");
        Equal(2, global.Count, "default board has two global offers");
        for (int i = 0; i < local.Count; i++) Equal(ContractCategory.Local, ContractCategory.Normalize(local[i].Template.Category), "local classification");
        for (int i = 0; i < global.Count; i++) Equal(ContractCategory.Global, ContractCategory.Normalize(global[i].Template.Category), "global classification");
    }

    private static void TestBoardDeterminismNoDuplicatesAndProviderPriority()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        List<ContractTemplate> templates = Builtins();
        List<ContractOffer> first = ContractCore.BuildOffers(ContractCategory.Local, 4, "Home", "p1", templates, doc, 3);
        List<ContractOffer> again = ContractCore.BuildOffers(ContractCategory.Local, 4, "Home", "p1", templates, doc, 3);
        for (int i = 0; i < first.Count; i++) Equal(first[i].OccurrenceId, again[i].OccurrenceId, "same board revision deterministic");
        NotEqual(first[0].OccurrenceId,
            ContractCore.BuildOffers(ContractCategory.Local, 5, "Home", "p1", templates, doc, 3)[0].OccurrenceId,
            "revision changes occurrence identity");

        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < first.Count; i++) True(ids.Add(first[i].Template.ProviderId + "|" + first[i].Template.TemplateId), "no duplicate local templates");

        ContractTemplateRegistration reg = ProviderRegistration();
        reg.ProviderId = "priority_provider";
        reg.Priority = 100;
        templates.Add(ContractCore.FromRegistration(reg));
        List<ContractOffer> top = ContractCore.BuildOffers(ContractCategory.Local, 4, "Home", "p1", templates, doc, 1);
        Equal("priority_provider", top[0].Template.ProviderId, "provider priority remains authoritative");
    }

    private static void TestRewardPolicy()
    {
        Equal(300, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "road_check"), "road reward");
        Equal(400, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "local_perimeter"), "perimeter reward");
        Equal(500, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "local_circuit"), "circuit reward");
        Equal(500, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "wayfarer"), "wayfarer reward");
        Equal(600, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Local, "local_patrol"), "patrol reward");
        Equal(1200, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Global, "global_local_completions"), "regular reward");
        Equal(1500, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Global, "global_patrol"), "watch reward");
        Equal(1700, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Global, "global_expedition"), "expedition reward");
        Equal(1800, ContractRewardPolicy.ResolveXpBasisPoints(ContractCategory.Global, "global_wayfarer"), "tour reward");
        Equal(600, ContractRewardPolicy.CalculateXpAmount(10000, 600), "6 percent scaled XP");
        Equal(1700, ContractRewardPolicy.CalculateXpAmount(10000, 1700), "17 percent scaled XP");
        Equal(1, ContractRewardPolicy.CalculateXpAmount(1, 300), "small threshold still grants minimum one when configured");
        Equal(0, ContractRewardPolicy.CalculateXpAmount(10000, 0), "record-only policy zero");
    }

    private static void TestActivePlayRefreshCadenceAndOfflineNeutrality()
    {
        ContractDocument doc = new ContractDocument();
        ContractCore.AdvanceActivePlay(doc, 0, 45, 120);
        Equal(2700L, doc.NextLocalRefreshAtSeconds, "local 45 minute cadence initialized");
        Equal(7200L, doc.NextGlobalRefreshAtSeconds, "global 120 minute cadence initialized");
        DateTime irrelevantWallClock = DateTime.UtcNow.AddDays(30);
        True(irrelevantWallClock > DateTime.UtcNow, "wall-clock fixture created");
        ContractCore.AdvanceActivePlay(doc, 0, 45, 120);
        Equal(0L, doc.ActivePlaySeconds, "offline/wall clock does not advance active play");

        ContractRefreshResult almost = ContractCore.AdvanceActivePlay(doc, 2699, 45, 120);
        False(almost.AnyRefreshed, "no early local refresh");
        ContractRefreshResult local = ContractCore.AdvanceActivePlay(doc, 1, 45, 120);
        True(local.LocalRefreshed, "local refresh at 45 active minutes");
        False(local.GlobalRefreshed, "global not yet refreshed");
        ContractRefreshResult later = ContractCore.AdvanceActivePlay(doc, 4500, 45, 120);
        True(later.LocalRefreshed, "second local refresh occurs");
        True(later.GlobalRefreshed, "global refresh occurs by two hours");
        Equal(2, doc.LocalBoardRevision, "two local revisions");
        Equal(1, doc.GlobalBoardRevision, "one global revision");
    }

    private static void TestLocalBoardUsesRevisionPlusCurrentZone()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        long originalDeadline = doc.NextLocalRefreshAtSeconds;
        int originalRevision = doc.LocalBoardRevision;

        List<ContractOffer> home = ContractCore.BuildOffers(ContractCategory.Local, doc.LocalBoardRevision, "Home", "p1", Builtins(), doc, 3);
        List<ContractOffer> homeAgain = ContractCore.BuildOffers(ContractCategory.Local, doc.LocalBoardRevision, "Home", "p1", Builtins(), doc, 3);
        List<ContractOffer> forest = ContractCore.BuildOffers(ContractCategory.Local, doc.LocalBoardRevision, "Forest", "p1", Builtins(), doc, 3);
        List<ContractOffer> homeAfterTravel = ContractCore.BuildOffers(ContractCategory.Local, doc.LocalBoardRevision, "Home", "p1", Builtins(), doc, 3);

        Equal(home[0].OccurrenceId, homeAgain[0].OccurrenceId, "same revision and zone returns same local board");
        NotEqual(home[0].OccurrenceId, forest[0].OccurrenceId, "same revision different zone has zone-correct local identity");
        Equal(home[0].OccurrenceId, homeAfterTravel[0].OccurrenceId, "A to B to A returns original A local offer set");

        ContractCore.HandleZoneTransition(doc, "Home", "Forest");
        Equal(originalRevision, doc.LocalBoardRevision, "zoning does not advance local board revision");
        Equal(originalDeadline, doc.NextLocalRefreshAtSeconds, "zoning does not reset local refresh deadline");

        ContractInstance accepted = ContractCore.Accept(doc, home[0], "Home", DateTime.UtcNow);
        True(accepted != null, "local offer accepted in A");
        Equal("Home", accepted.OriginZone, "accepted local contract retains A origin");
        List<ContractOffer> forestAfterAccept = ContractCore.BuildOffers(ContractCategory.Local, doc.LocalBoardRevision, "Forest", "p1", Builtins(), doc, 3);
        True(forestAfterAccept.Count > 0, "unaccepted local board remains available in B");
        True(forestAfterAccept[0].OccurrenceId.IndexOf("|Forest|", StringComparison.OrdinalIgnoreCase) >= 0,
            "B local occurrence identity carries B zone");
        Equal("Home", accepted.OriginZone, "building B board does not rewrite accepted A origin");

        List<ContractOffer> globalHome = ContractCore.BuildOffers(ContractCategory.Global, doc.GlobalBoardRevision, "Home", "p1", Builtins(), doc, 2);
        List<ContractOffer> globalForest = ContractCore.BuildOffers(ContractCategory.Global, doc.GlobalBoardRevision, "Forest", "p1", Builtins(), doc, 2);
        Equal(globalHome[0].OccurrenceId, globalForest[0].OccurrenceId, "global board identity unaffected by current zone");

        ContractCore.AdvanceActivePlay(doc, 45 * 60, 45, 120);
        Equal(originalRevision + 1, doc.LocalBoardRevision, "local refresh still advances normally from active play");
        True(doc.NextLocalRefreshAtSeconds > originalDeadline, "local refresh advances deadline normally");

        // The legacy persisted field remains readable/migratable, but no longer controls runtime
        // available-board identity.
        ContractDocument legacy = NewDocumentWithSchedule();
        legacy.LocalBoardZone = "OldHome";
        False(ContractCore.EnsureLocalBoardZone(legacy, "LoginHome"), "legacy persisted board zone remains readable without destructive migration");
        Equal("OldHome", legacy.LocalBoardZone, "legacy board-origin field remains intact");
    }

    private static void TestActivePlayEligibility()
    {
        True(ContractCore.ShouldAccrueActivePlay(true, "Home", true, true), "focused running gameplay accrues active play");
        False(ContractCore.ShouldAccrueActivePlay(false, "Home", true, true), "not fully in world does not accrue");
        False(ContractCore.ShouldAccrueActivePlay(true, "MainMenu", true, true), "non-game zone does not accrue");
        False(ContractCore.ShouldAccrueActivePlay(true, "Home", false, true), "background/unfocused game does not accrue");
        False(ContractCore.ShouldAccrueActivePlay(true, "Home", true, false), "paused simulation does not accrue");
    }

    private static void TestWholeRevisionRefreshNoImmediateReplacement()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        List<ContractTemplate> templates = Builtins();
        List<ContractOffer> before = ContractCore.BuildOffers(ContractCategory.Local, 0, "Home", "p1", templates, doc, 3);
        ContractInstance active = ContractCore.Accept(doc, before[0], "Home", DateTime.UtcNow);
        CompleteAndApplyXp(doc, active, 25);
        True(ContractCore.CommitClaim(doc, active.OccurrenceId) != null, "claim commits after applied component");
        List<ContractOffer> same = ContractCore.BuildOffers(ContractCategory.Local, 0, "Home", "p1", templates, doc, 3);
        ContractOffer claimed = FindOffer(same, active.OccurrenceId);
        True(claimed != null && claimed.Claimed, "claimed slot remains until category refresh");
        ContractCore.AdvanceActivePlay(doc, 45 * 60, 45, 120);
        List<ContractOffer> refreshed = ContractCore.BuildOffers(ContractCategory.Local, doc.LocalBoardRevision, "Home", "p1", templates, doc, 3);
        False(ContainsOffer(refreshed, active.OccurrenceId), "whole local revision replaced old board slots");
    }

    private static void TestAbandonReacceptWithoutReroll()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        List<ContractTemplate> templates = Builtins();
        ContractOffer offer = ContractCore.BuildOffers(ContractCategory.Local, 0, "Home", "p1", templates, doc, 3)[0];
        ContractInstance first = ContractCore.Accept(doc, offer, "Home", DateTime.UtcNow);
        first.Progress = Math.Min(first.Target, 1);
        True(ContractCore.Abandon(doc, first.OccurrenceId), "abandon before reward transaction");
        ContractOffer same = FindOffer(ContractCore.BuildOffers(ContractCategory.Local, 0, "Home", "p1", templates, doc, 3), first.OccurrenceId);
        True(same != null && !same.Claimed, "abandon does not reroll");
        ContractInstance second = ContractCore.Accept(doc, same, "Home", DateTime.UtcNow);
        Equal(0, second.Progress, "reaccept starts clean");
        Equal(first.OccurrenceId, second.OccurrenceId, "same board occurrence reaccepted");
    }

    private static void TestLocalPatrolScope()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildPatrolTemplate(15), ContractCategory.Local, "Home");
        Equal(0, ContractCore.AddActiveSeconds(doc, "Away", 60), "patrol ignores other zone");
        Equal(0, active.Progress, "no local leak");
        Equal(1, ContractCore.AddActiveSeconds(doc, "Home", 60), "patrol advances in origin");
        Equal(60, active.Progress, "patrol amount");
    }

    private static void TestRoadCheck()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildRoadCheckTemplate(), ContractCategory.Local, "Home");
        Equal(1, ContractCore.HandleZoneTransition(doc, "Home", "Road"), "leaving starts away state");
        Equal(1, ContractCore.AddActiveSeconds(doc, "Road", ContractCore.RoadCheckAwaySeconds - 1), "away time accrues");
        Equal(1, ContractCore.HandleZoneTransition(doc, "Road", "Home"), "early return recorded");
        False(active.IsComplete, "early return does not complete");
        ContractCore.HandleZoneTransition(doc, "Home", "Road");
        ContractCore.AddActiveSeconds(doc, "Road", 1);
        ContractCore.HandleZoneTransition(doc, "Road", "Home");
        True(active.IsComplete, "qualified out-and-back completes");
    }

    private static void TestPerimeterSweep()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildPerimeterSweepTemplate(), ContractCategory.Local, "Home");
        ContractCore.AddActiveSeconds(doc, "Away", 600);
        Equal(0, active.Progress, "perimeter time cannot accrue away");
        ContractCore.AddActiveSeconds(doc, "Home", 599);
        Equal(0, active.Progress, "perimeter not ready early");
        ContractCore.HandleZoneTransition(doc, "Home", "Road");
        False(active.IsComplete, "early departure does not complete");
        ContractCore.HandleZoneTransition(doc, "Road", "Home");
        ContractCore.AddActiveSeconds(doc, "Home", 1);
        Equal(1, active.Progress, "ten local minutes arms departure phase");
        ContractCore.HandleZoneTransition(doc, "Home", "Road");
        True(active.IsComplete, "departure after local patrol completes");
    }

    private static void TestLocalWayfarerUniqueZones()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildWayfarerTemplate(), ContractCategory.Local, "Home");
        ContractCore.HandleZoneTransition(doc, "Home", "A");
        ContractCore.HandleZoneTransition(doc, "A", "Home");
        ContractCore.HandleZoneTransition(doc, "Home", "A");
        ContractCore.HandleZoneTransition(doc, "A", "B");
        ContractCore.HandleZoneTransition(doc, "B", "C");
        Equal(3, active.Progress, "three unique away zones only once");
        True(active.IsComplete, "wayfarer complete");
    }

    private static void TestLocalCircuit()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildLocalCircuitTemplate(), ContractCategory.Local, "Home");
        ContractCore.HandleZoneTransition(doc, "Home", "A");
        ContractCore.HandleZoneTransition(doc, "A", "Home");
        False(active.IsComplete, "return after only one away zone does not complete");
        ContractCore.HandleZoneTransition(doc, "Home", "B");
        Equal(2, active.Progress, "two unique away zones recorded");
        ContractCore.HandleZoneTransition(doc, "B", "C");
        Equal(2, active.Progress, "extra away zone does not replace return phase");
        ContractCore.HandleZoneTransition(doc, "C", "Home");
        True(active.IsComplete, "circuit completes on return to origin");
    }

    private static void TestGlobalLongWatchAcrossZones()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildGlobalPatrolTemplate(60), ContractCategory.Global, "Home");
        ContractCore.AddActiveSeconds(doc, "Home", 30);
        ContractCore.AddActiveSeconds(doc, "Away", 30);
        Equal(60, active.Progress, "global time crosses zones");
    }

    private static void TestGlobalGrandTour()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildGlobalWayfarerTemplate(), ContractCategory.Global, "Home");
        string old = "Home";
        for (int i = 0; i < 8; i++)
        {
            string next = "Zone" + i.ToString();
            ContractCore.HandleZoneTransition(doc, old, next);
            old = next;
        }
        True(active.IsComplete, "grand tour survives cross-zone travel and completes at eight");
    }

    private static void TestGlobalExpeditionRequiresBothCriteria()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildGlobalExpeditionTemplate(), ContractCategory.Global, "Home");
        ContractCore.AddActiveSeconds(doc, "Home", ContractCore.GlobalExpeditionSeconds);
        Equal(1, active.Progress, "time criterion alone is half complete");
        False(active.IsComplete, "time alone cannot complete expedition");
        string old = "Home";
        for (int i = 0; i < ContractCore.GlobalExpeditionZones; i++)
        {
            string next = "Exp" + i.ToString();
            ContractCore.HandleZoneTransition(doc, old, next);
            old = next;
        }
        Equal(2, active.Progress, "zone criterion plus time completes both criteria");
        True(active.IsComplete, "expedition complete");
    }

    private static void TestContractRegularSpansLocalRotation()
    {
        ContractTemplate regular = ContractCore.BuildGlobalLocalCompletionsTemplate();
        Equal(4, regular.Target, "regular requires four local claims");
        True(regular.Target > 3, "default three-slot local board cannot finish Regular in one fresh revision");
    }

    private static void TestNonGameplayTransitionsDoNotProgress()
    {
        ContractDocument roadDoc = NewDocumentWithSchedule();
        ContractInstance road = AcceptOnly(roadDoc, ContractCore.BuildRoadCheckTemplate(), ContractCategory.Local, "Home");
        Equal(0, ContractCore.HandleZoneTransition(roadDoc, "Home", "MainMenu"), "menu transition ignored");
        Equal(string.Empty, road.StateToken, "menu does not mark road away");

        ContractDocument tourDoc = NewDocumentWithSchedule();
        ContractInstance tour = AcceptOnly(tourDoc, ContractCore.BuildGlobalWayfarerTemplate(), ContractCategory.Global, "Home");
        Equal(0, ContractCore.HandleZoneTransition(tourDoc, "Home", "Loading"), "loading transition ignored");
        Equal(0, ContractCore.HandleZoneTransition(tourDoc, "Home", "LoadScreen"), "load-screen transition ignored");
        Equal(0, ContractCore.HandleZoneTransition(tourDoc, "Home", "CharSelect"), "character-select transition ignored");
        Equal(0, tour.Progress, "non-game scenes are not unique zones");
        Equal(0, ContractCore.AddActiveSeconds(tourDoc, "Title", 60), "title cannot accrue objective time");
    }

    private static void TestActivePlayOverflowSafety()
    {
        ContractDocument doc = new ContractDocument();
        doc.ActivePlaySeconds = long.MaxValue - 5L;
        doc.NextLocalRefreshAtSeconds = long.MaxValue;
        doc.NextGlobalRefreshAtSeconds = long.MaxValue;
        ContractCore.AdvanceActivePlay(doc, 10, 45, 120);
        Equal(long.MaxValue, doc.ActivePlaySeconds, "active-play clock saturates");
        Equal(long.MaxValue, doc.NextLocalRefreshAtSeconds, "local threshold saturates");
        Equal(long.MaxValue, doc.NextGlobalRefreshAtSeconds, "global threshold saturates");
        int localRevision = doc.LocalBoardRevision;
        int globalRevision = doc.GlobalBoardRevision;
        ContractRefreshResult again = ContractCore.AdvanceActivePlay(doc, 1, 45, 120);
        False(again.AnyRefreshed, "saturated thresholds do not refresh every later tick");
        Equal(localRevision, doc.LocalBoardRevision, "saturated local revision remains stable");
        Equal(globalRevision, doc.GlobalBoardRevision, "saturated global revision remains stable");

        ContractDocument init = new ContractDocument();
        init.ActivePlaySeconds = long.MaxValue - 10L;
        ContractCore.AdvanceActivePlay(init, 0, 45, 120);
        Equal(long.MaxValue, init.NextLocalRefreshAtSeconds, "near-max local initialization saturates");
        Equal(long.MaxValue, init.NextGlobalRefreshAtSeconds, "near-max global initialization saturates");
    }

    private static void TestPlannedXpAmountLocksAcrossRetry()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildPatrolTemplate(15), ContractCategory.Local, "Home");
        active.Progress = active.Target;
        True(ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, RewardComponentKind.Xp, 123), "first XP plan prepared");
        Equal(123, active.PlannedXpAmount, "planned XP persisted on contract state");
        True(ContractCore.MarkRewardComponentApplying(doc, active.OccurrenceId, RewardComponentKind.Xp), "planned XP enters applying");
        True(ContractCore.MarkRewardComponentRetryable(doc, active.OccurrenceId, RewardComponentKind.Xp), "pre-known failure can retry");
        False(ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, RewardComponentKind.Xp, 124), "retry cannot silently change planned XP amount");
        True(ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, RewardComponentKind.Xp, 123), "retry reuses same planned XP");
        True(ContractCore.MarkRewardComponentApplying(doc, active.OccurrenceId, RewardComponentKind.Xp), "retry applying");
        False(ContractCore.MarkRewardComponentApplied(doc, active.OccurrenceId, RewardComponentKind.Xp, 124, "+124 XP"), "applied XP must match persisted plan");
        True(ContractCore.MarkRewardComponentApplied(doc, active.OccurrenceId, RewardComponentKind.Xp, 123, "+123 XP"), "matching planned XP applies");
    }

    private static void TestRewardComponentLedgerExactlyOnce()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildPatrolTemplate(15), ContractCategory.Local, "Home");
        active.Progress = active.Target;
        True(ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, RewardComponentKind.Xp), "xp prepared");
        False(ContractCore.Abandon(doc, active.OccurrenceId), "cannot abandon after reward transaction starts");
        True(ContractCore.MarkRewardComponentApplying(doc, active.OccurrenceId, RewardComponentKind.Xp), "xp applying");
        True(ContractCore.MarkRewardComponentApplied(doc, active.OccurrenceId, RewardComponentKind.Xp, 42, "+42 XP"), "xp applied");
        ApplyComponent(doc, active, RewardComponentKind.Gold, active.RewardGoldAmount, "+" + active.RewardGoldAmount.ToString() + " Gold");
        Equal(RewardComponentStatus.Applied, active.XpRewardStatus, "xp applied status");
        ContractInstance completed = ContractCore.CommitClaim(doc, active.OccurrenceId);
        True(completed != null, "claim commits with required component applied");
        True(doc.Claimed.Contains(active.OccurrenceId), "occurrence claimed");
        True(ContractCore.CommitClaim(doc, active.OccurrenceId) == null, "second commit blocked");
        Equal(1, doc.TotalCompleted, "completion counted once");
    }

    private static void TestMixedComponentRetry()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildPatrolTemplate(15), ContractCategory.Local, "Home");
        active.Progress = active.Target;
        active.RewardGoldAmount = 10;
        active.RewardItemId = "common_resource";
        active.RewardItemName = "Common Resource";
        active.RewardItemQuantity = 2;

        ApplyComponent(doc, active, RewardComponentKind.Xp, 50, "+50 XP");
        True(ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, RewardComponentKind.Gold), "gold prepared after XP applied");
        True(ContractCore.MarkRewardComponentApplying(doc, active.OccurrenceId, RewardComponentKind.Gold), "gold applying");
        True(ContractCore.MarkRewardComponentRetryable(doc, active.OccurrenceId, RewardComponentKind.Gold), "gold failure retryable");
        Equal(RewardComponentStatus.Applied, active.XpRewardStatus, "successful XP remains applied while gold retries");
        True(ContractCore.CommitClaim(doc, active.OccurrenceId) == null, "mixed claim cannot commit with retryable gold");

        True(ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, RewardComponentKind.Gold), "retry gold prepares without touching XP");
        True(ContractCore.MarkRewardComponentApplying(doc, active.OccurrenceId, RewardComponentKind.Gold), "retry gold applying");
        True(ContractCore.MarkRewardComponentApplied(doc, active.OccurrenceId, RewardComponentKind.Gold, 10, "+10 gold"), "gold applied");
        ApplyComponent(doc, active, RewardComponentKind.Item, 2, "Common Resource");
        True(ContractCore.AllConfiguredRewardsApplied(active), "all mixed components applied");
        True(ContractCore.CommitClaim(doc, active.OccurrenceId) != null, "mixed claim commits exactly once");
    }

    private static void TestUnknownRewardLocksAbandonAndCommit()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildPatrolTemplate(15), ContractCategory.Local, "Home");
        active.Progress = active.Target;
        ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, RewardComponentKind.Xp);
        ContractCore.MarkRewardComponentApplying(doc, active.OccurrenceId, RewardComponentKind.Xp);
        ContractCore.MarkRewardComponentUnknown(doc, active.OccurrenceId, RewardComponentKind.Xp);
        True(ContractCore.HasUnknownRewardOutcome(active), "unknown native outcome recognized");
        False(ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, RewardComponentKind.Xp), "unknown outcome cannot retry");
        False(ContractCore.Abandon(doc, active.OccurrenceId), "unknown outcome cannot discard ledger");
        True(ContractCore.CommitClaim(doc, active.OccurrenceId) == null, "unknown outcome cannot commit");
    }

    private static void TestZoneBoardChangeDoesNotChangeAcceptedClaimIdentity()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        List<ContractOffer> home = ContractCore.BuildOffers(ContractCategory.Local, 0, "Home", "p1", Builtins(), doc, 3);
        ContractInstance active = ContractCore.Accept(doc, home[0], "Home", DateTime.UtcNow);
        True(active != null, "accepted local reward identity fixture");
        string occurrence = active.OccurrenceId;
        // This regression isolates occurrence/claim identity from the separate reward-ledger tests.
        active.RewardXpBasisPoints = 0;
        active.RewardGoldAmount = 0;
        active.RewardItemQuantity = 0;
        active.Progress = active.Target;

        // Building another zone's available board cannot mutate the accepted occurrence/claim key.
        ContractCore.BuildOffers(ContractCategory.Local, 0, "Forest", "p1", Builtins(), doc, 3);
        Equal(occurrence, active.OccurrenceId, "zoning available board cannot change accepted claim identity");
        ContractInstance claimed = ContractCore.ClaimRecordOnly(doc, occurrence);
        True(claimed != null, "accepted contract claims under original occurrence");
        True(ContractCore.ClaimRecordOnly(doc, occurrence) == null, "zone change cannot create duplicate claim opportunity");
    }

    private static void TestAppliedRewardSummary()
    {
        ContractInstance value = new ContractInstance();
        value.XpRewardStatus = RewardComponentStatus.Applied; value.AppliedXpAmount = 420;
        value.GoldRewardStatus = RewardComponentStatus.Applied; value.AppliedGoldAmount = 38;
        value.ItemRewardStatus = RewardComponentStatus.Applied; value.AppliedItemCount = 2; value.AppliedItemSummary = "Ore";
        Equal("+420 XP, +38 gold, 2x Ore", ContractCore.AppliedRewardSummary(value), "journal reward summary reflects only actual applied values");
    }

    private static void TestRecordOnlyProviderClaim()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractTemplateRegistration reg = ProviderRegistration();
        ContractInstance active = AcceptOnly(doc, ContractCore.FromRegistration(reg), ContractCategory.Local, "Home");
        active.Progress = active.Target;
        Equal(0, active.RewardXpBasisPoints, "provider v1 record-only");
        True(ContractCore.ClaimRecordOnly(doc, active.OccurrenceId) != null, "record-only claim works");
    }

    private static void TestLocalCompletionProgressesGlobal()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance global = AcceptOnly(doc, ContractCore.BuildGlobalLocalCompletionsTemplate(), ContractCategory.Global, "Home");
        Equal(1, ContractCore.RecordSuccessfulLocalCompletion(doc), "successful Local claim advances Contract Regular");
        Equal(1, global.Progress, "Regular increments exactly once per call");
    }

    private static void TestPlayerFacingProgressFormatting()
    {
        Equal("Target: 15 min", ContractCore.TargetText(ContractCore.BuildPatrolTemplate(15)), "patrol target is minutes");
        Equal("Target: 8 min away + return", ContractCore.TargetText(ContractCore.BuildRoadCheckTemplate()), "road target clear");
        Equal("Target: 10 min local + depart", ContractCore.TargetText(ContractCore.BuildPerimeterSweepTemplate()), "perimeter target clear");
        Equal("Target: 2 away zones + return", ContractCore.TargetText(ContractCore.BuildLocalCircuitTemplate()), "circuit target clear");
        Equal("Target: 45 min + 5 zones", ContractCore.TargetText(ContractCore.BuildGlobalExpeditionTemplate()), "expedition target clear");

        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance road = AcceptOnly(doc, ContractCore.BuildRoadCheckTemplate(), ContractCategory.Local, "Home");
        ContractCore.HandleZoneTransition(doc, "Home", "Road");
        ContractCore.AddActiveSeconds(doc, "Road", ContractCore.RoadCheckAwaySeconds / 2);
        True(ContractCore.ProgressText(road).IndexOf("4:00 / 8:00", StringComparison.Ordinal) >= 0, "road time readable");
        True(ContractCore.ProgressFraction(road) > 0.44f && ContractCore.ProgressFraction(road) < 0.46f, "road bar reserves final return step");
    }

    private static void TestCompletionSummaries()
    {
        ContractInstance tour = new ContractInstance(); tour.ProgressKey = "global_visit_unique_zone"; tour.Target = 8;
        Equal("Entered 8 different playable zones.", ContractCore.CompletionSummary(tour), "grand tour journal summary");
        ContractInstance regular = new ContractInstance(); regular.ProgressKey = "local_completion_claimed"; regular.Target = 4;
        Equal("Successfully claimed 4 Local contracts.", ContractCore.CompletionSummary(regular), "regular journal summary");
    }

    private static void TestJournalEntryAndClaimDeduplication()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractInstance active = AcceptOnly(doc, ContractCore.BuildGlobalWayfarerTemplate(), ContractCategory.Global, "Home");
        active.Progress = active.Target;
        CompleteAndApplyXp(doc, active, 420);
        ContractInstance completed = ContractCore.CommitClaim(doc, active.OccurrenceId);
        True(completed != null, "first claim succeeds for journal fixture");
        Equal("Completed global Contract: Grand Tour. Entered 8 different playable zones. Reward: +420 XP, +145 gold.",
            ContractCore.BuildJournalEntry(completed), "journal entry reports actual completion and applied reward");
        True(ContractCore.CommitClaim(doc, active.OccurrenceId) == null, "same occurrence cannot claim a second time");
        Equal(0, doc.Active.Count, "claimed occurrence removed from active set");
        True(doc.Claimed.Contains(active.OccurrenceId), "claimed occurrence remains as dedupe authority");

        ContractInstance recordOnly = new ContractInstance();
        recordOnly.Category = ContractCategory.Local;
        recordOnly.Title = "Provider Job";
        recordOnly.TemplateId = "provider";
        recordOnly.ProgressKey = "provider";
        recordOnly.Target = 3;
        recordOnly.Progress = 3;
        Equal("Completed local Contract: Provider Job. Completed objective (3/3).",
            ContractCore.BuildJournalEntry(recordOnly), "record-only journal does not invent a reward");
    }

    private static void TestContextFilter()
    {
        ContractDocument doc = NewDocumentWithSchedule();
        ContractTemplateRegistration reg = ProviderRegistration();
        reg.ContextFilter = "Silverleaf";
        ContractInstance active = AcceptOnly(doc, ContractCore.FromRegistration(reg), ContractCategory.Local, "Home");
        ContractProgressReport report = new ContractProgressReport();
        report.Channel = "gathering"; report.Key = "forage"; report.Amount = 1; report.Context = "Mushroom";
        Equal(0, ContractCore.ApplyExternalProgress(doc, report), "wrong provider context ignored");
        report.Context = "Fresh Silverleaf";
        Equal(1, ContractCore.ApplyExternalProgress(doc, report), "matching provider context applies");
        Equal(1, active.Progress, "provider progress changed");
    }

    private static void CompleteAndApplyXp(ContractDocument doc, ContractInstance active, int amount)
    {
        active.Progress = active.Target;
        ApplyComponent(doc, active, RewardComponentKind.Xp, amount, "+" + amount.ToString() + " XP");
        if (active.RewardGoldAmount > 0)
            ApplyComponent(doc, active, RewardComponentKind.Gold, active.RewardGoldAmount, "+" + active.RewardGoldAmount.ToString() + " Gold");
    }

    private static void ApplyComponent(ContractDocument doc, ContractInstance active, RewardComponentKind kind, int amount, string summary)
    {
        True(ContractCore.PrepareRewardComponent(doc, active.OccurrenceId, kind), "component prepared");
        True(ContractCore.MarkRewardComponentApplying(doc, active.OccurrenceId, kind), "component applying");
        True(ContractCore.MarkRewardComponentApplied(doc, active.OccurrenceId, kind, amount, summary), "component applied");
    }

    private static ContractInstance AcceptOnly(ContractDocument doc, ContractTemplate template, string category, string zone)
    {
        List<ContractTemplate> templates = new List<ContractTemplate>();
        templates.Add(template);
        ContractOffer offer = ContractCore.BuildOffers(category, 0, zone, "p1", templates, doc, 1)[0];
        ContractInstance active = ContractCore.Accept(doc, offer, zone, DateTime.UtcNow);
        if (active == null) throw new Exception("fixture accept failed for " + template.TemplateId);
        return active;
    }

    private static ContractTemplateRegistration ProviderRegistration()
    {
        ContractTemplateRegistration reg = new ContractTemplateRegistration();
        reg.ProviderId = "forage";
        reg.TemplateId = "silverleaf";
        reg.ZoneScope = "*";
        reg.Title = "Herbalist";
        reg.Description = "Gather Silverleaf.";
        reg.ProgressChannel = "gathering";
        reg.ProgressKey = "forage";
        reg.Target = 2;
        reg.Priority = 100;
        reg.RewardText = "Provider completion record.";
        return reg;
    }

    private static ContractDocument NewDocumentWithSchedule()
    {
        ContractDocument doc = new ContractDocument();
        ContractCore.AdvanceActivePlay(doc, 0, 45, 120);
        return doc;
    }

    private static List<ContractTemplate> Builtins()
    {
        List<ContractTemplate> result = new List<ContractTemplate>();
        result.Add(ContractCore.BuildPatrolTemplate(15));
        result.Add(ContractCore.BuildRoadCheckTemplate());
        result.Add(ContractCore.BuildPerimeterSweepTemplate());
        result.Add(ContractCore.BuildWayfarerTemplate());
        result.Add(ContractCore.BuildLocalCircuitTemplate());
        result.Add(ContractCore.BuildGlobalPatrolTemplate(60));
        result.Add(ContractCore.BuildGlobalWayfarerTemplate());
        result.Add(ContractCore.BuildGlobalLocalCompletionsTemplate());
        result.Add(ContractCore.BuildGlobalExpeditionTemplate());
        return result;
    }

    private static ContractOffer FindOffer(List<ContractOffer> offers, string id)
    {
        for (int i = 0; i < offers.Count; i++)
            if (offers[i] != null && string.Equals(offers[i].OccurrenceId, id, StringComparison.OrdinalIgnoreCase)) return offers[i];
        return null;
    }

    private static bool ContainsOffer(List<ContractOffer> offers, string id)
    {
        return FindOffer(offers, id) != null;
    }

    private static void True(bool value, string label)
    {
        _assertions++;
        if (!value) throw new Exception(label);
    }

    private static void False(bool value, string label) { True(!value, label); }

    private static void Equal<T>(T expected, T actual, string label)
    {
        _assertions++;
        if (!object.Equals(expected, actual)) throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }

    private static void NotEqual<T>(T left, T right, string label)
    {
        _assertions++;
        if (object.Equals(left, right)) throw new Exception(label + " values were equal");
    }
}
