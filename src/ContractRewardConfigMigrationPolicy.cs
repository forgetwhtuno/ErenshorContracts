namespace ErenshorContracts
{
    // Lunaris applies an existing serialized value after constructing ContractsSettings. The
    // schema marker distinguishes the former safety default (XP=false) from a later deliberate
    // player opt-out, which must never be overwritten again.
    internal static class ContractRewardConfigMigrationPolicy
    {
        internal const int CurrentSchema = 1;

        internal static bool ShouldMigrate(int schema) { return schema < CurrentSchema; }

        internal static bool Apply(ref bool xpEnabled, ref int schema)
        {
            if (!ShouldMigrate(schema)) return false;
            xpEnabled = true;
            schema = CurrentSchema;
            return true;
        }

        internal static string SourceLabel(bool storedValueBeforeMigration, int schemaBeforeMigration)
        {
            if (schemaBeforeMigration >= CurrentSchema) return "schema_persisted";
            return storedValueBeforeMigration ? "unversioned_default_true" : "legacy_0_4_0_false_migrated";
        }
    }
}
