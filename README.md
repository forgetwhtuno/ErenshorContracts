# Erenshor Contracts 0.4.0

A deterministic **Local + Global contract board** designed as optional old-school MMO side progression.

Contracts should be something you check occasionally while adventuring, not a daily-task treadmill. It does not create streaks, login chores, instant rerolls, GPS automation, or generated quests. The board stays stable for active-play windows, accepted work survives zoning/reload, and rewards are intentionally modest relative to normal progression.

The retained board uses the suite's compact dark/translucent/cyan presentation. Its header has a `▾` / `▸` collapse control so the board can shrink to a draggable header without closing; Reset and `X` remain available and expanding restores the existing board content.

## Default board cadence

- **3 Local** offers
- **2 Global** offers
- Local board revision every **~45 active-play minutes**
- Global board revision every **~120 active-play minutes**

A refresh advances the **whole category revision**. Claimed or abandoned slots do not instantly refill. Accepted contracts from an older revision remain active until completed/abandoned and continue to appear under their true Local or Global section.

Each Local revision also has one persisted **board origin zone**. Zoning does not generate a new Local board: the same three slots remain tied to that origin until the 45-minute Local refresh. Travel contracts can take you away, but new Local work can only be accepted while you are back in the board-origin zone. At the next Local refresh, the board rebinds to the playable zone you are currently in. This closes the zone-hop reroll/farming loop while keeping Local objectives genuinely local.

Active-play time advances only while the character is fully in world, the logical zone is playable, the game application is focused, and simulation time is running. Closing the game, sitting at character select/title/loading, alt-tabbing away, or pausing simulation does not advance refresh timers or time objectives.

## Built-in contract design

### Local — combat first

New Local boards are built from **real loaded native enemies in the persisted Local board-origin zone**. Contracts scans ordinary hostile `NPC` actors on a bounded cadence and rejects Sim-backed actors, players/friendly factions, resources/chests, summoned/owned actors, vendors, invulnerable actors, PvP temporary proxies when that optional capability is present, and boss-reward actors. It reads the native enemy display name and native level.

The generated Local board then:

- admits only enemy types whose observed level range is within five levels of the current character;
- prefers more plentiful observed enemy types when level fit is equal;
- uses deterministic **6–9 kill** targets;
- writes the exact destination zone into the contract;
- freezes those selected targets for the whole persisted Local board revision so spawns/deaths do not reroll the visible board.

If the first bounded scan after loading has no qualifying enemy yet, Contracts does **not** freeze an empty generation. It keeps the revision retryable and can populate it once authoritative live enemies appear.

`Local Patrol` remains as one low-priority time objective so a zone with too little verified combat evidence can still offer a small amount of non-combat work. The older Road Check / Perimeter Sweep / Wayfarer / Local Circuit definitions remain in code only for persisted backward compatibility and deterministic migration coverage; the runtime no longer registers them for new built-in boards.

### Global — observed cross-zone combat

The supplied game surface does not give Contracts a proven authoritative unloaded-zone bestiary, so Global work does not use an invented hardcoded mob list. Instead each character sidecar retains a bounded catalog of **actually observed native zone/enemy/level/population** facts.

New Global offers:

- target a named zone other than the player's current zone;
- use only previously observed qualifying native enemy types;
- apply the same level-appropriateness rule;
- prefer more plentiful observed targets when level fit is equal;
- use deterministic **10–14 kill** targets;
- show the exact destination on the card;
- freeze once a verified Global target set exists for that board revision.

A fresh sidecar may initially have no Global offer. That is intentional: explore another zone so Contracts can observe its native enemies. An empty Global revision remains retryable until cross-zone evidence exists; after a target set is generated it remains stable until the normal Global refresh. No vague built-in Global travel/time contract is registered as a replacement.

### Kill authority

Built-in kill progress requires **two independent native signals**:

1. a short-lived `Character.DoDeath` candidate whose attached NPC passed the ordinary-hostile filter;
2. a matching native kill log attributed to the local player or a current `GameData.GroupMembers` Sim.

The candidate is consumed before progress is reported. A despawn alone, a duplicate death callback, a duplicate log callback, the wrong zone, the wrong enemy name, a Duel Sim, or a detected PvP proxy cannot produce credit.

## Native rewards

### XP — implemented, but verification-gated by default

Same-snapshot handoff source evidence shows:

```text
Stats.ExperienceToLevelUp
GameData.AddExperience(xp, false)
```

The sibling PvP implementation uses `false` for a fixed award rather than ordinary grouped NPC-kill semantics. Same-snapshot Crafting findings independently identify `GameData.AddExperience(int,bool)` as combat/quest XP.

However, the supplied handoff does **not** contain the user's current installed `Assembly-CSharp.dll`. Contracts therefore does not claim a fresh authoritative trace from NPC-kill/quest call sites in this pass and keeps:

```text
EnableNativeXpRewards = false
```

by default.

When a local tester deliberately enables it after inspecting the installed DLL, `ContractNativeRewardAdapter` still re-resolves the exact static `(int,bool)` method shape and verifies the current player XP threshold. The amount is planned once and persisted before the native transaction so a safe retry cannot change payout after a level/threshold change.

### Gold — disabled

The same-snapshot PvP source demonstrates:

```text
GameData.PlayerInv.Gold += gold;
GameData.PlayerInv.UpdatePlayerInventory();
```

That is concrete evidence that the live Inventory field/UI refresh path works for that sibling mod, but it is **not** the authoritative grant operation requested for Contracts. No quest/vendor/loot currency-award call chain is present in this handoff and there is no current installed DLL to trace it from. Contracts therefore does not mutate the Gold field.

### Items/resources — disabled

Same-snapshot Crafting research is strong evidence for the native inventory mechanics:

- `ItemDatabase.GetItemByID(string)` lookup;
- `Inventory.AddItemToInv(item, quantity)` where available;
- single-item `AddItemToInv(item)`;
- forced inventory fallback used by native mining when ordinary add cannot place the item.

Crafting also live-tested persistence/stacking for its own registered Wild Herb. That does **not** establish a safe Contracts reward catalog. This handoff does not prove a stable native **common resource id/value policy** or the exact current full-inventory behavior Contracts should promise, so no common item bundle is enabled and Contracts does not hard-depend on Crafting.

## Exactly-once reward transaction

Every irreversible reward component has persisted state:

```text
NotStarted
  -> Prepared       (plan persisted; native mutation has not happened)
  -> Applying       (persisted immediately before native call)
  -> Applied        (actual amount persisted immediately after normal return)

Prepared/Applying -> FailedRetryable only when Contracts knows no native mutation occurred
Applying          -> OutcomeUnknown when the process/reward call leaves the result unknowable
```

Safety properties:

1. configured unsupported components are preflighted before any new supported component is attempted;
2. XP amount is locked/persisted before invocation;
3. a restart from `Applying` becomes `OutcomeUnknown`, never a blind retry;
4. `Applied` components are never invoked again and can be finalized after reload;
5. reward transactions that have started cannot be abandoned;
6. the active claim is removed only after all configured components are safely `Applied`;
7. successful Local-claim counters advance only after final claim commit;
8. Journal delivery occurs only after the final claimed sidecar state becomes durable; a bounded process-local queue may stage the pending entry before that final write, but it is character-keyed and occurrence-deduplicated so delayed delivery cannot overwrite another claim or write the old character's history into a newly selected slot.

The design prefers a visibly locked claim over duplicating an irreversible native reward whose outcome cannot be proven.

## Persistence and character isolation

Contracts owns only sidecar state under:

```text
plugins/config/ErenshorContracts/Characters/<character-key>/contracts.dat
```

It never edits Erenshor save files.

V3 stores board revisions, the persisted Local board-origin zone, active-play time, active/claimed occurrences, generated combat target sets, the bounded observed native enemy catalog, objective state, reward definitions, planned reward amounts, component transaction state, and actual applied amounts. It reads V1/V2, preserves older accepted objectives, infers the current Local board origin from current-revision active/claimed occurrence evidence where possible (preventing a one-time upgrade reroll), and migrates a legacy in-flight pending XP marker to `OutcomeUnknown` rather than risking replay.

Persistence hardening includes:

- atomic temp/write + backup behavior;
- recovery from a valid `.bak` when the primary is corrupt/truncated or missing;
- recovery from a complete orphaned `.tmp` when the primary is missing;
- preservation of an unreadable primary as `.corrupt-*`;
- size/record/string/target/reward bounds;
- duplicate occurrence suppression;
- claimed occurrence authority over stale active rows.

Character keys prefer verified `save-slot index + character name`. When the slot index is temporarily unavailable, the established name-only fallback is allowed only when `GameData.SaveSlots` proves exactly one matching raw character name **and** exactly one matching sanitized sidecar key. Zero/unknown, duplicate raw names, or sanitized-name collisions such as `A-B` vs `A B` fail closed and pause instead of risking a shared sidecar.

Provider progress reports in API v1 contain no character id. They are consumed only while an authoritative character scope is loaded; reports arriving during login/logout or a blocked character switch are discarded, not replayed later into another slot.

## UI

The retained Contracts board stays compact and readable:

- separate **LOCAL CONTRACTS** and **GLOBAL CONTRACTS** sections, with the Local board origin shown in the section header;
- live top-line `LOCAL REFRESH  00:27:41    GLOBAL REFRESH  01:42:36` countdowns driven by persisted active-play seconds;
- title, explicit `LOCATION`, objective, progress, reward and state on each card;
- Accept / Abandon / Claim / Retry / Finalize as appropriate;
- older accepted contracts remain under the correct scope;
- unavailable native XP is described accurately without turning the board into a developer console;
- draggable/resizable window with scrolling;
- draggable standalone launcher;
- no global hotkey.

## Journal integration

Journal remains a soft reflection bridge. A successful finalized claim appends one concise Chronicle entry, for example:

```text
Completed global Contract: Bonepits Suppression. Defeated 12 Bone Guards in Bonepits. Reward: +420 XP.
```

Only reward components actually persisted as `Applied` are included. Record-only provider contracts do not invent a reward. A failed final sidecar save does not **deliver** Journal history; a staged in-memory entry waits for a later successful save in that same character scope. The queue is deliberately memory-only because Journal API v1 has no idempotency key for a cross-mod durable two-phase commit.

## Suite / launcher contract

Contracts advertises:

```text
id=showLauncher
label=Show Contracts Launcher
tier=basic
type=bool
mutable=true
```

and a panel action through the generic module surface.

Expected behavior:

- Hub healthy + preference OFF -> standalone Contracts launcher hidden
- Hub healthy + preference ON -> standalone launcher visible
- Hub unavailable/unvalidated -> recovery launcher forced visible
- `Open Contracts` from MODS works regardless of launcher preference

No Contracts-specific Hub renderer is implemented here.

## Provider API v1

Other mods can register Local record-only templates and report progress after **they** have verified the event. See `docs/INTEGRATION_GUIDE.md`. Provider strings/amounts are bounded and single-line normalized at enqueue time so an optional integration cannot park arbitrarily large payloads in Contracts' static queues.

Contracts deliberately does not reinterpret provider `rewardText` as a Contracts-owned payout.

## Build and test

Deterministic tests:

```powershell
powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1
```

Full native build/install:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

The full script must find the current Erenshor managed assemblies and `Lunaris.dll`, compiles to a temporary DLL, and only then replaces the live Contracts plugin.

Because Erenshor is Early Access, rebuild/reverify native compatibility after game updates.

## Product boundary

Contracts does not own combat, Crafting, Journal UI, Guild Life, party simulation, Deep Sims, PvP, Practice Duels, or Suite Hub. Optional integrations remain soft/versioned. Erenshor remains authoritative for native XP, currency, inventory, quests, combat, and saves.
