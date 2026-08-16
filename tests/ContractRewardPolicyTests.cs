using System;
using ErenshorContracts;

internal static class ContractRewardPolicyTests
{
    internal static int RunAll()
    {
        Equal(22, ContractRewardPolicy.ResolveGoldAmount("local", "road_check", 1), "road gold");
        Equal(165, ContractRewardPolicy.ResolveGoldAmount("global", "global_expedition", 1), "expedition gold");
        Equal(0, ContractRewardPolicy.ResolveGoldAmount("local", "unknown", 1), "unknown has no policy");
        Equal(19, ContractRewardPolicy.ResolveCombatGoldAmount("local", 1, 1), "combat floor");
        Equal(300, ContractRewardPolicy.ResolveCombatGoldAmount("global", 1000, 1000), "combat bounded");
        Equal(300, ContractRewardPolicy.CalculateXpAmount(10000, 300), "xp percentage");
        Equal(1, ContractRewardPolicy.CalculateXpAmount(1, 1), "xp lower bound");
        string text = ContractRewardPolicy.DescribeReward(42, 500);
        True(text.IndexOf("42 Gold", StringComparison.Ordinal) >= 0 && text.IndexOf("5", StringComparison.Ordinal) >= 0, "reward presentation");
        bool xp = true;
        int schema = 0;
        True(ContractRewardConfigMigrationPolicy.Apply(ref xp, ref schema), "fresh config receives reward schema");
        True(xp && schema == ContractRewardConfigMigrationPolicy.CurrentSchema, "fresh config defaults XP on");
        xp = false;
        schema = 0;
        True(ContractRewardConfigMigrationPolicy.Apply(ref xp, ref schema), "legacy false migrates once");
        True(xp && schema == ContractRewardConfigMigrationPolicy.CurrentSchema, "legacy false becomes production XP on");
        xp = false;
        True(!ContractRewardConfigMigrationPolicy.Apply(ref xp, ref schema), "migration marker prevents a second forced change");
        True(!xp, "explicit post-migration XP opt-out persists");
        True(ContractRewardConfigMigrationPolicy.SourceLabel(false, 0) == "legacy_0_4_0_false_migrated", "legacy source diagnostic");
        True(ContractRewardConfigMigrationPolicy.SourceLabel(true, 1) == "schema_persisted", "persisted schema diagnostic");
        return 17;
    }
    private static void Equal(int expected, int actual, string label) { if (expected != actual) throw new Exception(label + ": expected " + expected + ", got " + actual); }
    private static void True(bool value, string label) { if (!value) throw new Exception(label); }
}
