# Shared normal-ship comparison support

2026-10-31: extended the catalog guard and card integration from Exotics to
Shuttles, Fighters, Haulers and Explorers. The final arithmetic was already
checked for all native classes. Card integration had still hardcoded Royal/S;
it now passes the actual ship type and shown class.

The catalog scene traversal now checks every encountered custom inventory
component for DesiredTechs, independently of whether its size tag resolves.
Failed required scene or attachment reads leave an unknown guard. An override
anywhere in a type's scanned scenes conservatively disables the normal pipeline
for that type; this does not infer absence from the selected size alone.
Installed-game scans found no desired-tech overrides for all seven scanned
models. Solar and Sentinel remain excluded because their category and initial
technology paths are not the normal-ship selector, even with empty DesiredTechs.
Catalog format 53 records the guards; older catalog data cannot establish them.

Additional isolated native checks: 16 composed cases (four normal classes x
four ratings), four final values each, all 64 floats exact. They reuse one
explicit reference layout, six selected technologies, intrinsic input values,
model seed and active player beam context. Supercharged slots are regenerated
for each rating. The harness explicitly supplies photon range, ignores runtime
weapon overrides, and materializes module rolls using the checked Core rolls.
This verifies composition with class/control changes, not 16 independently
located and confirmed in-game wrecks. Fixtures and extracted game data remain
local. Unknown, missing and overriding model guards have synthetic checks.

In-game follow-up: the owner's screenshots show S-class Shuttle The Skylark of
Gravity on Obbiellub Uefuk at -38.14, -135.47 (system portal 0034FF67B291).
Compare its four values before repair with the updated card. Record the active
player ship and installed technology if any value disagrees. No save writes,
app launch or UI clicks were used for these checks.

Default Core checks passed; isolated WinUI build passed with zero errors.


## Owner in-game confirmation

The owner supplied location and Compare screenshots for two wrecks on
Obbiellub Uefuk, system 0034FF67B291, with MainShip (Fighter) active:

| Wreck | Coordinates | Damage | Shield | Range | Maneuverability | Units |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| The Skylark of Gravity (S Shuttle) | -38.14, -135.47 | 191.7 | 200.1 | 128.3 | 336.7 | 12,650,000 |
| The Master of Sleep (S Exotic) | +8.84, +34.77 | 353.1 | 283.0 | 182.0 | 513.0 | 41,000,000 |

All eight displayed stat values and both unit values match the app screenshots.
The Shuttle location screenshot reads -135.46 from the player's position beside
the wreck, consistent with the app site coordinate rounded to -135.47. This is
confirmation for these wrecks and active ship, not every model or runtime state.
