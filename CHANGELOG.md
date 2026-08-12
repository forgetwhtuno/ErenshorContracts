# Changelog

## Unreleased (native Lunaris migration)

- Converted the plugin host from BepInEx (`BaseUnityPlugin`/`[BepInPlugin]`/`[BepInProcess]`) to
  native Lunaris (`LunarisPlugin`/`[LunarisPlugin]`/`[LunarisPermission(FileAccess | Reflection)]`).
  No Harmony or Network permission requested — this mod patches no game methods and makes no
  network calls.
  There is no chat-command interception in this mod (UI-button-only, no global hotkey), so
  nothing here changes command syntax.
- Config replaced `ConfigEntry<T>`/`Config.Bind` with native typed Lunaris config
  (`ContractsSettings`); all 9 existing settings (section/key/default/description) preserved
  unchanged, plus a loader-neutral `ContractsConfigEntry<T>` shim so call sites kept their
  existing `.Value` access pattern.
- Logging replaced `BepInEx.Logging`/`ManualLogSource` with native Lunaris `Logging`.
- Local sidecar storage moved from `BepInEx/config/ErenshorContracts/` to
  `plugins/config/ErenshorContracts/` (`Paths.ConfigPath` was BepInEx-specific).
- `BUILD_AND_INSTALL.ps1`/`UNINSTALL.ps1` now target `<Erenshor>\plugins` instead of a BepInEx
  profile and no longer require `BepInEx.dll`.

## 0.1.0 - Preview foundation

- Added standalone daily/local contract board.
- Added draggable UI launcher; no global hotkey.
- Added Party Tools / Follow-style draggable and resizable board window.
- Added deterministic daily per-scene offerings.
- Added accept, abandon, completion, and claim lifecycle.
- Added local sidecar persistence with `.bak` / corrupt-file recovery.
- Added built-in patch-light Local Patrol, Road Check, and Wayfarer activities.
- Added reflection-friendly provider template/progress API.
- Added optional Erenshor Journal Chronicle integration.
- Added deterministic core tests.
- Deliberately deferred native XP/gold/item rewards and kill/inventory hooks until verified against the installed game assembly.
