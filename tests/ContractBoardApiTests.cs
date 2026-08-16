using System;
using ErenshorContracts;

internal static class ContractBoardApiTests
{
    internal static int RunAll()
    {
        int assertions = 0;
        assertions += TestAvailabilityLifecycle();
        ContractBoardApi.SetRuntimeAvailable(true);
        assertions += TestTemplateRegistrationBounds();
        assertions += TestProgressReportBounds();
        assertions += TestInvalidInputRejected();
        ContractBoardApi.ResetRuntimeState();
        return assertions;
    }

    private static int TestAvailabilityLifecycle()
    {
        False(ContractBoardApi.IsAvailable, "API unavailable before runtime activation");
        ContractBoardApi.SetRuntimeAvailable(true);
        True(ContractBoardApi.IsAvailable, "API available after runtime activation");
        ContractBoardApi.ResetRuntimeState();
        False(ContractBoardApi.IsAvailable, "API unavailable after runtime reset");
        return 3;
    }

    private static int TestTemplateRegistrationBounds()
    {
        string longValue = new string('x', 800) + "\r\nTAIL";
        True(ContractBoardApi.RegisterTemplate(
            longValue, longValue, longValue, longValue, longValue,
            longValue, longValue, longValue, 2000000, 5000, longValue),
            "bounded template registration accepted");

        ContractTemplateRegistration value;
        True(ContractBoardApi.TryDequeueTemplate(out value), "template dequeued");
        Equal(64, value.ProviderId.Length, "provider id bounded");
        Equal(64, value.TemplateId.Length, "template id bounded");
        Equal(96, value.ZoneScope.Length, "zone scope bounded");
        Equal(120, value.Title.Length, "title bounded");
        Equal(320, value.Description.Length, "description bounded");
        Equal(64, value.ProgressChannel.Length, "channel bounded");
        Equal(64, value.ProgressKey.Length, "key bounded");
        Equal(160, value.ContextFilter.Length, "context filter bounded");
        Equal(1000000, value.Target, "target bounded");
        Equal(1000, value.Priority, "priority bounded");
        Equal(200, value.RewardText.Length, "reward text bounded");
        False(value.ProviderId.IndexOf('\r') >= 0 || value.ProviderId.IndexOf('\n') >= 0, "template line breaks removed");
        return 13;
    }

    private static int TestProgressReportBounds()
    {
        string longValue = new string('y', 900) + "\nTAIL";
        True(ContractBoardApi.ReportProgress(longValue, longValue, 2000000, longValue), "bounded progress accepted");
        ContractProgressReport value;
        True(ContractBoardApi.TryDequeueProgress(out value), "progress dequeued");
        Equal(64, value.Channel.Length, "progress channel bounded");
        Equal(64, value.Key.Length, "progress key bounded");
        Equal(1000000, value.Amount, "progress amount bounded");
        Equal(512, value.Context.Length, "progress context bounded");
        False(value.Context.IndexOf('\r') >= 0 || value.Context.IndexOf('\n') >= 0, "progress line breaks removed");
        return 7;
    }

    private static int TestInvalidInputRejected()
    {
        False(ContractBoardApi.RegisterTemplate("", "id", "*", "Title", "", "c", "k", "", 1, 0, ""), "blank provider rejected");
        False(ContractBoardApi.RegisterTemplate("p", "id", "*", "Title", "", "c", "k", "", 0, 0, ""), "nonpositive target rejected");
        False(ContractBoardApi.ReportProgress("", "k", 1, ""), "blank progress channel rejected");
        False(ContractBoardApi.ReportProgress("c", "k", 0, ""), "nonpositive progress rejected");
        return 4;
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
    }

    private static void False(bool condition, string label)
    {
        if (condition) throw new Exception(label);
    }

    private static void Equal(int expected, int actual, string label)
    {
        if (expected != actual) throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }
}
