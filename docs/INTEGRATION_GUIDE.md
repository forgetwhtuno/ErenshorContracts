# Contract provider integration

Contracts is intentionally not the authority for most gameplay facts.

A provider should report progress **after the provider itself has verified the event**.

## Reflection shape

Resolve:

```text
assembly: any loaded assembly
type: ErenshorContracts.ContractBoardApi
```

Methods:

```text
RegisterTemplate(
  string providerId,
  string templateId,
  string zoneScope,
  string title,
  string description,
  string progressChannel,
  string progressKey,
  string contextFilter,
  int target,
  int priority,
  string rewardText
) -> bool

ReportProgress(
  string channel,
  string key,
  int amount,
  string context
) -> bool
```

## Suggested channels

These are conventions, not authority:

```text
crafting / food
crafting / recipe
gathering / forage
gathering / mine
gathering / fish
combat / kill
combat / named_kill
duel / win
pvp / win
travel / expedition
guild / activity
```

Do not report a generic `combat/kill` because text said something died. Report it only from a verified gameplay event.

## Zone scope

- `*` = eligible in every playable scene.
- exact Unity scene name = eligible only there.

Do not invent display-name-to-scene mappings in Contracts. A provider that owns location data may register exact verified scene names.

## Context filter

If blank, any matching channel/key progresses the contract.

If nonblank, `context` must contain the filter text case-insensitively.

Example:

```text
channel = gathering
key = forage
filter = Silverleaf
```

Only a report such as:

```text
ReportProgress("gathering", "forage", 1, "Silverleaf")
```

counts.

## Rewards

`rewardText` is presentation only in 0.1.0.

The board does not grant native XP/gold/items.

A later verified reward adapter should remain separate from progress detection and must use current native game operations rather than direct save-file mutation.
