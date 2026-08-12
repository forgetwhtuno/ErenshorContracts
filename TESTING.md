# Erenshor Contracts 0.1.0 Preview - acceptance checklist

## Build

- [ ] `RUN_TESTS.ps1` reports PASS.
- [ ] `BUILD_AND_INSTALL.ps1` compiles against the current installed BepInEx/Unity assemblies.
- [ ] Only `ErenshorContracts.dll`, `LICENSE`, and `NOTICE` are installed under the plugin folder.
- [ ] No `Assembly-CSharp.dll` or Harmony reference is required.

## UI

- [ ] `CONTRACTS` launcher is visible in an ordinary gameplay scene.
- [ ] No F-key or other global hotkey is registered.
- [ ] Launcher opens/closes the board.
- [ ] Launcher can be dragged and persists after restart.
- [ ] Main window can be dragged.
- [ ] Main window can be resized from the lower-right `//` grip.
- [ ] Window/launcher recover onscreen after resolution change.

## Built-in contracts

### Local Patrol

- [ ] Accept Local Patrol.
- [ ] Progress increases only while the player remains in the originating scene.
- [ ] Zoning away pauses the timer.
- [ ] Returning resumes it.
- [ ] Progress caps at the target.

### Road Check

- [ ] Accept Road Check.
- [ ] Leaving the originating scene marks the contract as away but does not complete it.
- [ ] Returning to the originating scene completes it exactly once.

### Wayfarer

- [ ] Accept Wayfarer.
- [ ] First different scene counts once.
- [ ] Re-entering the same scene does not count twice.
- [ ] A second different scene completes it.

## Daily state

- [ ] Accepted contracts survive restart.
- [ ] Claimed contracts do not reappear as claimable on the same day/scene/profile.
- [ ] Abandon removes an active contract without marking it completed.
- [ ] Next local calendar day produces new occurrence IDs.

## Journal integration

With Journal absent:
- [ ] Claiming succeeds with no errors.

With Journal installed:
- [ ] Claiming writes exactly one `Contract` Chronicle entry.
- [ ] Journal remains optional; Contracts has no hard DLL dependency.

## Provider API

Use a tiny test plugin or reflection console to:

- [ ] register a provider template at priority 100;
- [ ] verify it can outrank the built-in fallback in a one-slot board;
- [ ] accept it;
- [ ] report wrong-context progress and confirm no change;
- [ ] report matching-context progress and confirm change;
- [ ] complete and claim it.

## Preview safety

- [ ] No XP is granted.
- [ ] No gold is granted.
- [ ] No items are created/deleted.
- [ ] No quest/faction state changes.
- [ ] No NPC/Sim movement or combat control.
- [ ] No network requests.
