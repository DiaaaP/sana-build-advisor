# SANA Build Advisor 0.14.3

Read-only helper for Risk of Rain 2. It reads the local game state and explains
item/equipment choices; it never grants items, edits stats or sends network data.

CONTROLS
- F8 shows/hides the advisor. The key can be changed to F9/F10 in Settings.
- Hold Left Alt or Right Alt to click, scroll and drag the window.
- Release Alt to return mouse control to the game immediately.
- Top buttons switch icon / compact / expanded window modes.

WHAT 0.14.3 DOES
- Updates ordinary ground loot every 0.30 seconds even without holding Alt;
  expensive Command/picker and airborne-droplet scans remain separately throttled.
- Detects visible hidden-catalog boss rewards and both flying and landed yellow loot.
- Loads the current recommendation icon first, retries transient Addressables
  failures, and heals icons after scene assets are unloaded.
- Separates build goal (balanced, damage, survival, mobility, OP, strategy)
  from risk style (safe, balanced, aggressive).
- Shows a practical next target plus per-rarity alternatives and a long-term goal.
- Separates the immediate visible choice from the long-term build target.
- Shows stage readiness plus first-hit damage, damage tempo, burst survival,
  sustained survival and crowd-clear bars with hover explanations.
- Detects exploration, swarm, active teleporter and boss contexts and adjusts
  supported AoE, defense and boss-damage recommendations.
- Calculates marginal stack return for supported linear effects and labels each
  next stack as strong, useful, weak or saturated.
- Profiles every installed live item by ItemDef tags when an exact formula is not
  available, and clearly labels those rankings as estimates.
- Previews supported bar changes before a nearby/Command item is taken.
- Shows read-only world markers, build DNA, synergy gaps, overinvestment warnings,
  next-stage/teleporter forecasts and nearby printer/scrapper/cauldron advice.
- Builds a run trend chart, host damage summary and local post-death report.
- Reads visible nearby shop terminals, printers, ground pickups and Command tier
  essences within 70 m. If a supported option is really nearby, the compact HUD
  shows BEST FROM NEARBY LOOT with source and distance.
- Advises on 23 Q equipment choices and compares visible supported equipment.
- Detects narrow verified Huntress Flurry and Railgunner crit archetypes with
  SEARCH / ACTIVATION / STRENGTHENING / SATURATION / BLOCKED phases.
- Shows team snapshots from standard NetworkUser/Inventory objects and suggests
  a recipient for the current supported item when remote inventory is visible.
- Ranks the weakest teammate using both stage readiness and threat-profile bars;
  AoE items can be assigned to the player with the largest clear deficit.
- Stores a local death review in BepInEx/config/SanaBuildOverlay-history.tsv.
- Displays every item present in the live inventory, including Void and Hallowed
  Concepts tiers. Tier sorting supports Hex, Cursed, Page, FoodTier and runtime
  tiers. Unknown DLC effects are marked UNKNOWN instead of receiving fake scores.
- Restores window position, mode, scale, opacity, goal, risk and mode settings.
- Labels advice evidence: VERIFIED, CALCULATION, ESTIMATE, EXPERIMENT, UNKNOWN.
- Scores 49 item rules: core white/green/red choices, selected Lunar and Void
  choices, and all 11 principal Hallowed items from the installed DLC4 catalog.
- Reads exact options from an opened Command/choice picker and reads shop prices;
  gold purchases are marked affordable or currently too expensive.
- Shows armor reduction/amplification, conditional EHP, OSP state, regeneration,
  active buff/debuff counts and all enabled artifacts without changing them.
- Includes experimental Seeker/Sojourn and Acolyte/Transcendence routes behind
  the Experimental Advice setting. Their unverified behavior is clearly labelled.

LIMITS
The scored item engine currently contains 49 rules and the Q engine 23 rules.
Other live items still appear in Inventory and Nearby Loot, but are
not scored. Proc chains, attack-specific coefficients, exact boss HP/time-to-kill,
bypass damage, all survivor archetypes and all Hallowed mechanics are not claimed.
Host/client inventory completeness needs an actual multiplayer check. Successful
compilation does not make this a v1 release.

BUILD AND INSTALL
For the easiest Windows x64 setup, download
`SanaBuildAdvisor-0.14.3-Windows-OneClick.zip` from `packages`, close the game,
and extract the archive directly beside `Risk of Rain 2.exe`. It already contains
the official BepInEx 5.4.23.5 release. Start the game normally through Steam and
press F8; there is no separate EXE to run. Friends do not need this helper
to join because it does not add network messages or game content. Advice and UI
exist only on the PC that has the DLL. Remote inventory visibility can differ for
host and client, so team recommendations still need the multiplayer test matrix.

If BepInEx 5 is already installed, use the smaller regular package and copy only
`SanaBuildOverlay.dll` to `BepInEx\plugins\SanaBuildOverlay`.

`Sana-RoR2-Modpack-2026.10.22.1-Windows-OneClick.zip` contains BepInEx and all
three Sana mods in one archive: Quick Items, Build Advisor and Risk Upgrader.

Run from PowerShell without changing the machine policy:
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Install
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\package-oneclick.ps1
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\package-all-in-one.ps1

The installer refuses to replace the DLL while Risk of Rain 2 is running and
creates a dated backup beside the installed DLL.

Manual installation: close the game, create
  BepInEx\plugins\SanaBuildOverlay
inside the game folder and copy only SanaBuildOverlay.dll there. To update,
replace that DLL while the game is closed. To uninstall, remove that folder.
Never place two versions of SanaBuildOverlay.dll under BepInEx\plugins.

VALIDATION
- Pure engine suite: 125 assertions passed.
- Compiled against the 79 assemblies from this installed game: passed.
- The installed 0.9.0 build passed a real active-run log smoke test.
- The earlier 0.9.1 build passed startup and live catalog validation:
  49/49 item rules and 23/23 Q rules, with no missing IDs or tier mismatch.
- Built and installed SHA256 match. In-run UI and multiplayer scenarios remain
  the manual acceptance matrix in TESTING.md.

See MASTER_PROMPT_STATUS.md for the exact implementation matrix and deferred
work. See DEVELOPMENT.md for technical evidence and TESTING.md for acceptance.


