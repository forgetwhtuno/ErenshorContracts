# AGENTS.md — Erenshor Contracts

Instructions for AI/coding agents working in this repository. Read this before making changes.

## What this mod is

A standalone local/daily contract board for Erenshor (BepInEx 5 plugin, .NET Framework 4.8, C# 5 effective language level via `csc`). Players accept small local objectives from a draggable board UI; a handful of patch-light fallback contracts (Local Patrol, Road Check, Wayfarer) work out of the box using only Unity scene lifecycle. Everything else is meant to be fed by other mods through the provider API.

## Core design boundary

- **0.1.0 does not grant native XP, gold, items, faction, quest credit, or crafting materials.** That is a deliberate Preview boundary, not an oversight — see `README.md` and `MOD_OVERLAP_NOTES.md` for why.
- The board does not parse chat, guess a kill/inventory event, or ask an LLM whether something happened. A provider mod that already verified an event calls `ContractBoardApi.ReportProgress(...)`; Contracts trusts the caller for provenance, it does not re-derive facts.
- Contracts does not reference `Assembly-CSharp.dll` and does not use Harmony. The only live game-adjacent fact it reads is the active Unity scene name. Keep it that way unless a change is explicitly requested and understood to widen the patch-maintenance surface.

## What Erenshor remains authoritative for

Native inventory, XP, gold, quest state, and combat outcomes. This mod never writes to them.

## Forbidden

- Do not invent Erenshor APIs, fields, or behavior that hasn't been verified against the actual installed game assemblies.
- Do not add a Harmony patch, an `Assembly-CSharp.dll` reference, or a generic kill/inventory scanner without discussing the design boundary first — that is the line the mod is deliberately drawn on.
- Do not commit `bin/`, `obj/`, `refs/`, compiled DLLs, game assemblies, or anything under a live BepInEx/Erenshor install path. `.gitignore` already covers the standard cases; don't work around it.
- No secrets, personal file paths, tokens, or real names in source, docs, or commit messages.
- Do not commit or push changes unrelated to the task at hand.

## Important source files

- `src/ContractCore.cs` — deterministic rotation, accept/abandon/claim lifecycle.
- `src/ContractStore.cs` — local sidecar persistence (`BepInEx/config/ErenshorContracts/`), `.bak`/corrupt recovery.
- `src/ContractModels.cs` — data shapes.
- `src/ContractBoardApi.cs` — the public provider-facing surface (`RegisterTemplate`, `ReportProgress`).
- `src/ContractBoardWindow.cs`, `src/ContractLauncher.cs` — UI.
- `src/JournalIntegration.cs` — optional reflection-based Erenshor Journal Chronicle hook; must stay optional (no hard DLL dependency).
- `src/ErenshorContractsPlugin.cs` — BepInEx plugin entry point.

## Build / test procedure

- Deterministic core tests: `powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1` (compiles `ContractModels.cs` + `ContractCore.cs` + `tests/ContractCoreTests.cs` standalone via `csc`, no game/BepInEx dependency, safe to run anywhere).
- Full plugin build: `powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1` — this locates the current Erenshor/BepInEx install and **installs over the live plugin folder**. Do not run this as a compile check; prefer a manual `csc` build against local reference DLLs with output redirected elsewhere, or run `RUN_TESTS.ps1` for logic changes.
- `LangVersion` in the `.csproj` says 7.3, but the shipped build uses the legacy .NET Framework `csc.exe`, which is effectively C# 5. Avoid string interpolation, `nameof`, null-conditional operators, expression-bodied members, and inline `out` variables in shipped code.
- Compile and run the deterministic tests before claiming a change works.

## Compatibility boundaries

- Optional integration only: Erenshor Journal (Chronicle sink via reflection). Never a hard dependency.
- Crafting Orders belong to the Crafting mod, not here — see "Why this is separate from Crafting" in `README.md`.
- Do not take on responsibilities that belong to Deep Sims, PvP/Duel mods, Guild Life, or Crafting.
