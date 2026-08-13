using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        internal const string PluginVersion = "0.1.1";

        internal static ErenshorContractsPlugin Instance;
        private ContractsSuiteAuraProvider _auraProvider;

        private readonly List<ContractTemplate> _templates = new List<ContractTemplate>();
        private readonly Dictionary<string, ContractTemplate> _templateByKey =
            new Dictionary<string, ContractTemplate>(StringComparer.OrdinalIgnoreCase);

        private ContractsSettings _settings;
        private ContractsConfigEntry<float> _launcherX;
        private ContractsConfigEntry<float> _launcherY;
        private ContractsConfigEntry<bool> _showStandaloneLauncherWithHub;
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
        private bool _open;
        private bool _dirty;
        private float _saveAfter;
        private bool _cursorVisibleBeforeOpen;
        private CursorLockMode _cursorLockBeforeOpen;
        private string _currentZone;
        private float _nextBuiltinTick;
        private List<ContractOffer> _currentOffers = new List<ContractOffer>();

        // Character-scoped data. No contract data is loaded until a real, spawned character is
        // confirmed present (see IsLocalCharacterReady/EnsureCharacter) so nothing is ever read
        // from or written to disk while sitting at the title/login/character-select screens.
        private string _dataRoot;
        private string _legacyDataPath;
        private string _legacyClaimMarkerPath;
        private string _characterKey;

        // Retained-uGUI callbacks queue open/close/toggle requests; Update applies the authoritative
        // state transition once per frame so UI, commands and Hub actions share the same route.
        private bool _pendingToggle;
        private bool _pendingClose;
        private bool _pendingOpen;

        // Flip to true only while diagnosing the launcher/board toggle chain; keep false for
        // release builds so this stays quiet in the log.
        private const bool DiagLogEnabled = false;

        // Duplicate-instance investigation (closed): a fresh lunaris.log confirmed exactly one live
        // Contracts instance throughout a full session (instance hash stayed constant from Awake
        // through OnDestroy). The per-tick Update() diagnostic that produced that evidence has been
        // removed since it's served its purpose and was spamming the log every ~5s; the cheap
        // Awake/OnDestroy instance-id lines below are kept in case this ever needs re-checking.

        private void Awake()
        {
            Instance = this;
            _settings = new ContractsSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();

            _dataRoot = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorContracts");
            _legacyDataPath = Path.Combine(_dataRoot, "contracts.dat");
            _legacyClaimMarkerPath = _legacyDataPath + ".claimed";
            // _store/_document/_characterKey stay null until EnsureCharacter() resolves a real
            // player-controlled character; see IsLocalCharacterReady().

            _window = new ContractBoardWindow();
            _launcher = new ContractLauncher();
            InitializeRetainedUi();

            RegisterOrReplace(ContractCore.BuildPatrolTemplate(_patrolMinutes.Value));
            RegisterOrReplace(ContractCore.BuildRoadCheckTemplate());
            RegisterOrReplace(ContractCore.BuildWayfarerTemplate());

            _currentZone = CurrentSceneName();
            SceneManager.sceneLoaded += OnSceneLoaded;
            RebuildOffers();

            try { _auraProvider = new ContractsSuiteAuraProvider(this); }
            catch (Exception ex) { Logging.LogError("Erenshor Contracts Aura provider init failed: " + ex); }

            Logging.LogInfo(
                "Erenshor Contracts " + PluginVersion +
                " loaded. Use the draggable CONTRACTS UI button. No global hotkey is registered. " +
                "This Preview tracks local contracts but deliberately does not grant native XP, gold, or items.");

            if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts lifecycle: Awake.");
        }

        private void InitializeConfigEntries()
        {
            _launcherX = new ContractsConfigEntry<float>(delegate { return _settings.LauncherX; }, delegate(float v) { _settings.LauncherX = v; });
            _launcherY = new ContractsConfigEntry<float>(delegate { return _settings.LauncherY; }, delegate(float v) { _settings.LauncherY = v; });
            _showStandaloneLauncherWithHub = new ContractsConfigEntry<bool>(delegate { return _settings.ShowStandaloneLauncherWithHub; }, delegate(bool v) { _settings.ShowStandaloneLauncherWithHub = v; });
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

                if (_pendingOpen) { _pendingOpen = false; if (SuiteUiPolicy.IsGameplayReady() && !_open) OpenBoard(); }
                if (_pendingClose) { _pendingClose = false; if (_open) CloseBoard(); }
                if (_pendingToggle) { _pendingToggle = false; ToggleBoard(); }

                bool ready = SuiteUiPolicy.IsGameplayReady();
                if (ready) EnsureCharacter();
                else
                {
                    if (_open) CloseBoard();
                    SuiteDragHandler.ForceReleaseIfOwned();
                }

                string scene = CurrentSceneName();
                if (!string.Equals(scene, _currentZone, StringComparison.Ordinal))
                {
                    HandleTransition(_currentZone, scene);
                    _currentZone = scene;
                    RebuildOffers();
                }

                if (ready && _document != null && Time.unscaledTime >= _nextBuiltinTick)
                {
                    _nextBuiltinTick = Time.unscaledTime + 1f;
                    if (IsUsableScene(_currentZone) && ContractCore.AddZoneSeconds(_document, _currentZone, 1) > 0)
                    {
                        MarkDirty();
                        RebuildOffers();
                    }
                }

                bool bridgeRegistered = _auraProvider != null && _auraProvider.Registered;
                bool showLauncher = SuiteUiPolicy.ShouldShowStandaloneLauncher(
                    bridgeRegistered,
                    _showStandaloneLauncherWithHub != null && _showStandaloneLauncherWithHub.Value);
                if (_launcher != null) _launcher.Tick(showLauncher, _open);
                if (_window != null) _window.Tick(ready && _open, _currentZone, _currentOffers, _document, AcceptOffer, Abandon, Claim);

                if (_dirty && Time.unscaledTime >= _saveAfter) SaveNow();
            }
            catch (Exception ex)
            {
                try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
                Logging.LogError("Erenshor Contracts update failed: " + ex);
            }
        }

        // Verified player-ready signal (not scene-name matching). Same pattern already live-
        // tested in the sibling Erenshor-Nemesis mod's NemesisDirector.Ready(). Recomputed every
        // frame cheaply; never cached across scene loads.
        private static bool IsLocalCharacterReady()
        {
            return SuiteUiPolicy.IsGameplayReady();
        }

        private static string PlayerName()
        {
            try
            {
                string name = GameData.PlayerControl.Myself.MyStats.MyName;
                return string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            }
            catch { return "Player"; }
        }

        // Two save slots can hold the same character name, so persistence keys from the verified
        // slot index when the slot's recorded name matches the live character, and from the name
        // alone otherwise. No Erenshor save file is written or modified by this mod.
        private static int ResolveSlotIndex()
        {
            try
            {
                SaveGameData active = GameData.CurrentCharacterSlot != null ? GameData.CurrentCharacterSlot : GameData.ActiveSaveSlot;
                if (active == null || active.index < 0) return -1;
                string recorded = (active.CharName ?? string.Empty).Trim();
                if (recorded.Length > 0 && !string.Equals(recorded, PlayerName(), StringComparison.OrdinalIgnoreCase)) return -1;
                return active.index;
            }
            catch { return -1; }
        }

        private static string ResolveCharacterKey()
        {
            return ContractCharacterKey.Resolve(PlayerName(), ResolveSlotIndex());
        }

        // Called every frame while IsLocalCharacterReady() is true. On a resolved-key change
        // (including the very first resolution after Awake): save+release the previous
        // character's data, close the board so character A's contracts can never be shown while
        // character B's data is loading, then load (or legacy-claim then load) the new
        // character's own file and rebuild the transient offer list.
        private void EnsureCharacter()
        {
            string key = ResolveCharacterKey();
            if (string.Equals(key, _characterKey, StringComparison.Ordinal)) return;

            if (_dirty) SaveNow();
            if (_open) CloseBoard();
            _store = null;
            _document = null;

            _characterKey = key;
            string characterDirectory = Path.Combine(Path.Combine(_dataRoot, "Characters"), key);
            string characterDataPath = Path.Combine(characterDirectory, "contracts.dat");

            ContractStore.TryClaimLegacyData(_legacyDataPath, _legacyClaimMarkerPath, characterDataPath, key);

            _store = new ContractStore(characterDataPath);
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning))
                Logging.LogWarning("Erenshor Contracts recovered from unreadable local data for character '" + key + "'. " + warning);

            _currentOffers = new List<ContractOffer>();
            RebuildOffers();
            Logging.LogInfo("Erenshor Contracts: active character resolved to '" + key + "'.");
        }

        private void OnDestroy()
        {
            if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts lifecycle: OnDestroy.");
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
            try { SaveNow(); } catch { }
            try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            _window = null; _launcher = null; _document = null; _store = null; _characterKey = null;
            SuiteUiPolicy.Reset();
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SuiteDragHandler.ForceReleaseIfOwned();
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

        internal bool ControlPanelOpen { get { return _open; } }
        internal string ControlCharacterKey { get { return _characterKey ?? string.Empty; } }
        internal string ControlZone { get { return _currentZone ?? string.Empty; } }
        internal ContractDocument ControlDocument { get { return _document; } }
        internal List<ContractOffer> ControlOffers { get { return new List<ContractOffer>(_currentOffers); } }
        internal int ControlDailySlots { get { return _dailySlots == null ? 3 : Math.Max(1, Math.Min(6, _dailySlots.Value)); } }
        internal int ControlPatrolMinutes { get { return _patrolMinutes == null ? 3 : Math.Max(1, Math.Min(60, _patrolMinutes.Value)); } }
        internal void RequestOpenBoard() { _pendingOpen = true; }
        internal void RequestCloseBoard() { _pendingClose = true; }
        internal bool ControlShowStandaloneLauncher { get { return _showStandaloneLauncherWithHub != null && _showStandaloneLauncherWithHub.Value; } }
        internal void SetShowStandaloneLauncher(bool value)
        {
            if (_showStandaloneLauncherWithHub != null) _showStandaloneLauncherWithHub.Value = value;
            try { Config.Save(); } catch { }
        }
        internal void ResetLauncherPosition() { if (_launcher != null) _launcher.ResetPosition(); }

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

        private void InitializeRetainedUi()
        {
            _window.Initialize(_windowX.Value, _windowY.Value, _windowWidth.Value, _windowHeight.Value,
                PersistWindowPosition, PersistWindowSize, RequestCloseBoard, ResetWindowPosition);
            _launcher.Initialize(_launcherX.Value, _launcherY.Value, PersistLauncherPosition,
                delegate { _pendingToggle = true; });
        }

        private void PersistWindowPosition(float x, float y)
        {
            if (_windowX == null || _windowY == null) return;
            _windowX.Value = x; _windowY.Value = y;
            try { Config.Save(); } catch { }
        }

        private void PersistWindowSize(float width, float height)
        {
            if (_windowWidth == null || _windowHeight == null) return;
            if (float.IsNaN(width) || float.IsInfinity(width) || float.IsNaN(height) || float.IsInfinity(height)) return;
            _windowWidth.Value = Mathf.Max(520f, width);
            _windowHeight.Value = Mathf.Max(360f, height);
            try { Config.Save(); } catch { }
        }

        private void PersistLauncherPosition(float x, float y)
        {
            if (_launcherX == null || _launcherY == null) return;
            _launcherX.Value = x; _launcherY.Value = y;
            try { Config.Save(); } catch { }
        }

        internal void ResetWindowPosition() { if (_window != null) _window.ResetPosition(); }

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

    }
}
