# Planet shop cabinets (minor settlement multi-tools)

Steam build 26295378 (Forsaken 7.10 + 2026-10-29 patch), Slot 8, Mephar VI (planet 2 of system `0x00003400FF67B291`,
Korvax, medium wealth). Method: owner-approved observe-only hook on the inventory class roll `0x1404D8C00` (function
entry only, build and code bytes checked), then a brute-force search of whole-unit world positions.

## Found 2026-10-29

- The shop sites are Core's building grid (class 5, Shop): both minor settlements stood at predicted sites
  (`-38.56, -52.86` and `-40.50, -57.31`); the live slot table equals Core's for every standard class (2..35), so
  class 5 really is Shop. (Rows 52, 53, 56, 60, 61, 62 - sentinel distress, robot camp, ancient guardian, cargo drop,
  scrapyard, crashed swarm - were absent in game: their conditions aren't modelled yet. The Sentinel Pillar is
  class 51 DroneHive, at the predicted site within 1 m.)
- The cabinet's class roll seed is the position hash Core already has (`MultiToolSiteGeneration.SeedForPosition`:
  each world coordinate rounded half away from zero, `Combine(Combine(x, y), z)`, system space). For each seed
  exactly one whole-unit position in a 1 km cube matched:

| Shop site | Seed | Cabinet (system space) | Visor | Above sphere | Terrain (Core) at cabinet / site | Sideways | Class (game) |
|---|---|---|---|---|---|---|---|
| -38.56, -52.86 (`E6D3994BBD665684`) | `21D4EC1C705BDBE6` | -139241, 81731, -620490 | -38.57, -52.83 | 77.4 | 44.8 / 54.5 | 28.4 m | A (Molecule Obstructer 79C-5/57P) |
| -40.50, -57.31 (`FF7DAE84AA18D8B5`) | `275BD0D32348BCBC` | -140306, 80074, -624489 | -40.50, -57.31 | 38.8 | 34.6 / 34.8 | 2.6 m | C (Yoshk's Waveform Obstructer) |
| -35.09, -53.40 (`4BA49F342178350A`) | `26F86F361BC54F51` | -141441, 84845, -619464 | -35.10, -53.39 | 112.0 | - / 116.6 | 16.9 m | C (Structural Skaiush Puncher, pistol) |

Class from the seed with the medium-wealth row (49/35/15/1): A, C, C - 3/3 as the game showed.

Offset of each cabinet from its site point on Core's ground (east, north, up, metres): shop 1 (27.1, -8.4, +22.9),
shop 2 (2.5, -0.9, +4.1), shop 3 (14.5, -8.7, -4.6). No common direction, so the settlement layout and/or yaw vary;
shop 3 sits 4.6 m below Core's ground at the site (terrain on a slope, or the settlement levels the ground).

  Planet centre `(-99000, 122000, -651000)` (Core layout floored to 1,000), radius 64,512, sector seed 7015.

Not explained by the site's starting yaw alone (top 3 bits of the key's first draw, 45 degree steps): the three
offsets keep different lengths (28.4, 2.7, 16.9 m), so each settlement uses a different layout.

The shop L-system survey (`planet-shop-survey` from `feature/planet-multi-tool-locations`, rerun 2026-10-29 on a
detached merge with today's libMBIN; output kept local) lists 7 Korvax (Explorers) weapon-crate references in
SHOPSCIENTIFIC: `Roof_Shop` (ROOFMODULE_SHOP, crate about (-4.83, 3.1-3.2, 4.83) in the module, probability 25, three
variants), and `Layout_NPC`, `LayoutShop_1..3` (crate at the WEAPONSCRATE_ locator, probability 100). Shop 1's cabinet
(22.9 m up) fits a roof module, shops 2 and 3 (about 4 m and ground level) fit ground-floor layouts. The positions are
relative to each rule's entry model, so the cabinet needs the L-system's module choice and stacking from the site
seed - native tracing of the L-system builder.

## Still open

The cabinet's offset from the site point: the settlement's layout (L-system choice) and where its weapon crate sits,
the building's rotation, and its height (shop 2's cabinet is ~4 m above Core's terrain at the site; shop 1's is
~23 m above it, so a different layout or level). More cabinets, then the layout and yaw rules from NMS.exe.
