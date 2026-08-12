using System;
using System.Collections.Generic;
using ErenshorContracts;

internal static class ContractCoreTests
{
    private static int _assertions;

    public static int Main()
    {
        try
        {
            TestStableDailyOffers();
            TestProviderPriority();
            TestAcceptProgressClaim();
            TestLeaveReturn();
            TestUniqueZoneVisit();
            TestContextFilter();
            Console.WriteLine("PASS Erenshor Contracts core - " + _assertions.ToString() + " assertions");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL Erenshor Contracts core: " + ex.Message);
            return 1;
        }
    }

    private static void TestStableDailyOffers()
    {
        ContractDocument doc = new ContractDocument();
        List<ContractTemplate> templates = Builtins();
        DateTime day = new DateTime(2026, 8, 12);
        List<ContractOffer> a = ContractCore.BuildDailyOffers(day, "TestZone", "p1", templates, doc, 3);
        List<ContractOffer> b = ContractCore.BuildDailyOffers(day, "TestZone", "p1", templates, doc, 3);
        Equal(3, a.Count, "three built-ins offered");
        Equal(a[0].OccurrenceId, b[0].OccurrenceId, "same day/zone/profile stable");
        NotEqual(a[0].OccurrenceId,
            ContractCore.BuildDailyOffers(day.AddDays(1), "TestZone", "p1", templates, doc, 3)[0].OccurrenceId,
            "next day occurrence changes");
    }

    private static void TestProviderPriority()
    {
        ContractDocument doc = new ContractDocument();
        List<ContractTemplate> templates = Builtins();
        ContractTemplateRegistration reg = new ContractTemplateRegistration();
        reg.ProviderId = "crafting";
        reg.TemplateId = "food";
        reg.ZoneScope = "*";
        reg.Title = "Provisioning";
        reg.Description = "Craft food.";
        reg.ProgressChannel = "crafting";
        reg.ProgressKey = "food";
        reg.Target = 3;
        reg.Priority = 100;
        templates.Add(ContractCore.FromRegistration(reg));

        List<ContractOffer> offers = ContractCore.BuildDailyOffers(
            new DateTime(2026, 8, 12), "TestZone", "p1", templates, doc, 1);
        Equal("crafting", offers[0].Template.ProviderId, "provider priority beats fallback");
    }

    private static void TestAcceptProgressClaim()
    {
        ContractDocument doc = new ContractDocument();
        ContractTemplateRegistration reg = new ContractTemplateRegistration();
        reg.ProviderId = "crafting";
        reg.TemplateId = "food";
        reg.ZoneScope = "*";
        reg.Title = "Provisioning";
        reg.Description = "Craft food.";
        reg.ProgressChannel = "crafting";
        reg.ProgressKey = "food";
        reg.Target = 3;
        reg.Priority = 100;

        List<ContractTemplate> templates = new List<ContractTemplate>();
        templates.Add(ContractCore.FromRegistration(reg));
        ContractOffer offer = ContractCore.BuildDailyOffers(
            new DateTime(2026, 8, 12), "TestZone", "p1", templates, doc, 1)[0];

        ContractInstance active = ContractCore.Accept(doc, offer, "TestZone", DateTime.UtcNow);
        True(active != null, "accept returns instance");
        Equal(1, doc.Active.Count, "active count");

        ContractProgressReport progress = new ContractProgressReport();
        progress.Channel = "crafting";
        progress.Key = "food";
        progress.Amount = 2;
        progress.Context = "stew";
        Equal(1, ContractCore.ApplyExternalProgress(doc, progress), "progress applies");
        Equal(2, active.Progress, "progress amount");

        progress.Amount = 2;
        ContractCore.ApplyExternalProgress(doc, progress);
        Equal(3, active.Progress, "progress clamps");
        True(active.IsComplete, "complete");

        ContractInstance claimed = ContractCore.Claim(doc, active.OccurrenceId);
        True(claimed != null, "claim succeeds");
        Equal(0, doc.Active.Count, "claim removes active");
        Equal(1, doc.TotalCompleted, "completion counter");
        True(doc.Claimed.Contains(active.OccurrenceId), "occurrence claimed");
    }

    private static void TestLeaveReturn()
    {
        ContractDocument doc = new ContractDocument();
        ContractTemplate template = ContractCore.BuildRoadCheckTemplate();
        List<ContractTemplate> templates = new List<ContractTemplate>();
        templates.Add(template);
        ContractOffer offer = ContractCore.BuildDailyOffers(
            new DateTime(2026, 8, 12), "Home", "p1", templates, doc, 1)[0];
        ContractInstance active = ContractCore.Accept(doc, offer, "Home", DateTime.UtcNow);

        Equal(1, ContractCore.HandleZoneTransition(doc, "Home", "Road"), "leaving marks away");
        Equal("away", active.StateToken, "away state");
        Equal(0, active.Progress, "not complete until return");

        Equal(1, ContractCore.HandleZoneTransition(doc, "Road", "Home"), "return progresses");
        Equal(1, active.Progress, "return complete");
    }

    private static void TestUniqueZoneVisit()
    {
        ContractDocument doc = new ContractDocument();
        ContractTemplate template = ContractCore.BuildWayfarerTemplate();
        List<ContractTemplate> templates = new List<ContractTemplate>();
        templates.Add(template);
        ContractOffer offer = ContractCore.BuildDailyOffers(
            new DateTime(2026, 8, 12), "Home", "p1", templates, doc, 1)[0];
        ContractInstance active = ContractCore.Accept(doc, offer, "Home", DateTime.UtcNow);

        ContractCore.HandleZoneTransition(doc, "Home", "A");
        Equal(1, active.Progress, "first unique zone");
        ContractCore.HandleZoneTransition(doc, "A", "A");
        Equal(1, active.Progress, "duplicate not counted");
        ContractCore.HandleZoneTransition(doc, "A", "B");
        Equal(2, active.Progress, "second unique zone");
        True(active.IsComplete, "wayfarer complete");
    }

    private static void TestContextFilter()
    {
        ContractDocument doc = new ContractDocument();
        ContractTemplateRegistration reg = new ContractTemplateRegistration();
        reg.ProviderId = "forage";
        reg.TemplateId = "silverleaf";
        reg.ZoneScope = "*";
        reg.Title = "Herbalist";
        reg.Description = "Gather Silverleaf.";
        reg.ProgressChannel = "gathering";
        reg.ProgressKey = "forage";
        reg.ContextFilter = "Silverleaf";
        reg.Target = 2;
        reg.Priority = 100;

        List<ContractTemplate> templates = new List<ContractTemplate>();
        templates.Add(ContractCore.FromRegistration(reg));
        ContractOffer offer = ContractCore.BuildDailyOffers(
            new DateTime(2026, 8, 12), "Home", "p1", templates, doc, 1)[0];
        ContractInstance active = ContractCore.Accept(doc, offer, "Home", DateTime.UtcNow);

        ContractProgressReport wrong = new ContractProgressReport();
        wrong.Channel = "gathering";
        wrong.Key = "forage";
        wrong.Amount = 1;
        wrong.Context = "Mushroom";
        Equal(0, ContractCore.ApplyExternalProgress(doc, wrong), "wrong context ignored");
        Equal(0, active.Progress, "wrong context no progress");

        wrong.Context = "Fresh Silverleaf";
        Equal(1, ContractCore.ApplyExternalProgress(doc, wrong), "matching context applies");
        Equal(1, active.Progress, "matching progress");
    }

    private static List<ContractTemplate> Builtins()
    {
        List<ContractTemplate> result = new List<ContractTemplate>();
        result.Add(ContractCore.BuildPatrolTemplate(3));
        result.Add(ContractCore.BuildRoadCheckTemplate());
        result.Add(ContractCore.BuildWayfarerTemplate());
        return result;
    }

    private static void True(bool value, string label)
    {
        _assertions++;
        if (!value) throw new Exception(label);
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        _assertions++;
        if (!object.Equals(expected, actual))
            throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }

    private static void NotEqual<T>(T left, T right, string label)
    {
        _assertions++;
        if (object.Equals(left, right))
            throw new Exception(label + " values were equal");
    }
}
