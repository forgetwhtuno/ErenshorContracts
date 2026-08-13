using System;
using Lunaris;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorContracts
{
    // Standalone/Hub UI readiness+presence policy. Canonical v1 (integration handoff). No
    // compile-time dependency on Suite Hub. See CONTRACT_RECONCILIATION.md "Readiness contract".
    internal static class SuiteUiPolicy
    {
        private const float StableReadySeconds = 1.0f;
        private const float HubProbeSeconds = 1.0f;
        private const string HubPluginTypeName = "ErenshorSuiteHub.ErenshorSuiteHubPlugin";

        private static float _rawReadySince = -1f;
        private static int _readySceneHandle = int.MinValue;
        private static bool _canMoveLatched;
        private static bool _acquired;
        private static float _nextHubProbe;
        private static bool _hubAvailable;

        internal static bool IsGameplayReady()
        {
            if (!RawGameplayReady())
            {
                _rawReadySince = -1f;
                _readySceneHandle = int.MinValue;
                _canMoveLatched = false;
                _acquired = false;
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (_readySceneHandle != scene.handle)
            {
                _readySceneHandle = scene.handle;
                _rawReadySince = Time.unscaledTime;
                _canMoveLatched = false;
                _acquired = false;
            }
            if (_rawReadySince < 0f) _rawReadySince = Time.unscaledTime;

            if (_acquired)
            {
                // Once Ready is acquired, native UI temporarily setting CanMove=false must not
                // revoke it - do not re-check CanMove here.
                return true;
            }

            try { if (GameData.PlayerControl != null && GameData.PlayerControl.CanMove) _canMoveLatched = true; }
            catch { }

            if (!_canMoveLatched) return false;
            if (Time.unscaledTime - _rawReadySince < StableReadySeconds) return false;

            _acquired = true;
            return true;
        }

        internal static bool ShouldShowStandaloneLauncher(bool bridgeRegistered, bool explicitlyVisibleWithHub)
        {
            return LauncherVisibilityPolicy.ShouldShow(
                IsGameplayReady(), IsHubAvailable(), bridgeRegistered, explicitlyVisibleWithHub);
        }

        internal static bool IsHubAvailable()
        {
            if (Time.unscaledTime < _nextHubProbe) return _hubAvailable;
            _nextHubProbe = Time.unscaledTime + HubProbeSeconds;
            _hubAvailable = false;
            try
            {
                LunarisPlugin[] plugins = UnityEngine.Object.FindObjectsOfType<LunarisPlugin>();
                for (int i = 0; i < plugins.Length; i++)
                {
                    LunarisPlugin plugin = plugins[i];
                    if (plugin == null) continue;
                    Type type = plugin.GetType();
                    if (type != null && string.Equals(type.FullName, HubPluginTypeName, StringComparison.Ordinal))
                    {
                        _hubAvailable = true;
                        break;
                    }
                }
            }
            catch
            {
                _hubAvailable = false;
            }
            return _hubAvailable;
        }

        internal static void Reset()
        {
            _rawReadySince = -1f;
            _readySceneHandle = int.MinValue;
            _canMoveLatched = false;
            _acquired = false;
            _nextHubProbe = 0f;
            _hubAvailable = false;
        }

        private static bool RawGameplayReady()
        {
            try
            {
                if (GameData.InCharSelect || GameData.Zoning) return false;
                if (GameData.PlayerControl == null || GameData.PlayerControl.Myself == null) return false;
                Character player = GameData.PlayerControl.Myself;
                if (player.MyStats == null || player.gameObject == null || !player.gameObject.activeInHierarchy) return false;

                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded) return false;
                // The local Character is persistent (DontDestroyOnLoad) - do not compare its
                // GameObject's scene to the active zone scene.

                if (GameData.SimMngr == null || GameData.SimPlayerGrouping == null || GameData.GroupMembers == null)
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
