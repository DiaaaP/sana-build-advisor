# Master prompt status — Sana Build Advisor 0.14.3

This file is the honest completion matrix for the requested master plan. “Done”
means implemented and compiled. It does not mean the behavior has passed every
in-game, resolution and multiplayer scenario.

## Done through 0.14.3

- Three UI modes, F8 toggle, hold-Alt interaction and instant return to gameplay.
- Live character stats, selected skill variants, inventory, run context and Q slot.
- Separate build goal and risk style; automatic/manual Command mode.
- Deterministic item/equipment ranking with explicit evidence status and reasons.
- Practical target, white/green/red alternatives and long-term goal.
- Actual revealed nearby choices: terminals, printers, ground pickups and Command
  tier essences. Unknown choices stay visible with status `UNKNOWN`.
- Initial lifecycle archetypes for Huntress Flurry and Railgunner crit conversion.
- Stage/difficulty readiness for damage, HP, defense and mobility with explicit
  heuristic labels rather than fabricated win percentages.
- Threat profiles for first-hit damage, damage tempo, burst survival, sustained
  survival and crowd clear; exploration/swarm/teleporter/boss context.
- Marginal stack return and risk-style soft limits for supported linear effects.
- Explicit immediate and long-term plans, plus local death review history.
- Live fallback profiles for the full installed ItemCatalog, what-if previews,
  world markers, printer/scrapper/cauldron advice and adaptive risk scoring.
- Build DNA, generic routes for every survivor profile, team roles/gaps, visible
  enemy radar, next-stage forecast, run graph and local post-run export.
- Read-only team snapshots and recipient comparison when remote Inventory is visible.
- Live DLC-aware inventory: Void plus Hallowed Concepts tier groups `Hex`, `Cursed`,
  `Page`, `FoodTier`, `AssignedAtRuntime`; icons come from the live PickupCatalog.
- Settings persistence for hotkey, position, size, scale, opacity, goal, style and mode.
- Pure engine suite (125 assertions), fail-safe build/install and dated DLL backups.
- 49 scored item rules and 23 Q rules, including all 11 main Hallowed items.
- Open choice picker inspection, shop price display and gold affordability status.
- Armor-based EHP model, OSP state, regeneration, buff/debuff counts and artifacts.
- Hover tooltips, position locking and synergy notices.

## Partially done

- Item knowledge: 49 scored rules; every other item is displayed but unscored.
- Equipment knowledge: 23 scored Q rules; every other nearby Q is displayed unscored.
- Build archetypes: two narrow verified routes and two explicitly experimental DLC routes.
- Team mode: compares stage and threat profiles visible on this client; remote
  synchronization is not proven.
- Damage models: first-hit and tempo proxies use live stats and supported item
  mechanics; they are not a skill-by-skill combat simulator.
- Survivability models include ordinary armor EHP, supported block/regen/healing
  coverage and encounter pressure; bypass damage and exact enemy attacks remain unknown.

## Deferred until evidence and runtime tests exist

- Complete formulas for every catalog item; unsupported effects remain visible and UNKNOWN.
- Proc-chain simulation, luck, attack-specific proc coefficients and real skill DPS.
- Verified Seeker/Sojourn, Acolyte and every-survivor archetype library.
- Non-money shop affordability and a full exchange-value model for printers/scrappers.
- Multiplayer validation matrix: host/client, modded/unmodded peers, death, reconnect,
  stage change and different inventory visibility states.
- 16:9/21:9/4K/UI-scale visual pass, long-run profiling and localization pass.

## Release rule

The project remains pre-v1. It must not be labelled v1 until the deferred runtime
matrix is executed and the expanded mechanics have direct evidence plus tests.

