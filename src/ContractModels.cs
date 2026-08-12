using System;
using System.Collections.Generic;

namespace ErenshorContracts
{
    internal sealed class ContractTemplate
    {
        internal string ProviderId;
        internal string TemplateId;
        internal string ZoneScope;
        internal string Title;
        internal string Description;
        internal string ProgressChannel;
        internal string ProgressKey;
        internal string ContextFilter;
        internal string RewardText;
        internal int Target;
        internal int Priority;
    }

    internal sealed class ContractInstance
    {
        internal string OccurrenceId;
        internal string ProviderId;
        internal string TemplateId;
        internal string OriginZone;
        internal string Title;
        internal string Description;
        internal string ProgressChannel;
        internal string ProgressKey;
        internal string ContextFilter;
        internal string RewardText;
        internal int Target;
        internal int Progress;
        internal string StateToken;
        internal DateTime AcceptedUtc;

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
    }

    internal sealed class ContractOffer
    {
        internal string OccurrenceId;
        internal ContractTemplate Template;
        internal ContractInstance Active;
        internal bool Claimed;

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
}
