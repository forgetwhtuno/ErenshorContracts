using System;
using System.Collections.Generic;

namespace ErenshorContracts
{
    internal static class ContractCategory
    {
        internal const string Local = "local";
        internal const string Global = "global";

        internal static string Normalize(string value)
        {
            return string.Equals(value, Global, StringComparison.OrdinalIgnoreCase) ? Global : Local;
        }
    }

    internal enum RewardComponentKind
    {
        Xp = 0,
        Gold = 1,
        Item = 2
    }

    // Persisted per irreversible reward component. Applying is intentionally treated as
    // OutcomeUnknown after a process restart: the sidecar cannot prove whether the native call
    // returned before the process stopped, so retrying would risk duplication.
    internal enum RewardComponentStatus
    {
        NotStarted = 0,
        Prepared = 1,
        Applying = 2,
        Applied = 3,
        FailedRetryable = 4,
        OutcomeUnknown = 5
    }

    internal sealed class ContractTemplate
    {
        internal string ProviderId;
        internal string TemplateId;
        internal string Category;
        internal string ZoneScope;
        internal string TargetZone;
        internal string Title;
        internal string Description;
        internal string ProgressChannel;
        internal string ProgressKey;
        internal string ContextFilter;
        internal string RewardText;
        internal int RewardXpBasisPoints;
        internal int RewardGoldAmount;
        internal string RewardItemId;
        internal string RewardItemName;
        internal int RewardItemQuantity;
        internal int Target;
        internal int Priority;
    }

    internal sealed class ContractInstance
    {
        internal string OccurrenceId;
        internal string ProviderId;
        internal string TemplateId;
        internal string Category;
        internal string OriginZone;
        internal string TargetZone;
        internal string Title;
        internal string Description;
        internal string ProgressChannel;
        internal string ProgressKey;
        internal string ContextFilter;
        internal string RewardText;
        internal int RewardXpBasisPoints;
        internal int RewardGoldAmount;
        internal string RewardItemId;
        internal string RewardItemName;
        internal int RewardItemQuantity;
        internal int Target;
        internal int Progress;
        internal string StateToken;
        internal DateTime AcceptedUtc;

        internal RewardComponentStatus XpRewardStatus;
        internal int PlannedXpAmount;
        internal int AppliedXpAmount;
        internal RewardComponentStatus GoldRewardStatus;
        internal int AppliedGoldAmount;
        internal RewardComponentStatus ItemRewardStatus;
        internal int AppliedItemCount;
        internal string AppliedItemSummary;

        internal bool IsComplete
        {
            get { return Target > 0 && Progress >= Target; }
        }
    }

    internal sealed class ContractDocument
    {
        internal readonly List<ContractInstance> Active = new List<ContractInstance>();
        internal readonly HashSet<string> Claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal int TotalCompleted;
        internal int TotalLocalCompleted;
        internal int TotalGlobalCompleted;
        internal long ActivePlaySeconds;
        internal int LocalBoardRevision;
        internal int GlobalBoardRevision;
        internal long NextLocalRefreshAtSeconds;
        internal long NextGlobalRefreshAtSeconds;
        internal string LocalBoardZone;
        internal int LocalCombatGenerationRevision = -1;
        internal string LocalCombatGenerationZone;
        internal int GlobalCombatGenerationRevision = -1;
        internal readonly List<ContractEnemyRecord> EnemyCatalog = new List<ContractEnemyRecord>();
        internal readonly List<ContractGeneratedCombatOffer> GeneratedCombatOffers = new List<ContractGeneratedCombatOffer>();
    }

    internal sealed class ContractEnemyObservation
    {
        internal string Zone;
        internal string EnemyName;
        internal int MinLevel;
        internal int MaxLevel;
        internal int Count;
    }

    internal sealed class ContractEnemyRecord
    {
        internal string Zone;
        internal string EnemyName;
        internal int MinLevel;
        internal int MaxLevel;
        internal int ObservedCount;
        internal long LastSeenActiveSeconds;
    }

    internal sealed class ContractGeneratedCombatOffer
    {
        internal string Category;
        internal int BoardRevision;
        internal string BoardZone;
        internal string TargetZone;
        internal string EnemyName;
        internal int EnemyLevel;
        internal int TargetCount;
        internal int RewardXpBasisPoints;
    }

    internal sealed class ContractOffer
    {
        internal string OccurrenceId;
        internal ContractTemplate Template;
        internal ContractInstance Active;
        internal bool Claimed;
        internal bool RewardLocked;
        internal bool RewardRetryable;

        internal bool IsActive
        {
            get { return Active != null; }
        }
    }

    internal sealed class ContractTemplateRegistration
    {
        internal string ProviderId;
        internal string TemplateId;
        internal string ZoneScope;
        internal string TargetZone;
        internal string Title;
        internal string Description;
        internal string ProgressChannel;
        internal string ProgressKey;
        internal string ContextFilter;
        internal string RewardText;
        internal int Target;
        internal int Priority;
    }

    internal sealed class ContractProgressReport
    {
        internal string Channel;
        internal string Key;
        internal int Amount;
        internal string Context;
    }

    internal sealed class ContractRefreshResult
    {
        internal bool LocalRefreshed;
        internal bool GlobalRefreshed;

        internal bool AnyRefreshed
        {
            get { return LocalRefreshed || GlobalRefreshed; }
        }
    }
}
