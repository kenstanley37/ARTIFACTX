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
