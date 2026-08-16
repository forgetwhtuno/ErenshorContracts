# Changelog

## 0.4.4 — release-candidate locality and target-quality repair

- Available Local offers now use `LocalBoardRevision + current playable zone`; zoning no longer leaves the board visually/semantically pinned to the legacy persisted `LocalBoardZone`.
- Accepted Local contracts still retain their immutable `OriginZone`, while unaccepted boards follow travel without changing the active-play refresh revision/deadline.
- Local generated combat targets are frozen per revision+zone, so A → B → A restores the original A set instead of deleting it or creating a zoning reroll.
- Preserved V1/V2/V3 compatibility by retaining the legacy board-origin fields as read-only migration/history data rather than deleting them.
- Tightened combat-target quality using only current verified evidence (display name, level, observed population): repeatable creature-like targets are preferred, counts are capped by observed population, and likely proper-name/one-off targets become count-one bounty work.
- Kill credit still matches the exact native display identity; generic/plural wording is presentation only and does not invent a native family field.
- Old accepted contracts remain self-contained and are not rewritten. Unaccepted persisted generated targets may only be narrowed when persisted enemy evidence proves the old count is excessive.
- Preserved the existing Gold + direct personal XP component-ledger transaction, raid deferral, persistence ordering, unknown-outcome lock, and exactly-once Claim boundary unchanged.
- Added deterministic coverage for current-zone Local identity, A → B → A stability, refresh neutrality on zoning, accepted-origin persistence, per-zone combat freezing, named-target bounds, population caps, legacy persistence, and claim identity across zoning.

## 0.4.2 — Gold + XP claim activation and legacy config migration

- Added a one-time reward schema migration. A persisted pre-production `EnableNativeXpRewards=false` from the 0.4.0 safe default is promoted to `true` once and saved with `RewardConfigVersion=1`; later explicit player opt-outs are preserved.
- Replaced Contracts' reflected XP invocation with the installed-assembly-proven `GameData.AddExperience(xp, false)` call and now refreshes Gold with `GameData.PlayerInv.UpdatePlayerInventory()`.
- Preserved component-ledger, overflow, preflight, raid-deferral, and fail-closed unknown-outcome protections. Raid mode defers the entire claim before Gold or XP mutation.
- Added concise reward diagnostics through `ContractsControlApi.GetRewardDiagnostics()` and deterministic migration/reward-wiring tests.

## Unreleased - bounded Suite UI polish

- Aligned Contracts and its launcher with the canonical dark/translucent/cyan Sim Actions palette and added a thin cyan frame.
- Added a `▾` / `▸` header collapse control. Collapsed Contracts keeps a draggable 32px header with Reset/Close while contract rows, footer, countdown content, and resize grip are hidden.
- Collapse/expand preserves the header's screen position and clamps both states; the retained board structure is not rebuilt for ordinary countdown/progress text changes.
- Fixed initial/small-screen panel extent clamping so the Contracts window cannot deliberately resolve larger than the available screen merely to satisfy its normal minimum.
- Reduced contract-card opacity to match the Suite visual family without changing contract generation, reward, or kill-credit behavior.
- Extended Unity-free Suite UI policy tests for compact geometry, collapse height, top-edge preservation, containment clamp, launcher fallback, and structural-vs-dynamic rebuild behavior.

## 0.4.0 — MMO kill-contract board

### Combat-first Local / Global contracts

- Added native-enemy discovery from the currently loaded zone using the same-snapshot ordinary-hostile filter already proven by Follow/PvP.
- Repeatable generation additionally excludes `BossXp > 0` actors and prefers more plentiful observed enemy types when level fit is equal, reducing one-off/named targets in a grind board.
- Local boards now prioritize real current-zone enemy culls, level-filtered to within five levels and frozen for the persisted board revision once at least one verified target exists. Empty first scans remain retryable instead of freezing an empty board.
- Added a bounded per-character observed enemy catalog (zone, native display name, level range, observed population, active-play sighting time) so Global boards can target real enemy types previously seen in an explicit other zone instead of inventing a destination. Empty Global generation remains retryable until another-zone evidence exists; once populated, the revision freezes.
- Global combat contracts use larger 10–14 kill counts; Local uses 6–9.
- Generated combat rows carry an explicit target zone and enemy name through accept, persistence, UI, progress and journal completion.
- New built-in rotation keeps only Local Patrol as a low-priority Local fallback. Older accepted time/travel/global contracts remain self-contained and completable after migration, but new Global built-ins are combat-only.

### Kill-credit authority

- Added Harmony observation for `Character.DoDeath` plus the same-snapshot `UpdateSocialLog.LogAdd(string,string)` / `LogAdd(string)` kill-message paths.
- A qualifying kill requires both an eligible native death candidate and matching native kill text attributed to the local player/current party.
- Death candidates are consumed exactly once, time-bounded, zone-bound and enemy-name-bound.
- Remote/temporary/non-enemy surfaces are rejected before a death becomes a Contracts candidate; optional PvP proxy rejection binds reflectively to PvP's `IsTemporaryNpc` when present.
- Scene transitions clear pending death candidates.

### Board / UI

- Added a clear `LOCATION:` line to every contract card. New Global combat work always names an exact destination.
- Replaced rounded refresh text with live active-play `HH:MM:SS` countdowns at the top:
  `LOCAL REFRESH 00:27:41` / `GLOBAL REFRESH 01:42:36`.
- Countdown text updates independently of the retained row structure, so rows are not rebuilt every second.
- Persisted generated combat selections so living-spawn churn cannot reroll a revision.

### Persistence

- Added sidecar V3 while retaining V1/V2 reads.
- V3 persists active target zone, observed enemy catalog, generated combat selections and combat-generation revision markers.
- Existing active V1/V2 contracts remain readable and keep their prior semantics.

### Rewards

- Re-ran the supplied-project reward evidence audit.
- XP remains verification-gated/default-off: same-snapshot source proves the exact `GameData.AddExperience(int,bool)` call shape, but the export still contains no current installed `Assembly-CSharp.dll` to prove the game's own current caller semantics.
- Gold remains blocked: same-snapshot PvP writes `GameData.PlayerInv.Gold` directly and refreshes inventory, but no authoritative native currency-grant path is supplied.
- No incomplete or abandoned contract can enter the reward transaction; existing component-ledger exactly-once claim protection remains authoritative.

### Tests/build

- Added pure tests for semantic enemy exclusion, level filtering, abundance preference, Local/Global combat generation, empty-generation retry, revision freezing, combat-over-fallback priority, explicit location, seconds countdown timing/formatting, kill-line parsing, wrong-zone kill rejection, exact progress caps, incomplete/exactly-once claim, reward-authority fail-closed policy and V3 persistence.
- Build now declares Harmony permission and requires `0Harmony.dll` alongside `Lunaris.dll`.


## 0.3.0 — deep playable-state pass

### Contract catalog and progression

- Expanded the intentionally small built-in catalog to **5 Local / 4 Global templates** using only authoritative logical-zone and active-play facts.
- Added **Perimeter Sweep**: ten active minutes in the origin, then depart to another playable zone.
- Added **Local Circuit**: enter two different away zones, then return to the origin.
- Added **Expedition**: forty-five active minutes plus five different playable zones.
- Kept Local Patrol, Road Check, Wayfarer, Long Watch, Grand Tour (now eight unique zones), and Contract Regular; Contract Regular remains four successful Local claims so a default three-slot board cannot finish it in one rotation.
- Improved player-facing target/progress/completion wording for time, travel, circuits, claims and combined criteria.
- Logical travel uses `GameData.SceneName` after gameplay readiness; login/title/character-select/loading transitions cannot progress travel objectives.
- Active-play accumulation now requires focused, running gameplay in a usable logical zone. Offline, unfocused and paused time does not advance board/objective timers.
- Added overflow-safe active-time/refresh arithmetic.

### Board / anti-reroll

- Preserved deterministic whole-category board revisions: ~45 active minutes Local, ~120 Global by default.
- Claim/abandon still never creates an instant replacement.
- Persist one **Local board-origin zone per revision**. Zoning no longer changes the Local seed/occurrence namespace, so zone-hopping cannot generate a fresh three-slot board before the 45-minute refresh.
- New Local work can be accepted only at that persisted origin; accepted travel contracts continue normally while away. At the next Local refresh, the origin rebinds to the current playable zone.
- V1 migration infers the current-revision Local origin from persisted active/claimed occurrence evidence before falling back to the current zone, avoiding a migration-time free reroll where evidence exists.
- Older accepted contracts survive revision changes and render under their actual Local/Global section.
- Kept deterministic template selection and duplicate-template suppression.
- Added an explicit player-facing message when preserved older work fills the six-active-contract cap, rather than letting a seventh Accept appear to do nothing.

### Rewards and claim safety

- Replaced the old single reward-pending guard with a persisted **per-component reward ledger**:
  `NotStarted -> Prepared -> Applying -> Applied / FailedRetryable / OutcomeUnknown`.
- Persist the planned XP amount before the first irreversible native call and reuse that exact amount on a safe retry.
- Persist `Applying` before native invocation; a restart from that state becomes `OutcomeUnknown` instead of risking duplicate XP.
- Persist actual applied amounts immediately after a normal native return; an `Applied` component is never invoked again and may be finalized safely after reload.
- Added future-ready persisted component fields for XP/gold/item without enabling unproven native gold/item mutations.
- Unsupported configured components fail preflight before a new component is attempted, preventing known XP-first partial transactions.
- A started reward transaction cannot be abandoned.
- Journal history is now built from actual applied components; delivery is attempted only after final claimed state saves. A bounded process-local retry queue is character-keyed and occurrence-deduplicated so temporary Journal unavailability cannot overwrite another claim or cross a slot boundary.
- Centralized built-in XP balance: Local 3–6%, Global 12–18% of the current level XP threshold.
- Kept native XP **OFF by default** because this handoff contains no current installed `Assembly-CSharp.dll`. The exact-signature `GameData.AddExperience(int,bool)` adapter remains deliberately live-testable after local binary verification.
- Gold remains disabled: same-snapshot evidence is direct `PlayerInv.Gold += gold` + inventory refresh, not a proven authoritative currency grant API.
- Items remain disabled: native inventory grant mechanics are strongly evidenced by Crafting, but Contracts has no proven native common-resource catalog/policy in this handoff.

### Persistence and character isolation

- Introduced sidecar **V2** with persisted Local board origin, reward definitions, component states, planned XP and actual applied reward amounts; V1 remains readable.
- Legacy V1 in-flight reward pending state migrates fail-closed to `OutcomeUnknown`.
- Truncated V2 reward-ledger rows are rejected and recover through a valid backup instead of silently resetting transaction state.
- Added recovery from a valid `.bak` when the primary is corrupt or missing, plus recovery from a complete orphaned `.tmp` when primary is missing.
- Retained corrupt-primary snapshots and record/string/count/value bounds.
- Added stricter character switching: unsaved outgoing state must persist before scope changes; live provider progress is discarded while no authoritative character scope exists.
- Added character-key collision protection: if the active slot index is unavailable, Contracts permits the name-only fallback only when the save-slot roster proves exactly one matching raw name and exactly one matching sanitized sidecar key; zero/unknown, duplicate names, or sanitized-key collisions pause instead of risking a shared sidecar.
- Bounded/normalized provider API v1 template/progress payloads at enqueue time so oversized optional-integration strings cannot accumulate in the static queues.

### UI / Suite

- Kept the compact retained-uGUI visual style and Local/Global sections.
- Refresh countdowns remain rounded to player-facing minutes/hours with no seconds spam.
- Added explicit `RETRY READY`, `REWARD APPLIED`, and fail-closed unknown-outcome presentation.
- Polished unavailable-XP wording so the board remains player-facing rather than diagnostic-heavy.
- Widened the progress/target columns inside the same compact default panel to prevent combined time+zone and return-route status strings from clipping.
- Added the Unity UI module reference to the project file to match the retained-uGUI build script/reference set.
- Preserved the Basic mutable `Show Contracts Launcher` Aura setting and generic `Open Contracts` panel action, healthy-Hub visibility policy, fallback recovery launcher, and no global hotkey.

### Tests

- Expanded deterministic coverage for all nine built-ins, locality/global scope, unique-zone tracking, persisted Local board-origin anti-reroll, active-play eligibility, refresh boundaries, overflow, planned-XP locking, component retry/unknown state, exactly-once claim behavior, Journal text/dedupe authority, V1/V2 migrations, corrupt/truncated/missing-primary recovery, malformed data bounds, character-key collisions, and provider API input bounds.

## 0.2.0

- Introduced Local/Global board revisions, active-play refresh timers, built-in XP policy, the initial verification-gated XP adapter, character-scoped sidecars, launcher/Aura integration and retained board UI.
## 0.4.3 — Forgotten Roads launcher/header chrome

- Standardized the standalone retained-uGUI launcher at 154x32 with programmatic grip marks and collection hover/pressed colors.
- Replaced font-dependent collapse triangles with mod-owned Image chevrons while preserving panel behavior.
