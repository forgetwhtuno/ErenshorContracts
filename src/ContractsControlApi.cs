using System;
using System.Collections.Generic;

namespace ErenshorContracts
{
    public sealed class ContractControlRow
    {
        public string Id;
        public string Title;
        public string Category;
        public string State;
        public int Progress;
        public int Target;
    }

    public sealed class ContractsControlState
    {
        public bool GameplayReady;
        public string CharacterKey;
        public string Zone;
        public bool PanelOpen;
        public int CompletedCount;
        public int LocalRows;
        public int GlobalRows;
        public List<ContractControlRow> Contracts = new List<ContractControlRow>();
    }

    public static class ContractsControlApi
    {
        public const int ApiVersion = 1;
        public const string ModuleId = "contracts";
        public static bool HasDedicatedPanel { get { return true; } }
        public static bool IsPanelOpen { get { return ErenshorContractsPlugin.Instance != null && ErenshorContractsPlugin.Instance.ControlPanelOpen; } }

        public static string GetStatus()
        {
            ContractsControlState s = GetBasicState();
            if (!s.GameplayReady) return "Not fully in world.";
            return s.LocalRows.ToString() + " local, " + s.GlobalRows.ToString() + " global contract row(s).";
        }

        public static ContractsControlState GetBasicState()
        {
            ContractsControlState state = new ContractsControlState();
            state.GameplayReady = SuiteUiPolicy.IsGameplayReady();
            ErenshorContractsPlugin plugin = ErenshorContractsPlugin.Instance;
            if (plugin == null) return state;
            state.CharacterKey = plugin.ControlCharacterKey;
            state.Zone = plugin.ControlZone;
            state.PanelOpen = plugin.ControlPanelOpen;
            ContractDocument doc = plugin.ControlDocument;
            if (doc != null) state.CompletedCount = doc.TotalCompleted;
            List<ContractOffer> offers = plugin.ControlOffers;
            for (int i = 0; i < offers.Count; i++)
            {
                ContractOffer offer = offers[i]; if (offer == null || offer.Template == null) continue;
                ContractControlRow row = new ContractControlRow();
                row.Id = offer.OccurrenceId;
                row.Title = offer.Template.Title;
                row.Category = ContractCategory.Normalize(offer.Template.Category);
                if (string.Equals(row.Category, ContractCategory.Global, StringComparison.Ordinal)) state.GlobalRows++;
                else state.LocalRows++;

                if (offer.RewardLocked) row.State = "reward_locked";
                else if (offer.RewardRetryable) row.State = "reward_retryable";
                else if (offer.Claimed) row.State = "completed";
                else if (offer.Active != null)
                {
                    row.State = offer.Active.IsComplete ? "ready_to_claim" : "active";
                    row.Progress = offer.Active.Progress;
                    row.Target = offer.Active.Target;
                }
                else row.State = "available";
                state.Contracts.Add(row);
            }
            return state;
        }

        public static bool OpenPanel() { ErenshorContractsPlugin p = ErenshorContractsPlugin.Instance; if (p == null || !SuiteUiPolicy.IsGameplayReady()) return false; p.RequestOpenBoard(); return true; }
        public static bool ClosePanel() { ErenshorContractsPlugin p = ErenshorContractsPlugin.Instance; if (p == null) return false; p.RequestCloseBoard(); return true; }
        public static bool GetShowLauncher() { ErenshorContractsPlugin p = ErenshorContractsPlugin.Instance; return p != null && p.ControlShowStandaloneLauncher; }
        public static bool SetShowLauncher(bool visible) { ErenshorContractsPlugin p = ErenshorContractsPlugin.Instance; if (p == null) return false; p.SetShowStandaloneLauncher(visible); return true; }
        public static bool ResetPanelPosition() { ErenshorContractsPlugin p = ErenshorContractsPlugin.Instance; if (p == null) return false; p.ResetWindowPosition(); return true; }
        public static bool ResetLauncherPosition() { ErenshorContractsPlugin p = ErenshorContractsPlugin.Instance; if (p == null) return false; p.ResetLauncherPosition(); return true; }
        // Equivalent to `/contracts diag rewards` for Suite consumers; it deliberately exposes no local paths.
        public static string GetRewardDiagnostics() { ErenshorContractsPlugin p = ErenshorContractsPlugin.Instance; return p == null ? "contracts_unavailable" : p.ControlRewardDiagnostics(); }
    }
}
