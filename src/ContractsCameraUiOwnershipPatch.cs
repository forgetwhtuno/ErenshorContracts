using System;
using System.Reflection;
using HarmonyLib;

namespace ErenshorContracts
{
    // Only promotes the game's answer while this module owns a left-button drag/resize gesture;
    // it never clears a native UI result and ForceReleaseIfOwned clears ownership on every close,
    // scene transition, disposal, and Update exception path.
    internal static class ContractsCameraUiOwnershipPatch
    {
        internal static bool TryInstall(Harmony harmony, out string diagnostic)
        {
            diagnostic = "camera containment unavailable";
            if (harmony == null) return false;
            try
            {
                MethodInfo target = typeof(CameraController).GetMethod("UsingUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                MethodInfo postfix = typeof(ContractsCameraUiOwnershipPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (target == null || target.ReturnType != typeof(bool) || postfix == null) { diagnostic = "CameraController.UsingUI shape mismatch"; return false; }
                harmony.Patch(target, null, new HarmonyMethod(postfix));
                diagnostic = "verified bool CameraController.UsingUI postfix installed";
                return true;
            }
            catch (Exception ex) { diagnostic = "camera patch failed: " + ex.GetType().Name; return false; }
        }

        private static void Postfix(ref bool __result)
        {
            __result = SuiteCameraOwnershipPolicy.PromoteUsingUi(__result, SuiteDragHandler.HasOwners || SuiteResizeHandler.HasOwners);
        }
    }
}
