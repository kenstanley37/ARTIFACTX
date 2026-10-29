# Crashed ships: which ship lies at a crashed-ship site

NMS build 183791 (Forsaken 7.10), traced 2026-10-28/29 with owner-approved research hooks on the Slot 8 test save
(Frida, function entries only, observe-only) and read-only memory reads. Locations of crashed ships are in
`PLANET_BUILDING_PLACEMENT.md` (DistressSignal grid sites, Core `PlanetBuildingSites.SitesNear`).

## Result

A crashed ship does not get a seed of its own: the building picks **one ship of the system's own starship pool**
(`GcSolarSystemData.SystemShips`, `+0x24A0`, 0x40-byte `GcAISpaceshipPreloadCacheData`: TextureDescriptorHint,
Seed `+0x20`, Faction `+0x30`, FrigateClass `+0x34`, ShipClass `+0x38`, ShipRole `+0x3C`).

Checked: Ribhakyam (system `0x0000FE00011476E2`) - Final Eokak III (C Hauler) = pool #1, Exhausted Song LK1
(C Fighter) = pool #5, Iwada's Swift Pathfinder (A Shuttle) = pool #15: **3/3 picked exactly** from their site keys.

## The pick (`0x1417550E0`, called by the building code with the site key)

1. An MWC seeded with the DistressSignal **site key** (the scanner marker's BuildingSeed).
2. Draw 1: ship class 0..11 by the race's row of `GcSolarGenerationGlobals.SpaceshipWeightings` (12 weights per
   race; Gek 0, Vy'keen 1, Korvax 2...): total = sum of the 12 weights (float32, in order), x = frac * total, walk
   the weights subtracting while x > weight.
3. Solar override: when the class is 4 (Shuttle), or in a pirate system, a second MWC seeded with
   `Hash(key, system UA)` (the pool's solar hash: `(ua ^ key) * K; ^= >>47; ^= ua; * K; ^= >>47; * K`) draws once;
   below `PirateClassShipOverrideProbability` (0.1; 0.85 for a Shuttle in a pirate system) the class becomes 8 (Sail).
4. Draw 2 (first MWC): one of the pool ships of that class, `candidates[(n * word) >> 32]`.

## The pool (built by `0x14167C700` when a system is generated)

Core `SystemStarshipGenerator`'s recipe (verified on build 180836 with the portal code and a fixed skip of 41) holds
in this build with two changes: the stream is seeded from the **packed system UA** (galaxy included), and the 21
ship seeds start after a **per-system number of draws**. 21/21 seeds and the Solar override at #17 matched in two
systems; the start was confirmed in 8 systems.

The builder resets the generator stream (`+0x510/+0x514`) from the system seed twice; after the second reset
`0x1416821E0`, `0x141682F00` (6 draws) and the locator step `0x141684990` run, then 3 more draws, then the pool.

### Locator step `0x141684990` (why the start varies)

Places space locators (GcSolarSystemData `+0x2470`). Draws, from stream index 10:
- fixed: 28 draws (2 + 3x2 around the station; then 3, 1 and 4x4 with sin/cos);
- per **primary body** (moons included, prime bodies not; `[+0x2544]`): count = `Next(Max - Min + 1) + Min`
  (`LocatorScatterMinCount` 30, `LocatorScatterMaxCount` 65); per locator 4 draws (direction y = 1 - 2u, phi = u * 2pi;
  distance `u * LocatorScatterMaxDistanceFromPlanet (40000) + 1.5 * shell radius`; type `Next(100) <
  LocatorScatterChanceOfCapitalShips` 5); a locator within `500 + 1.5 * shell radius` of **any body's** centre
  (all bodies, moons and primes too - Core `SolarLayoutGeneration` centres, shell radius with MassiveSolarSystems) is
  dropped, otherwise one more draw (`Next(100) < LocatorScatterChanceOfPirates` 20).

Pool start = 10 + 28 + per-body draws + 3. Checked **8/8 systems**: 835, 242, 887, 858, 1147 (pool starts read
from memory) and 887, 764, 900 (locator step end from the hook, + 3).

## In Core (2026-10-29)

`SystemShipPool.PoolStart` (locator step), `SystemStarshipGenerator.GenerateForSystem` (pool) and
`SystemShipPool.CrashedShip` (pick); settings from the catalog (`SolarLayoutTables.ShipPool`, format 50:
LocatorScatter counts and distance, `CivilianClassWeightings` rows - row 7 is race None - and both Sail chances).
Scratch check against the game: pool start 8/8 systems, 21/21 seeds, 3/3 crashed ships by name and type.
Used by Planet Details (crashed-ship rows) and the Current System starship list.

## The class (Steam build 26295378, 2026-10-29)

The inventory class roll `0x1404D8C00` (one MWC draw from a seed against the system's `GcInventoryTable` class row,
the same roll as multi-tool cabinets: Core `MultiToolSiteGeneration.InventoryClass`) is seeded with the **site key
itself**: an observe-only hook saw key `0x767814A47FF6AE99` go into the pick (`0x141754F00`) and then into the class
roll. Row = system wealth (Low/Medium/High), pirate row in pirate systems (unchecked). Checked 5/5 on Ribhakyam
(medium): Final Eokak III C, Exhausted Song LK1 C (two sites), Iwada's Swift Pathfinder A (two sites). Core
`SystemShipPool.CrashedShipClass`. Because the key's first draw also picks the ship type, a pool ship tends to come
out in the same class at every site, which is why the repeats matched.

Build 26295378 moved the code: pick `0x141754F00`, pool builder `0x14167C520`, locator step `0x1416847B0`.

## More checks (2026-10-29, Steam build 26295378)

- Pirate system Nugepan III (`0x0000C100001476E1`, Korvax, pirate): Toyot's Voyage, C-class Solar, at the predicted
  sites on planet 2 (+6.89, +42.82) and on the prime planet 4 Lora (-4.30, +120.63) - position, name, type (the pirate
  Sail override) and class all exact; The Breath of Providence, C-class Shuttle (a pirate-system Shuttle that stayed a
  Shuttle), at +0.76, +114.06 on Lora; on Lora also Oetsu's Timeless Pilgrim, C-class Explorer, at -29.96, +70.49
  and -7.24, +125.14, and Toyot's Voyage at +1.00, +115.89. 11/11 crashed ships so far, all exact.
- Markers: of three DISTRESS markers on Lora, two led exactly to their ships; the first led to the other Toyot's
  Voyage site on planet 2, while the save kept our entry unchanged (key, Lora's address, position). On Efarien only one
  of three showed and it led elsewhere. So the game sometimes re-targets a DISTRESS marker to another real site
  (maybe the nearest to the player when it takes up the event) - not traced. Both re-targeted markers led to **another
  wreck of the same ship** (Toyot's Voyage on planet 2; Oetsu's Timeless Pilgrim at -29.96, +70.49 instead of -7.80,
  +132.08). The owner noticed it happens when the save was made in space or on the freighter; marked with the save made
  on Lora, both new markers led exactly to their sites.
- Uncharted system Shlifs (`0x0000F900011476E2`, no race): **not solved.** Nothing at the predicted sites on Efarien.
  Read-only memory: the planets' GcPlanetBuildingData (PlanetUA `+0x30`, PlanetRadius `+0x38`, VoronoiPointDivisions
  `+0x40` float, point/sector seeds `+0x44/+0x48`, IsPrime `+0x4D`) equal Core's on all 4 bodies, and the system pool
  (21 ships) equals Core's. But the live slot tables hold only 8 building types (Efarien: one slot 70 and the
  DistressSignal slot 200; Yeqo Gamma: Shelter 20-28, slots 70-74, 200, Base 220-221), so Core's slot table is wrong
  for uncharted systems, and the predicted DistressSignal at -3.41, -16.72 was still empty in game.

- Not every distress wreck is claimable: a Planetary Chart on Lora led to Ayase's Blazing Adventurer at +11.75, -175.28
  with its Korvax pilot nearby (repair it, no claim). That site is class 33 NPCDistressSignal on Core's grid at
  exactly those coordinates. ArtifactX lists only class 20 (DistressSignal), the claimable wrecks; NPC wrecks are left
  out until players ask for them.

## Still open

- Prime-only / gas-giant systems (layout not covered), pirate systems' flag (`[+0x2e17]`), uninhabited systems' row 7.
