# Erenshor Contracts 0.4.4

Part of the **Forgotten Roads for Erenshor** mod collection.

A deterministic **Local + Global contract board** designed as optional old-school MMO side progression.

Contracts should be something you check occasionally while adventuring, not a daily-task treadmill. It does not create streaks, login chores, instant rerolls, GPS automation, or generated quests. The board stays stable for active-play windows, accepted work survives zoning/reload, and rewards are intentionally modest relative to normal progression.

The retained board uses the suite's compact dark/translucent/cyan presentation. Its header has a `▾` / `▸` collapse control so the board can shrink to a draggable header without closing; Reset and `X` remain available and expanding restores the existing board content.

## Default board cadence

- **3 Local** offers
- **2 Global** offers
- Local board revision every **~45 active-play minutes**
- Global board revision every **~120 active-play minutes**

A refresh advances the **whole category revision**. Claimed or abandoned slots do not instantly refill. Accepted contracts from an older revision remain active until completed/abandoned and continue to appear under their true Local or Global section.

Available Local offers follow the **current playable zone**. Their deterministic identity is `LocalBoardRevision + current zone`, so zoning changes locality without advancing the 45-minute revision or refresh deadline. Returning A → B → A within one revision returns the same A offer set rather than creating a fresh reroll. Once a Local contract is accepted, its `OriginZone` is captured permanently and that accepted work remains self-contained while the available board follows later travel. The legacy persisted `LocalBoardZone` field is still read for backward compatibility but is no longer runtime board authority.

Active-play time advances only while the character is fully in world, the logical zone is playable, the game application is focused, and simulation time is running. Closing the game, sitting at character select/title/loading, alt-tabbing away, or pausing simulation does not advance refresh timers or time objectives.

## Built-in contract design

### Local — combat first

New Local boards are built from **real loaded native enemies in the current playable zone**. Contracts scans ordinary hostile `NPC` actors on a bounded cadence and rejects Sim-backed actors, players/friendly factions, resources/chests, summoned/owned actors, vendors, invulnerable actors, PvP temporary proxies when that optional capability is present, and boss-reward actors. Current verified runtime evidence exposes the native enemy display name, native level, and observed population count; it does not expose a proven creature-family/template identifier in this packet.

The generated Local board then:

- admits only enemy types whose observed level range is within five levels of the current character;
- prefers more plentiful observed enemy types when level fit is equal;
- prefers repeatable/generic-looking enemy types over likely personal-name targets when both are otherwise eligible;
- uses deterministic repeatable counts that are capped by the observed population (normally 5–8 Local before the evidence cap);
- treats likely one-off/proper-name identities as bounty-style exact targets with a count of **1**;
- writes the exact destination zone into the contract;
- freezes each selected **revision + zone** target set, so spawn churn does not reroll a zone and A → B → A returns the original A set.

If the first bounded scan after loading has no qualifying enemy yet, Contracts does **not** freeze an empty generation. It keeps the revision retryable and can populate it once authoritative live enemies appear.

`Local Patrol` remains as one low-priority time objective so a zone with too little verified combat evidence can still offer a small amount of non-combat work. The older Road Check / Perimeter Sweep / Wayfarer / Local Circuit definitions remain in code only for persisted backward compatibility and deterministic migration coverage; the runtime no longer registers them for new built-in boards.

### Global — observed cross-zone combat

The supplied game surface does not give Contracts a proven authoritative unloaded-zone bestiary, so Global work does not use an invented hardcoded mob list. Instead each character sidecar retains a bounded catalog of **actually observed native zone/enemy/level/population** facts.

New Global offers:

- target a named zone other than the player's current zone;
- use only previously observed qualifying native enemy types;
- apply the same level-appropriateness rule;
- prefer more plentiful observed targets when level fit is equal;
- use deterministic repeatable counts (normally 8–12 before the observed-population cap), while likely exact/named targets remain bounded to one;
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

The current installed game assembly and the live-proven sibling PvP module confirm this exact reward call. Contracts uses the same direct typed API and defers the **entire** claim while `GameData.RaidActive` so contract XP is never routed to raid XP. The production default is:

```text
EnableNativeXpRewards = true
```

by default. Existing 0.4.0 persisted `false` values are migrated once to schema 1; later explicit player opt-outs are preserved. The amount is planned once and persisted before the native transaction so a safe retry cannot change payout after a level/threshold change.

### Gold — disabled

The same-snapshot PvP source demonstrates:

```text
GameData.PlayerInv.Gold += gold;
GameData.PlayerInv.UpdatePlayerInventory();
```

That is the current same-game, live-proven path used by Contracts: it changes `GameData.PlayerInv.Gold` only after all unapplied reward components have passed preflight, then calls `GameData.PlayerInv.UpdatePlayerInventory()`. The component ledger is persisted before and after every irreversible call.

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

V3 stores board revisions, the legacy Local board-origin field, active-play time, active/claimed occurrences, per-zone generated combat target sets, the bounded observed native enemy catalog, objective state, reward definitions, planned reward amounts, component transaction state, and actual applied amounts. It reads V1/V2 and preserves older accepted objectives. The legacy `LocalBoardZone` value remains readable but no longer controls available-board locality. Existing unaccepted generated combat rows may be narrowed on load when current persisted population evidence proves an old count is excessive; accepted `A` rows are never rewritten. Legacy in-flight reward state still migrates fail-closed rather than risking replay.

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

- separate **LOCAL CONTRACTS** and **GLOBAL CONTRACTS** sections, with the current playable Local zone shown in the section header;
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
