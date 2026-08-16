using System;
using ErenshorContracts;

internal static class SuiteUiPolicyTests
{
    private static int Main()
    {
        string result = SuiteUiPositionPolicy.RunSelfTests();
        if (SuiteCameraOwnershipPolicy.PromoteUsingUi(false, false) ||
            !SuiteCameraOwnershipPolicy.PromoteUsingUi(true, false) ||
            !SuiteCameraOwnershipPolicy.PromoteUsingUi(false, true) ||
            SuiteCameraOwnershipPolicy.CollapseGlyph(false) != "▲" ||
            SuiteCameraOwnershipPolicy.CollapseGlyph(true) != "▼")
            result = "FAIL camera containment/collapse policy";
        Console.WriteLine(result);
        return result != null && result.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1;
    }
}
