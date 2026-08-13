# Erenshor Contracts 0.1.0 Preview

A small standalone **local contract board** for Erenshor.

The goal is old-school MMO activity: log in, look at what the local board has today, take something that sounds fun, and ignore the rest. No streaks, no login punishment, no mandatory checklist.

This first Preview deliberately builds the **board, daily rotation, progress/persistence core, UI, and companion-mod API** before touching native reward or kill/item hooks.

## What works now

- retained-uGUI `CONTRACTS` launcher with Suite-style drag/fallback visibility; **no global hotkey**;
- retained-uGUI Contract Board with visible close/reset controls, Suite-style drag, retained resize grip, and a scrollable contract list;
- deterministic per-day, per-scene contract rotation;
- accept / abandon / claim flow;
- persisted active contracts and completion history;
- three patch-light fallback contracts that need only Unity scene lifecycle:
  - **Local Patrol** — spend a configured amount of active time in the current scene;
  - **Road Check** — leave the scene and return;
  - **Wayfarer** — visit two other scenes;
- reflection-friendly provider API for other mods to register real activity contracts;
- provider progress reports are fact-only: the companion mod must verify the event;
- optional Journal integration: claiming a contract appends one Chronicle entry if Erenshor Journal is installed;
- local sidecar storage under `plugins/config/ErenshorContracts/`.

## Important Preview boundary

**0.1.0 does not grant native XP, gold, items, faction, quest credit, or crafting materials.**

That is intentional.

The Erenshor reference work shows that inventory/crafting/reward state is a save-sensitive boundary, and the game has had recent item-duplication/save fixes. I do not want a daily-contract prototype inventing a direct reward path before the current installed assemblies prove a safe native operation.

Likewise, 0.1.0 does not guess a generic enemy-death or inventory hook. Kill, gathering, crafting, fishing, duel, expedition, and future activity contracts should be fed by a provider that already knows what actually happened.

## Companion-mod API

The public surface is:

```csharp
ContractBoardApi.RegisterTemplate(
    providerId,
    templateId,
    zoneScope,
    title,
    description,
    progressChannel,
    progressKey,
    contextFilter,
    target,
    priority,
    rewardText);

ContractBoardApi.ReportProgress(
    channel,
    key,
    amount,
    context);
```

Callers that want to remain standalone should resolve this through reflection rather than reference `ErenshorContracts.dll`.

Example future Crafting integration:

```text
register:
  providerId      = crafting
  templateId      = local_food_order
  zoneScope       = <verified scene or *>
  progressChannel = crafting
  progressKey     = food
  target          = 4

after Crafting itself verifies a successful food craft:
  ReportProgress("crafting", "food", 1, craftedItemName)
```

The board does **not** parse chat or an LLM response and decide that a craft happened.

## Why this is separate from Crafting

Crafting Orders belong in the Crafting mod because Crafting knows its own recipes, item results, gathering nodes, and safe reward flow.

Contracts is the broader local-adventure board:

- hunt / bounty tasks;
- exploration;
- dungeon/local patrol activities;
- future duel/PvP activities;
- future guild activities;
- provider-backed gathering/crafting tasks when that makes sense.

That lets players use Contracts without installing Crafting, and Crafting without installing Contracts.

## UI philosophy

This follows the same general visual language already used by the `forgetwhtuno` Party Tools / Follow / PvP projects:

- dark translucent panel;
- cyan/teal frame;
- compact retained-uGUI launcher with Suite-style drag;
- no required F-key;
- dedicated scrollable Contract Board with configured size;
- normalized, saved/clamped panel positions.

## Maintenance philosophy

The contract/persistence/provider core remains deliberately narrow and does not mutate game inventory, NPC combat, quest state, native rewards, or Erenshor saves. The current native-Lunaris shell does reference `Assembly-CSharp.dll` only for bounded gameplay-readiness/character context; the retained-uGUI migration itself uses no Harmony patching. Native rewards remain outside this Preview until a verified adapter exists.

## Build / install

This version requires **native Lunaris** — BepInEx is no longer required. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

The script locates the current Erenshor install and the Lunaris developer reference, compiles, and installs only `ErenshorContracts.dll` to `<Erenshor>\plugins\`. Lunaris manages enable/disable and config; local contract state moves to `plugins\config\ErenshorContracts\`. A legacy BepInEx release remains available in this repository's Git history.

**Status:** the pre-uGUI native baseline compiled and passed its deterministic tests. The retained-uGUI candidate in this handoff is source-verified but could not be recompiled here because the handoff omitted native Erenshor/Lunaris reference DLLs. Live enable/disable/reload verification is still required.

## Testing

```powershell
powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1
```

The deterministic core suite covers:

- stable daily offerings;
- provider priority;
- accept / progress / claim;
- leave-and-return state;
- unique-zone progress;
- context-filtered provider progress.

See `TESTING.md` for in-game acceptance checks.

## Credits / related projects

- **Erenshor Journal** — optional Chronicle sink through reflection.
- **Erenshor Practice Duels**, **Erenshor PvP**, **Deep Sims**, **Erenshor Follow**, **Campmaster**, **Party Tools**, and the in-development Crafting expansion are natural future providers/consumers, but none are required by this Preview.

## Development note

This project has been developed heavily with AI-assisted coding tools. The goal is to build features I wanted to use in Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.


## Optional Suite Hub integration

Erenshor Suite Hub is **optional**. When it is installed, this mod can expose its normal player-facing controls there through the versioned public `ContractsControlApi` surface. The mod remains independently usable without Suite Hub and does not compile against Hub types or assume Hub load order.

The Contract Board remains the dedicated interface for accepting, abandoning, and claiming contracts. A compact standalone launcher is a fallback and is hidden by default while Suite Hub is loaded.

Hub can show available/active/progress summaries and open or close the Contract Board.

The shared control/API and fully-in-world UI policy in this handoff are source-validated but **not yet live-tested under Lunaris hot reload**.

### Content/UI migration candidate

The current source moves the Contract Board and launcher to retained Unity uGUI and removes the obsolete UI-only world-click/camera Harmony interception. Contract actions still route through the existing authoritative contract services; the Hub receives only bounded status/settings and Open/Close/Reset actions. The completed duplicate-instance investigation no longer emits per-tick diagnostics. Native compile and live Lunaris UI/reload verification remain required for this candidate.
