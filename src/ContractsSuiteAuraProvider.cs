using System;
using System.Text;
using Lunaris;
using Lunaris.IPC;

namespace ErenshorContracts
{
    internal sealed class ContractsSuiteAuraProvider
    {
        private const string Prefix = "forgetwhtuno.erenshor.suite.contracts.v1.";
        private IAuraProvider<string> _describe;
        private IAuraProvider<string> _basicSettings;
        private IAuraProvider<string, string, string> _settingSet;
        private IAuraProvider<string, string, string> _action;

        internal bool Registered { get; private set; }

        internal ContractsSuiteAuraProvider(LunarisPlugin owner)
        {
            if (owner == null) return;
            _describe = owner.IPCAuraProvider<string>(Prefix + "describe"); _describe.RegisterFunc(Describe);
            _basicSettings = owner.IPCAuraProvider<string>(Prefix + "settings.basic"); _basicSettings.RegisterFunc(BasicSettings);
            _settingSet = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set"); _settingSet.RegisterFunc(SetSetting);
            _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action"); _action.RegisterFunc(InvokeAction);
            Registered = true;
        }

        internal void Unregister()
        {
            Safe(_describe); _describe = null; Safe(_basicSettings); _basicSettings = null;
            Safe(_settingSet); _settingSet = null; Safe(_action); _action = null; Registered = false;
        }

        private static void Safe(IAuraProvider p) { if (p == null) return; try { p.UnregisterFunc(); } catch { } }

        private string Describe()
        {
            return "protocol=1&module=" + ContractsControlApi.ModuleId
                + "&display=" + Uri.EscapeDataString("Erenshor Contracts")
                + "&version=" + Uri.EscapeDataString(ErenshorContractsPlugin.PluginVersion)
                + "&summary=" + Uri.EscapeDataString("Local Preview contract board; no native XP/gold/item rewards.")
                + "&status=" + Uri.EscapeDataString(SuiteUiControlPolicy.BoundStatus(ContractsControlApi.GetStatus()))
                + "&actions=openPanel,closePanel,resetPanel,resetLauncher";
        }

        private string BasicSettings()
        {
            StringBuilder sb = new StringBuilder();
            AppendBool(sb, "showLauncher", "Show Contracts launcher", ContractsControlApi.GetShowLauncher(), true);
            ErenshorContractsPlugin p = ErenshorContractsPlugin.Instance;
            if (p != null)
            {
                AppendNumber(sb, "dailySlots", "Daily contract slots", p.ControlDailySlots);
                AppendNumber(sb, "patrolMinutes", "Local Patrol minutes", p.ControlPatrolMinutes);
            }
            return sb.ToString();
        }

        private string SetSetting(string id, string value)
        {
            if (!string.Equals(id, "showLauncher", StringComparison.Ordinal)) return "unknown setting";
            bool parsed;
            if (!SuiteUiControlPolicy.TryParseBool(value, out parsed)) return "rejected";
            return ContractsControlApi.SetShowLauncher(parsed) ? "ok" : "rejected";
        }

        private string InvokeAction(string actionId, string argument)
        {
            switch (SuiteUiControlPolicy.ParsePanelAction(actionId))
            {
                case SuitePanelAction.OpenPanel: return ContractsControlApi.OpenPanel() ? "ok" : "rejected";
                case SuitePanelAction.ClosePanel: return ContractsControlApi.ClosePanel() ? "ok" : "rejected";
                case SuitePanelAction.ResetPanel: return ContractsControlApi.ResetPanelPosition() ? "ok" : "rejected";
                case SuitePanelAction.ResetLauncher: return ContractsControlApi.ResetLauncherPosition() ? "ok" : "rejected";
                default: return "unknown action";
            }
        }

        private static void AppendBool(StringBuilder sb, string id, string label, bool value, bool mutable)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("id=").Append(Uri.EscapeDataString(id)).Append("&label=").Append(Uri.EscapeDataString(label));
            sb.Append("&tier=basic&type=bool&value=").Append(value ? "true" : "false");
            sb.Append("&mutable=").Append(mutable ? "true" : "false");
        }

        private static void AppendNumber(StringBuilder sb, string id, string label, int value)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("id=").Append(Uri.EscapeDataString(id)).Append("&label=").Append(Uri.EscapeDataString(label));
            sb.Append("&tier=basic&type=number&value=").Append(value.ToString()).Append("&mutable=false");
        }

    }
}
