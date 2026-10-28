# Planet Building Placement

## Goal (2026-10-28)

Reproduce where the game puts every building on a planet (settlements, shops, ruins, crashed ships,
monoliths...) so ArtifactX can say exactly what is where: the multi-tool in each shop cabinet, the
claimable crashed ship at each site. The owner wants the game's own logic in the library because a
mobile helper app will reuse it, so this is a long research track done in verified steps rather than a
shortcut. Possibility lists (pools, odds) are not shown for located items: only exact results.

Roadmap:

1. **Building sites.** Port the per-planet site generation (Voronoi points on the planet sphere, slot
   probabilities, building type per slot, building seed). Check it against saved scan markers.
2. **Building contents.** Layout/L-system choice, building orientation, and the cabinet or ship's local
   transform inside it (`docs/research/PLANETARY_MULTITOOL_LOCATIONS.md` covers the shop asset survey).
3. **Ground height.** Only if step 1 does not already give the stored surface position: the terrain
   height at the site, which feeds the exact world origin.
4. **Contents.** The rounded world origin feeds the station-verified item pipeline (multi-tools) and the
   crashed-ship pipeline.

## Game data shapes (vendored libMBIN 7.07)

`GcPlanetData.BuildingData` (`+0x3338`) is a `GcPlanetBuildingData`:

| Field | Offset | Notes |
| --- | --- | --- |
| Buildings | +0x00 | `List<GcBuildingSpawnData>`: building types for this planet (Seed, LSystemID, Classification, Density, ClusterLayouts, Min/MaxHeight, MaxXZRotation, AlignToNormal, LowerIntoGround, WFC preset...) |
| BuildingSlots | +0x10 | `List<GcBuildingSpawnSlot>`: per site `BuildingDataIndex`, `Probability`, `HasBuilding` |
| OverrideBuildings | +0x20 | |
| PlanetUA | +0x30 | universe address |
| PlanetRadius | +0x38 | |
| Spacing | +0x3C | native default 250.0 written at `0x14168F282` / `0x141691197` |
| VoronoiPointDivisions | +0x40 | |
| VoronoiPointSeed | +0x44 | |
| VoronoiSectorSeed | +0x48 | |
| InitialBuildingsPlaced, IsPrime, IsWaterworld | +0x4C.. | |

So sites are Voronoi points on the sphere (positions not stored), each slot optionally holding one of the
planet's building types. Per-race building files come from `PLANETBUILDINGTABLE.MBIN`
(`GcBuildingDefinitionTable`: density levels, cluster layouts, min/max height, flatten options).

## Ground truth in the save

`vLc.6f=.A1f` holds scan events (`GcScanEventSave`): planetary charts, signal boosters, beacons.
Observed key mapping (Slot 1, 2026-10-28, 32 events):

| Save key | Field | Example |
| --- | --- | --- |
| `TWn` | BuildingLocation | `[-995715.0, 1760.69, 352990.3]` |
| `5bU` | BuildingSeed `[valid, seed]` | `[true, "0xFAD2F4D0784E3610"]` |
| `iqv.iqv` | BuildingClass | Outpost, Ruin, Plaque, Monolith, Settlement_Hub, Abandoned, Observatory (15 are `None`: mission events) |
| `FN5` | Event | `^TRADE_OUTPOST`, `^RUIN_FAR`, `^PLAQUE`, `^MONOLITH` |
| `oZw` | GalacticAddress | planet-level address (planet nibble set) |
| `9@i` | Table (3 or 1) | |

**Resolved 2026-10-28: `TWn` is in solar-system space**, like `rnc.mEH` and `aHM`. Signal booster test on
Peak XV (Oystana, Slot 1): the Depot marker (seed `0x52782ECF461FC6ED`) and a Factory marker (seed
`0x9000E0F810A08E36`) were 130,707 and 131,076 units from the planet's generated centre
(`SolarLayoutGeneration`), with the player standing on the surface at 129,808. Their raw `TWn` lengths were
325,806 and 162,566. So subtract the planet centre; `OfScanEvent` was fixed on `fix/scan-marker-coordinates`.
The radii differ by up to about 1,300 units, which is terrain height: **markers carry the ground position**, so
the game evaluates terrain height for a site without loading it. Expected in-game readings to confirm the
frame: player -37.51, +17.28; depot +8.81, -61.92.

**Planet centres are on a 1,000-unit grid (2026-10-28).** The Analysis Visor read -37.21, +17.61 where the
player stood. That is exactly what the centre floored to 1,000 gives (the save's `aHM`, (42000, -1000, 256000));
the raw generated centre gives -37.51, +17.28. So the planet sits at `floor(generated / 1000) * 1000`. Marker radii
from that centre: depot 130,319, factory 130,193, player 130,242; the depot should read +8.96, -61.54.

**Site check (2026-10-28).** At the Manufacturing Facility the Factory marker gave +7.72, -140.37; the visor read
+7.71, -140.40 at the facility's save beacon, about 55 units from the marker point. The player there stood 14
units above the marker's radius (130,207 vs 130,193), so the marker sits at ground level near the building. The
marker left `A1f` once reached, so markers must be captured before arrival. At the Depot (Aeganya Encampment) the
visor read +8.97, -61.54 against the marker's +8.96, -61.54: two of two sites match to 0.01-0.03 degrees.

## Native leads (NMS build 180836)

- `0x14168F282`, `0x141691197`: write `Spacing = 250.0` at `+0x3374` of a planet-data object
  (`0x3338 + 0x3C`), the building-data defaults.
- `0x141468440..0x141468600` (function `0x141467F3B`): writes `+0x3370` (float), `+0x3378` (qword from
  `0x142D1D160`), `+0x3380`/`+0x33A0` (16 bytes each). Same offsets as PlanetRadius/divisions/seeds, but
  the object is not confirmed to be `GcPlanetData`; verify before trusting.
- Scan-event serializer: `0x1420E4E4A` (BuildingSeed at struct+0x30), `0x1420E4EC2` (BuildingLocation at +0x00).
- `static/dispscan.py 0xOFFSET` lists every instruction using a struct offset; filter by base register.

### Building stage (traced 2026-10-28)

The planet generation pipeline `0x14168EE20` runs its stages in a row. It writes `Spacing = 250` at
`+0x3374` and calls the building stage `0x141608F90` (also through the wrapper `0x141691190`).

**In memory the planet-data object is larger than the MBIN layout:** this stage writes `+0x33D4` (read as
Spacing), `+0x33D8` (VoronoiPointDivisions) and `+0x33E4..E6` (InitialBuildingsPlaced, IsPrime, IsWaterworld).
That is libMBIN's `GcPlanetBuildingData` shifted by +0x60, so its runtime base is about `+0x3398`. Use runtime
offsets for native work and MBIN offsets for files; don't mix them.

`0x141608F90(generator, edx = [planet+0x3534], r8, r9 = generator+0xBA4, planet = r15, ..., flag)`:

1. `n = 0x14166BFA0([planet+0x3B80])`: a count from a planet parameter.
2. `P = (int)powf(2, (int)ceilf(log2(n)))` (log2 is the game's fast approximation `0x1402C1B30`), the next
   power of two at or above `n`.
3. `R = (float)(P - max(1, P - n)) * 32 * 32` (constant `0x144B29D14` = 32.0, twice), passed as an argument.
4. `0x14160B500(generator, edx, r8, r9, planet, R, flag)`: the main site generation (next to trace).
5. Allocates 0x800 building records of 0x350 bytes (`0x141603340` initialises each).
6. `VoronoiPointDivisions = 2 * ceilf((2R / Spacing) * pi/6 * 0.5)` (`0x144B2934C` = 0.5235988, `0x144B29308` = 0.5);
   it is -1.0 when the global byte `0x14526F595` is 0.
7. `IsPrime = [planet+0x3B92]`, `IsWaterworld = ([planet+0x3B70] == 14)`, `InitialBuildingsPlaced = false`.

**Seeds (traced 2026-10-28).** The pipeline `0x14168EE20(generator, planet, input = GcPlanetGenerationInputData, ...)`
derives 26 stage seeds from `input.Seed` (`+0x20`, valid byte `+0x28`), at `0x14168EF47..0x14168F093`:

- MWC init from the 64-bit seed: `lo = seed_lo + (seed_lo == 0)`, `hi = ror16(seed_lo) ^ seed_hi ^ seed_lo` (the
  same as `UniverseRandom`'s seed rule). An invalid seed gives `lo = 1, hi = 0`.
- 13 iterations, two seeds each: `seed = Mix64((NextWord() second << 32) | NextWord() first)`, with the multi-tool
  pool's Mix64 (`0x64DD81482CBD31D7`, `0xE36AA5C613612997`, shifts of 33), stored as valid GcSeeds in a
  26-entry local table at `rsp+0x50` (stride 0x10).
- Before each stage the generator's MWC at `generator+0xBA4` is reseeded from one table entry. The building
  stage uses **entry 3** (`[rbp-0x80]`, valid `[rbp-0x78]`; `rbp = rsp+0x100`). Stage 19 (`[rbp+0x80]`) and
  stage 4 (`[rbp-0x70]`) feed the two stages before it.
- In the building stage `0x14160B500`, the only two draws are `VoronoiPointSeed = Next(10000) + 1` (`+0x33DC`)
  and `VoronoiSectorSeed = Next(10000) + 1` (`+0x33E0`). `PlanetRadius = R` (`+0x33D0`);
  `PlanetUA = 0x1416778A0(planet)` (`+0x33C8`). The building list (`+0x3398`) and slots (`+0x33B8`) are
  sized from a loop over 63 building types; `+0x33A8` reserves 1000.

`input.Seed` is the planet's seed, which `SystemGenerator.Bodies` already produces (`PlanetSeeds`, also used for
planet names), so both Voronoi seeds are computable for any planet. Not yet verified by execution.

Helpers: `static/importof.py` names imported CRT functions behind `jmp [rip+X]` thunks (`0x1433E4BEE` = ceilf,
`0x1433E4BC4` = powf).

## Live memory findings (2026-10-28, game running on Peak XV, read-only)

Tools: `scripts/diagnostics/read_planet_building_data.py` (finds every planet's runtime building block by its
signature), `read_planet_building_slots.py`, `find_value_anywhere.py`, `dump_memory.py`, and
`scripts/research/region-generator/buildingseeds.py` (the seed port).

**Seed chain confirmed on all five Oystana bodies.** The point and sector seeds in memory equal the port's
prediction from each planet seed: Peak XV 4217/2243, Ilfordo VII 5725/6583, Akui 44/T4 4075/3161,
Epsh X 8034/5595, Varijing Zurum 5906/3953. Radius and divisions match the traced formula (Peak XV
R = 130,048, 546 divisions; Ilfordo VII R = 15,360, 66 divisions). Peak XV holds 224 building types,
1,000 slots (86 with HasBuilding set: slots 0-19 empty, 20 onward filled, building indices 0..149) and 9 overrides.
Building records are 0xF0 bytes, with each type's seed at +0x68.

**Marker seeds are per site, not per type.** The Depot (`0x52782ECF461FC6ED`) and Factory (`0x9000E0F810A08E36`)
seeds are not among the 224 type records. They appear in:

- a site record: `[seed][4 bytes][packed word][point A xyzw][point B xyzw]...`. Depot point A is
  (-112,933.3, 20,268.2, 61,220.2) and Factory point A is (-82,177.6, 17,456.7, -99,270.3), both planet-relative
  and **at exactly the base radius 130,048**. Further points nearby are the building's parts.
- scene nodes holding the building's world transform. For the Depot, the axes are (0.2656, -0.6557, 0.7068),
  **(-0.8684, 0.1558, 0.4708) = the radial "up" at the site**, and (-0.4188, -0.7388, -0.5280), at position
  (-71,165.8, 19,301.2, 317,351): the marker point.
- the runtime scan event (marker position plus the event name `DEPOT`).

**Sites are corners of a cube-sphere grid.** Projecting point A onto a cube (dominant axis, then
`u = d_other / |d_dominant|`), in cells of `(u + 1) / 2 * divisions`:

| Site | Face | Grid position (546 divisions) |
| --- | --- | --- |
| Depot | -X | (321.995, 420.991) |
| Factory | -Z | (47.006, 321.007) |

Both are within 0.01 of whole-number grid corners. The leftover (about 2-4 units) may be a small seeded
jitter or the game's cube-to-sphere mapping; settle it with more sites. **The marker is that point raised to the
ground:** the Factory's point A scaled to the marker's radius (130,193) lands within about 20 units of the marker.

**Confirmed on 1,275 sites (2026-10-28).** `scripts/diagnostics/find_site_points.py 130048 546` found 3,168
distinct `(x, y, z, 1.0)` points at the base radius in memory, in records of stride 0x530 (point A first, then
the building parts). 1,275 of them lie within 0.02 of a grid corner, on all six faces (-X 356, -Z 301, +Z 265,
-Y 201, +X 78, +Y 74). Their leftover is a **constant 0.005 cell** with no slope across the face; its sign is
fixed per face and axis (-X: -,-; +X: +,+; -Z: +,+; +Z: -,-; +Y: -,+; -Y: +,-). So it's a fixed offset in the
game's cube-to-sphere formula, not a random jitter. Work it out from the face axis convention.

**Site records and the code that fills them (2026-10-28).** The 0x530-byte records are nodes of a hash map
keyed by the 64-bit value at +0x00 (what looked like the "site seed" is the map key; the marker's
BuildingSeed equals it for the Depot and the Factory). Node layout: `+0x00` key, `+0x08` packed word (Depot
`0x1C050A00`), `+0x10` up to 41 `(x, y, z, 1)` points (point A first, then the buildings of the cluster),
`+0x2A0` direction vectors. Pool: `0x141613630` allocates blocks of records (stride 0x530); `0x141610920`
takes one, copies the key and clears the points. It's called from the site function **`0x14160694B`**
(pdata chunks to `0x141608533`), which:

- loops over the cluster's buildings with an MWC at `[rbp+0x1368]`;
- reads the cluster layout's AlignmentJitter (`+0x20`) and AlignmentSteps (`+0x24`), draws the yaw
  (`Next(steps)`-based, plus jitter) and calls the game's own `Sin`/`Cos` (`0x140206E60`/`0x140206D80`,
  already ported in `GameTrig`);
- inserts into the map through `0x141610650` (lookup) and `0x141610920` (insert); the key being inserted is
  built at `[rbp+0x1370]`.

Next: trace the start of `0x14160694B`: where its MWC (`[rbp+0x1368]`) is seeded, how the key at
`[rbp+0x1370]` is formed (likely from the grid corner plus the point/sector seeds), and how the corner and
building type are chosen.

**The Voronoi query (2026-10-28).** Call chain, from the terrain-side caller down:

1. `0x14128E800` (pdata chunk `0x14128EB30`): per query position. Calls the Voronoi wrapper with the
   building data `rbx` (base `+0x3398`): `edx = VoronoiSectorSeed [rbx+0x48]`,
   `xmm3 = VoronoiPointDivisions [rbx+0x40] * 0.5`, `xmm2` = a scale, `rcx` = the query position. Outputs:
   **site key** (u64, `[rsp+0x78]`), a float (`[rsp+0x70]`, likely the distance), **site point**
   (`[rsp+0x60]`) and two more vectors (`[rbp-0x70]`, `[rbp-0x50]`, likely the next sites).
   It then checks the key against OverrideBuildings (`[rbx+0x20]`, count `[rbx+0x28]`, stride 0x30, key at +0x10)
   and seeds an MWC from the key (same seed rule as `UniverseRandom`) at `[rbp-0x60]`. The key's top 3 bits
   (`(ror16(key)^hi^lo + lo*0x5A76F899) >> 29`, times a constant) give the starting yaw. Then it calls the
   site builder `0x141605800`, which calls `0x141606330` (cluster placement and the site map insert).
   An override path (`[rbp+0x908]` set) takes a stored key/point instead of querying.
2. `0x142D76D40`: wrapper, unpacks the position and calls the worker, then packs three output points.
3. **`0x1419883E0`** (to `0x14198983A`, about 1,180 instructions): the Voronoi worker. `floorf`/`ceilf` on the
   cell coordinates, MurmurHash3 mixing (`imul 0x1B873593`) of cell coordinates and seed, square roots, and a
   long 64-bit multiply chain at the end (the site key). It sits beside the ported noise library
   (`0x142D74xxx`).

**Emulated (2026-10-28): `scripts/research/region-generator/voronoiemu.py <sector seed> <radius> <divisions> x y z`**
runs `0x142D76D40` with the planet-relative query position, sector seed, radius (`xmm2`) and divisions * 0.5 (`xmm3`).
On Peak XV (2243, 130,048, 546) it returns the Depot key `52782ECF461FC6ED` and the Factory key
`9000E0F810A08E36` exactly, with point A matching the live site records. The second output is the cell in face
units (face coordinate -273, then (48.996, 147.99) = grid (321.996, 420.99) minus 273); the third is the face
normal. So the emulator is an exact oracle for porting the worker.

**Ported: the site key from cell and seed, exact (2026-10-28).** `scripts/research/region-generator/siteport.py`.
For face index `f` (the worker's `r9`; outward normals from table `0x1450AF190`: 0 -Z, 1 +Z, 2 +X, 3 -X, 4 -Y, 5 +Y)
and an integer cell `(X, Y, Z)` in face units (the face axis is +-divisions/2, e.g. +-273 for Peak XV):

- jitters: `a = J(bits((X*113 + Y) + Z*57))`, `b = J(bits((Y*113 + X*57) + Z))`, `c = J(bits((Y*57 + X) + Z*113))`,
  all float ops in order, where `J(u) = (float((W(u) & 0x7FFFFF) | 0x3F800000) - 1) * 0.01` and
  `W(u) = x ^ (x >> 15)` after `x = ((u ^ 0x3D0000) >> 16) ^ u; x *= 9; x ^= x >> 4; x *= 0x1B873593` (32-bit);
- feature point `= (((T2+T1).x * c) + X, ((T2+T1).y * b) + Y, ((T2+T1).z * a) + Z)` with axis tables
  `T1 = 0x1450AF140`, `T2 = 0x1450AF0F0` (indexed by `f`);
- key `= Hash128to64(seed, Hash128to64(h3, Hash128to64(h2, h1)))`, with `hi = Mix64((bits(p_i) << 32) | bits(p_i * -2))`,
  CityHash's `Hash128to64` (kMul `0x9DDFEA08EB382D69`) and the station/stage-seed Mix64.

Checks: the Depot and Factory feature points and keys are exact, and **all 1,246 live site records on Peak XV match**
(`sitecheck_live.py 2243 130048 546`, read-only). The 145 other candidates it reports are building-part points
whose preceding 8 bytes are the previous point (e.g. `0xC7506E1D470248BE` = floats 33,352.7 and -53,358.1),
not keys.

**Building choice (2026-10-28).** The caller seeds an MWC from the site key (lo = key_lo + (key_lo == 0),
hi = ror16(key_lo) ^ key_hi ^ key_lo) and **steps it once** before storing it (`0x14128EB5B`). The site builder
`0x141605800` (building data `rcx`, that MWC `rdx`, face `r8d`, radius `xmm3`, key as the first stack argument) then:

1. `slot = (int)((Next() + 0) * 999)` (`0x144B29EF8` = 999.0), an index into BuildingSlots (12 bytes each: index,
   probability, has-building);
2. no building unless `slot.HasBuilding`, `slot.Probability > Next()` and `slot.Index != -1`;
3. the type is `Buildings[slot.Index]` (records 0xE0 apart in this build; classification byte at +0xD3). Classes 0x24
   (Settlement_Hub) and 0x39 (Settlement_Hub_Builders) take a separate path unless a global flag is set.
   (`0x142D76EF0`, a point-seed hash of the site's point, is called first but its result isn't used here.)

Checks against the running game (`buildingcheck_live.py`, read-only, Peak XV's live slot table and types):

- Depot key -> building 20, class 11 (Depot); Factory key -> building 21, class 12 (Factory).
- All 1,246 genuine live site records are predicted to have a building (random cells: 8.4%), so the rule is right
  for every site that exists.
- Cells next to them with no record: 9,102 predicted empty, 536 predicted built. In the loaded area about 70% of
  predicted-built sites are placed: **terrain filters the rest** (types carry Min/MaxHeight, and water, slope and
  flatten rules apply). Placement needs ground height (roadmap step 3), and the slot table itself still has to be
  ported from the planet stage (`0x14160B500`'s type loop and slot fill).

**Slot table fill (partly traced, 2026-10-28).** In `0x14160B500` after the seeds, a loop over building classes
(`bl`) checks each class with `0x141648DE0` (allowed on this planet), takes its density from the type's
`Density[densityLevel]` (`[type+0x50 + level*4]`; `[rsp+0x140]` is the level), and writes slots two at a time
(`0x14160BC00..0x14160BE9C`, 12-byte entries: `+0` building index, `+4` probability, `+8` has-building). A slot's
probability is 1 except for the last partial slot of a count, which gets the fractional part (`x - floorf(x)`).
Has-building depends on: the density being above 0 (or the type's "always" flag at `+0x98`), sentinel/biome
values (`[planet+0x31D8]`, `[planet+0x3318]`, waterworld = biome 14), type fields `+0x74`, `+0x84`, `+0x88`/`+0x8C`
(waterworld variant), and classes in the mask `0x200084` (Shelter 2, Waypoint 7, LandingPad 21).
Each type instance is created by `0x14160C480` (building spawn data: seed, layout, cluster), after
`0x141614880` (an allowed-on-planet check). Porting this needs the planet's race, density level, biome
and sentinel values, plus `PLANETBUILDINGTABLE` in the catalog. **Not ported yet.**

**Slot table: ported exactly (2026-10-28).** `slotport.py` rebuilds all 1,000 slots for each of the 5 loaded
Oystana planets (1000/1000 on every planet, compared with `dump_planet_buildings.py`), given the planet's type list
and its BuildingLevel. Class `c` owns slots `10c..10c+9`; with `k = BuildingPlacement[c].Density[level]`, slots
`j < floor(k)` hold (first type of `c`, probability 1), a fractional rest gives one more slot with probability
`frac`, the rest hold no building. A class missing from the type list has no building slots. (The index stored in a
no-building slot is irrelevant; it is not ported.)

**BuildingLevel (GcPlanetData `+0x3528`, runtime `+0x3588`): solved.** Set at `0x141695B22` from
`GenerationData`: Class (`+0x32C0`) == 1 (Initial) -> Full; else Biome Weird -> Weird; else SubType 24 (Infested)
-> HalfWeird; else Waterworld -> Waterworld; GasGiant -> GasGiant; Dead -> Dead; anything else -> Mid. Live check
(`scripts/diagnostics/read_planet_level_inputs.py`): Peak XV Lush/22/0 Mid, Ilfordo VII Frozen/1/0 Mid, Akui Dead
Dead, Epsh X Radioactive/27/**class 1** Full, Varijing Zurum Frozen/1/0 Mid: 5/5. Biome, subtype and class all come
from `ArtifactX.Core.Universe.PlanetGenerator`, so the level is computable from the address.
The stage `0x141691110`'s `Next(3)` from stage seed 1 is **InhabitingRace** (`+0x3534`), not the level; it is
overridden by a debug/global path (live planets all read 0).

**Which classes a planet has (type loop `0x14160B9C0`, 63 classes).** Count per class =
overrides + models, where:
- restriction check `0x141648DE0(class, corrupt, water, relic, scrap)`: type `+0x94` RequiresCorruptSentinels
  (planet: `GroundCombatDataPerDifficulty[difficulty]` sentinel value == 3), `+0x95` RequiresRelicWorld (planet:
  biome != Weird and subtype == 3), `+0x96` RequiresScrapWorld (planet `HasScrap`), `+0x97` RequiresWater (planet
  terrain float runtime `+0x31D8` > 0). Live: Ilfordo VII has 52/53/62 (corrupt) and 60/61 (scrap), Epsh X 60/61,
  the others none of them.
- overrides = NumOverridesToGenerateWaterworlds on a Waterworld when >= 0, else NumOverridesToGenerate; 0 when the
  type is NPC-only on a planet without NPCs, or when FlattenType `+0x74` == 3 and there is no water (class 32 on
  Ilfordo/Epsh/Akui).
- models = NumModelsToGenerate when `0x141614880` passes (`Density[level] > 0`, NPC rule) and race != 7 (None).
- Exceptions not decoded: classes 2, 7, 21, 22, 28 get their models even at density 0 (Dead Akui), and settlement
  classes (36-50, 57-59) come from a second pass after the loop (`0x14160C0ED`, jump table on class - 0x24) that
  adds them twice (two races?) and uses the Hub's density (`0x141648E50` maps 37-44, 46-50, 58 to 36 and 59 to 57).
  `BuildingFiles` is `[race 0-8][class 0-62]` (9 x 63 x 0x60).
For **site placement only classes with density > 0 matter**, so the class at a site is predictable from: the level,
the four restriction flags and the NPC rule. The NPC rule only touches the two settlement hubs (36, 57: density
0.05); every other density class has EnabledWhenPlanetHasNoNPCs.

**Life / Sentinel / scrap stage `0x1416956C0` (stage seed entry 0).** Re-derived and checked live (`lifeport.py`,
`read_planet_life_inputs.py`): ResourceLevel (draw > 0.75), sentinel level per difficulty [Peak XV 0,1,1,2;
Ilfordo VII 0,0,3,3; Akui 0,0,0,0; Epsh X 0,0,0,0; Varijing 0,0,0,1] and HasScrap [0,1,0,1,0], 5/5 exact.
Thresholds are NMS.exe globals (`0x145260E18` .55, `0x145260E1C` .25, `0x145260B20` .13, `0x145260F40` .5,
`0x1452612FC` .25, `0x145260C44` .25) and `.rdata` 0.2 for scrap. **This stage was already in Core**
(`PlanetProfileGenerator.Life`, with corrupted = sentinels[CheckDifficulty] == 3, ScrapWorld and Water from the
terrain step), so the four restriction flags all come from Core. Water = terrain SeaLevel (`+0x2030 + 0x1148`) > 0.

**Terrain drops: rare.** The earlier "about 30% of predicted-built cells have no record" was an edge effect (cells
beside the loaded area are simply not queried). Counting only cells with records on all four sides within 6 cells:
45 cells without a record, 44 predicted empty, 1 predicted built (Peak XV).

**Site point = normalize(feature) x PlanetRadius**, within 2 cm of every live record (2,592). Radius by
PlanetSize from the live planets: Large 130048, Medium 98304, Small 64512, Moon 15360 (Giant not seen).

## Core port: `ArtifactX.Core.Universe.PlanetBuildingSites` (2026-10-28)

Voronoi seeds, divisions, density level, slot table from `BuildingClassPlacement` rows + `BuildingPlanetFacts`,
site key/point/class per cell, latitude/longitude, enumeration per face. Check: `RegionCheck sites` against the
running game (`buildingcheck_live.py` with `SITES_OUT`, `dump_planet_buildings.py`): on all 5 Oystana planets seeds
and divisions exact, slots 1000/1000, 2,592/2,592 live records same key, built and same class, worst point 2 cm,
1,000,000/1,000,000 random cells same class as the game's own slot table.
**Saved markers, offline (2026-10-28, `RegionCheck markers`).** The 19 building markers in Slot 1/Slot 8 (A1f,
10 systems, all planet sizes and many biomes) checked from address + catalog only (PlanetGenerator,
PlanetProfileGenerator, no live game): **19/19 keys found** on the generated grid, **18/19 classes as saved**. The
miss is a Portal: portals have density 0 everywhere and come from the override list (OverrideBuildings, not slots),
which is not modelled. `RegionCheck suggest` lists predicted sites of chosen classes nearest a latitude/longitude
with marker values, for the Slot 8 marker test (Observatory on Yikjav XIV, -0.84, +0.42, written at base radius).

**Slot 8 marker test: CONFIRMED in game (2026-10-28, build 183791 "Forsaken", save version 4230).** One A1f entry
added with the game closed (copied shape of a real ^OBSERVATORY marker: 9@i 1, oZw = planet address, 5bU = [true,
predicted site key], TWn = planet centre (aHM) + site point at the base radius, iqv Observatory). In game the HUD
showed "Observatory" from space, led to a real Observatory and read "Destination Reached" at visor -0.84, +0.40
(predicted -0.84, +0.42). A base-radius TWn is enough: no terrain height needed for markers. The planet also scanned
"Salvageable Scrap", as predicted (ScrapWorld). Building placement unchanged by the Forsaken update here.

**Override buildings: portals (2026-10-28, live read on Slot 8, build 183791).** Portals have density 0, so the slot
table never places them; both saved portal markers (Yikjav XIV face 5 (-39, -137); a galaxy-9 planet face 1 (33, -109))
sit on ordinary grid sites predicted "none". They come from the planet's **OverrideBuildings** list (building block
+0x20, count +0x28; records 0x30 bytes): `+0x00` world position, system space, terrain height included (x, y, z
float), `+0x0C` float about 1001, `+0x10` the site key, `+0x18` a pointer, `+0x20` index into the planet's
building-type list, then three u32. Yikjav XIV (start planet) holds 8: Shelter, **Portal** (position
-20478.96, 113334.71, 202123.59 and key 0x72B04B034BEA26D0, exactly the in-game marker), DistressSignal, Base and
4 GameStartSpawn; Nitanya XIX holds 9, again one Portal. Counts follow NumOverridesToGenerate (Waterworld variant on
water worlds). So **every planet has one guaranteed portal on a grid site**. Next: find the code that fills
OverrideBuildings (which site each override takes) - then portal locations are exact from the address.

**Override placement code (build 183791, traced 2026-10-28).** `0x141635D10` (one caller, `0x1416350AD` in
`0x141634FEE`) fills OverrideBuildings (`planet+0x33B8`, count `+0x33C0`):
1. Loop over the 63 classes (definition stride 0xA0): per class the override count (NumOverridesToGenerate, or the
   Waterworld count on biome 14; zero for NPC-only classes without NPCs and FlattenType 3 without water), plus models
   when `0x141640650` passes; each override record gets `+0x20` = the class's first type index + n.
2. For each record, a random direction from the MWC passed as the 5th argument (`[rbp+0x6F8]`): two draws u1, u2;
   `y = 1 - 2*u1`, `r = sqrt(max(0, 1 - y*y))`, `phi = u2 * C` (C at `0x1451ECEB0`, presumably 2*pi), direction
   `(r*f520(phi), y, r*f600(phi))` (`0x140201520` / `0x140201600`: the game's cos/sin, to confirm which is which).
   Later records may be placed relative to an earlier one (a hash map of placed points, offset by a distance that
   grows with the retry count) - the GameStartSpawn cluster. The direction times PlanetRadius goes to
   `0x141631160`, which returns accept / reject; reject = retry with new draws.
3. `0x141631160` runs the per-position site query `0x1412B2D70` (domain-warps the point with three calls of the 3D
   noise `0x1419BBAE0`, then the Voronoi wrapper -> site key and point) and three terrain-looking calls on the planet
   data (`0x1412B5220`, `0x14128AA20`, `0x14128C350`) plus `0x141631F60`, then compares.
To predict portals exactly: the MWC seed (which stage seed), a port of the noise `0x1419BBAE0`, and whether the
accept test needs terrain height (if it does, rejected attempts consume draws and the port needs the terrain).
Ground truth: Yikjav XIV portal (face 5 (-39, -137), visor +62.43, -164.10) and Nitanya XIX's override list.

**Seed and directions CONFIRMED (live, 2026-10-28).** The caller `0x141634FB0` seeds the MWC from the GcSeed at
runtime planet `+0x3B60` (valid `+0x3B68`; lo=1, hi=0 when invalid) - live it is **the planet seed itself**
(Yikjav XIV 896996F8AE054363, Nitanya XIX 4DB99357E93A17AD). Directions are `(r*cos(2*pi*u2), 1 - 2*u1,
r*sin(2*pi*u2))` with u1, u2 consecutive words. Matching every override record's direction (record position minus
the aHM centre) against consecutive word pairs of that stream: Yikjav XIV (Scorched, no sea) Shelter at word 0,
**Portal at word 56**, Base 58; Nitanya XIX (Lava) Shelter 404, **Portal 478**, Base 482, last record 486 - all
within 0.002 rad. Gaps are rejected attempts (2 words each) plus cluster placement; the lava world rejected about
200 attempts before its first override, so the accept test in `0x141631160` is almost certainly terrain-based
(height against sea/lava level). Exact portal prediction therefore needs that test: either port the terrain
height (TkVoxelGenerator noise - large, but also gives exact building heights) or find a cheaper rule.

**Accept test decoded (2026-10-28).** `0x141631160`: (1) warped site query `0x1412B2D70` must find a site and a
finite point; (2) building type = Buildings[index] (`+0xC0` a flatten value, `+0xD3` class), `0x1412B5220(class)`
-> flag; (3) **height h = `0x14128AA20(point, Terrain)`**; (4) `0x14128C350` (flatten / second terrain value; with the
flag it replaces h); (5) `0x141631F60(h, SeaLevel = Terrain+0x1148, ...)` accepts when: not below sea (h < sea + c
counts only when sea > 0), the type's water rule (`+0x9C`: 3 = water-only, 1-2 = land-only variants), the type's
Min/Max height (`+0xB4`/`+0xBC`), and the second terrain value in [0, limit]. Accepted position = site point +
normalize(point) * h (+ planet position): the record's ground position.
**Height function `0x14128AA20`** = TkVoxelGeneratorData (libMBIN offsets): hash of BaseSeed (`+0xAB0`); layers 7
and 6 of `NoiseLayers` (`+0xAC0`, TkNoiseUberLayerData stride 0x84: Active `+0x80`, Subtract `+0x81`, SeedOffset
`+0x68`, Height `+0x40`) form the base (h7 + h7 / Height7 * max(h6, 0)), layers 0-3 add by max (or subtract-min when
Subtract), then + NoSeaBaseLevel (`+0x1140`). Each layer = `0x142C3D4E0` (uber layer, ~580 instructions) ->
uber noise core `0x142DE3F20` (~930) -> primitives `0x1419BD4C0` / `0x1419BCA30`.
**Plan:** port the uber noise + this height function (verify against the game's own function run in the emulator
harness on real TkVoxelGeneratorData, then against the live override records), then the accept test and the
placement loop -> exact portal (and every override) positions with ground height. Also needed by the mobile app
(heights, sea, ground-level markers).

**Emulator reference for the height (2026-10-28).** `emu_height.py` runs `0x14128AA20` in Unicorn on a planet's real
TkVoxelGeneratorData (`read_terrain_live.py`: GcPlanetData + 0x2030, 0x1150 bytes; planet position = runtime planet
+ 0xD74D0, equal to the save's aHM centre). Unvisited **Nitanya XIX: all 9 override records' heights reproduced to
within 0.08 m** (62 to 596 m). Visited Yikjav XIV's records differ (24.7 vs 22.4, 135 vs 271...): once an area loads
the game corrects stored heights with the full voxel terrain, so only unvisited planets show generation heights.
Notes: stub `_aligned_malloc` (the noise allocates scratch), `roundf`; do NOT copy the live .data (its caches point
into game memory). Sea level: Yikjav 0 (no sea; NoSeaBaseLevel 20), Nitanya 120 (lava).

**Placement emulation, in progress (2026-10-28).** `emu_overrides.py` runs `0x141635D10` on a planet object read live
(`read_planet_object_live.py`, planet = block - 0x3398, 0xD7500 bytes; types/slots/overrides re-pointed). Needed so
far: skip `0x141630100` (post-placement building-record pool + manager vtable call), set the run-time global byte
`0x145340EC0` = 1 (else the accept test rejects everything), stubs for memset/memmove/_fdclass/_aligned_malloc/roundf.
It now runs to completion (27 loop passes, about 2,700 attempts) and writes type indices but no positions - next
suspect: the container allocator `0x142C8E290` (global allocator object empty in the file) breaking the hash map of
placed points. **Also found: the game only fills OverrideBuildings when the player approaches a planet** - right
after warping to a new system all records are zero (even the type index), so fresh ground truth needs an approach.

**Ground truth set 2 (2026-10-28, system 00FE00011476E2, build 183791, after approaching).** All 6 planets placed;
Ribhakyam (Frozen start planet) portal record +67.11, +81.40, visor at the portal +67.09, +81.40. Word index in the
planet-seed stream of each record's direction (seeds from SystemGenerator: C11D1E6EE8A8FD60, B6B082CA23FE0DDC,
10430ECD09C556F1, 0D5CBCAACF0B2404, 447553726422F63C, 7752D26E93C5A40F):
Ribhakyam cluster 606, portal 644, base 646; Loisense XIII cluster 606, portal 662, base 664; Nikong Beta cluster
1414, portal 1444, base 1446 (+ class 62 at 1450); Hunnieldebur Iomi cluster 1414, portal 1462, base 1472;
Ewtone Waraz cluster 215/404, portal 432, base 434; Withe (Weird) has no portal (types 0, 20, 23, 26, 35-38).
Order everywhere: start cluster anchor, then the portal a few dozen words later, base 2 words after the portal.
Open: two planet pairs share the same anchor index (606, 1414) despite different seeds - either a fixed rule picks
the anchor or the matcher is wrong; check before porting. Live override JSONs kept locally (scratch, not committed).

**Phase 1 is a chain of points (decoded 2026-10-28).** `0x1416360D3..0x141636508`: points are kept in a small
ring/hash structure (nodes from the pool allocator `0x142C8E290`, freed by `0x142C8C960`). The first point of a chain
is a uniform random direction that passes the accept test; each later point is a random step from the previous point
(direction + offset, renormalised, distance growing with the attempt number) and must lie in a distance band from it
(min/max squared distances at `0x14163632C..0x1416363B2`). After 100 failed attempts in a round (`r13 > 0x64`) the
chain is cleared (`0x140314000`) and restarts; total cap 20,000 attempts (`0x4E20`). Every attempt uses 2 words, so a
chain can only start at word 2*101*k - **which is why the start clusters sit at words 606 (k=3) and 1414 (k=7)** on
planets with different seeds. Once the chain holds enough points, **phase 2** (`0x1416365C6` / `0x141636600`)
assigns each override record (up to 5,000 tries each, its own draws at `0x141636740` and accept call at
`0x14163683B`): class 2 (Shelter), 20/32 water variants and 24/31 on water worlds take chain points; others (Portal,
Base) are placed relative to chain points. Positions are written at `0x141636A45`.
**Placement EXACT in the emulator (2026-10-28).** With the start-up values below, `emu_overrides.py` reproduces
**49/49 override records on all 6 planets of system 00FE00011476E2** (Withe, Ribhakyam, Loisense XIII, Nikong Beta,
Hunnieldebur Iomi, Ewtone Waraz): same site key, type and position (0.000 m), portals included; Ribhakyam's chain
restarts exactly 3 times (word 606). Start-up values (0 in the file, read live): `0x145006F00` = 10,
`0x145006EF8` = 35, chain band `0x14535178C` = 600, `0x145351790` = 500, `0x145351794` = 1000, `0x145351798` = 800;
sources identified: the band is **GcBuildingGlobals** StartCrashSiteMaxDistance 600 (`+0xD0C`),
StartCrashSiteMinDistance 500, StartShelterMaxDistance 1000, StartShelterMinDistance 800 (the start area: crash
site and shelter at set distances); 35/10 are **GcTerrainGlobals** SeaLevelGasGiant (`+0x278`) and SeaLevelMoon
(`+0x280`) - special sea levels the override function compares against.
**Next: port to Core.** Order: (A) the per-planet TkVoxelGeneratorData (noise layer values drawn from the terrain
setting's ranges; Core's PlanetProfileGenerator.Terrain only picks the setting, sea level and caves) - check against
the 8 live terrain dumps; (B) uber noise + height `0x14128AA20`, the domain-warped site query `0x1412B2D70` (noise
`0x1419BBAE0`), the accept rule `0x141631F60`, the chain + phase 2 - each checked against the emulator.
Crashed-ship ground truth for later: Ribhakyam, "Final Eokak III", C-class, at +66.87, +81.48 (next to the portal),
with a distress beacon pod (owner, 2026-10-28).
Emulator notes: replace the pool allocator with heap allocations; the emulation needs well over 50 million
instructions (about 1,000 attempts x noise), so raise the step cap.

**Forsaken (build 183791, 2026-10-28).** PLANETBUILDINGTABLE decodes with libMBIN 7.07 and is identical to the
180836 table. NMS.exe moved: the BuildingLevel setter is now at `0x1416C84E3` (was `0x141695B73`, +0x32970) with the
same logic. Addresses in this file are build 180836; re-locate by byte pattern before using them.

Next for the app: PLANETBUILDINGTABLE rows in the catalog, a Giant radius, then a "nearest Shop / Depot / crashed
freighter" finder from the player's position; and for exact contents, the model chosen within a class and the
cabinet / crashed-ship seeds.

So **every cell of every face is a site** with a computable key and feature point. Next: the world point of a
site (the worker's projection of the feature point to the sphere; the emulator gives ground truth), then which sites
get a building and of which type (`HasBuilding`, slot probabilities, the type list), and the per-site yaw (top 3
bits of the key, then the cluster steps).

Open: which corners hold a building (the sector seed and slot probabilities), which building type each gets,
how the per-site seed and yaw are made, and the exact jitter. Next: collect more sites (more booster markers, or
read site records from memory) and find the code that writes the site records (search memory for the
record layout, then the writer).

## Next

**Where the Voronoi points are built: not found yet (2026-10-28).** The runtime building data starts at
`planet+0x3398` with exactly libMBIN's `GcPlanetBuildingData` layout (point seed `+0x44`, sector seed `+0x48`,
divisions `+0x40`, radius `+0x38`), so consumers read it through a pointer with small offsets that a byte scan
can't single out. Ruled out: `0x141E1CB20` reads `+0x33D0`/`+0x33D8` only to compare fields with defaults (a
serializer); `0x1418C62DA..0x1418C62EC` writes defaults (Spacing, divisions 1.0, sector seed); `0x14160B500`'s
type loop (`0x14160B9C0..`) builds the per-planet building-type list from `PLANETBUILDINGTABLE` densities via
`0x141648DE0` (is the type allowed) and does not place sites.
Field-group scan (`static/fieldgroup.py 0x38 0x40 0x44 0x48`): 865 functions read all four offsets from one
register, far too generic. Near the building code: `0x141691DA0` is a later stage drawing float lerps into
`+0x33AC`/`+0x33B0`; `0x14168C640`/`0x14168D950` copy `GcPlanetGenerationInputData` fields. Neither is the point builder.
Runtime list layout (base `+0x3398`): Buildings `+0x3398` (sized from the type loop), **BuildingSlots `+0x33A8`
(1,000 reserved: at most about 1,000 sites)**, OverrideBuildings `+0x33B8`.
Proposed: a read-only live read of the running game's planet data (like `read_region_star_positions.py`) to check
the seed port and see the filled slot list.
Next approaches: (a) callers of the building-data getters and of `lea reg, [planet+0x3398]`;
(b) functions that read `+0x38`, `+0x40`, `+0x44` and `+0x48` from the same base register (a disp8 scan
grouped by `.pdata` function); (c) the scan-event path that fills `BuildingLocation` must look up sites, so
trace back from the `GcScanEventSave` writer.

1. Trace the Voronoi point and slot code that reads `+0x33DC`/`+0x33E0` (after `0x14160B993` in `0x14160B500`,
   and its callees `0x14160C480`, `0x14160C590`, `0x141614880`), and `0x14166BFA0` (the count `n`).
2. Find the scan-event creation path (what writes `BuildingLocation`) to learn whether markers carry
   terrain height.
3. Emulate the site generator for a planet with saved markers and match a marker's seed and location.


## Terrain data generation `0x1416C6BF0` (build 183791; old 0x141694280), started 2026-10-28

Writes the planet's TkVoxelGeneratorData (GcPlanetData `+0x2030`) from the terrain settings table
(generator `+0x510`, elements of 0x22A0 = a **minimum TkVoxelGeneratorData and a maximum one at `+0x1150`**), on the
planet generator's MWC (`+0xBA4`, child seed 4 - Core's PlanetProfileGenerator.Terrain already ports the start:
setting index `+0x32CC`, purple / prime ranges, sea level):
- TerrainControls copied from the biome file (`+0x244..+0x2BC` -> `+0x3240`), BaseSeed from `+0x3220` -> `+0x2AE0`.
- Noise layers 0-7 (stride 0x84): one draw against the layer's chance (`+0x3264 + 4*i`, from TerrainControls) sets
  Active (`+0x2B70 + 0x84*i`); active layers are built by `0x1416C4F60(gen, out, min layer, max layer, controls)`
  (draws between the min and max values) and copied in. Layer 7 has water-world / sea-level special cases
  (`+0x3178` vs constants; Height set to sea level minus a constant, a fixed offset type 4).
- Then the features (`+0x2EE0` area) the same way with `0x1416C6760`, and further draws (12 more MWC steps) for the
  remaining values.
Plan: emulate this function per planet (biome files + settings table loaded) to get exact references, then port it
step by step; end-to-end check = the live TkVoxelGeneratorData dumps (8 planets).

**Layer generation decoded and checked live (2026-10-28, `voxelfit.py`).** On the planet generator's stage-4 stream
(stage seed 4 of the planet seed, as Core's terrain step), after the terrain step's first draws (28 words here):
- per noise layer 0-7: if the setting's layer (second data set, `+0x1150`) is not Active, no draw; else one draw
  `unit < TerrainControls chance` (GcPlanetData `+0x3264 + 4*i`) decides Active; an active layer then draws its fields
  in the order `+0x7C, +0x40, +0x60, +0x64, +0x08(int), +0x14(int), +0x30, +0x04, +0x18, +0x00, +0x2C, +0x38, +0x10,
  +0x0C, +0x20, +0x1C, +0x28, +0x24, +0x3C, +0x34, +0x70, +0x6C, +0x44, +0x58, +0x50, +0x54(int)` as
  `second + u * (first - second)` (ints `Next(first - second + 1) + second`); SeedOffset, Subtract, Offset and
  MaximumLOD are copied from the second set, VoxelType via `0x1416C4E30` (extra draws only for random-rock types 8/9).
- layer 0's Height is overridden by word 4 of the stream (drawn early in the terrain step).
- after the loop: layer 4 is switched off when SeaLevel <= 0, layer 6 on moons (size 3); layer 7 has sea rules
  (active when sea level is above a threshold or a terrain-control flag is set; Height = sea level minus a constant).
Check on 6 planets: all varying layer fields exact (3/3 on the two planets that have any; the rest of the active
layers are constants of their setting) and all 8 Active flags reproduced.

## Uber noise (terrain height) - structure, 2026-10-28 (build 183791)

- Layer evaluator `0x142C3D4E0` (577 instructions): takes arrays of x, y, z (+ count), removes duplicate points,
  calls the core `0x142DE3F20(xs, ys, zs, layer (TkNoiseUberLayerData), seed, ...)` and post-processes per layer
  (RegionRatio `+0x60` and others).
- Core `0x142DE3F20` (926): for groups of 4 points calls the SIMD uber noise `0x1419BD4C0` (960 instructions), for
  the remaining points the **scalar uber noise `0x1419BCA30` (566)** - the same function. The height query used by
  the override accept test passes ONE point, so only the scalar path runs: port `0x1419BCA30` (point, octaves
  `+0x14`, and the TkNoiseUberData floats `+0x00, +0x04, +0x0C..+0x3C` as arguments) and the core's own steps
  (`0x140201600`, `0x142DE3730`, a second pass).
- The site query's domain warp uses `0x1419BBAE0` (869). Noise tables near the Perlin permutation `0x144AFAEA0`
  (`0x144AFAD80..0x144AFAFD0`).
Method: port function by function, each checked bit for bit against the same function run in the emulator.

**Scalar uber noise PORTED, bit-exact (2026-10-28).** `uberport.py` transcribes `0x1419BCA30` in float32 lane for lane:
500/500 random vectors from the emulator (`emu_uber.py`) bit-identical. Arguments = TkNoiseUberData: Octaves,
PerturbFeatures, SharpToRoundFeatures (`roundf`), AmplifyFeatures, AltitudeErosion, RidgeErosion, SlopeErosion,
Lacunarity, Gain, RemapFrom/To Min/Max, SlopeGain, SlopeBias. Per octave: inlined simplex with derivatives (its own
add order - NOT GalaxySimplexNoise's: falloff 0.6 - (y^2 + (x^2 + z^2)), transpose-add sums), sharp/round blend,
slope (SlopeErosion) and ridge (x0.8) derivative accumulators, perturb offset, sum += n * ridgeAmp / (1 + slope^2),
altitude erosion via smoothstep of the clamped sum, amplitude *= that (first octave uses Amplify + Gain, later Gain;
start 1.0 for one octave else 0.9). Tail: (sum + 1) / 2 when SharpToRound rounds to 0, remap + clamp, Schlick
gain (SlopeGain) and bias (SlopeBias). Constants and tables read from NMS.exe (perm `0x144AFAEA0`, gradients
`0x144AFADA0`). Next: the core `0x142DE3F20` / layer evaluator `0x142C3D4E0` around it, then the height function.

**Simplex fBm `0x1419BBAE0` scalar path PORTED, bit-exact (2026-10-28).** `fbmport.py`: 88/88 emulator vectors
(octaves 1-3, `emu_fbm4.py`). Its two terrain uses only run the scalar path: the site query's domain warp passes 2
octaves (`0x14128A850`), the region mask 3 (global `0x145097F6C`, 3 in the file); the 4-octaves-per-lane SIMD path
needs more than 3. Same simplex as the uber noise without derivatives; sum += n * amp, amp *= 0.5, freq *= 2;
result * norm[octaves] (`0x144BE9418`). Its 256-entry gradient table `0x146F45EE0` (SIMD path only) is filled at
start-up as gradient16[perm[i] & 15].
Region step `0x142C37340` (per layer value): uses RegionRatio/RegionScale/Width, the region mask
`0x142DE3E70` -> `0x1419BE730` (point + (seed%13, seed%13, seed%17) * k, / (RegionScale*Width), fBm with 3 octaves,
then normalised by the min/max table `0x144AFAD00`), powf-shaped smoothstep, plateaus (PlateauStratas/Sharpness/
RegionSize with a second mask at seed + 1), x Height, then quantised. Core `0x142DE3F20` for one point:
(p + (seed%13, seed%13, seed%17) * k) / Width -> scalar uber noise.

**Terrain height PORTED, exact (2026-10-28).** `heightport.py` (= `0x14128AA20`): base-seed hash of BaseSeed
(SuperFastHash-style), per layer seed = SeedOffset + layer + hash, layer value = core ((p + (seed%13, seed%13,
seed%17) * k) / Width -> scalar uber noise) then the region step (clamp/rescale by sqrt(max(RegionRatio, 0.5)),
region mask fBm with 3 octaves at RegionScale*Width shaped by powf gain RegionGain, plateaus, x Height, quantised),
combination (layer 7 base scaled by max(layer 6, 0); layers 0-3 max / subtract-min from NoSeaBaseLevel).
Checks: **450/450 emulator heights bit-exact** on 3 planets (`emu_height.py vectors`), per-layer evaluator
`emu_layer.py` 40/40; against the game's own override records on 4 unvisited planets: within 0.15 m (the records'
float32 system-space positions at about 1,000,000 units). Emulator lesson: the game's pool allocator
(`0x142C8E190/0x142C8E290`) must be replaced by heap allocations in every harness, or arrays alias (the evaluator
then read the noise value as x) - `emu_height.py` reference heights before that fix were wrong.
Next: the domain-warped site query `0x1412B2D70` (its fBm is ported), the accept rule `0x141631F60`, the chain +
phase 2 placement; then all of it to C# in Core.

**Site query (which site a point falls in) PORTED, exact (2026-10-28).** The single-point query `0x1412B2D70` calls
the Voronoi wrapper `0x142DE7B30` (worker `0x1419BE7E0`) on the raw point - no domain warp (only the batched query
warps). `siteport.nearest_site`: for each face the point faces, cells around its projection (floor +-2); a cell counts
for a face only if its jittered feature point lies on that face (edge cells belong to the face their jitter keeps
them on - the 9 misses of a "own face only" rule were all edge cells); nearest feature point on the sphere wins.
600/600 against the emulator (`voronoi183791.py`: 300 random, 300 near cube edges and corners). The worker's float
output is that distance; its third output is the face normal.

**Cheap base height + flatness test PORTED, exact (2026-10-28).** `heightport.base_height` (= `0x14128CA10`: layers 7
and 6 only, combined as in the full height, at normalize(p) * radius; 60/60 on 3 planets) and `flatport.flatness`
(= `0x14128C350`, non-settlement path: 8 samples on a ring of radius 64 - or the type's `+0xC0` when building-data
byte `+0x4D` is set - around the site, a Rodrigues rotation of the tangent built from n x up (up = (0,1,0)) about n
by i*pi/4 with the game's own sin/cos (`0x140201600` / `0x140201520`, Core GameTrig); slope = max |h0 - hi| / 64;
also the ring min/max). 20/20 on 2 planets. Settlement types (classes 0x24..0x3B, `0x1412B5220`) take a second
ring and an engine check - not needed for portals.
