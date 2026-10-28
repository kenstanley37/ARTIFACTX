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

## Next

1. Find the function that fills `BuildingSlots` and computes Voronoi points (readers of `+0x337C`/`+0x3380`
   with a known `GcPlanetData` base; the `Spacing` writers' callers are a starting point).
2. Find the scan-event creation path (what writes `BuildingLocation`) to learn whether markers carry
   terrain height.
3. Emulate the site generator for a planet with saved markers and match a marker's seed and location.
