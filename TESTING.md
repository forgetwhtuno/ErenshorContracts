# Erenshor Contracts 0.4.0 — combat-board release / playable-state acceptance checklist

This checklist is intentionally split between **deterministic source-only tests** and **live Erenshor verification**. Native reward behavior is never considered proven from documentation alone.

## 1. Deterministic build/test gates

- [ ] Run `RUN_TESTS.ps1`; the Contracts core suite reports PASS.
- [ ] The Suite UI policy suite reports PASS.
- [ ] Run `BUILD_AND_INSTALL.ps1` against the **current installed** Erenshor managed assemblies and `Lunaris.dll`.
- [ ] Build stops on missing references or compiler errors before replacing the live DLL.
- [ ] Only `ErenshorContracts.dll` is installed by the build script.
- [ ] Harmony patches are limited to native kill-authority observation (`Character.DoDeath` and the two current `UpdateSocialLog.LogAdd` overloads); any patch failure disables combat generation and rolls back partial Contracts patches.
- [ ] `EnableNativeXpRewards` remains **false** until the current installed DLL/live verification below is completed.

## 2. Native XP verification gate

Before enabling XP, inspect the exact installed `Erenshor_Data/Managed/Assembly-CSharp.dll` and trace the current call chain for NPC/quest/fixed XP awards.

- [ ] Exact static `GameData.AddExperience(int, bool)` exists in the installed binary.
- [ ] Determine the current meaning of the bool parameter from native callers, not from an old signature guess.
- [ ] Trace at least NPC kill and quest/fixed-reward callers where present.
- [ ] Verify level-up / level-cap / Ascension handling.
- [ ] Verify UI/log refresh behavior.
- [ ] Verify save/persistence side effects.
- [ ] Confirm calling the method once with `false` is appropriate for a fixed Contracts award.
- [ ] Only then deliberately set `EnableNativeXpRewards=true` for a controlled live test.
- [ ] With XP disabled, claim remains complete/unclaimed and performs no native mutation.
- [ ] With XP enabled and verified, the pre-claim display amount matches the persisted planned amount.
- [ ] Claim a Local contract and verify exactly that amount is granted once.
- [ ] Save/reload and verify the same occurrence cannot grant XP again.

## 3. Suite / launcher

- [ ] `CONTRACTS` launcher is visible in ordinary gameplay when no usable Hub bridge is available.
- [ ] No F-key or other global hotkey is registered.
- [ ] Launcher opens/closes the board.
- [ ] Launcher drag position persists.
- [ ] Healthy Hub + `Show Contracts Launcher = OFF` hides the standalone launcher.
- [ ] Healthy Hub + `Show Contracts Launcher = ON` shows the standalone launcher.
- [ ] `Open Contracts` from MODS opens the board regardless of standalone launcher visibility.
- [ ] If Hub becomes unavailable/unready/unvalidated, the recovery launcher is forced visible.
- [ ] Aura advertises `id=showLauncher`, `label=Show Contracts Launcher`, `tier=basic`, `type=bool`, `mutable=true`.
- [ ] `ui.state`/`closePanel` work through the generic suite contract; Contracts adds no special Hub renderer.

## 4. Board presentation

- [ ] Expanded Contracts header shows `▾`; collapse leaves only the compact ~32px draggable header with Reset and `X`, then `▸` restores the same retained board.
- [ ] Contract rows, footer, countdown body, and resize grip are hidden/non-interactive while collapsed.
- [ ] Drag the collapsed header to each edge, expand, and confirm the board remains screen-contained with the header staying in place.
- [ ] Repeated collapse/expand creates no duplicate Canvas/EventSystem roots.
- [ ] Countdown/progress text updates retained TMP/Image state without rebuilding contract-card structure.
- [ ] Small-resolution initialization/fitting never intentionally resolves the panel outside the available screen bounds.

- [ ] Main window remains compact (default 690×540), draggable, resizable, and scrollable.
- [ ] LOCAL and GLOBAL sections are visually distinct.
- [ ] Local header shows the persisted board-origin zone.
- [ ] Top countdown reads `LOCAL REFRESH  HH:MM:SS    GLOBAL REFRESH  HH:MM:SS` and visibly ticks once per active-play second.
- [ ] Closing/reopening the panel does not reset either countdown.
- [ ] Countdown text changes do not rebuild the whole retained row hierarchy each second.
- [ ] Every card shows title, explicit `LOCATION`, objective, progress, reward, state, and appropriate action.
- [ ] Generated Global cards always name a concrete destination zone.
- [ ] States are understandable: AVAILABLE, ACTIVE, READY TO CLAIM, CLAIMED, retry-ready/applied/unknown reward states.
- [ ] Older accepted legacy work remains under its real Local/Global section after a refresh.
- [ ] When away from the Local board origin, new Local offers show a return-to-board state and cannot be accepted.
- [ ] If six active contracts are already retained, a seventh Accept reports the active-contract limit instead of silently doing nothing.

## 5. Local combat objective matrix

- [ ] Enter Hidden Hills (or another normal combat zone) with several ordinary native enemies loaded.
- [ ] Within the bounded scan cadence, new Local offers overwhelmingly become native-enemy culls.
- [ ] Every generated Local target is actually present in the current Local board-origin zone at generation time.
- [ ] Friendly Sims, player actors, vendors, mining/resources, treasure chests, summons/pets, invulnerable actors, known friendly/debug factions, detected PvP proxies, and `BossXp > 0` actors never become cull targets.
- [ ] Targets outside the ±5 level-distance policy are not selected.
- [ ] When level fit is equal, a visibly common enemy type is preferred over a one-off/sparse type.
- [ ] Local kill counts are 6–9.
- [ ] Killing the exact target in the exact zone advances once.
- [ ] The same enemy name in another zone does not advance the contract.
- [ ] A despawn with no attributed native kill line does not advance.
- [ ] Duplicate death/log callbacks do not double-credit.
- [ ] A current party Sim's native `has been slain by <Sim>` kill advances once.
- [ ] An unrelated/non-party killer does not advance.
- [ ] Local Patrol appears only as lower-priority fallback when the board has fewer verified combat types than slots.
- [ ] If the first post-load scan has zero qualifying targets, the revision stays retryable; later loaded/spawned authoritative targets can populate it without waiting 45 minutes.

## 6. Global combat objective matrix

- [ ] On a brand-new V3 sidecar, Global may be empty rather than inventing an unloaded-zone enemy list.
- [ ] Visit another playable combat zone so its qualifying native enemies enter the per-character observed catalog.
- [ ] Once another-zone evidence exists, an otherwise-empty current Global revision can populate immediately and then freezes.
- [ ] Every generated Global card names a concrete destination zone.
- [ ] Global never targets the player's current zone at generation.
- [ ] Global uses only previously observed qualifying native zone/enemy pairs.
- [ ] Level policy remains ±5 from the player's current level range at generation.
- [ ] More plentiful equal-level enemy types are preferred.
- [ ] Global kill counts are 10–14.
- [ ] Traveling to the destination and killing the target advances; killing it in the wrong zone does not.
- [ ] Once generated, later discoveries/spawn churn do not silently reroll the current Global revision.
- [ ] No vague Long Watch/Grand Tour/Contract Regular/Expedition objective is newly registered into the built-in Global board.

### Legacy accepted-objective regression

Older persisted Road Check / Perimeter Sweep / Wayfarer / Local Circuit / Long Watch / Grand Tour / Contract Regular / Expedition instances are still self-contained and must remain completable/abandonable after upgrade. Their existing deterministic tests remain regression coverage; they are **not** newly generated by the 0.4 combat-first runtime registration.

## 7. Refresh / anti-reroll

- [ ] New character gets up to the configured Local combat slots once native enemies are authoritatively scanned; Global may stay empty until another zone has been observed.
- [ ] Local board revision remains stable for ~45 active-play minutes by default.
- [ ] Global board revision remains stable for ~120 active-play minutes by default.
- [ ] Closing the game/logging out for wall-clock time advances neither timer.
- [ ] Claimed slot does not instantly refill.
- [ ] Abandoned slot does not reroll; the same occurrence can be reaccepted with reset progress until refresh.
- [ ] Reopening the board does not change offers.
- [ ] Zoning does **not** change current Local board seed/origin or generate fresh Local slots.
- [ ] Local refresh advances the whole Local category revision and binds the new revision to the current playable zone.
- [ ] Global refresh advances the whole Global category revision independently; an empty revision remains retryable until cross-zone combat evidence exists.
- [ ] Accepted older-revision contracts survive refresh.
- [ ] Repeated zone-hopping before 45 active minutes yields no fresh Local board.
- [ ] Active contract count remains capped at six to prevent indefinite backlog accumulation.
- [ ] Refresh arithmetic behaves safely at large/corrupt timer values without overflow loops.

## 8. Per-component reward transaction

For an XP-only built-in with verified XP enabled:

- [ ] Completed claim begins with XP `NotStarted`.
- [ ] XP amount is calculated once and persisted as the plan before native invocation.
- [ ] `Prepared` is persisted before crossing the irreversible boundary.
- [ ] `Applying` is persisted immediately before the native call.
- [ ] Normal native return records `Applied` plus the actual XP amount and persists it immediately.
- [ ] Final claim removal occurs only after all configured reward components are safely `Applied`.
- [ ] A started transaction cannot be abandoned.
- [ ] An already `Applied` XP component loaded from disk finalizes without invoking XP again.

Failure injection / reasoning:

- [ ] Preparation save failure means native XP is never called and the component remains safely retryable.
- [ ] Pre-invocation `Applying` save failure means native XP is never called; reload sees the previous safe persisted state.
- [ ] Native adapter failure **before invocation** becomes retryable.
- [ ] Exception/unknown failure **after invocation begins** becomes `OutcomeUnknown` and is never blindly retried.
- [ ] Process restart from persisted `Applying` normalizes to `OutcomeUnknown`.
- [ ] Native success followed by immediate Applied-ledger save failure never invokes XP again in that same process.
- [ ] If `Applied` was persisted but final claim save fails, reload can finalize the claim without regranting the component.
- [ ] Malformed/inconsistent component ledger fails closed rather than manufacturing a retryable payout.

Future mixed-component rule (once gold/items have proven adapters):

- [ ] Unsupported/configured components preflight before the first new irreversible component.
- [ ] Each irreversible component has its own persisted state and actual applied amount/count.
- [ ] A successfully `Applied` component is never repeated just because a later component failed.
- [ ] Journal only describes components actually finalized as `Applied`.

## 9. Gold / item safety

- [ ] Contracts source contains no direct `GameData.PlayerInv.Gold += ...` reward mutation.
- [ ] Built-ins configure zero gold.
- [ ] Contracts source contains no direct `AddItemToInv` / `ForceItemToInv` reward call.
- [ ] Built-ins configure no item id/quantity.
- [ ] Do not enable gold until quest/vendor/loot currency authority and save/UI effects are proven in current installed IL/runtime.
- [ ] Do not enable common item bundles until a native low-value item catalog plus grant/stack/full-inventory policy is proven.
- [ ] Do not use Crafting's custom Wild Herb as an implicit Contracts hard dependency.

## 10. Character isolation / lifecycle

- [ ] Slot A and Slot B use different sidecars.
- [ ] Two slots with the same character name still use distinct slot-qualified sidecars when slot identity is authoritative.
- [ ] If two known slots share a raw name and active slot identity cannot be resolved, Contracts pauses/fails closed.
- [ ] If distinct raw names sanitize to the same sidecar key (for example `A-B` and `A B`) and slot identity is unavailable, Contracts pauses/fails closed.
- [ ] A blank/uninitialized live player name never resolves to a fallback `player` sidecar.
- [ ] A dirty outgoing character sidecar must save before Contracts switches scope.
- [ ] If that save fails, board/progress pauses until it succeeds.
- [ ] Provider progress arriving while scope is blocked is discarded, not replayed into the next character.
- [ ] First load/current-zone seeding does not count the previous character's final zone as travel.
- [ ] Leaving gameplay clears the live travel edge; logging back in seeds current logical zone rather than counting an offline transition.
- [ ] Logout/login does not advance objective/board active time.

## 11. Persistence / migrations / malformed state

- [ ] V3 round-trip preserves revisions, Local board origin, combat generation markers, observed enemy catalog including population count, generated target sets, active time, claimed set, objective state, reward definitions, planned amount, statuses, applied amounts, and target zone.
- [ ] V1 normal active contract remains loadable.
- [ ] V1 legacy reward-pending XP occurrence migrates to `OutcomeUnknown`.
- [ ] V1 no-pending XP occurrence remains unattempted.
- [ ] Legacy/provider record-only active contract does not gain synthetic XP.
- [ ] V1 current Local board origin is inferred from current-revision active/claimed occurrence evidence where possible.
- [ ] Truncated V2 reward-ledger active row rejects primary and recovers a valid `.bak`.
- [ ] Corrupt primary recovers valid `.bak` and preserves `.corrupt-*` diagnostic copy.
- [ ] Missing primary recovers valid `.bak`.
- [ ] Missing primary can recover a complete orphaned `.tmp`.
- [ ] Malformed target/progress/reward/string values are bounded.
- [ ] Duplicate active occurrence ids are deduplicated.
- [ ] Claimed occurrence suppresses a stale active row.
- [ ] Active record count is bounded.
- [ ] Invalid required-component status text normalizes to `OutcomeUnknown`, never `NotStarted`.
- [ ] Prepared/retryable XP without a persisted planned amount normalizes to `OutcomeUnknown`.
- [ ] A `NotStarted` XP component carrying a hidden nonzero plan normalizes to `OutcomeUnknown`.

## 12. Journal soft bridge

With Journal absent:

- [ ] Successful claim still finalizes with no error.

With Journal installed:

- [ ] Successful finalized claim appends exactly one Chronicle entry.
- [ ] Entry says Local/Global, contract title, meaningful objective result and **only actually Applied rewards**.
- [ ] Record-only provider claim contains no invented reward.
- [ ] Final sidecar-save failure delivers no Journal entry before persistence succeeds.
- [ ] A later successful save in the **same character scope** retries delivery from the in-memory Journal queue; slot switching cannot redirect the old text.
- [ ] Multiple pending in-process entries retain FIFO order per character and occurrence dedupe.
- [ ] Queue remains bounded.
- [ ] Process-loss after claim persistence but before Journal delivery may lose that optional history entry rather than risk a duplicate, because Journal API v1 has no durable idempotency key.
- [ ] Already claimed occurrence cannot emit another completion entry because it cannot be claimed again.

## 13. Provider API v1

Using a tiny provider or reflection console:

- [ ] Register a priority Local provider template; it appears in Local, not Global.
- [ ] Accept it.
- [ ] Wrong channel/key/context does not progress it.
- [ ] Matching verified provider report progresses it.
- [ ] Complete/claim records it locally without automatic Contracts XP.
- [ ] Queue progress during character-select/scope-block and confirm it is discarded.
- [ ] Oversized template fields are single-line normalized and bounded before entering the static template queue.
- [ ] Oversized progress channel/key/context and amount are bounded before entering the progress queue.
- [ ] Blank required provider/template/title/channel/key or nonpositive target/amount is rejected.
- [ ] Provider `rewardText` remains presentation only and never becomes a Contracts-owned native payout.

## 14. Three-hour playable-session scenario

Suggested live run:

1. Start in Hidden Hills (or another normal combat zone) and wait for the bounded native-enemy scan.
2. Confirm Local is dominated by real zone-native culls, with Local Patrol only filling a spare slot if needed.
3. Accept 2–3 culls, kill targets naturally with both player and party-Sim finishing blows, and verify exact once-only progress.
4. Reopen the board repeatedly while the countdown runs; offers and timers must not reset.
5. Observe claimed slots remain claimed/empty rather than instantly replacing themselves.
6. Zone repeatedly before 45 active minutes and verify there is still no fresh Local board/origin reroll.
7. In the second zone, let Contracts observe native enemies; if Global was empty, verify it can now acquire a verified cross-zone target for the current revision.
8. Travel to that Global destination, verify its explicit LOCATION/objective remain stable, and progress it only on the correct enemy in the correct zone.
9. Cross ~45 active minutes: one whole Local revision appears and binds to the zone currently being played.
10. Accept new Local combat work without losing older accepted work; if six actives are retained, verify the explicit cap message.
11. Cross ~90 active minutes: Local advances again independently.
12. Cross ~120 active minutes: Global advances once while accepted old Global work survives.
13. Logout completely for several wall-clock minutes, log back in, and verify no offline countdown/objective jump.
14. Reload and verify active/claimed progress, target locations, generated target sets, observed enemy catalog, board origins, countdowns, and reward ledgers reconstruct correctly.
15. Confirm legacy accepted travel/time contracts from an upgraded sidecar still function, but no new vague built-in Global objective appears.
16. Confirm the overall cadence feels like optional old-MMO grinding rather than a daily-task treadmill.

Anti-exploit checks during the run:

- [ ] claim -> no instant replacement;
- [ ] abandon/reaccept -> same occurrence, reset progress, no reroll;
- [ ] zone hop -> no Local reroll;
- [ ] board reopen -> deterministic;
- [ ] offline time -> no cadence progress;
- [ ] duplicate death/log callbacks -> no duplicate kill credit;
- [ ] wrong-zone same-name enemy -> no credit;
- [ ] Duel/PvP temporary actor -> no credit;
- [ ] old active accumulation -> capped at six;
- [ ] applied reward component -> no duplicate on reload/finalize.

## 15. General safety / privacy

- [ ] No Erenshor save-file mutation.
- [ ] No quest/faction mutation.
- [ ] No NPC/Sim movement or combat control.
- [ ] Kill observation is only the documented two-signal native path; no text-only/despawn-only generic kill inference and no unproven item/quest observer.
- [ ] No hard dependency on Journal/Crafting/PvP/Deep Sims/Suite Hub.
- [ ] No network requests.
- [ ] No private paths, names, emails, secrets, logs, or AI co-author metadata in public deliverables.
- [ ] Public identity, where needed, is only `forgetwhtuno` / `314876526+forgetwhtuno@users.noreply.github.com`.


## 0.4.0 combat-board live matrix

### Local discovery / board

1. Enter a normal hostile zone such as Hidden Hills with a level-appropriate character.
2. Open Contracts after the first enemy scan.
3. Verify the Local board contains native enemy names that are actually alive/available in this zone.
4. Verify every combat card shows `LOCATION: <current Local board zone>`.
5. Verify counts are 6–9 and objective text names both enemy and zone.
6. Kill/respawn mobs without waiting for the Local refresh; the offered target identities must not reroll.
7. Zone away and reopen; the Local board remains tied to its persisted origin until its active-play refresh.
8. At Local countdown zero, verify one category refresh occurs and the new Local board binds the current playable zone.

### Global discovery / board

1. On a new V3 sidecar, note that Global combat work may be empty until other zones have actually been observed.
2. Visit at least two level-appropriate hostile zones and let the native scan run.
3. Wait for / test the next Global refresh boundary.
4. Verify Global cards name a different exact destination zone and a native enemy seen there.
5. Verify counts are 10–14.
6. Verify the current zone is not selected as a Global destination at generation.
7. New enemy discoveries during the same Global revision must not reroll existing Global rows.

### Refresh countdown

1. With the board open, verify the top line visibly shows:
   `LOCAL REFRESH HH:MM:SS` and `GLOBAL REFRESH HH:MM:SS`.
2. Seconds decrement only while Contracts' existing active-play eligibility is true.
3. Alt-tab/unfocus: countdown freezes.
4. Pause simulation: countdown freezes.
5. Close/reopen the panel: countdown resumes from the same sidecar-backed state.
6. At zero, verify the category revision changes exactly once and countdown resets to its next active-play cadence.

### Kill credit

For an accepted kill contract:

1. Local player melee kill -> exactly +1.
2. Local player spell kill -> exactly +1.
3. Party Sim kill with native `has been slain by <Sim>` text -> exactly +1.
4. Kill the correct enemy in the wrong zone -> +0.
5. Kill a different enemy in the correct zone -> +0.
6. Despawn/reset an enemy without a native kill line -> +0.
7. Trigger duplicate social-log presentation if reproducible -> still exactly +1.
8. Duel a Sim -> +0.
9. Kill/expire an Erenshor-PvP temporary proxy -> +0.
10. Pet/summon/resource/vendor/scenery actors must never appear as generated targets.
11. Zone during a death/log transition -> stale candidate must not credit in the next zone.

### Rewards

- Default `EnableNativeXpRewards=false`: completed contract Claim must fail closed before XP mutation and remain claimable.
- Gold: no built-in contract should attempt a gold mutation in this build.
- Incomplete: Claim unavailable/rejected.
- Abandoned: no payout.
- If local binary inspection + controlled live testing later authorizes XP:
  - enable the config deliberately;
  - claim once;
  - verify exact XP delta;
  - reload and verify no duplicate claim;
  - interrupt at ledger phases only on a disposable test character.

### Transition / unload

- Zone repeatedly with Contracts open.
- Hot unload/reload under Lunaris while no kill is occurring.
- Hot unload shortly after a kill.
- Verify Harmony patches are removed on unload and no stale death candidate survives scene change/reload.
