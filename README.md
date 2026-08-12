# Erenshor Contracts 0.1.0 Preview

A small standalone **local contract board** for Erenshor.

The goal is old-school MMO activity: log in, look at what the local board has today, take something that sounds fun, and ignore the rest. No streaks, no login punishment, no mandatory checklist.

This first Preview deliberately builds the **board, daily rotation, progress/persistence core, UI, and companion-mod API** before touching native reward or kill/item hooks.

## What works now

- draggable `CONTRACTS` HUD button; **no global hotkey**;
- draggable/resizable Party Tools / Follow-style window;
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
- compact draggable launcher;
- no required F-key;
- resizable main window;
- saved/clamped panel positions.

## Maintenance philosophy

0.1.0 deliberately does **not** reference `Assembly-CSharp.dll`, Harmony, game inventory, NPC combat, quest state, or save files.

The only live game-adjacent fact it owns is the active Unity scene name.

That should make the board/persistence/provider core unusually resilient to Erenshor patches. Later adapters can be narrow and capability-checked.

## Build / install

This version requires **native Lunaris** — BepInEx is no longer required. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

The script locates the current Erenshor install and the Lunaris developer reference, compiles, and installs only `ErenshorContracts.dll` to `<Erenshor>\plugins\`. Lunaris manages enable/disable and config; local contract state moves to `plugins\config\ErenshorContracts\`. A legacy BepInEx release remains available in this repository's Git history.

**Status:** this native build compiles cleanly against the installed Lunaris/Assembly-CSharp and passes its deterministic test suite. It has not yet been live-tested in-game under Lunaris (enable/disable/reload behavior). Do not assume hot-reload safety until that pass is done.

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
