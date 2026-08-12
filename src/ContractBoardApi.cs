using System;
using System.Collections.Generic;

namespace ErenshorContracts
{
    /// <summary>
    /// Reflection-friendly optional integration surface.
    ///
    /// Companion mods can register daily contract templates and report verified progress
    /// without taking a hard compile-time dependency on Erenshor Contracts.
    ///
    /// This API never grants rewards and never infers game state. The provider is responsible
    /// for only reporting events it actually verified.
    /// </summary>
    public static class ContractBoardApi
    {
        public const int ContractVersion = 1;
        private const int MaximumQueuedTemplates = 256;
        private const int MaximumQueuedProgress = 512;

        private static readonly object Sync = new object();
        private static readonly Queue<ContractTemplateRegistration> Templates = new Queue<ContractTemplateRegistration>();
        private static readonly Queue<ContractProgressReport> Progress = new Queue<ContractProgressReport>();

        public static bool RegisterTemplate(
            string providerId,
            string templateId,
            string zoneScope,
            string title,
            string description,
            string progressChannel,
            string progressKey,
            string contextFilter,
            int target,
            int priority,
            string rewardText)
        {
            if (string.IsNullOrWhiteSpace(providerId) ||
                string.IsNullOrWhiteSpace(templateId) ||
                string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(progressChannel) ||
                string.IsNullOrWhiteSpace(progressKey) ||
                target <= 0)
                return false;

            ContractTemplateRegistration value = new ContractTemplateRegistration();
            value.ProviderId = providerId.Trim();
            value.TemplateId = templateId.Trim();
            value.ZoneScope = string.IsNullOrWhiteSpace(zoneScope) ? "*" : zoneScope.Trim();
            value.Title = title.Trim();
            value.Description = description == null ? string.Empty : description.Trim();
            value.ProgressChannel = progressChannel.Trim();
            value.ProgressKey = progressKey.Trim();
            value.ContextFilter = contextFilter == null ? string.Empty : contextFilter.Trim();
            value.Target = Math.Max(1, Math.Min(1000000, target));
            value.Priority = Math.Max(-1000, Math.Min(1000, priority));
            value.RewardText = rewardText == null ? string.Empty : rewardText.Trim();

            lock (Sync)
            {
                if (Templates.Count >= MaximumQueuedTemplates) return false;
                Templates.Enqueue(value);
            }
            return true;
        }

        public static bool ReportProgress(string channel, string key, int amount, string context)
        {
            if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(key) || amount <= 0)
                return false;

            ContractProgressReport value = new ContractProgressReport();
            value.Channel = channel.Trim();
            value.Key = key.Trim();
            value.Amount = Math.Max(1, Math.Min(1000000, amount));
            value.Context = context == null ? string.Empty : context.Trim();

            lock (Sync)
            {
                if (Progress.Count >= MaximumQueuedProgress) return false;
                Progress.Enqueue(value);
            }
            return true;
        }

        internal static bool TryDequeueTemplate(out ContractTemplateRegistration value)
        {
            lock (Sync)
            {
                if (Templates.Count == 0)
                {
                    value = null;
                    return false;
                }
                value = Templates.Dequeue();
                return true;
            }
        }

        internal static bool TryDequeueProgress(out ContractProgressReport value)
        {
            lock (Sync)
            {
                if (Progress.Count == 0)
                {
                    value = null;
                    return false;
                }
                value = Progress.Dequeue();
                return true;
            }
        }
    }
}
