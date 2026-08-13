using System;
using ErenshorContracts;

internal static class SuiteUiPolicyTests
{
    private static int Main()
    {
        string result = SuiteUiPositionPolicy.RunSelfTests();
        Console.WriteLine(result);
        return result != null && result.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1;
    }
}
