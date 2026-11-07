# Industrial Cleanup guide - 2026-11-03

## Evidence and scope

Installed game build 184271. Read the player's installed PAK files through ArtifactX.GameData. No saves or player databases were changed for this investigation.

`METADATA/SIMULATION/MISSIONS/TABLES/TUTORIALMISSIONTABLE.MBIN` contains `HAUL_TUT_INTRO`, `HAUL_TUT` and `HAUL_LOOP`. The introduction checks for installed `GRAVITYGUN` technology while on a planet. The tutorial contains four localized objective/tip groups: investigate a processing centre, acquire the Gravitino Coil, install a Colossus Flatbed module, and process industrial waste. The guide presents their extracted text; it does not simulate mission activation or claim to identify a specific plant.

`UI_GUIDE_BODY_SCRAP` supplies discovery guidance: scan planets from space and use the Exocraft Radar while driving the Colossus on a scrap world to locate plants and heaps. The tutorial explains valuation-scale automatic recycling, special containers, and manual sorting for maximum resource recovery. Exact vendor/acquisition routing for the Coil is not established here; the guide retains the game's own acquisition objective without inventing a vendor.

The `HAUL_LOOP` FormatStat mappings match the owner's mission-log screenshots:

| Text token | Global stat |
| --- | --- |
| SCRAP_VAL | CARGO_VALUE |
| SCRAP_NUM | CARGO_SCRAP |
| TOX_NUM | CARGO_TOX |
| RAD_NUM | CARGO_RAD |
| EXP_NUM | CARGO_EXP |

Read these from the loaded editor session's global stat group (`vLc/6f=/gUR`, group `:rc = ^GLOBAL_STATS`, nested `gUR`, stat ID `b2n`, numeric wrapper `>MX`). Empty numeric wrappers mean zero. Missing groups/counters remain unavailable. This is lifetime recovery, not mission completion. Notifications from another running slot must not replace the loaded slot's totals.

The existing extracted title/customization definitions supply six Colossus requirement associations. These requirements do not prove ownership or successful reward granting. The separate unlock editor remains the place to change unlocks. Expedition hauling distance and community targets are excluded.

## Implementation

- Core: portable guide records and read-only numeric counter reader.
- GameData: catalog format 59, mission/localization extraction into an Industrial Cleanup projection.
- WinUI: Reference page composed from a UserControl and ViewModel; collapsible discovery, lifetime totals and reward sections. Catalog/session notifications update rows in place without rebuilding the page.
- No loaded save is required to use the catalog guide.

## Validation

- Installed-data catalog check passed: four tutorial objectives, discovery text and exactly five mapped lifetime counters.
- Core checks passed, including global group isolation, integer/float/empty/malformed values, absent data and no JSON mutation.
- WinUI CLI build passed with zero errors. Existing unrelated warnings remain.
- App visual and in-game guide testing are pending owner testing; the agent did not launch the app.
