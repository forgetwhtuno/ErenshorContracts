using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorContracts
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Erenshor.exe")]
    public sealed class ErenshorContractsPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.contracts";
        internal const string PluginName = "Erenshor Contracts";
        internal const string PluginVersion = "0.1.0";

        private readonly List<ContractTemplate> _templates = new List<ContractTemplate>();
        private readonly Dictionary<string, ContractTemplate> _templateByKey =
            new Dictionary<string, ContractTemplate>(StringComparer.OrdinalIgnoreCase);

        private ConfigEntry<float> _launcherX;
        private ConfigEntry<float> _launcherY;
        private ConfigEntry<float> _windowX;
        private ConfigEntry<float> _windowY;
        private ConfigEntry<float> _windowWidth;
        private ConfigEntry<float> _windowHeight;
        private ConfigEntry<int> _dailySlots;
        private ConfigEntry<int> _patrolMinutes;
        private ConfigEntry<string> _profileKey;

        private ContractStore _store;
        private ContractDocument _document;
        private ContractBoardWindow _window;
        private ContractLauncher _launcher;
        private Rect _windowRect;
        private Rect _launcherRect;
        private bool _open;
        private bool _dirty;
        private float _saveAfter;
        private bool _launcherDirty;
        private float _launcherSaveAfter;
        private bool _cursorVisibleBeforeOpen;
        private CursorLockMode _cursorLockBeforeOpen;
        private string _currentZone;
        private float _nextBuiltinTick;
        private List<ContractOffer> _currentOffers = new List<ContractOffer>();

        private void Awake()
        {
            _launcherX = Config.Bind("UI", "LauncherX", -1f,
                "Saved Contracts launcher X position. -1 places it near the right side on first use.");
            _launcherY = Config.Bind("UI", "LauncherY", -1f,
                "Saved Contracts launcher Y position. -1 places it below the usual map area on first use.");
            _windowX = Config.Bind("UI", "WindowX", -1f, "Saved Contracts window X position.");
            _windowY = Config.Bind("UI", "WindowY", -1f, "Saved Contracts window Y position.");
            _windowWidth = Config.Bind("UI", "WindowWidth", 690f, "Contracts window width in pixels.");
            _windowHeight = Config.Bind("UI", "WindowHeight", 540f, "Contracts window height in pixels.");

            _dailySlots = Config.Bind("Contracts", "DailySlots", 3,
                "Number of deterministic daily contracts offered in each scene, clamped to 1-6.");
            _patrolMinutes = Config.Bind("Contracts", "PatrolMinutes", 3,
                "Minutes required by the built-in Local Patrol fallback, clamped to 1-60.");
            _profileKey = Config.Bind("Contracts", "ProfileKey", "local",
                "Local sidecar profile key used to keep daily rotation stable. Change it only if you intentionally want a separate Contracts profile.");

            string dataDirectory = Path.Combine(Paths.ConfigPath, "ErenshorContracts");
            _store = new ContractStore(Path.Combine(dataDirectory, "contracts.dat"));
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning))
                Logger.LogWarning("Erenshor Contracts recovered from unreadable local data. " + warning);

            _window = new ContractBoardWindow();
            _launcher = new ContractLauncher();
            _windowRect = ResolveInitialWindowRect();
            _launcherRect = ResolveInitialLauncherRect();

            RegisterOrReplace(ContractCore.BuildPatrolTemplate(_patrolMinutes.Value));
            RegisterOrReplace(ContractCore.BuildRoadCheckTemplate());
            RegisterOrReplace(ContractCore.BuildWayfarerTemplate());

            _currentZone = CurrentSceneName();
            SceneManager.sceneLoaded += OnSceneLoaded;
            RebuildOffers();

            Logger.LogInfo(
                "Erenshor Contracts " + PluginVersion +
                " loaded. Use the draggable CONTRACTS UI button. No global hotkey is registered. " +
                "This Preview tracks local contracts but deliberately does not grant native XP, gold, or items.");
        }

        private void Update()
        {
            try
            {
                DrainApi();

                if (_dirty && Time.unscaledTime >= _saveAfter) SaveNow();
                if (_launcherDirty && Time.unscaledTime >= _launcherSaveAfter) PersistLauncherRect();

                string scene = CurrentSceneName();
                if (!string.Equals(scene, _currentZone, StringComparison.Ordinal))
                {
                    HandleTransition(_currentZone, scene);
                    _currentZone = scene;
                    RebuildOffers();
                }

                if (Time.unscaledTime >= _nextBuiltinTick)
                {
                    _nextBuiltinTick = Time.unscaledTime + 1f;
                    if (IsUsableScene(_currentZone) && ContractCore.AddZoneSeconds(_document, _currentZone, 1) > 0)
                    {
                        MarkDirty();
                        RebuildOffers();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Erenshor Contracts update failed: " + ex);
            }
        }

        private void OnGUI()
        {
            try
            {
                if (!IsUsableScene(_currentZone))
                {
                    if (_open) CloseBoard();
                    return;
                }

                if (_open && _window != null && _document != null)
                {
                    _windowRect = ClampWindowRect(_window.Draw(
                        _windowRect,
                        _currentZone,
                        _currentOffers,
                        _document,
                        AcceptOffer,
                        Abandon,
                        Claim));
                    if (_window.RequestClose) CloseBoard();
                }

                if (_launcher != null)
                {
                    Rect previous = _launcherRect;
                    _launcherRect = ClampLauncherRect(_launcher.Draw(_launcherRect, _open));
                    if (!RectsNearlyEqual(previous, _launcherRect)) MarkLauncherDirty();
                    if (_launcher.RequestToggle) ToggleBoard();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Erenshor Contracts UI failed: " + ex);
                if (_open) CloseBoard();
            }
        }

        private void OnDestroy()
        {
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
            try { SaveNow(); } catch { }
            try { PersistWindowRect(); } catch { }
            try { PersistLauncherRect(); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }

            _window = null;
            _launcher = null;
            _document = null;
            _store = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string next = scene.name ?? string.Empty;
            if (string.Equals(next, _currentZone, StringComparison.Ordinal)) return;
            HandleTransition(_currentZone, next);
            _currentZone = next;
            RebuildOffers();
        }

        private void HandleTransition(string oldZone, string newZone)
        {
            if (_document == null) return;
            if (ContractCore.HandleZoneTransition(_document, oldZone, newZone) > 0)
            {
                MarkDirty();
                RebuildOffers();
            }
        }

        private void DrainApi()
        {
            bool catalogChanged = false;
            ContractTemplateRegistration registration;
            while (ContractBoardApi.TryDequeueTemplate(out registration))
            {
                ContractTemplate template = ContractCore.FromRegistration(registration);
                if (template == null) continue;
                RegisterOrReplace(template);
                catalogChanged = true;
            }

            ContractProgressReport report;
            bool progressChanged = false;
            while (ContractBoardApi.TryDequeueProgress(out report))
            {
                if (ContractCore.ApplyExternalProgress(_document, report) > 0)
                    progressChanged = true;
            }

            if (progressChanged)
            {
                MarkDirty();
                RebuildOffers();
            }
            if (catalogChanged) RebuildOffers();
        }

        private void RegisterOrReplace(ContractTemplate value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ProviderId) || string.IsNullOrWhiteSpace(value.TemplateId))
                return;

            string key = value.ProviderId + "|" + value.TemplateId;
            ContractTemplate existing;
            if (_templateByKey.TryGetValue(key, out existing))
                _templates.Remove(existing);

            _templateByKey[key] = value;
            _templates.Add(value);
        }

        private void RebuildOffers()
        {
            _currentOffers = ContractCore.BuildDailyOffers(
                DateTime.Now.Date,
                _currentZone,
                _profileKey == null ? "local" : _profileKey.Value,
                _templates,
                _document,
                _dailySlots == null ? 3 : _dailySlots.Value);
        }

        private void AcceptOffer(string occurrenceId)
        {
            for (int i = 0; i < _currentOffers.Count; i++)
            {
                ContractOffer offer = _currentOffers[i];
                if (!string.Equals(offer.OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase)) continue;
                if (ContractCore.Accept(_document, offer, _currentZone, DateTime.UtcNow) != null)
                {
                    MarkDirty();
                    RebuildOffers();
                }
                return;
            }
        }

        private void Abandon(string occurrenceId)
        {
            if (!ContractCore.Abandon(_document, occurrenceId)) return;
            MarkDirty();
            RebuildOffers();
        }

        private void Claim(string occurrenceId)
        {
            ContractInstance completed = ContractCore.Claim(_document, occurrenceId);
            if (completed == null) return;

            MarkDirty();
            RebuildOffers();
            SaveNow();

            string text = "Completed contract \"" + completed.Title + "\" in " +
                          (string.IsNullOrWhiteSpace(completed.OriginZone) ? "an unknown zone" : completed.OriginZone) + ".";
            JournalIntegration.TryAppend(text);
        }

        private void ToggleBoard()
        {
            if (_open) CloseBoard();
            else OpenBoard();
        }

        private void OpenBoard()
        {
            if (_open) return;
            _open = true;
            _cursorVisibleBeforeOpen = Cursor.visible;
            _cursorLockBeforeOpen = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void CloseBoard()
        {
            if (!_open) return;
            _open = false;
            SaveNow();
            PersistWindowRect();
            RestoreCursor();
        }

        private void RestoreCursor()
        {
            Cursor.visible = _cursorVisibleBeforeOpen;
            Cursor.lockState = _cursorLockBeforeOpen;
        }

        private void MarkDirty()
        {
            _dirty = true;
            _saveAfter = Time.unscaledTime + 0.8f;
        }

        private void MarkLauncherDirty()
        {
            _launcherDirty = true;
            _launcherSaveAfter = Time.unscaledTime + 0.8f;
        }

        private void SaveNow()
        {
            if (_store == null || _document == null) return;
            if (!_dirty && File.Exists(_store.PathOnDisk)) return;
            try
            {
                _store.Save(_document);
                _dirty = false;
            }
            catch (Exception ex)
            {
                _dirty = true;
                _saveAfter = Time.unscaledTime + 5f;
                Logger.LogError("Erenshor Contracts could not save local state: " +
                                ex.GetType().Name + ": " + ex.Message);
            }
        }

        private Rect ResolveInitialWindowRect()
        {
            float width = Mathf.Clamp(_windowWidth.Value, 540f, Mathf.Max(540f, Screen.width - 20f));
            float height = Mathf.Clamp(_windowHeight.Value, 380f, Mathf.Max(380f, Screen.height - 20f));
            float x = _windowX.Value < 0f ? (Screen.width - width) * 0.5f : _windowX.Value;
            float y = _windowY.Value < 0f ? (Screen.height - height) * 0.5f : _windowY.Value;
            return ClampWindowRect(new Rect(x, y, width, height));
        }

        private Rect ResolveInitialLauncherRect()
        {
            float x = _launcherX.Value < 0f ? Mathf.Max(0f, Screen.width - ContractLauncher.Width - 18f) : _launcherX.Value;
            float y = _launcherY.Value < 0f ? Mathf.Min(Mathf.Max(8f, 168f), Mathf.Max(0f, Screen.height - ContractLauncher.Height)) : _launcherY.Value;
            return ClampLauncherRect(new Rect(x, y, ContractLauncher.Width, ContractLauncher.Height));
        }

        private static Rect ClampWindowRect(Rect rect)
        {
            float maxWidth = Mathf.Max(540f, Screen.width - 20f);
            float maxHeight = Mathf.Max(380f, Screen.height - 20f);
            rect.width = Mathf.Clamp(rect.width, 540f, maxWidth);
            rect.height = Mathf.Clamp(rect.height, 380f, maxHeight);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private static Rect ClampLauncherRect(Rect rect)
        {
            rect.width = ContractLauncher.Width;
            rect.height = ContractLauncher.Height;
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private void PersistWindowRect()
        {
            if (_windowX == null || _windowY == null || _windowWidth == null || _windowHeight == null) return;
            Rect rect = ClampWindowRect(_windowRect);
            bool previous = Config.SaveOnConfigSet;
            try
            {
                Config.SaveOnConfigSet = false;
                _windowX.Value = rect.x;
                _windowY.Value = rect.y;
                _windowWidth.Value = rect.width;
                _windowHeight.Value = rect.height;
                Config.Save();
            }
            finally
            {
                Config.SaveOnConfigSet = previous;
            }
        }

        private void PersistLauncherRect()
        {
            if (_launcherX == null || _launcherY == null) return;
            Rect rect = ClampLauncherRect(_launcherRect);
            bool previous = Config.SaveOnConfigSet;
            try
            {
                Config.SaveOnConfigSet = false;
                _launcherX.Value = rect.x;
                _launcherY.Value = rect.y;
                Config.Save();
                _launcherDirty = false;
            }
            finally
            {
                Config.SaveOnConfigSet = previous;
            }
        }

        private static bool IsUsableScene(string scene)
        {
            if (string.IsNullOrWhiteSpace(scene)) return false;
            string lower = scene.ToLowerInvariant();
            if (lower.IndexOf("title", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("login", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("characterselect", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("mainmenu", StringComparison.Ordinal) >= 0)
                return false;
            return true;
        }

        private static string CurrentSceneName()
        {
            try { return SceneManager.GetActiveScene().name ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static bool RectsNearlyEqual(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.25f &&
                   Mathf.Abs(a.y - b.y) < 0.25f &&
                   Mathf.Abs(a.width - b.width) < 0.25f &&
                   Mathf.Abs(a.height - b.height) < 0.25f;
        }
    }
}
