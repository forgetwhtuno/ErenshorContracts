# Contracts 0.4.2 reward-config findings

## Why did source default `true` load as `false`?

`ContractsSettings.EnableNativeXpRewards` is correctly initialized to `true` in source. Lunaris then calls `Config.Register(ref _settings)`, which loads and applies existing serialized setting values by field key. The current live file contains the key `ContractsSettings.EnableNativeXpRewards` in:

`forgetwhtuno.erenshor.contracts.lpcfg`

Its final Boolean record is `03 00`: the type tag for Boolean followed by `false`. The current `lunaris.log` therefore correctly reported `Gold rewards enabled; XP disabled by config.` The backup log identifies the installed predecessor as Contracts 0.4.0, whose release policy deliberately kept XP default-off pending verification. A source initializer does not override an existing Lunaris key.

## Migration

0.4.2 adds `ContractsSettings.RewardConfigVersion` and `ContractRewardConfigMigrationPolicy`.

- If `RewardConfigVersion < 1`, Contracts sets `EnableNativeXpRewards=true`, writes schema `1`, and immediately saves the Lunaris config.
- A stored 0.4.0 `false` is therefore recognized as the former compatibility default and migrated exactly once.
- On later starts, schema `1` prevents any forced change. If the player subsequently sets XP rewards to `false`, that explicit choice remains false.
- If the migration save fails, XP is enabled only for the current session and the migration retries at next startup; no reward mutation is attempted by the migration itself.

The control diagnostic is available to suite consumers as `ContractsControlApi.GetRewardDiagnostics()`. It reports `xpConfigValue`, `xpConfigSource`, `rewardSchema`, `raidActive`, XP/Gold API availability, claim eligibility, and last claim result without exposing local paths.

## Claim path

Contracts now uses the installed-assembly-proven, PvP-aligned operations:

```csharp
GameData.AddExperience(xp, false);
GameData.PlayerInv.Gold += gold;
GameData.PlayerInv.UpdatePlayerInventory();
```

The preflight runs before Gold or XP mutation. It rejects the whole claim during `GameData.RaidActive` with `Finish or leave the raid before claiming this contract`, preserving `CompletedUnclaimed`. The existing per-component durable ledger remains authoritative: Prepared and Applying are saved before invocation, Applied is saved after success, restart from Applying becomes fail-closed unknown, and repeated claims cannot regrant an Applied component.

## Current evidence and remaining blocker

The live log proves the old persisted false configuration and current 0.4.1 startup result. It does not yet prove a 0.4.2 in-game claim. The exact live acceptance is in `CONTRACTS_042_LIVE_TEST.md`.
