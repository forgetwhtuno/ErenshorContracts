using System;
using System.Collections.Generic;

namespace ErenshorContracts
{
    /// <summary>
    /// Reflection-friendly optional integration surface.
    ///
    /// Companion mods can register local contract templates and report verified progress
    /// without taking a hard compile-time dependency on Erenshor Contracts.
    ///
    /// This provider API never grants rewards and never infers game state. Registered provider templates remain record-only in v1 because Contracts cannot infer their effort/balance. The provider is responsible
    /// for only reporting events it actually verified.
    /// </summary>
    public static class ContractBoardApi
    {
        public const int ContractVersion = 1;
        private static bool RuntimeAvailable;
        public static bool IsAvailable { get { lock (Sync) { return RuntimeAvailable; } } }

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
            if (!IsAvailable) return false;
            if (string.IsNullOrWhiteSpace(providerId) ||
                string.IsNullOrWhiteSpace(templateId) ||
                string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(progressChannel) ||
                string.IsNullOrWhiteSpace(progressKey) ||
                target <= 0)
                return false;

            ContractTemplateRegistration value = new ContractTemplateRegistration();
            value.ProviderId = Bound(providerId, 64);
            value.TemplateId = Bound(templateId, 64);
            value.ZoneScope = string.IsNullOrWhiteSpace(zoneScope) ? "*" : Bound(zoneScope, 96);
            value.Title = Bound(title, 120);
            value.Description = Bound(description, 320);
            value.ProgressChannel = Bound(progressChannel, 64);
            value.ProgressKey = Bound(progressKey, 64);
            value.ContextFilter = Bound(contextFilter, 160);
            value.Target = Math.Max(1, Math.Min(1000000, target));
            value.Priority = Math.Max(-1000, Math.Min(1000, priority));
            value.RewardText = Bound(rewardText, 200);

            lock (Sync)
            {
                if (Templates.Count >= MaximumQueuedTemplates) return false;
                Templates.Enqueue(value);
            }
            return true;
        }

        public static bool ReportProgress(string channel, string key, int amount, string context)
        {
            if (!IsAvailable) return false;
            if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(key) || amount <= 0)
                return false;

            ContractProgressReport value = new ContractProgressReport();
            value.Channel = Bound(channel, 64);
            value.Key = Bound(key, 64);
            value.Amount = Math.Max(1, Math.Min(1000000, amount));
            value.Context = Bound(context, 512);

            lock (Sync)
            {
                if (Progress.Count >= MaximumQueuedProgress) return false;
                Progress.Enqueue(value);
            }
            return true;
        }

        private static string Bound(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength);
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

        internal static void SetRuntimeAvailable(bool available)
        {
            lock (Sync)
            {
                RuntimeAvailable = available;
                if (!available)
                {
                    Templates.Clear();
                    Progress.Clear();
                }
            }
        }

        internal static void ResetRuntimeState()
        {
            SetRuntimeAvailable(false);
        }
    }
}
