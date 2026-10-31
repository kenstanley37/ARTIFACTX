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
