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
4. Draw 2 (first MWC): one of the pool ships of that class, `candidates[(n * word) >> 32]`; when the pool has no ship
   of that class (a Sail in a system without Solar ships) the same draw picks from all 21 standard pool ships
   (`0x141755588`). Checked on Mephar VI: Hyper Meyame XIX, S-class Fighter (pool #4), at -50.42, -44.65 - the first
   S-class wreck predicted and found. 12/12 crashed ships so far.

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

## Slots and stats (2026-10-29, Steam build 26295378)

Hook on the inventory builder `0x1404D5E90` (entry only) at Josonix's Defeat BO3, Mephar VI -2.78, -160.56
(site key `AD5FA885AD4CB86F`, predicted B-class Explorer - exact): the wreck's three inventories (kind 4 cargo,
5 tech, 6) are all built from **the site key** (`[r8]` = key, stored at inventory `+0xE0`), class at `+0x100` = 1 (B).
Slot count at `+0x84`: cargo 0x1B = 27, tech 0x11 = 17 - the game showed "Explorer (27, 17)". Damage 104.0, shield
197.9, hyperdrive 143.8, maneuverability 384.3 shown in game, roll not traced yet. Next: port the slot and base-stat
rolls (class + type + site key), check against the 13 wrecks seen in game.

The S-class Skylark of Gravity marker (+29.18, +145.63) led to this B-class Explorer instead - re-targeted markers
don't keep the ship either.

## Save Beacons instead of markers (2026-10-29, Slot 8)

A Save Beacon placed in game is a loose object in `vLc.6f=.c5s` (GcPersistentBBObjectData): `r<7` "^BUILDBEACON",
`oZw` planet address, `wMC` position from the planet centre (radius = planet radius + ground), `wJ0` up = the outward
normal, `aNu` facing, `b1:` Unix time, `CVX` 0, `ofi` RegionSeed. Core's ground height at the real beacon: 64,532.64
vs 64,532.74. Two beacons written by script (game closed, backups kept) at predicted S-class wrecks, one with `ofi`
copied and one with `ofi` 0: both showed on the HUD from 68 / 79 km - `ofi` doesn't matter for display.

At the first (+29.18, +145.63, The Skylark of Gravity) there was **no wreck**: the site is inside a mountain. The
predicted shelter 290 m away stood at +29.10, +145.36 (predicted +29.10, +145.35), so the grid is right - the game
**rejects sites by terrain** and Core doesn't yet (the same site's marker had led to a different wreck). Next: port
the regular building site's terrain accept test (the portal work ported the flatness test 0x14128C350 and the override
accept test 0x141631160; the regular site builder's rule is untraced), then offer Save Beacons in the app.

Second test beacon (ofi 0) at The Master of Sleep (-69.22, -125.55): the wreck stood there exactly as predicted (S-class
Exotic, cost 41,000,000, "37 / 21", damage 353.1, shield 324.6, hyperdrive 182.3, maneuverability 507.6) and the beacon
object was inside the hull - so `ofi` 0 works and beacons must be offset (owner: beacons may float; ~30 m above).
Claimed: name/class the same, shield 324.6 and hyperdrive 182.3 the same, damage 305.7 and maneuverability 534.5
changed (installed damaged tech counted differently after the claim - not traced). 14/14 wrecks found as predicted;
the one empty site was the mountain site.

## Ground test (2026-10-29)

Height and flatness (Core `PlanetTerrain`) at the crash sites seen in game: 12 real wrecks have slope 0.00-0.45 on the
64 m ring (35 m on the prime planet Lora); the empty mountain site has 1.43. The override accept test's land rule
(slope <= 1, not under the sea) separates them, so Core `PlanetBuildingSites.GroundAllows` applies it to regular crash
sites (assumed shared, not traced). It drops 17/1,039 sites on Ribhakyam, 17/447 on Mephar VI and 321/1,062 on Lora
(mostly under its sea), ~0.2 s per planet. Next: more empty/real sites in game to confirm, then the traced rule.

More in-game checks (2026-10-29, Mephar VI, markers from the app): Hyper Meyame XIX S-class Fighter at -13.64, -44.15
(15,650,000, 29/18, 443.7 / 242.8 / 101.0 / 504.9) and The Skylark of Gravity S-class Shuttle at -24.64, -73.61
(predicted -24.63; 12,650,000, 32/19, 217.2 / 227.2 / 130.7 / 332.4) - 16/16 wrecks found as predicted. The marker on
the empty mountain site (+29.18, +145.63) again led to Josonix's Defeat BO3 at -2.78, -160.56: **a marker on a site
with no wreck is moved by the game to a real one** - the cause of the wandering markers. That wreck's stats were
identical on both visits (6,400,000, 27/17, 104.0 / 197.9 / 143.8 / 384.3): stats are fixed per site.
The Master of Sleep was back at its site after being claimed, with identical stats - claiming doesn't remove the
wreck. With the ground test the app lists 4 S-class wrecks on Mephar VI (the mountain site gone); all 4 seen in game.

## Underwater wrecks (2026-10-29, not checked in game)

Underwater wrecks are their own class, 32 WaterDistressSignal (WaterPlacement 3 UnderwaterOnly, OverrideRadius 20).
Core lists them with the override test's underwater rule (at least 35 m below the sea, slope <= 1) and the same ship
pick and class as land wrecks (assumed). Lora: 207 of 985 sites pass; Mephar VI (no sea): none. No planetary chart event
leads to them (scaneventtableplanet.mbin has only DISTRESS and DISTRESS_NPC), so the app offers no marker. To check:
visit a few underwater sites on a planet with a sea.

**Underwater wreck confirmed (2026-10-29):** Rozawa Guardian III, S-class Exotic, on the sea floor at -14.39, -13.39 on the
water world Waterworld (`0x006427005B6DA013`, sea 1,500) - position, ship, type, class exact. Stats: 41,000,000, 37/21,
353.1 / 255.0 / 177.6 / 634.9. Depth: game sea floor 1,480.9, Core 1,456 (generated ground 43.8; the game refines it).
Markers for underwater wrecks: DISTRESS event with building class "WaterDistressSignal" - **works**: the marker for
+29.40, +2.73 led to Rozawa Guardian III, S-class Exotic, at +29.40, +2.72 (41,000,000, 36/22, 402.9 / 260.6 / 195.0 /
648.5) - the second underwater wreck confirmed.

**Crashed Sentinel Interceptor, not researched:** on Riwor V (Blighted Planet, dissonance, system Saitaky-Erjah III)
a claimable Sentinel ship (owner: "Reforged" is the Sentinel Interceptor): name Fragment-v / Reforged-iz, class "Salvaged // C:32+22", value 23,400,000, damage 175.5
(65.5 core), shield 171.6, hyperdrive 601.0 (101.0 core), maneuverability 290.3 (214.1 core), at +36.15, +68.05. Likely
class 52 SentinelDistressSignal (RequiresCorruptSentinels - dissonant planets only; absent on Mephar VI) with the
system's Sentinel Interceptor (pool #36, SystemStarshipGenerator includeSentinel). Next: list them, check at Riwor V.

**Sentinel wrecks (2026-10-29, Nionne Sigma, moon of Amazing Planet Biomes `0x...64275B6DA013`, planet 2):** class 52
sites; marker (DISTRESS + iqv "SentinelDistressSignal", set from the freighter) led to the right planet and site:
-70.97, -37.91 (predicted -70.96, -37.87), **C-class as predicted**. But the ship is **Crimson-σ** ("ε-Flameborn",
"Salvaged // C:34+24", 28,600,000, 181.4 (70.6 core) / 171.6 / 601.0 (101.0 core) / 299.1 (214.1 core)), not the pool's
Sentinel ship Urobeoka's Spirit DQ8 - crashed Sentinel ships are named separately (also Reforged-ιζ on Riwor V).
Two more on Nionne Sigma, markers set from the freighter both led true: Flameborn-9-MH3 **A-class as predicted** at
-7.59, -61.41 (61,000,000, 40/28, 442.6 (302.9) / 184.0 / 637.4 (137.4) / 349.4 (231.9)) and Hope-1-D **B-class as
predicted** at -28.36, -8.62 (45,500,000, 38/27, 305.1 (177.8) / 178.7 / 628.8 (128.8) / 348.6 (222.7)). 3/3 sites and
classes; the analysis visor shows "ε-Flameborn" as the Name line for all three (a model family?), the ship name differs
per site. Names and stats not traced.

## Stats research (started 2026-10-29, branch research/crashed-ship-stats)

INVENTORYTABLE.MBIN `GenerationData.GenerationDataPerSizeType` (GcInventoryLayoutSizeType) gives per ship size the
cargo (Min/MaxSlots) and tech (Min/MaxTechSlots) ranges: Sci 24-29/14-19, 24-32/19-24, 30-38/24-30; Fgt 24-28/14-19,
24-32/14-24, 30-38/19-30; Shu 24-32/12-19, Sht 28-36/18-26, 32-42/18-28; Drp 30-36/12-18, 36-40/18-24, 40-48/20-30;
Roy 24-30/14-19, 24-30/20-28, 24-32/26-30; Alien 36/30; Sail 24-30/13-18, 24-32/14-22, 30-36/20-30; Robot 32-40/22-28
(small/medium/large). Wrecks seen: Exotic S 37/21 and 36/22 (above Roy's 32 - a class bonus?), Fighter S 29/18 and
31/16, Explorer B 27/17, Shuttle S 32/19. The size type comes from the ship model (Table entries map inventory Ids to a
LayoutSizeType). Next: a hook session at a wreck (reload near it) logging the builder 0x1404D5E90's size/class inputs
and the inventory after each sub-step (0x1404D7350, 0x1404DD6E0, 0x1404D7E40, 0x1404D7600).

## Still open

- Prime-only / gas-giant systems (layout not covered), pirate systems' flag (`[+0x2e17]`), uninhabited systems' row 7.
