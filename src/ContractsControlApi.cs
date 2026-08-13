using System;
using System.Collections.Generic;

namespace ErenshorContracts
{
    public sealed class ContractControlRow
    {
        public string Id;
        public string Title;
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
            return s.GameplayReady ? s.Contracts.Count + " contract row(s) in " + (string.IsNullOrEmpty(s.Zone) ? "current zone" : s.Zone) + "." : "Not fully in world.";
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
                row.Id = offer.OccurrenceId; row.Title = offer.Template.Title;
                if (offer.Claimed) row.State = "completed";
                else if (offer.Active != null) { row.State = offer.Active.IsComplete ? "ready_to_claim" : "active"; row.Progress = offer.Active.Progress; row.Target = offer.Active.Target; }
                else row.State = "available";
                state.Contracts.Add(row);
            }
            return state;
        }
        public static bool OpenPanel() { var p = ErenshorContractsPlugin.Instance; if (p == null || !SuiteUiPolicy.IsGameplayReady()) return false; p.RequestOpenBoard(); return true; }
        public static bool ClosePanel() { var p = ErenshorContractsPlugin.Instance; if (p == null) return false; p.RequestCloseBoard(); return true; }
        public static bool GetShowLauncher() { var p = ErenshorContractsPlugin.Instance; return p != null && p.ControlShowStandaloneLauncher; }
        public static bool SetShowLauncher(bool visible) { var p = ErenshorContractsPlugin.Instance; if (p == null) return false; p.SetShowStandaloneLauncher(visible); return true; }
        public static bool ResetPanelPosition() { var p = ErenshorContractsPlugin.Instance; if (p == null) return false; p.ResetWindowPosition(); return true; }
        public static bool ResetLauncherPosition() { var p = ErenshorContractsPlugin.Instance; if (p == null) return false; p.ResetLauncherPosition(); return true; }
    }
}
