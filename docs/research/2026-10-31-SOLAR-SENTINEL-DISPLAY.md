# Solar and Sentinel comparison investigation

2026-10-31: native selector 0x1404D7FCB checks ship class 8 and inventory category
5 before admitting SOLAR_SAIL. Other normal ship classes skip it before their
candidate RNG draw. Solar adds the row in source table order, then uses the same
ranking/budget/placement logic. Native selection has six Always technologies:
SHIPJUMP1, LAUNCHER, HYPERDRIVE, SHIPSHIELD, SOLAR_SAIL, SHIPGUN1, followed by
selected extras. The extra cell changes Photon Cannon and upgrade positions.

Core derives the Solar candidate list from the existing complete definition lookup
and the normal eligible IDs, preserving source order. No catalog format change or
new extracted content is needed beyond format 53. Missing Sail metadata fails
closed. Broken-slot accounting also skips the extra occupied Solar technology.

Checks: four wealth cases match native selected IDs, positions and supplied
materialized module records (7/8/9/8 installed technologies). The Solar technology
damage matrix matched 840 native flags. Four composed Solar class-rating cases
matched all 16 final floats exactly. Those composed cases share an explicit
reference layout/model seed/intrinsic values/player context; they do not establish
an independently located Solar wreck match. Harness scope retains supplied photon
range, no runtime weapon override and checked Core module materialization, as in
the normal-ship checks. Game data and fixture JSON remain local.

Sentinel native selection is different: RobotShip, AllShips and AllShipsExceptAlien
categories, yielding LAUNCHER_ROBO, SHIPJUMP_ROBO, HYPERDRIVE_ROBO,
SHIPSHIELD_ROBO, LIFESUP_ROBO, SHIPGUN_ROBO and selected extras. The inspected
wreck initializer at 0x141747908 zeros both initial technology and slot damage
chances for ship class 9. Quest-stage damage and the additional ROBOT_SHIP
intrinsic mapping have not been composed or validated. Sentinel comparison stats
remain disabled; no intact-loadout approximation is exposed.

No app/game launch, UI automation, real-save writes or manual player-data changes.

Verification: default Core checks passed; previous 8 reference and 64 normal
composed values still match; isolated WinUI build passed with zero errors.
Solar app/game comparison is still pending owner testing.


## Solar screenshots supplied by owner

The Departure of the Abyss, S Solar, Nihon 64/S7 (planet 3), Euclid,
portal system 00A7FF67B291, site B6129900574CA7E1 at
+32.712911, +28.100458, model F503B0B40FB6F4C0. Active player ship MainShip.
The owner supplied the app card, visor location, initial interaction inventory
and Compare screens before repairs. The card still showed intrinsic bonuses;
the screenshot alone does not establish which binary produced it.

Read-only evaluation of this exact site using the installed catalog and saved
active ship reproduces every displayed game value:

| Stat | Calculated float | Game display |
| --- | ---: | ---: |
| Damage Potential | 309.86874 | 309.9 |
| Shield Strength | 218.16382 | 218.2 |
| Hyperdrive Range | 143.52567 | 143.5 |
| Maneuverability | 559.2687 | 559.3 |

The generated technologies match the screenshot positions: SHIPJUMP1 (1,0),
LAUNCHER (2,0), HYPERDRIVE (3,0), SHIPSHIELD (4,0), SOLAR_SAIL (5,0),
SHIPGUN1 (1,1), UP_S_SHLX#19494 (3,1). The shield upgrade roll is 0.07181981.
Intrinsic inputs: 62.92413 / 28.408072 / 42.525665 / 38.490273.

The composed check runner now accepts an explicit CompareDisplayedValues flag
for screenshot fixtures, using one decimal instead of requiring unobservable
float bits. Native fixtures retain their bit-exact comparison. The local Solar
screenshot fixture passes 4/4 displayed values; the 8 reference native values
and 16 composed Solar native values also still pass. Fixture contains extracted
catalog/player data and remains outside the repository. No production code or
catalog format change was required for this confirmation.

Further Sentinel inspection: INVENTORYTABLE maps ROBOT_SHIP (and ALIEN_SHIP)
to Unspecified, LessIsBetter=false, not the displayed stat targets. The owned
Sentinel in the read-only Slot8 snapshot has its six core technologies intact;
that repaired/owned state cannot establish an unclaimed salvage state. Sentinel
support remains guarded pending its complete loadout and salvage-state check.

The owner's new MAUI project, solution and shared package edits were left alone.


## Sentinel reference and marker report supplied by owner

Uncharted Hamagan-Oerat VI, planet Neusie T13 (Euclid): first predicted marker
led elsewhere and no wreck was present; later markers did not appear. App listed
1,034 Sentinel sites and showed its no-race warning. Owner closed the game before
marking further sites; failures persisted. Discoveries explicitly showed Lifeform
Uncharted, economy/conflict unavailable. The report is posted on issue #34:
https://github.com/kenstanley37/ARTIFACTX/issues/34#issuecomment-6546742085

After changing to Usmanz XIX (Vy'keen, Power Generation economy), owner reports
markers worked correctly. Ciss L16, dissonant volcanic moon, portal
41E6FF67B290: app predicted C-class Sentinel at +31.22, -0.00, 28,600,000 units.
Visor beside the wreck reads +31.23, -0.00. Interaction title begins Blessed;
visor name differs (x-Blessed). No exact name-generation claim follows from this.

| Capture | Damage | Shield | Range | Maneuverability |
| --- | ---: | ---: | ---: | ---: |
| Initial visor | 181.1 (70.3 core) | 171.6 | 601.0 (101.0 core) | 284.9 (214.1 core) |
| Interaction / Compare | 195.3 | 171.6 | 601.0 | 284.9 |

The initial salvage interaction shows a damaged Pilot Interface, plus Pugneum,
Salvaged Glass and Hyaline Brain extraction items. Later inventory/Compare shows
seven installed technologies, no visible damage warnings, C class, 34 cargo and
24 technology slots; displayed unit value matches 28,600,000. MainShip is active
on the Compare screen. The captures do not isolate whether salvage progression,
repair, timing, active-ship context or evaluator differences caused the damage
value to change; do not equate the two measurements or infer a controlled diff.
These are game reference values, not yet matched Sentinel predictions.

No request for duplicate Solar captures is needed. MAUI/shared project edits
remain outside this task. No app/game launch or save edits by the agent.


## Sentinel Compare composition

The exact Ciss L16 site is 61C166403652916F, +31.216607, -0.004844,
system 01E6FF67B290, pool model A96ECE0A3E6650C7, native wealth 1, C class.
Intrinsic values: 10.034034 / 5.60515 / 0 / 9.125851. Tech layout requests
24 slots; the seven selected technologies occupy (2,0), (3,0), (4,0), (5,0),
(6,0), (1,1), (2,1), matching the supplied screenshot.

Catalog format 54 stores the native Robot eligibility list: RobotShip, AllShips,
AllShipsExceptAlien; 51 regular and 50 procedural candidates in table order.
It also retains ROBOT_SHIP and ALIEN_SHIP intrinsic mappings (Unspecified).
The normal definitions lookup already includes these technologies.

The comparison inventory regenerates its special technology cells at native
0x1408F6993..0x1408F69BA after copying the site inventory, using the comparison
inventory seed held at object+0x10 (stored by 0x1408F861A). For the supplied
Sentinel reference the site-seeded C slot is (2,1), exactly the highlighted rocket
cell in the screenshot; model-seeded placement instead gives (3,1). Core uses the
site seed for Sentinel Compare; other ship paths retain their validated behavior.

With the site special slot, production calculation gives 195.30984 / 171.59428 /
601 / 284.90802, matching all four Compare displays. With the model special slot,
it gives 181.0709 damage, matching the earlier visor display; the other three
values are unchanged. This supports the evaluator/materialization difference,
not an invented repair bonus. LIFESUP_ROBO (Pilot Interface) contributes only
stat 161, outside the displayed targets; it does not explain the damage change.

Additional checks: four wealth loadouts match native selected technology IDs
and positions. This site selects seven regular technologies in each case, so
these four cases do not add procedural-module coverage. Four class-rating compositions match all
16 native float outputs. The composition harness explicitly supplies the
screenshot-verified unlocked-slot mask: its generic standalone layout bootstrap
did not reproduce that mask, so these checks do not independently establish
Sentinel layout generation. Production layout is checked against this screenshot.
The native composition also retains the prior photon-range override, supplied
Core module materialization and no runtime weapon overrides. Fixture data stays
local. A separate screenshot fixture uses one-decimal comparisons and passes 4/4.

Sentinel cards use the Compare values, not visor values. Their names remain
unknown. Uncharted Sentinel stats remain guarded because location/marker tests
failed there; the existing issue #34 remains open. MAUI/shared project edits and
real player data remain untouched.

Final verification: default Core checks passed; 8 prior reference, 64 normal,
16 Solar and 16 Sentinel native float values match. Sentinel screenshot matches
4/4 displayed values. Isolated WinUI build: zero errors, existing warnings only.


## Additional owner confirmation before merge

B-class Sentinel on Ciss L16, app coordinate +36.33, -60.08 (visor beside the
ship +36.34, -60.06), interaction title alpha-Glass Soul. App, visor, initial
inventory and Compare all show 287.1 / 178.7 / 646.5 / 327.4. Class B and
41,500,000-unit value match. MainShip is active on Compare. This independently
confirms the B-class case in game, in addition to the earlier C-class reference.
The earlier visor/Compare damage difference is not present in this B reference.
