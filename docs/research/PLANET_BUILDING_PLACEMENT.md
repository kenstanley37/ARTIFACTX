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
the four restriction flags and the NPC rule. Still needed from generation: the Sentinel level per difficulty and
`HasScrap` (neither is in Core yet).

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
