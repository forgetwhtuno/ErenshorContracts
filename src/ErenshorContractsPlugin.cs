using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Lunaris;
using Lunaris.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorContracts
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "MMO-like local/global contract board focused on verified native enemy culls with active-play refresh and optional provider objectives.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorContractsPlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.contracts";
        internal const string PluginName = "Erenshor Contracts";
        internal const string PluginVersion = "0.4.1";

        internal static ErenshorContractsPlugin Instance;
        private ContractsSuiteAuraProvider _auraProvider;
        private Harmony _harmony;
        private bool _killHooksReady;

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
        private ContractsConfigEntry<int> _globalSlots;
        private ContractsConfigEntry<int> _patrolMinutes;
        private ContractsConfigEntry<int> _globalPatrolMinutes;
        private ContractsConfigEntry<int> _localRefreshMinutes;
        private ContractsConfigEntry<int> _globalRefreshMinutes;
        private ContractsConfigEntry<bool> _enableNativeXpRewards;
        private ContractsConfigEntry<string> _profileKey;

        private ContractStore _store;
        private ContractDocument _document;
        private ContractBoardWindow _window;
        private ContractLauncher _launcher;
        private bool _open;
        private double _panelActivatedAt;
        private bool _dirty;
        private float _saveAfter;
        private bool _cursorVisibleBeforeOpen;
        private CursorLockMode _cursorLockBeforeOpen;
        private string _currentZone;
        private float _nextBuiltinTick;
        private float _nextEnemyScan;
        private bool _nativeEnemyScanReady;
        private List<ContractEnemyObservation> _latestEnemyObservations = new List<ContractEnemyObservation>();
        private List<ContractOffer> _currentLocalOffers = new List<ContractOffer>();
        private List<ContractOffer> _currentGlobalOffers = new List<ContractOffer>();
        private string _claimStatus = string.Empty;
        private float _claimStatusUntil;

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
        private bool _scopeTransitionBlocked;

        // Journal API v1 has no durable idempotency key. Keep only a bounded, process-local
        // character-keyed retry queue; see ContractJournalQueue for the duplicate-safety tradeoff.
        private readonly ContractJournalQueue _pendingJournalDeliveries = new ContractJournalQueue();
        private float _nextJournalRetry;

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
            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();
                string cameraDiagnostic;
                if (!ContractsCameraUiOwnershipPatch.TryInstall(_harmony, out cameraDiagnostic))
                    Logging.LogWarning("Erenshor Contracts camera gesture containment unavailable: " + cameraDiagnostic);
                _killHooksReady = true;
            }
            catch (Exception ex)
            {
                _killHooksReady = false;
                try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
                _harmony = null;
                Logging.LogError("Erenshor Contracts native kill hooks unavailable; combat contracts will not be generated. " + ex);
            }
            _settings = new ContractsSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();
            SuiteUiPolicy.InitializeHubPresence(this);

            _dataRoot = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorContracts");
            _legacyDataPath = Path.Combine(_dataRoot, "contracts.dat");
            _legacyClaimMarkerPath = _legacyDataPath + ".claimed";
            // _store/_document/_characterKey stay null until EnsureCharacter() resolves a real
            // player-controlled character; see IsLocalCharacterReady().

            _window = new ContractBoardWindow();
            _launcher = new ContractLauncher();
            InitializeRetainedUi();

            // New boards are combat-first. Keep one zone-local time objective as a low-priority
            // fallback when the current zone cannot prove enough suitable native enemies. Older
            // accepted travel/global contracts remain self-contained in persisted instances.
            RegisterOrReplace(ContractCore.BuildPatrolTemplate(_patrolMinutes.Value));

            // Do not bind gameplay locality while still on login/title scenes. The first resolved
            // character context seeds this from GameData.SceneName once gameplay is actually ready.
            _currentZone = string.Empty;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RebuildOffers();

            try { _auraProvider = new ContractsSuiteAuraProvider(this); }
            catch (Exception ex) { Logging.LogError("Erenshor Contracts Aura provider init failed: " + ex); }

            ContractBoardApi.SetRuntimeAvailable(true);

            Logging.LogInfo(
                "Erenshor Contracts " + PluginVersion +
                " loaded. Native-enemy combat contracts use persisted active-play board revisions. No global hotkey is registered. " +
                ContractNativeRewardAdapter.CapabilitySummary(ControlNativeXpRewardsEnabled) + ".");

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
            _globalSlots = new ContractsConfigEntry<int>(delegate { return _settings.GlobalSlots; }, delegate(int v) { _settings.GlobalSlots = v; });
            _patrolMinutes = new ContractsConfigEntry<int>(delegate { return _settings.PatrolMinutes; }, delegate(int v) { _settings.PatrolMinutes = v; });
            _globalPatrolMinutes = new ContractsConfigEntry<int>(delegate { return _settings.GlobalPatrolMinutes; }, delegate(int v) { _settings.GlobalPatrolMinutes = v; });
            _localRefreshMinutes = new ContractsConfigEntry<int>(delegate { return _settings.LocalRefreshMinutes; }, delegate(int v) { _settings.LocalRefreshMinutes = v; });
            _globalRefreshMinutes = new ContractsConfigEntry<int>(delegate { return _settings.GlobalRefreshMinutes; }, delegate(int v) { _settings.GlobalRefreshMinutes = v; });
            _enableNativeXpRewards = new ContractsConfigEntry<bool>(delegate { return _settings.EnableNativeXpRewards; }, delegate(bool v) { _settings.EnableNativeXpRewards = v; });
            _profileKey = new ContractsConfigEntry<string>(delegate { return _settings.ProfileKey; }, delegate(string v) { _settings.ProfileKey = v; });
        }

        private void Update()
        {
            try
            {
                DrainTemplateApi();

                if (_pendingOpen) { _pendingOpen = false; if (SuiteUiPolicy.IsGameplayReady()) { if (_open) MarkPanelActivated(); else OpenBoard(); } }
                if (_pendingClose) { _pendingClose = false; if (_open) CloseBoard(); }
                if (_pendingToggle) { _pendingToggle = false; ToggleBoard(); }

                bool ready = SuiteUiPolicy.IsGameplayReady();
                bool characterChanged = false;
                if (ready) characterChanged = EnsureCharacter();
                else
                {
                    if (_open) CloseBoard();
                    SuiteDragHandler.ForceReleaseIfOwned();
                    // Never carry a live travel edge across logout/title/character-select time.
                    // The next ready frame seeds the authoritative logical zone without progress.
                    _currentZone = string.Empty;
                }

                if (ready && !_scopeTransitionBlocked && _document != null)
                {
                    DrainProgressApi();
                    string logicalZone = CurrentZoneName();
                    if (IsUsableScene(logicalZone))
                    {
                        if (string.IsNullOrWhiteSpace(_currentZone) || characterChanged)
                        {
                            // A new character/session starts in its current logical zone; never
                            // treat the previous character's last zone as this character's travel.
                            _currentZone = logicalZone;
                            RebuildOffers();
                        }
                        else if (!string.Equals(logicalZone, _currentZone, StringComparison.OrdinalIgnoreCase))
                        {
                            HandleTransition(_currentZone, logicalZone);
                            _currentZone = logicalZone;
                            RebuildOffers();
                        }
                    }
                }
                else
                {
                    // Provider progress is a live event with no character id in API v1. If there is
                    // no authoritative character scope, discard it rather than replaying it later
                    // into a different slot. Templates are retained independently above.
                    DiscardProgressApi();
                }

                if (ready && !_scopeTransitionBlocked && _document != null && _killHooksReady &&
                    IsUsableScene(_currentZone) && Time.unscaledTime >= _nextEnemyScan)
                {
                    _nextEnemyScan = Time.unscaledTime + 5f;
                    ScanNativeEnemies();
                }

                if (ready && !_scopeTransitionBlocked && _document != null && Time.unscaledTime >= _nextBuiltinTick)
                {
                    _nextBuiltinTick = Time.unscaledTime + 1f;
                    bool simulationRunning = Time.timeScale > 0.0001f;
                    if (ContractCore.ShouldAccrueActivePlay(ready, _currentZone, Application.isFocused, simulationRunning))
                    {
                        ContractRefreshResult refresh = ContractCore.AdvanceActivePlay(
                            _document, 1, ControlLocalRefreshMinutes, ControlGlobalRefreshMinutes);
                        if (refresh.LocalRefreshed) ContractCore.EnsureLocalBoardZone(_document, _currentZone);
                        int progressChanged = ContractCore.AddActiveSeconds(_document, _currentZone, 1);
                        MarkProgressDirty();
                        if (refresh.AnyRefreshed || progressChanged > 0) RebuildOffers();
                    }
                }

                bool bridgeRegistered = _auraProvider != null && _auraProvider.Registered;
                bool showLauncher = SuiteUiPolicy.ShouldShowStandaloneLauncher(
                    bridgeRegistered,
                    _showStandaloneLauncherWithHub != null && _showStandaloneLauncherWithHub.Value);
                if (_launcher != null) _launcher.Tick(showLauncher, _open);
                if (_window != null) _window.Tick(ready && !_scopeTransitionBlocked && _open, _currentZone,
                    _document == null ? string.Empty : _document.LocalBoardZone, _currentLocalOffers, _currentGlobalOffers, _document,
                    ContractCore.SecondsUntilLocalRefresh(_document), ContractCore.SecondsUntilGlobalRefresh(_document),
                    ControlNativeXpRewardsEnabled, CurrentClaimStatus(), AcceptOffer, Abandon, Claim);

                if (_dirty && Time.unscaledTime >= _saveAfter) SaveNow();
                if (ready && !_scopeTransitionBlocked) TryFlushPendingJournal();
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
                return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            }
            catch { return string.Empty; }
        }

        // Two save slots can hold the same character name, so persistence keys use the verified
        // slot index when the slot's recorded name matches the live character. A name-only key is
        // allowed only when SaveSlots proves exactly one raw-name match and one sanitized-key match;
        // otherwise Contracts fails closed.
        // No Erenshor save file is written or modified by this mod.
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
            string name = PlayerName();
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            int slot = ResolveSlotIndex();
            return ContractCharacterKey.ResolveStrict(name, slot, CountMatchingSaveSlotNames(name), CountMatchingSafeSlotKeys(name));
        }

        private static int CountMatchingSaveSlotNames(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return 0;
            try
            {
                if (GameData.SaveSlots == null) return 0;
                int count = 0;
                foreach (SaveGameData slot in GameData.SaveSlots)
                {
                    if (slot == null) continue;
                    string recorded = (slot.CharName ?? string.Empty).Trim();
                    if (string.Equals(recorded, playerName.Trim(), StringComparison.OrdinalIgnoreCase)) count++;
                }
                return count;
            }
            catch { return 0; }
        }

        private static int CountMatchingSafeSlotKeys(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return 0;
            string target = ContractCharacterKey.SafeKey(playerName);
            if (string.IsNullOrWhiteSpace(target)) return 0;
            try
            {
                if (GameData.SaveSlots == null) return 0;
                int count = 0;
                foreach (SaveGameData slot in GameData.SaveSlots)
                {
                    if (slot == null || string.IsNullOrWhiteSpace(slot.CharName)) continue;
                    if (string.Equals(ContractCharacterKey.SafeKey(slot.CharName), target, StringComparison.Ordinal)) count++;
                }
                return count;
            }
            catch { return 0; }
        }

        // Called every frame while IsLocalCharacterReady() is true. On a resolved-key change
        // (including the very first resolution after Awake): save+release the previous
        // character's data, close the board so character A's contracts can never be shown while
        // character B's data is loading, then load (or legacy-claim then load) the new
        // character's own file and rebuild the transient offer list.
        private bool EnsureCharacter()
        {
            string key = ResolveCharacterKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                if (_dirty && !SaveNow())
                {
                    BlockForCharacterScopeSave();
                    return false;
                }
                BlockForAmbiguousCharacterScope();
                return false;
            }
            if (string.Equals(key, _characterKey, StringComparison.Ordinal))
            {
                _scopeTransitionBlocked = false;
                return false;
            }

            // Never discard an unsaved character-scoped document merely because the player changed
            // slots. While the old sidecar is retrying, hide/stop the board and do not dequeue new
            // character progress events. This prevents both state loss and cross-character progress.
            if (_dirty && !SaveNow())
            {
                BlockForCharacterScopeSave();
                return false;
            }

            if (_open) CloseBoard();
            _scopeTransitionBlocked = false;
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
                Logging.LogWarning("Erenshor Contracts recovered from unreadable character-scoped local data. " + warning);

            _currentLocalOffers = new List<ContractOffer>();
            _currentGlobalOffers = new List<ContractOffer>();
            _latestEnemyObservations = new List<ContractEnemyObservation>();
            _nativeEnemyScanReady = false;
            _nextEnemyScan = 0f;
            ContractKillCreditRuntime.Reset();
            ContractCore.AdvanceActivePlay(_document, 0, ControlLocalRefreshMinutes, ControlGlobalRefreshMinutes);
            _currentZone = CurrentZoneName();
            ContractCore.EnsureLocalBoardZone(_document, _currentZone);
            MarkProgressDirty();
            RebuildOffers();
            Logging.LogInfo("Erenshor Contracts: active character scope resolved.");
            return true;
        }

        private void OnDestroy()
        {
            if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts lifecycle: OnDestroy.");
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _harmony = null; _killHooksReady = false;
            ContractKillCreditRuntime.Reset();
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
            try { SceneManager.sceneUnloaded -= OnSceneUnloaded; } catch { }
            try { SaveNow(); } catch { }
            try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            _window = null; _launcher = null; _document = null; _store = null; _characterKey = null;
            SuiteUiPolicy.Reset();
            if (Instance == this) Instance = null;
            ContractBoardApi.ResetRuntimeState();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Unity sceneLoaded fires before all Erenshor logical game state is necessarily stable.
            // Travel is recognized later from GameData.SceneName while SuiteUiPolicy says gameplay
            // is ready; this prevents loading/title transitions from becoming contract progress.
            SuiteDragHandler.ForceReleaseIfOwned();
            ContractKillCreditRuntime.Reset();
            _latestEnemyObservations = new List<ContractEnemyObservation>();
            _nativeEnemyScanReady = false;
            _nextEnemyScan = 0f;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            ContractKillCreditRuntime.Reset();
            _latestEnemyObservations = new List<ContractEnemyObservation>();
            _nativeEnemyScanReady = false;
            _nextEnemyScan = 0f;
        }

        private void HandleTransition(string oldZone, string newZone)
        {
            ContractKillCreditRuntime.Reset();
            _latestEnemyObservations = new List<ContractEnemyObservation>();
            _nativeEnemyScanReady = false;
            _nextEnemyScan = 0f;
            if (_document == null) return;
            if (ContractCore.HandleZoneTransition(_document, oldZone, newZone) > 0)
            {
                MarkDirty();
                RebuildOffers();
            }
        }

        private void DrainTemplateApi()
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
            if (catalogChanged) RebuildOffers();
        }

        private void DrainProgressApi()
        {
            if (_document == null || _scopeTransitionBlocked) return;
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
        }

        private static void DiscardProgressApi()
        {
            ContractProgressReport discarded;
            while (ContractBoardApi.TryDequeueProgress(out discarded)) { }
        }

        private void BlockForCharacterScopeSave()
        {
            _scopeTransitionBlocked = true;
            _currentLocalOffers = new List<ContractOffer>();
            _currentGlobalOffers = new List<ContractOffer>();
            if (_open)
            {
                try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
                _open = false;
                try { RestoreCursor(); } catch { }
            }
            SetClaimStatus("Contracts paused while the previous character sidecar retries a failed save.");
        }

        private void BlockForAmbiguousCharacterScope()
        {
            _scopeTransitionBlocked = true;
            _currentLocalOffers = new List<ContractOffer>();
            _currentGlobalOffers = new List<ContractOffer>();
            if (_open)
            {
                try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
                _open = false;
                try { RestoreCursor(); } catch { }
            }
            SetClaimStatus("Contracts paused: active character identity is not authoritative enough to select a unique sidecar yet.");
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
            if (_document == null)
            {
                _currentLocalOffers = new List<ContractOffer>();
                _currentGlobalOffers = new List<ContractOffer>();
                return;
            }

            string profile = _profileKey == null ? "local" : _profileKey.Value;
            ContractCore.EnsureLocalBoardZone(_document, _currentZone);

            bool generatedChanged = false;
            if (_killHooksReady && _nativeEnemyScanReady)
            {
                int playerLevel = CurrentPlayerLevel();
                if (string.Equals(_document.LocalBoardZone, _currentZone, StringComparison.OrdinalIgnoreCase))
                {
                    generatedChanged |= ContractCombatPolicy.EnsureLocalCombatBoard(
                        _document, _document.LocalBoardRevision, _document.LocalBoardZone,
                        profile, playerLevel, ControlDailySlots, _latestEnemyObservations);
                }
                generatedChanged |= ContractCombatPolicy.EnsureGlobalCombatBoard(
                    _document, _document.GlobalBoardRevision, _currentZone,
                    profile, playerLevel, ControlGlobalSlots);
            }

            List<ContractTemplate> available = new List<ContractTemplate>(_templates);
            if (_killHooksReady)
                available.AddRange(ContractCombatPolicy.BuildGeneratedTemplates(_document));

            _currentLocalOffers = ContractCore.BuildOffers(
                ContractCategory.Local, _document.LocalBoardRevision, _document.LocalBoardZone,
                profile, available, _document, ControlDailySlots);
            _currentGlobalOffers = ContractCore.BuildOffers(
                ContractCategory.Global, _document.GlobalBoardRevision, _currentZone,
                profile, available, _document, ControlGlobalSlots);

            if (generatedChanged) MarkDirty();
        }

        private void ScanNativeEnemies()
        {
            if (_document == null || !_killHooksReady || !IsUsableScene(_currentZone)) return;
            List<ContractEnemyObservation> scan = ContractNativeEnemyRuntime.Scan(_currentZone);
            _latestEnemyObservations = scan ?? new List<ContractEnemyObservation>();
            _nativeEnemyScanReady = true;
            int merged = ContractCombatPolicy.MergeObservations(_document, _latestEnemyObservations, _document.ActivePlaySeconds);
            if (merged > 0) MarkDirty();
            RebuildOffers();
        }

        private static int CurrentPlayerLevel()
        {
            try
            {
                if (GameData.PlayerControl == null || GameData.PlayerControl.Myself == null ||
                    GameData.PlayerControl.Myself.MyStats == null) return 0;
                return Math.Max(0, GameData.PlayerControl.Myself.MyStats.Level);
            }
            catch { return 0; }
        }

        internal void NoteQualifyingNativeKill(string zone, string enemyName)
        {
            if (_document == null || _scopeTransitionBlocked || !_killHooksReady) return;
            if (!string.Equals(CurrentZoneName(), zone ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return;
            int changed = ContractCombatPolicy.RecordQualifyingKill(_document, zone, enemyName);
            if (changed <= 0) return;
            MarkDirty();
            RebuildOffers();
        }

        internal static string CurrentZoneForRuntime()
        {
            return CurrentZoneName();
        }

        private void AcceptOffer(string occurrenceId)
        {
            ContractOffer offer = FindOffer(occurrenceId);
            if (offer == null) return;
            if (_document != null && offer.Active == null && _document.Active.Count >= ContractCore.MaxActiveContracts)
            {
                SetClaimStatus("Active contract limit reached (" + ContractCore.MaxActiveContracts.ToString() + "). Finish or abandon an unstarted contract before accepting another.");
                return;
            }
            string origin = _currentZone;
            if (string.Equals(ContractCategory.Normalize(offer.Template.Category), ContractCategory.Local, StringComparison.Ordinal))
            {
                origin = _document == null ? string.Empty : _document.LocalBoardZone;
                if (!string.Equals(_currentZone, origin, StringComparison.OrdinalIgnoreCase))
                {
                    SetClaimStatus("This Local board belongs to " + ContractCore.Clean(origin, 60) + " until its next refresh. Return there to accept new Local work.");
                    return;
                }
            }
            if (ContractCore.Accept(_document, offer, origin, DateTime.UtcNow) != null)
            {
                MarkDirty();
                RebuildOffers();
            }
        }

        private ContractOffer FindOffer(string occurrenceId)
        {
            for (int i = 0; i < _currentLocalOffers.Count; i++)
                if (_currentLocalOffers[i] != null && string.Equals(_currentLocalOffers[i].OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase)) return _currentLocalOffers[i];
            for (int i = 0; i < _currentGlobalOffers.Count; i++)
                if (_currentGlobalOffers[i] != null && string.Equals(_currentGlobalOffers[i].OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase)) return _currentGlobalOffers[i];
            return null;
        }

        private void Abandon(string occurrenceId)
        {
            if (!ContractCore.Abandon(_document, occurrenceId)) return;
            MarkDirty();
            RebuildOffers();
        }

        private void Claim(string occurrenceId)
        {
            ContractInstance candidate = ContractCore.FindClaimable(_document, occurrenceId);
            if (candidate == null) return;
            if (ContractCore.HasUnknownRewardOutcome(candidate))
            {
                SetClaimStatus("Reward outcome is locked because a prior native component may already have applied. This prevents a duplicate grant.");
                return;
            }

            if (ContractCore.IsRewardComponentRequired(candidate, RewardComponentKind.Item))
            {
                SetClaimStatus("Claim not completed: native item reward policy is not verified in this build. No new reward component was attempted.");
                return;
            }

            // Preflight every unapplied native component before the first irreversible write. In
            // particular, RaidActive would redirect AddExperience to raid XP, so Gold must wait too.
            ContractRewardGrantPlan plan;
            string preparationReason;
            if (!ContractNativeRewardAdapter.TryPrepare(candidate, ControlNativeXpRewardsEnabled, out plan, out preparationReason))
            {
                SetClaimStatus("Claim not completed: " + preparationReason + ". No native reward was attempted.");
                return;
            }

            if (ContractCore.IsRewardComponentRequired(candidate, RewardComponentKind.Gold) &&
                candidate.GoldRewardStatus != RewardComponentStatus.Applied)
            {
                if (!ApplyGoldComponent(occurrenceId, plan)) return;
            }

            if (ContractCore.IsRewardComponentRequired(candidate, RewardComponentKind.Xp) &&
                candidate.XpRewardStatus != RewardComponentStatus.Applied)
            {
                if (!ContractCore.PrepareRewardComponent(_document, occurrenceId, RewardComponentKind.Xp, plan.XpAmount))
                {
                    SetClaimStatus("Claim not completed: XP component could not enter the prepared state.");
                    return;
                }
                MarkDirty();
                if (!SaveNow())
                {
                    ContractCore.MarkRewardComponentRetryable(_document, occurrenceId, RewardComponentKind.Xp);
                    MarkDirty();
                    SetClaimStatus("Claim not completed: the safe pre-grant ledger could not be persisted, so XP was not attempted.");
                    RebuildOffers();
                    return;
                }

                if (!ContractCore.MarkRewardComponentApplying(_document, occurrenceId, RewardComponentKind.Xp))
                {
                    SetClaimStatus("Claim not completed: XP component could not enter the applying state.");
                    return;
                }
                MarkDirty();
                if (!SaveNow())
                {
                    // No native call has happened yet. In memory this is retryable; the last valid
                    // disk state remains Prepared, which is also a safe-retry state on reload.
                    ContractCore.MarkRewardComponentRetryable(_document, occurrenceId, RewardComponentKind.Xp);
                    MarkDirty();
                    SetClaimStatus("Claim not completed: the pre-invocation ledger could not be persisted, so XP was not attempted.");
                    RebuildOffers();
                    return;
                }

                string outcome;
                bool invocationAttempted;
                if (!ContractNativeRewardAdapter.TryGrantXp(plan, out outcome, out invocationAttempted))
                {
                    if (!invocationAttempted)
                    {
                        ContractCore.MarkRewardComponentRetryable(_document, occurrenceId, RewardComponentKind.Xp);
                        MarkDirty();
                        SaveNow();
                        SetClaimStatus("Claim not completed: " + outcome + ". Safe to retry later.");
                    }
                    else
                    {
                        ContractCore.MarkRewardComponentUnknown(_document, occurrenceId, RewardComponentKind.Xp);
                        MarkDirty();
                        SaveNow();
                        SetClaimStatus("Native XP outcome is unknown; this component is locked to prevent duplicate XP. " + outcome);
                    }
                    RebuildOffers();
                    return;
                }

                if (!ContractCore.MarkRewardComponentApplied(_document, occurrenceId, RewardComponentKind.Xp, plan.XpAmount, "+" + plan.XpAmount.ToString() + " XP"))
                {
                    ContractCore.MarkRewardComponentUnknown(_document, occurrenceId, RewardComponentKind.Xp);
                    MarkDirty();
                    SaveNow();
                    SetClaimStatus("Native XP returned successfully, but the component ledger could not mark it applied. Retry is locked to prevent duplication.");
                    RebuildOffers();
                    return;
                }

                // Persist the irreversible component immediately. If this save fails, keep the
                // active contract with Applied in memory and never invoke XP again this session.
                MarkDirty();
                if (!SaveNow())
                {
                    SetClaimStatus("XP was applied, but its applied ledger could not be saved. Contracts will keep retrying the sidecar save; do not exit until it succeeds.");
                    RebuildOffers();
                    return;
                }
            }

            // An Applied component loaded from disk is already safe to finalize without invoking
            // native reward code again. If it only exists in memory after a prior save failure,
            // force a save before removing the active claim record.
            if (_dirty && !SaveNow())
            {
                SetClaimStatus("Claim cannot finalize until the applied reward ledger saves successfully.");
                return;
            }

            ContractInstance completed;
            if (!ContractCore.IsRewardComponentRequired(candidate, RewardComponentKind.Xp) &&
                !ContractCore.IsRewardComponentRequired(candidate, RewardComponentKind.Gold) &&
                !ContractCore.IsRewardComponentRequired(candidate, RewardComponentKind.Item))
                completed = ContractCore.ClaimRecordOnly(_document, occurrenceId);
            else
                completed = ContractCore.CommitClaim(_document, occurrenceId);

            if (completed == null)
            {
                SetClaimStatus("Claim could not finalize because not every configured reward component is safely applied.");
                return;
            }

            if (string.Equals(ContractCategory.Normalize(completed.Category), ContractCategory.Local, StringComparison.Ordinal))
                ContractCore.RecordSuccessfulLocalCompletion(_document);

            string rewardSummary = ContractCore.AppliedRewardSummary(completed);
            if (string.IsNullOrWhiteSpace(rewardSummary)) rewardSummary = "No native reward";

            string journalText = ContractCore.BuildJournalEntry(completed);
            if (!string.IsNullOrWhiteSpace(journalText))
                EnqueuePendingJournal(_characterKey, completed.OccurrenceId, journalText);

            MarkDirty();
            RebuildOffers();
            if (!SaveNow())
            {
                // The last persisted state still contains the active contract with any irreversible
                // components marked Applied. Reload can therefore finalize without regranting. Keep
                // the Journal text only in memory and deliver it after this exact character's claim
                // state becomes durable; never enqueue it into another character's Journal scope.
                SetClaimStatus("Claim finalized in memory, but local persistence failed. Journal history is waiting for a successful save; the saved component ledger still prevents duplicate rewards.");
                return;
            }

            TryFlushPendingJournal();
            SetClaimStatus("Claimed " + completed.Title + ". " + rewardSummary + ".");
        }

        // Gold is ledgered independently from XP. If the process stops after Gold is applied,
        // the persisted Applied state prevents another Gold write and a later Claim only finishes XP.
        private bool ApplyGoldComponent(string occurrenceId, ContractRewardGrantPlan plan)
        {
            if (!ContractCore.PrepareRewardComponent(_document, occurrenceId, RewardComponentKind.Gold, plan.GoldAmount))
            { SetClaimStatus("Claim not completed: Gold component could not enter the prepared state."); return false; }
            MarkDirty();
            if (!SaveNow())
            {
                ContractCore.MarkRewardComponentRetryable(_document, occurrenceId, RewardComponentKind.Gold);
                MarkDirty(); SetClaimStatus("Claim not completed: the safe pre-grant ledger could not be persisted, so Gold was not attempted."); RebuildOffers(); return false;
            }
            if (!ContractCore.MarkRewardComponentApplying(_document, occurrenceId, RewardComponentKind.Gold))
            { SetClaimStatus("Claim not completed: Gold component could not enter the applying state."); return false; }
            MarkDirty();
            if (!SaveNow())
            {
                ContractCore.MarkRewardComponentRetryable(_document, occurrenceId, RewardComponentKind.Gold);
                MarkDirty(); SetClaimStatus("Claim not completed: the pre-invocation ledger could not be persisted, so Gold was not attempted."); RebuildOffers(); return false;
            }
            string outcome; bool invocationAttempted;
            if (!ContractNativeRewardAdapter.TryGrantGold(plan, out outcome, out invocationAttempted))
            {
                if (!invocationAttempted)
                {
                    ContractCore.MarkRewardComponentRetryable(_document, occurrenceId, RewardComponentKind.Gold);
                    MarkDirty(); SaveNow(); SetClaimStatus("Claim not completed: " + outcome + ". Safe to retry later.");
                }
                else
                {
                    ContractCore.MarkRewardComponentUnknown(_document, occurrenceId, RewardComponentKind.Gold);
                    MarkDirty(); SaveNow(); SetClaimStatus("Native Gold outcome is unknown; this component is locked to prevent duplicate Gold. " + outcome);
                }
                RebuildOffers(); return false;
            }
            if (!ContractCore.MarkRewardComponentApplied(_document, occurrenceId, RewardComponentKind.Gold, plan.GoldAmount, "+" + plan.GoldAmount.ToString() + " Gold"))
            {
                ContractCore.MarkRewardComponentUnknown(_document, occurrenceId, RewardComponentKind.Gold);
                MarkDirty(); SaveNow(); SetClaimStatus("Native Gold returned successfully, but the component ledger could not mark it applied. Retry is locked to prevent duplication."); RebuildOffers(); return false;
            }
            MarkDirty();
            if (!SaveNow()) { SetClaimStatus("Gold was applied, but its applied ledger could not be saved. Contracts will keep retrying the sidecar save; do not exit until it succeeds."); RebuildOffers(); return false; }
            return true;
        }


        private void EnqueuePendingJournal(string characterKey, string occurrenceId, string text)
        {
            if (_pendingJournalDeliveries.Enqueue(characterKey, occurrenceId, text))
                Logging.LogWarning("Erenshor Contracts Journal retry queue reached its in-memory limit; the oldest undelivered history entry was dropped.");
        }

        private void TryFlushPendingJournal()
        {
            if (_pendingJournalDeliveries.Count == 0) return;
            if (_dirty || _scopeTransitionBlocked || _document == null || _store == null) return;
            if (Time.unscaledTime < _nextJournalRetry) return;

            // Journal API v1 scopes an entry using Journal's currently active character. Re-resolve
            // the live Contracts identity before enqueueing so a delayed save from character A can
            // never write A's history into character B after a slot switch. Entries for another
            // character remain queued until that exact character is authoritative again.
            string liveKey = ResolveCharacterKey();
            if (string.IsNullOrWhiteSpace(liveKey) || !string.Equals(_characterKey, liveKey, StringComparison.Ordinal)) return;

            ContractJournalDelivery delivery;
            if (!_pendingJournalDeliveries.TryPeekForCharacter(liveKey, out delivery) || delivery == null) return;

            if (JournalIntegration.TryAppend(delivery.Text))
            {
                _pendingJournalDeliveries.Remove(delivery.CharacterKey, delivery.OccurrenceId);
                _nextJournalRetry = 0f;
            }
            else _nextJournalRetry = Time.unscaledTime + 5f;
        }

        internal bool ControlPanelOpen { get { return _open; } }
        internal double ControlPanelActivatedAt { get { return _panelActivatedAt; } }
        internal string ControlCharacterKey { get { return _characterKey ?? string.Empty; } }
        internal string ControlZone { get { return _currentZone ?? string.Empty; } }
        internal ContractDocument ControlDocument { get { return _document; } }
        internal List<ContractOffer> ControlOffers
        {
            get
            {
                List<ContractOffer> rows = new List<ContractOffer>(_currentLocalOffers);
                rows.AddRange(_currentGlobalOffers);
                return rows;
            }
        }
        internal int ControlDailySlots { get { return _dailySlots == null ? 3 : Math.Max(1, Math.Min(6, _dailySlots.Value)); } }
        internal int ControlGlobalSlots { get { return _globalSlots == null ? 2 : Math.Max(1, Math.Min(3, _globalSlots.Value)); } }
        internal int ControlPatrolMinutes { get { return _patrolMinutes == null ? 15 : Math.Max(5, Math.Min(60, _patrolMinutes.Value)); } }
        internal int ControlGlobalPatrolMinutes { get { return _globalPatrolMinutes == null ? 60 : Math.Max(30, Math.Min(120, _globalPatrolMinutes.Value)); } }
        internal int ControlLocalRefreshMinutes { get { return _localRefreshMinutes == null ? 45 : Math.Max(15, Math.Min(240, _localRefreshMinutes.Value)); } }
        internal int ControlGlobalRefreshMinutes { get { return _globalRefreshMinutes == null ? 120 : Math.Max(60, Math.Min(480, _globalRefreshMinutes.Value)); } }
        internal bool ControlNativeXpRewardsEnabled { get { return _enableNativeXpRewards != null && _enableNativeXpRewards.Value; } }
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
            if (_open) { MarkPanelActivated(); return; }
            _open = true;
            MarkPanelActivated();
            _cursorVisibleBeforeOpen = Cursor.visible;
            _cursorLockBeforeOpen = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void CloseBoard()
        {
            if (!_open) return;
            SuiteDragHandler.ForceReleaseIfOwned();
            _open = false;
            SaveNow();
            RestoreCursor();
        }

        private void MarkPanelActivated()
        {
            _panelActivatedAt = Time.realtimeSinceStartup;
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

        private void MarkProgressDirty()
        {
            if (!_dirty) _saveAfter = Time.unscaledTime + 15f;
            _dirty = true;
        }

        private bool SaveNow()
        {
            if (_store == null || _document == null) return false;
            if (!_dirty && File.Exists(_store.PathOnDisk)) return true;
            try
            {
                _store.Save(_document);
                _dirty = false;
                return true;
            }
            catch (Exception ex)
            {
                _dirty = true;
                _saveAfter = Time.unscaledTime + 5f;
                Logging.LogError("Erenshor Contracts could not save local state: " +
                                ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private void SetClaimStatus(string value)
        {
            _claimStatus = ContractCore.Clean(value, 220);
            _claimStatusUntil = Time.unscaledTime + 12f;
        }

        private string CurrentClaimStatus()
        {
            if (string.IsNullOrWhiteSpace(_claimStatus)) return string.Empty;
            if (Time.unscaledTime > _claimStatusUntil) { _claimStatus = string.Empty; return string.Empty; }
            return _claimStatus;
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
            return ContractCore.IsProgressZone(scene);
        }

        private static string CurrentZoneName()
        {
            try
            {
                string logical = GameData.SceneName;
                if (!string.IsNullOrWhiteSpace(logical)) return logical.Trim();
            }
            catch { }
            try { return SceneManager.GetActiveScene().name ?? string.Empty; }
            catch { return string.Empty; }
        }

    }
}
