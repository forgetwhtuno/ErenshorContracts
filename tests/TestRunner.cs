using System;

internal static class TestRunner
{
    public static int Main()
    {
        try
        {
            int assertions = 0;
            assertions += ContractCoreTests.RunAll();
            assertions += ContractCombatPolicyTests.RunAll();
            assertions += ContractCharacterKeyTests.RunAll();
            assertions += ContractStoreTests.RunAll();
            assertions += ContractJournalQueueTests.RunAll();
            assertions += ContractBoardApiTests.RunAll();
            assertions += ContractRewardPolicyTests.RunAll();
            Console.WriteLine("PASS Erenshor Contracts test suite - " + assertions.ToString() + " assertions");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL Erenshor Contracts test suite: " + ex.Message);
            return 1;
        }
    }
}
