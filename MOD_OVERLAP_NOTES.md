# Mod overlap check - 2026-08-12

Target concept: Local/Global old-MMO side contract board with provider-backed verified objectives.

## Current public Erenshor scene

The public Thunderstore Erenshor category showed 57 packages during the August 12, 2026 check.

Targeted searches did not surface a current package whose core feature is:

- a location-aware daily/local contract board;
- a provider API for other mods to register verified contract objectives;
- a local contract activity layer separate from the native quest log.

### Nearby projects, deliberately not duplicated

**AdventureGuide**
- quest walkthrough/navigation companion;
- 170+ quest walkthroughs, item sources, world markers/GPS.
- Contracts does not provide walkthroughs, quest answers, GPS, or markers.

**ErenshorQoL**
- commands, QoL and automation.
- Contracts does not replace bank/forge/auction/guild commands.

**GuildNamePlates**
- guild-name display customization.
- Contracts does not alter nameplates.

**Recks PvP**
- real hostile combat with SimPlayers.
- Contracts does not create combat.

**forgetwhtuno Erenshor PvP / Practice Duels**
- own their combat and result authority.
- Contracts may later consume their sanitized semantic result events; it does not reproduce combat.

**forgetwhtuno Crafting expansion**
- owns recipes, gathering, crafting actions and crafting orders.
- Crafting Orders should remain in Crafting. Contracts is a broader adventure board and can optionally consume verified Crafting progress.

## Boundary

0.3.0 still avoids a built-in generic kill tracker, item scanner, quest parser, crafting observer, or party-state objective. The expanded built-in catalog uses only logical-zone transitions and active-play time. Built-in reward policy remains limited to the isolated XP adapter, which is OFF by default pending current installed-assembly verification; gold/items remain outside Contracts until their native authority/catalog gaps are independently closed. These event-detection and mutation areas are exactly where overlap or inferred state would be riskiest.

Provider-backed objectives let the mod that already owns the fact report it once.
