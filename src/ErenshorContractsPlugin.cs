using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lunaris;
using Lunaris.Config;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorContracts
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "Local/daily contract board with a provider API for other mods to register verified objectives.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorContractsPlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.contracts";
        internal const string PluginName = "Erenshor Contracts";
        internal const string PluginVersion = "0.1.1";

        internal static ErenshorContractsPlugin Instance;
        private Harmony _harmony;

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

        // Character-scoped data. No contract data is loaded until a real, spawned character is
        // confirmed present (see IsLocalCharacterReady/EnsureCharacter) so nothing is ever read
        // from or written to disk while sitting at the title/login/character-select screens.
        private string _dataRoot;
        private string _legacyDataPath;
        private string _legacyClaimMarkerPath;
        private string _characterKey;

        // Toggle/close requests observed in OnGUI are applied in Update, never mid-OnGUI. IMGUI
        // dispatches several event passes (Layout, input, Repaint) per rendered frame; flipping
        // _open in the middle of that sequence desyncs GUI.Window's Layout/Repaint bookkeeping,
        // throws, and is swallowed by OnGUI's own try/catch below -- which then force-closes the
        // board it just opened. Deferring the mutation to Update keeps _open constant for the
        // whole of every OnGUI pass in a given frame. See DiagLogEnabled for how this was traced.
        private bool _pendingToggle;
        private bool _pendingClose;

        // Flip to true only while diagnosing the launcher/board toggle chain; keep false for
        // release builds so this stays quiet in the log.
        private const bool DiagLogEnabled = false;

        // TEMPORARY diagnostic instrumentation requested by the user to investigate whether the
        // Lunaris host invokes Awake twice per native [LunarisPlugin] mod and, if so, whether the
        // second Awake produces a genuinely live, ticking second instance (two GUI.Window calls
        // sharing one WindowId per frame) or a quickly-orphaned one. Left active by default
        // (unlike DiagLogEnabled above) because it is needed for the next live test. Remove once
        // the root cause is confirmed. See [ContractsInstanceDiag] log lines.
        private static float _nextInstanceDiagTick;

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
            _windowRect = ResolveInitialWindowRect();
            _launcherRect = ResolveInitialLauncherRect();

            RegisterOrReplace(ContractCore.BuildPatrolTemplate(_patrolMinutes.Value));
            RegisterOrReplace(ContractCore.BuildRoadCheckTemplate());
            RegisterOrReplace(ContractCore.BuildWayfarerTemplate());

            _currentZone = CurrentSceneName();
            SceneManager.sceneLoaded += OnSceneLoaded;
            RebuildOffers();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logging.LogInfo(
                "Erenshor Contracts " + PluginVersion +
                " loaded. Use the draggable CONTRACTS UI button. No global hotkey is registered. " +
                "This Preview tracks local contracts but deliberately does not grant native XP, gold, or items.");

            // TEMPORARY: see _nextInstanceDiagTick comment above. Proves whether Awake runs twice
            // and, via the periodic Update tick log, whether a second instance stays alive.
            Logging.LogInfo("[ContractsInstanceDiag] Awake instance=" +
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this) +
                " harmonyPatches=" + _harmony.GetPatchedMethods().Count());
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
                // TEMPORARY: see _nextInstanceDiagTick comment near the top of this class. Throttled
                // so it doesn't spam every frame; if two live instances exist, both distinct hash
                // codes will show up across successive ticks in the log.
                if (Time.unscaledTime >= _nextInstanceDiagTick)
                {
                    _nextInstanceDiagTick = Time.unscaledTime + 5f;
                    Logging.LogInfo("[ContractsInstanceDiag] Update tick instance=" +
                        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this));
                }

                DrainApi();

                // Apply any toggle/close requested during last frame's OnGUI passes now, before
                // this frame's OnGUI runs, so _open is stable for every event pass in the frame.
                if (_pendingClose)
                {
                    _pendingClose = false;
                    if (_open) CloseBoard();
                }
                if (_pendingToggle)
                {
                    _pendingToggle = false;
                    if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts: toggle consumed, _open before=" + _open);
                    ToggleBoard();
                    if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts: toggle consumed, _open after=" + _open);
                }

                bool ready = IsLocalCharacterReady();
                if (ready)
                {
                    EnsureCharacter();
                }
                else if (_open)
                {
                    CloseBoard();
                }

                if (_dirty && Time.unscaledTime >= _saveAfter) SaveNow();
                if (_launcherDirty && Time.unscaledTime >= _launcherSaveAfter) PersistLauncherRect();

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
                // UI visibility is gated on a verified player-ready signal, not a scene-name
                // guess. IsUsableScene is still used above (Update) for legitimate zone-scoped
                // gameplay bookkeeping only -- it is not a substitute for this check.
                if (!IsLocalCharacterReady())
                {
                    if (_open) CloseBoard();
                    return;
                }

                if (_open && _window != null && _document != null)
                {
                    if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts: window Draw() entry, _open=" + _open);
                    _windowRect = ClampWindowRect(_window.Draw(
                        _windowRect,
                        _currentZone,
                        _currentOffers,
                        _document,
                        AcceptOffer,
                        Abandon,
                        Claim));
                    if (_window.RequestClose)
                    {
                        if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts: board requested close (deferred to Update)");
                        _pendingClose = true;
                    }
                }

                if (_launcher != null)
                {
                    Rect previous = _launcherRect;
                    _launcherRect = ClampLauncherRect(_launcher.Draw(_launcherRect, _open));
                    if (!RectsNearlyEqual(previous, _launcherRect)) MarkLauncherDirty();
                    if (_launcher.RequestToggle)
                    {
                        if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts: launcher click detected, _open=" + _open);
                        _pendingToggle = true;
                        if (DiagLogEnabled) Logging.LogInfo("Erenshor Contracts: toggle queued (deferred to Update)");
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.LogError("Erenshor Contracts UI failed: " + ex);
                if (_open) CloseBoard();
            }
        }

        // True while the pointer (already converted to GUI screen-space by the caller) is over
        // the contract board window or its launcher button. The click-passthrough Harmony
        // patches below use this so a click on the panel cannot also drop the player's world
        // target or spin the camera.
        internal bool PointerIsOverUi(Vector2 guiPoint)
        {
            if (_open && _windowRect.Contains(guiPoint)) return true;
            if (_launcherRect.Contains(guiPoint)) return true;
            return false;
        }

        // Verified player-ready signal (not scene-name matching). Same pattern already live-
        // tested in the sibling Erenshor-Nemesis mod's NemesisDirector.Ready(). Recomputed every
        // frame cheaply; never cached across scene loads.
        private static bool IsLocalCharacterReady()
        {
            try
            {
                return !GameData.InCharSelect && GameData.PlayerControl != null && GameData.PlayerControl.Myself != null &&
                    GameData.PlayerControl.Myself.MyStats != null && GameData.PlayerControl.Myself.gameObject.activeInHierarchy;
            }
            catch { return false; }
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
            // TEMPORARY: see _nextInstanceDiagTick comment near the top of this class.
            Logging.LogInfo("[ContractsInstanceDiag] OnDestroy instance=" +
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this));

            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
            try { ContractsCameraLookPatch.Restore(); } catch { }
            try { SaveNow(); } catch { }
            try { PersistWindowRect(); } catch { }
            try { PersistLauncherRect(); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }

            _window = null;
            _launcher = null;
            _document = null;
            _store = null;
            _characterKey = null;
            if (Instance == this) Instance = null;
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

    // IMGUI doesn't own the raw click Erenshor reads here, so a click on the Contracts window or
    // its launcher would otherwise also affect the world (deselect target, move camera).
    [HarmonyPatch(typeof(PlayerControl), "LeftClick")]
    internal static class ContractsPanelLeftClickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            try
            {
                if (ErenshorContractsPlugin.Instance == null) return true;
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                return !ErenshorContractsPlugin.Instance.PointerIsOverUi(mouse);
            }
            catch { return true; }
        }
    }

    [HarmonyPatch(typeof(csMouseOrbit), "LateUpdate")]
    internal static class ContractsCameraLookPatch
    {
        private static csMouseOrbit _muted;
        private static float _mutedX;
        private static float _mutedY;

        internal static void Restore()
        {
            csMouseOrbit orbit = _muted;
            _muted = null;
            if (orbit == null) return;
            try { orbit.xSpeed = _mutedX; orbit.ySpeed = _mutedY; } catch { }
        }

        [HarmonyPrefix]
        private static void Prefix(csMouseOrbit __instance)
        {
            Restore();
            try
            {
                if (__instance == null || ErenshorContractsPlugin.Instance == null) return;
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!ErenshorContractsPlugin.Instance.PointerIsOverUi(mouse)) return;
                _mutedX = __instance.xSpeed;
                _mutedY = __instance.ySpeed;
                __instance.xSpeed = 0f;
                __instance.ySpeed = 0f;
                _muted = __instance;
            }
            catch { }
        }

        [HarmonyPostfix]
        private static void Postfix() { Restore(); }
    }
}
