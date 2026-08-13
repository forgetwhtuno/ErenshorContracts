using System;

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
            if (LauncherVisibilityPolicy.ShouldShow(false, false, false, false)) return "FAIL launcher visible before gameplay ready";
            if (!LauncherVisibilityPolicy.ShouldShow(true, false, true, false)) return "FAIL launcher hidden without Hub";
            if (!LauncherVisibilityPolicy.ShouldShow(true, true, false, false)) return "FAIL launcher hidden when module bridge unavailable";
            if (LauncherVisibilityPolicy.ShouldShow(true, true, true, false)) return "FAIL launcher visible with usable Hub when setting off";
            if (!LauncherVisibilityPolicy.ShouldShow(true, true, true, true)) return "FAIL explicit launcher setting ignored";
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
            return "PASS suite ui policies";
        }
    }
}
