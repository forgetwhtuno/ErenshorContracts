using System;
using System.Collections.Generic;
using System.IO;
using Lunaris;
using Lunaris.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorContracts
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "Local/daily contract board with a provider API for other mods to register verified objectives.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection)]
    public sealed class ErenshorContractsPlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.contracts";
        internal const string PluginName = "Erenshor Contracts";
        internal const string PluginVersion = "0.1.0";

        private readonly List<ContractTemplate> _templates = new List<ContractTemplate>();
        private readonly Dictionary<string, ContractTemplate> _templateByKey =
            new Dictionary<string, ContractTemplate>(StringComparer.OrdinalIgnoreCase);

        private ContractsSettings _settings;
        private ContractsConfigEntry<float> _launcherX;
        private ContractsConfigEntry<float> _launcherY;
        private ContractsConfigEntry<float> _windowX;
        private ContractsConfigEntry<float> _windowY;
        private ContractsConfigEntry<float> _windowWidth;
        private ContractsConfigEntry<float> _windowHeight;
        private ContractsConfigEntry<int> _dailySlots;
        private ContractsConfigEntry<int> _patrolMinutes;
        private ContractsConfigEntry<string> _profileKey;

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
            _settings = new ContractsSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();

            string dataDirectory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorContracts");
            _store = new ContractStore(Path.Combine(dataDirectory, "contracts.dat"));
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning))
                Logging.LogWarning("Erenshor Contracts recovered from unreadable local data. " + warning);

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

            Logging.LogInfo(
                "Erenshor Contracts " + PluginVersion +
                " loaded. Use the draggable CONTRACTS UI button. No global hotkey is registered. " +
                "This Preview tracks local contracts but deliberately does not grant native XP, gold, or items.");
        }

        private void InitializeConfigEntries()
        {
            _launcherX = new ContractsConfigEntry<float>(delegate { return _settings.LauncherX; }, delegate(float v) { _settings.LauncherX = v; });
            _launcherY = new ContractsConfigEntry<float>(delegate { return _settings.LauncherY; }, delegate(float v) { _settings.LauncherY = v; });
            _windowX = new ContractsConfigEntry<float>(delegate { return _settings.WindowX; }, delegate(float v) { _settings.WindowX = v; });
            _windowY = new ContractsConfigEntry<float>(delegate { return _settings.WindowY; }, delegate(float v) { _settings.WindowY = v; });
            _windowWidth = new ContractsConfigEntry<float>(delegate { return _settings.WindowWidth; }, delegate(float v) { _settings.WindowWidth = v; });
            _windowHeight = new ContractsConfigEntry<float>(delegate { return _settings.WindowHeight; }, delegate(float v) { _settings.WindowHeight = v; });
            _dailySlots = new ContractsConfigEntry<int>(delegate { return _settings.DailySlots; }, delegate(int v) { _settings.DailySlots = v; });
            _patrolMinutes = new ContractsConfigEntry<int>(delegate { return _settings.PatrolMinutes; }, delegate(int v) { _settings.PatrolMinutes = v; });
            _profileKey = new ContractsConfigEntry<string>(delegate { return _settings.ProfileKey; }, delegate(string v) { _settings.ProfileKey = v; });
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
                Logging.LogError("Erenshor Contracts update failed: " + ex);
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
                Logging.LogError("Erenshor Contracts UI failed: " + ex);
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
                Logging.LogError("Erenshor Contracts could not save local state: " +
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
            _windowX.Value = rect.x;
            _windowY.Value = rect.y;
            _windowWidth.Value = rect.width;
            _windowHeight.Value = rect.height;
            Config.Save();
        }

        private void PersistLauncherRect()
        {
            if (_launcherX == null || _launcherY == null) return;
            Rect rect = ClampLauncherRect(_launcherRect);
            _launcherX.Value = rect.x;
            _launcherY.Value = rect.y;
            Config.Save();
            _launcherDirty = false;
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
