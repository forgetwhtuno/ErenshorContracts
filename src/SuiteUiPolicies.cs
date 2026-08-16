using System;
using System.Globalization;

namespace ErenshorContracts
{
    // Unity-free suite UI rules so fallback/access and persistence recovery are deterministic.
    internal static class LauncherVisibilityPolicy
    {
        internal static bool ShouldShow(bool gameplayReady, bool hubUsable, bool bridgeRegistered, bool explicitlyVisibleWithHub)
        {
            return gameplayReady && (explicitlyVisibleWithHub || !hubUsable || !bridgeRegistered);
        }
    }


    // Exact module-owned wire record for the Hub Basic setting. Keeping this pure makes the
    // Contracts side of the Suite contract deterministic and testable without Lunaris.
    internal static class ContractsSuiteWirePolicy
    {
        internal static string BuildBasicSettings(bool showLauncher)
        {
            return "id=showLauncher&label=Show%20Contracts%20Launcher&tier=basic&type=bool&value="
                + (showLauncher ? "true" : "false") + "&mutable=true";
        }
    }

    // Launcher fallback follows actual retained-Hub availability, not a manual validation flag.
    // The Contracts launcher may honor its Show Launcher preference only while the Hub says it is
    // Ready and its retained UI is actually built. Missing/malformed/unavailable presence fails
    // safe to the standalone launcher. interactionValidated remains diagnostic metadata only.
    internal static class SuiteHubPresencePolicy
    {
        internal static bool IsUsable(string payload)
        {
            if (string.IsNullOrEmpty(payload) || payload.Length > 2048) return false;
            string protocol = null;
            string module = null;
            string status = null;
            string uiAvailable = null;
            string[] fields = payload.Split('&');
            for (int i = 0; i < fields.Length; i++)
            {
                int equals = fields[i].IndexOf('=');
                if (equals <= 0) return false;
                string key = fields[i].Substring(0, equals);
                string value = fields[i].Substring(equals + 1);
                if (key == "protocol") { if (protocol != null) return false; protocol = value; }
                else if (key == "module") { if (module != null) return false; module = value; }
                else if (key == "status") { if (status != null) return false; status = value; }
                else if (key == "uiAvailable") { if (uiAvailable != null) return false; uiAvailable = value; }
            }
            return string.Equals(protocol, "1", StringComparison.Ordinal)
                && string.Equals(module, "suitehub", StringComparison.Ordinal)
                && string.Equals(status, "Ready", StringComparison.Ordinal)
                && string.Equals(uiAvailable, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    // Pure formatter for the optional centralized quick-close endpoint. Keeping the validation and
    // invariant-culture number formatting outside Unity makes the module half of the contract easy
    // to test without a game runtime.
    internal static class SuiteUiStatePolicy
    {
        internal static string Build(string moduleId, bool open, int sortOrder, double activated)
        {
            if (string.IsNullOrEmpty(moduleId)) return string.Empty;
            if (sortOrder < -10000) sortOrder = -10000;
            if (sortOrder > 10000) sortOrder = 10000;
            if (double.IsNaN(activated) || double.IsInfinity(activated) || activated < 0d) activated = 0d;
            return "protocol=1&module=" + moduleId
                + "&open=" + (open ? "true" : "false")
                + "&closeable=true&sortOrder=" + sortOrder.ToString(CultureInfo.InvariantCulture)
                + "&activated=" + activated.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    internal enum SuitePanelAction
    {
        Unknown = 0,
        OpenPanel = 1,
        ClosePanel = 2,
        ResetPanel = 3,
        ResetLauncher = 4
    }

    internal static class SuiteUiControlPolicy
    {
        internal static bool TryParseBool(string value, out bool parsed)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) { parsed = true; return true; }
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) { parsed = false; return true; }
            parsed = false;
            return false;
        }

        internal static SuitePanelAction ParsePanelAction(string actionId)
        {
            if (string.Equals(actionId, "openPanel", StringComparison.Ordinal)) return SuitePanelAction.OpenPanel;
            if (string.Equals(actionId, "closePanel", StringComparison.Ordinal)) return SuitePanelAction.ClosePanel;
            if (string.Equals(actionId, "resetPanel", StringComparison.Ordinal)) return SuitePanelAction.ResetPanel;
            if (string.Equals(actionId, "resetLauncher", StringComparison.Ordinal)) return SuitePanelAction.ResetLauncher;
            return SuitePanelAction.Unknown;
        }

        internal static string BoundStatus(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ');
            return clean.Length <= 240 ? clean : clean.Substring(0, 240);
        }
    }

    // Pure gesture lifetime state. The Unity handlers own pointer conversion and GameData mutation;
    // this tiny state object makes "force release means no stale active gesture" testable offline.
    internal sealed class SuiteUiGestureState
    {
        internal bool IsActive { get; private set; }

        internal void Begin() { IsActive = true; }

        internal bool End()
        {
            bool wasActive = IsActive;
            IsActive = false;
            return wasActive;
        }

        internal void ForceRelease() { IsActive = false; }
    }

    internal static class SuiteCameraOwnershipPolicy
    {
        internal static bool PromoteUsingUi(bool nativeResult, bool contractsOwnsGesture)
        {
            return nativeResult || contractsOwnsGesture;
        }

        internal static string CollapseGlyph(bool collapsed)
        {
            return collapsed ? "▼" : "▲";
        }
    }


    // Shared window-chrome geometry for the dedicated retained panels in this standalone mod.
    // Keep this Unity-free so collapse/containment behavior is testable without Erenshor.
    internal static class SuiteWindowChromePolicy
    {
        internal const float HeaderHeight = 32f;
        internal const float CollapsedHeight = 32f;
        internal const float NormalRowHeight = 26f;
        internal const float RowSpacing = 3f;
        internal const float SectionSpacing = 6f;

        internal static float ResolveDisplayHeight(bool collapsed, float expandedHeight, float minimumExpandedHeight)
        {
            if (collapsed) return CollapsedHeight;
            if (!SuiteUiPositionPolicy.IsFinite(expandedHeight)) expandedHeight = minimumExpandedHeight;
            if (!SuiteUiPositionPolicy.IsFinite(minimumExpandedHeight) || minimumExpandedHeight < CollapsedHeight)
                minimumExpandedHeight = CollapsedHeight;
            return Math.Max(minimumExpandedHeight, expandedHeight);
        }

        internal static float PreserveTopBottomY(float currentBottomY, float oldHeight, float newHeight)
        {
            if (!SuiteUiPositionPolicy.IsFinite(currentBottomY)) currentBottomY = 0f;
            if (!SuiteUiPositionPolicy.IsFinite(oldHeight) || oldHeight < 0f) oldHeight = 0f;
            if (!SuiteUiPositionPolicy.IsFinite(newHeight) || newHeight < 0f) newHeight = 0f;
            return currentBottomY + oldHeight - newHeight;
        }

        internal static float ClampOrigin(float origin, float screenExtent, float panelExtent)
        {
            if (!SuiteUiPositionPolicy.IsFinite(origin)) origin = 0f;
            if (!SuiteUiPositionPolicy.IsFinite(screenExtent) || screenExtent < 0f) screenExtent = 0f;
            if (!SuiteUiPositionPolicy.IsFinite(panelExtent) || panelExtent < 0f) panelExtent = 0f;
            return SuiteUiPositionPolicy.Clamp(origin, 0f, Math.Max(0f, screenExtent - panelExtent));
        }

        internal static bool ShouldRebuildStructure(string previousSignature, string nextSignature)
        {
            return !string.Equals(previousSignature ?? string.Empty, nextSignature ?? string.Empty, StringComparison.Ordinal);
        }

        internal static bool IsCompactGeometryValid()
        {
            return HeaderHeight >= 28f && HeaderHeight <= 32f
                && NormalRowHeight >= 24f && NormalRowHeight <= 29f
                && RowSpacing >= 3f && RowSpacing <= 5f
                && SectionSpacing >= 6f && SectionSpacing <= 12f
                && CollapsedHeight == HeaderHeight;
        }
    }

    internal static class SuiteUiPositionPolicy
    {
        internal const float Unset = -1f;

        internal static float InterpretStoredAxis(float stored)
        {
            if (!IsFinite(stored) || stored < 0f) return Unset;
            if (stored <= 1f) return stored;
            // Pre-uGUI builds stored top-left pixel coordinates. Do not mirror/rescale them
            // into bottom-left retained-uGUI space; recover to the known-good default once.
            return Unset;
        }

        internal static float NormalizeAxis(float pixels, float extent)
        {
            if (!IsFinite(pixels) || !IsFinite(extent) || extent <= 0f) return 0f;
            return Clamp(pixels / extent, 0f, 1f);
        }

        internal static float ResolveAxis(float stored, float defaultNormalized, float extent, float size)
        {
            float normalized = InterpretStoredAxis(stored);
            if (normalized < 0f) normalized = Clamp(defaultNormalized, 0f, 1f);
            if (!IsFinite(extent) || extent <= 0f) return 0f;
            if (!IsFinite(size) || size < 0f) size = 0f;
            float max = Math.Max(0f, extent - size);
            return Clamp(normalized * extent, 0f, max);
        }

        internal static float Clamp(float value, float min, float max)
        {
            if (!IsFinite(value)) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static string RunSelfTests()
        {
            if (ContractsSuiteWirePolicy.BuildBasicSettings(false) != "id=showLauncher&label=Show%20Contracts%20Launcher&tier=basic&type=bool&value=false&mutable=true") return "FAIL contracts basic setting false wire";
            if (ContractsSuiteWirePolicy.BuildBasicSettings(true) != "id=showLauncher&label=Show%20Contracts%20Launcher&tier=basic&type=bool&value=true&mutable=true") return "FAIL contracts basic setting true wire";
            if (LauncherVisibilityPolicy.ShouldShow(false, false, false, false)) return "FAIL launcher visible before gameplay ready";
            if (!LauncherVisibilityPolicy.ShouldShow(true, false, true, false)) return "FAIL launcher hidden without Hub";
            if (!LauncherVisibilityPolicy.ShouldShow(true, true, false, false)) return "FAIL launcher hidden when module bridge unavailable";
            if (LauncherVisibilityPolicy.ShouldShow(true, true, true, false)) return "FAIL launcher visible with usable Hub when setting off";
            if (!LauncherVisibilityPolicy.ShouldShow(true, true, true, true)) return "FAIL explicit launcher setting ignored";
            if (!SuiteHubPresencePolicy.IsUsable("protocol=1&module=suitehub&status=Ready&uiAvailable=true&interactionValidated=false&quickClose=0")) return "FAIL usable Hub presence rejected";
            if (SuiteHubPresencePolicy.IsUsable("protocol=1&module=suitehub&status=Ready&uiAvailable=false")) return "FAIL unavailable Hub UI accepted";
            if (SuiteHubPresencePolicy.IsUsable("protocol=1&module=suitehub&status=Ready")) return "FAIL missing Hub UI availability accepted";
            if (SuiteHubPresencePolicy.IsUsable("protocol=1&module=suitehub&status=NotReady&uiAvailable=true")) return "FAIL not-ready Hub accepted";
            if (SuiteHubPresencePolicy.IsUsable("protocol=1&module=suitehub&status=Ready&uiAvailable=true&status=Ready")) return "FAIL duplicate Hub field accepted";
            if (SuiteHubPresencePolicy.IsUsable("protocol=1&module=suitehub&status=Ready&uiAvailable=true&uiAvailable=true")) return "FAIL duplicate UI availability field accepted";
            string uiState = SuiteUiStatePolicy.Build("contracts", true, 521, 12.5d);
            if (uiState != "protocol=1&module=contracts&open=true&closeable=true&sortOrder=521&activated=12.5") return "FAIL ui.state formatting";
            if (SuiteUiStatePolicy.Build("contracts", false, 50000, double.NaN).IndexOf("sortOrder=10000&activated=0", StringComparison.Ordinal) < 0) return "FAIL ui.state bounds";
            if (InterpretStoredAxis(float.NaN) != Unset) return "FAIL NaN storage accepted";
            if (InterpretStoredAxis(float.PositiveInfinity) != Unset) return "FAIL infinite storage accepted";
            if (InterpretStoredAxis(500f) != Unset) return "FAIL legacy pixel storage accepted as normalized";
            if (Math.Abs(InterpretStoredAxis(0.25f) - 0.25f) > 0.0001f) return "FAIL normalized storage rejected";
            if (Math.Abs(ResolveAxis(1f, 0.5f, 1000f, 300f) - 700f) > 0.001f) return "FAIL offscreen clamp";
            if (Math.Abs(ResolveAxis(Unset, 0.5f, 1000f, 200f) - 500f) > 0.001f) return "FAIL default resolve";
            bool parsed;
            if (!SuiteUiControlPolicy.TryParseBool("true", out parsed) || !parsed) return "FAIL true setting parse";
            if (!SuiteUiControlPolicy.TryParseBool("FALSE", out parsed) || parsed) return "FAIL false setting parse";
            if (SuiteUiControlPolicy.TryParseBool("1", out parsed)) return "FAIL loose bool parse";
            if (SuiteUiControlPolicy.ParsePanelAction("openPanel") != SuitePanelAction.OpenPanel) return "FAIL open action route";
            if (SuiteUiControlPolicy.ParsePanelAction("closePanel") != SuitePanelAction.ClosePanel) return "FAIL close action route";
            if (SuiteUiControlPolicy.ParsePanelAction("resetPanel") != SuitePanelAction.ResetPanel) return "FAIL reset panel action route";
            if (SuiteUiControlPolicy.ParsePanelAction("resetLauncher") != SuitePanelAction.ResetLauncher) return "FAIL reset launcher action route";
            if (SuiteUiControlPolicy.ParsePanelAction("unknown") != SuitePanelAction.Unknown) return "FAIL unknown action route";
            if (SuiteUiControlPolicy.BoundStatus(new string('x', 300)).Length != 240) return "FAIL status bound";
            if (SuiteUiControlPolicy.BoundStatus("a\nb").IndexOf('\n') >= 0) return "FAIL status newline";
            SuiteUiGestureState gesture = new SuiteUiGestureState();
            if (gesture.IsActive) return "FAIL gesture starts active";
            gesture.Begin();
            if (!gesture.IsActive) return "FAIL gesture begin";
            if (!gesture.End() || gesture.IsActive) return "FAIL gesture end";
            if (gesture.End()) return "FAIL gesture double-end";
            gesture.Begin();
            gesture.ForceRelease();
            if (gesture.IsActive) return "FAIL gesture force release";
            if (SuiteWindowChromePolicy.ShouldRebuildStructure("same", "same")) return "FAIL dynamic text requires structural rebuild";
            if (!SuiteWindowChromePolicy.ShouldRebuildStructure("old", "new")) return "FAIL structural change not detected";
            if (!SuiteWindowChromePolicy.IsCompactGeometryValid()) return "FAIL compact chrome geometry";
            if (Math.Abs(SuiteWindowChromePolicy.ResolveDisplayHeight(true, 480f, 320f) - 32f) > 0.001f) return "FAIL collapsed height";
            if (Math.Abs(SuiteWindowChromePolicy.ResolveDisplayHeight(false, 480f, 320f) - 480f) > 0.001f) return "FAIL expanded height";
            if (Math.Abs(SuiteWindowChromePolicy.ResolveDisplayHeight(false, 200f, 320f) - 320f) > 0.001f) return "FAIL expanded minimum height";
            if (Math.Abs(SuiteWindowChromePolicy.PreserveTopBottomY(100f, 400f, 32f) - 468f) > 0.001f) return "FAIL collapse top preservation";
            if (Math.Abs(SuiteWindowChromePolicy.PreserveTopBottomY(468f, 32f, 400f) - 100f) > 0.001f) return "FAIL expand top restoration";
            if (Math.Abs(SuiteWindowChromePolicy.ClampOrigin(900f, 1000f, 320f) - 680f) > 0.001f) return "FAIL panel containment clamp";
            return "PASS suite ui policies";
        }
    }
}
