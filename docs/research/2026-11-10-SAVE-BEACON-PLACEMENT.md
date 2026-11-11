# Save Beacon placement - 2026-11-10

Why Save Beacons placed by ArtifactX (Planet Details hotspots) show their HUD waypoint in game but no physical beacon,
and what is left to solve. All tests on Steam Slot 8 (DELETEME) with the owner, game closed for every write, save
files backed up before each write. Slot 1 was only read.

## What a real beacon is in the save

- Controlled diff, owner on open ground: save, build one Save Beacon, save again with the beacon (16:33:00 vs 16:33:52).
  The only beacon-related change is one new `vLc/6f=/c5s` entry:
  `{b1: unix seconds, r<7: "^BUILDBEACON", oZw: planet address, ofi: 64-bit seed, CVX: 0, wMC: position from the
  planet centre, wJ0: normalised wMC, aNu: facing}`. Everything else was timestamps, materials spent and running stats.
- `ofi` appears nowhere else in the save. `oZw` may be written as a number or a "0x" string; `ofi` unpadded.
- ArtifactX writes the same shape. The game loads ArtifactX's entries, keeps them, re-saves them in its own number
  style and counts them against the building limits ("Available to build: 0" beside them).

## Game limits

`GcBaseBuildingTable` (METADATA/REALITY/TABLES/BASEBUILDINGOBJECTSTABLE.MBIN, read from the owner's install):
`BUILDBEACON` RegionLimit 1, PlanetLimit 5; `BUILDSAVE` and `BUILDSIGNAL` RegionLimit 1. A beacon beyond a limit keeps
its waypoint but is not built. Test beacons 15/30/45 m from the owner's real beacon (A: an exact copy of the game's own
format; B: ArtifactX format; C: ArtifactX as the app places it) all showed only waypoints. The size of a "region" is
not known; it is more than 45 m. The limits are not in the catalog yet.

## Height: the real cause away from other beacons

Beacons are stored where they stand; the game does not move them to the ground. ArtifactX places them 0.2 m above
Core `PlanetTerrain.Height`. That height is bit-exact for the game's height function (450/450, 2026-10-28), but it is
not always the ground the game builds:

| Beacon (game-placed unless noted) | Planet | Real height | `Height` | `BaseHeight` | Far grid | Real - `Height` |
| --- | --- | --- | --- | --- | --- | --- |
| Slot 8, 28 Oct | 20FE00011476E2 | 20.0 | 20.0 | 0.0 | 20.0 | 0.0 |
| Slot 8 | 242700FF67B291 | 411.1 | 407.6 | 370.4 | 408.0 | +3.5 |
| Slot 8 | 2409005B6DA013 | 73.3 | 76.3 | 56.3 | 78.8 | -3.0 |
| Slot 8, open flat ground, 10 Nov | 505200FF67B291 | 281.2 | 267.7 | 247.7 | 271.7 | +13.5 |
| Slot 8, in a cave, 10 Nov | 105200FF67B291 | 226.9 | 205.4 | | | +21.5 |
| Slots 5 and 7 (14 more) | various | | | | | -7.5 to +48.6 |

(Heights in metres above the planet radius from `PlanetBuildingSites.Radius`. "Far grid" is
`PlanetShopCabinetGenerator.SpawnHeight` with the beacon as site and building point.)

On some planets the estimate is exact; on others the real ground is 13-21 m higher, so ArtifactX's beacon is buried far
deeper than a player digs. ArtifactX test beacons D and E, 3 km and 6 km from anything else on Alaikast VI
(105200FF67B291) and written at `Height` + 0.2, showed only waypoints (E checked by the owner).

None of `Height`, `BaseHeight` or the Far grid reproduces 281.2 m on open ground. Something the game adds to the
surface is not ported.

## Next

Owner decision (2026-11-10): research the full ground height before changing beacon placement. Start from the planet
voxel generator around 0x14128AA20 (the ported height) and find what else feeds the surface the player stands on at
near-LOD. Validate against the owner's real beacons in the table above (Slot 8 save files from 16:33 are a clean pair).
Then place at the real ground, and add the RegionLimit/PlanetLimit to the catalog so ArtifactX can refuse a beacon the
game would not build.

## Leads (not yet tested)

`TkVoxelGeneratorData` has four terrain parts; Core ports only `NoiseLayers` (8 uber-noise layers):

1. **Noise layer LOD gating.** Each `TkNoiseUberLayerData`/`TkNoiseGridData` has `MaximumLOD`. If some layers only
   contribute at near LODs, the ported height (0x14128AA20) may be the far-LOD surface, and the ground the player
   stands on adds those layers. Best fit for an even +13.5 m on open, flat ground. Check how 0x14128AA20 is called
   (LOD argument) and which layers the near-LOD path adds.
2. **`GridLayers` (`TkNoiseGridData[]`):** 3D primitives (Cube, Cone, Torus, Sphere, Cylinder, Capsule, Corridor,
   Pipe, Puck, SuperFormula 1-8, SuperPrimitive, File) with Min/MaxHeight, offsets, region ratio/scale. Rock
   formations and arches: local, so unlikely to explain uniform offsets, but they will matter near formations.
3. **`Features` (`TkNoiseFeatureData[]`, Tube or Blob, Height/HeightOffset/Subtract/Trench):** only their Active flags
   are in the catalog (for draw counting). Same local caveat.
4. **Voxel quantisation** (`PlanetShopCabinetGenerator.FarGridValue`, u16 density per layer) and building
   flattening (`BuildingSmoothingHeight/Radius`) near structures (the cave beacon stood beside one).

Validation set: the six beacons in the table plus Slots 5 and 7; any candidate must keep the 0.0 m planets exact.

## Findings, 2026-11-10 evening

- **Layers 4 and 5** (unused by the ported height) are inactive on every beacon planet checked. Ruled out.
- **Summing layers 0-3** instead of the ported max/min: matched the planet-5 open-ground beacon exactly (281.2 m) but
  missed the other 17 by up to 28 m. Coincidence; the ported combination stays.
- **Grid layers and features are active in every terrain setting** (VOXELGENERATORSETTINGS.MBIN, owner's install,
  Min/Max sets; not in the catalog beyond feature Active flags):

| Setting | Beacons (real - ported) | Grid layers | Features |
| --- | --- | --- | --- |
| 2 | +48.6, 0.0 | G0 sphere 8 m; G2-G8 spheres 10 m tall, 10-30 m wide, offset -8 | F6 Blob 50 |
| 3 | +2.2 (x3) | as 2 | F2/F3 Tube 48 (+16); F4 Blob 50 (+15); F6 Blob 50 |
| 4 | +13.5 (open ground), +2.7, +1.7 | G0 SuperFormula_03, G1 SuperFormula_08 256 x 256 (-160); G2-G8 | F4 Blob 256 (-16); F5 Blob 128 (+80); F6 Blob 50 |
| 5 | +1.5, 0.0 | G0 sphere 256 (-205), G1 sphere 256 (+20); G2-G8 | F0 Blob 100 (-30); F1 Blob 100 sub (+25); F6 Blob 50 |
| 6 | -7.5, +5.2, +3.3 | as 2 | F0 Tube 40 sub (+50); F6 Blob 50 |
| 9 | +5.3 | G0 sphere / G1 cone 128 tall, 64-96 wide | F0 Tube 64 sub; F4 Blob 256 (+16); F6 Blob 50 |
| 13 | +32.4, +6.9 | as 2 | F6 Blob 50 |
| 22 | +3.5 | G2-G8 | F6 Blob 50 |
| 24 | -3.0 | as 2 | as 3 |

  The small G2-G8 spheres (rocks/boulders) fit the common +2 to +7 m. The 50 m Blob feature present everywhere fits
  the +32 and +48 m beacons on settings 2 and 13, which have nothing larger. Setting 4's large superformula grids and
  128/256 m blobs fit the planet-5 +13.5 m.

**Conclusion:** the missing ground is the voxel generator's `Features` (TkNoiseFeatureData Blob/Tube: hills, trenches)
and `GridLayers` (TkNoiseGridData primitives: rocks, formations), neither ported. Next: find the feature and grid
evaluation in NMS.exe (callers of the uber-noise height around 0x14128AA20 / the voxel density builder), port Blob
first (in every setting), extract Features and GridLayers into the catalog (new format), and re-run this beacon table;
the target is every beacon within ~1 m except ones on structures or player-edited ground.

### Route to the exact ground (next session)

`scripts/research/region-generator/emu_far.py` runs the game's own Far-region generation (0x141294A00 ->
0x1412B3770) in Unicorn and reproduced a live Far height grid exactly (max diff 0.0, 2026-11-06,
PLANETARY_MULTITOOL_LOCATIONS.md). That grid is the voxel surface, so it already includes whatever features and grid
layers reach it. Plan:

1. With the game parked on planet 5 of system 0052 (Slot 8, the owner's real beacon at 0x540CE87FE07759C4, 281.2 m):
   `read_planet_object_live.py` (planet object bundle), a `.data` snapshot, and the live Far region dump around the
   beacon, all read-only, as in the cabinet work.
2. `emu_far.py ... SAMPLE=<beacon points>`: if the emulated surface gives 281.2 m where Core's `Height` gives 267.7 m,
   the reference covers the missing terms; then trace which calls add them (expect the TkNoiseFeatureData Blob/Tube and
   TkNoiseGridData evaluation inside 0x1412B3770's density pass) and port those.
3. Extract Features and GridLayers (Min/Max sets, drawn like the noise layers by 0x1416C4F60's neighbours) into the
   catalog and port their evaluation; re-run the beacon table above.

Check first that the installed NMS.exe is still build 184271 (the emulator's addresses).

**Blocked, 2026-11-10:** the installed NMS.exe is build **185034** (updated 2026-11-09); every emulator address
(`emu_columns.py`, `emu_far.py`, 0x14128DB30, 0x1412B5F70...) is from 184271, and `emu_columns.py` now crashes in the
table setup. Live reads still work: planet 5's building block was found at 0x1BBB66E8798 (UA 00505200FF67B291) with
`read_planet_object_live.py`, and the game loads at an ASLR base (0x7FF743AE0000 vs ImageBase 0x140000000), so a
`.data` snapshot must be keyed at ImageBase (scratch `data_snapshot.py` does this). Next session: re-find the
184271 functions in 185034 by byte signatures (static helpers in `scripts/research/region-generator/static/`), then
run step 2.

**Unblocked, 2026-11-10 (later):** build 185034 is Forsaken 7.14. The 184271 code came from the Ghidra project of
2026-11-05 (`static/ExportBlocks.java`, read-only); `static/remap.py` found all 13 emulator functions in 185034 by masked
byte match, and every body is the same code (only moved: +0x3A0 for the terrain functions, +0x5E0 for the frame
allocator). `nmsbuild.py` maps the 184271 addresses for the installed build; `emu_far.py` and `emu_columns.py` call
`A(0x...)`. `emu_columns.py` now runs on the planet-5 snapshot taken today: column 4 (the beacon) 267.67 m, as Core's
`Height`. Step 2 still needs a live Far region dump around the beacon (game loaded on planet 5 of system 0052, Slot 8).

### Step 2 run (2026-11-10 evening, build 185034, owner standing at the planet-5 beacon in Slot 8)

Read-only: `read_far_region_live.py` (Far vtable 0x144BC0690 on 185034, via `static/vtremap.py`), the planet object
(`read_planet_object_live.py`, block 0x19FB44E0798) and a `.data` snapshot. `emu_far.py` with `LIVE=1`:

- Five live Far regions round the beacon regenerate **exactly** (max |diff| 0.0 each): the remapped emulator is right on 185034.
- The beacon itself lies in a region not loaded at Far detail (the player stands there), about 3 m past the corner of
  the nearest one. Sampled inside that region 20-30 m away on the same open ground (`SAMPLE`, `PLANET_R=98304`):
  **284.5-287.3 m**; extrapolated to the beacon from five regions: 282.1-283.6 m.
- Core: `Height` 267.7 m, Far-grid estimate 271.7 m. Real beacon 281.2 m.

**Result:** the game's own Far voxel pass puts the ground about 13-16 m above Core's `Height` here, where the real
beacon stands; Core's port leaves those metres out. This confirms that the missing ground is added inside the voxel
pass (Features / GridLayers in 0x1412B3770's density pass, 0x1412B3B10 on 185034), not by an LOD switch. Next: hook the
density pass in `emu_far.py` to split the column into the ported noise term and the rest, then port the rest.

### Where the missing metres come from (2026-11-10 night, emulator experiments + 184271 decompile)

Wiping parts of TkVoxelGeneratorData (planet + 0x2090) before an exact Far regeneration: GridLayers or Features wiped -
**no change**; noise layers wiped - ground falls to `NoSeaBaseLevel` (20 m). Features are skipped at Far detail (only
their MaximumLOD is read). So on this ground the gap is the noise layers, combined differently from Core's `Height`.

The density pass (0x1412B1D60; 0x1412B2100 on 185034) reads one 2D column per noise layer from the column table at
**+0x8 + layer * 0x20** (+0x8 layer 0, +0xC8 layer 6 = Core `BaseHeight` exactly, +0xE8 layer 7). The voxel density
starts as `y - (NoSeaBaseLevel + column0)`; then for each active layer 1-4 within its MaximumLOD,
`FUN_1412b05d0` (184271) adds the layer's column on top of a reference chosen by its **Offset**
(TkNoiseOffsetEnum: Zero = 0, Base = node base surface +0x1C, All = surface so far, SeaLevel), plus HeightOffset,
a smooth-min over SmoothRadius (+0x6C), the WaterFade ramp (+0x78, divided by WaterFadeInDistance 0x114C) and
Subtract. Core's `Height` (0x14128AA20) instead takes `max(NoSeaBaseLevel, layer)` for layers 0-3, which drops
layer 0 whenever it is under 20 m.

Core's own `LayerValue` reproduces the game's columns (layer 0: 12.1904 vs 12.190; base 251.819 vs 251.819). Beacon
check with the 2D rule "NoSeaBaseLevel + base + Base-offset layers":

| Beacon | Real | Core `Height` | Rule | Notes |
| --- | --- | --- | --- | --- |
| 505200FF67B291 | 281.2 | 267.7 | **281.24** | L0, L1 Offset Base |
| 20FE00011476E2 | 20.0 | 20.0 | 20.0 | base 0 |
| 242700FF67B291 | 411.1 | 407.6 | - | L2/L3 Offset All: needs FUN_1412b05d0's All path |
| 2409005B6DA013 | 73.3 | 76.3 | - | L0 Offset Zero, L3 All |

**Next:** port FUN_1412b05d0 (1,178 bytes; decompile it again with `DecompileList.java` on the 184271 Ghidra project)
and the surface crossing as Core `PlanetTerrain.SurfaceHeight`, check it against `emu_far.py` regions on several
planets, then all beacons in the table, then place beacons with it.

### Ported: `PlanetTerrain.CombineLayers` / `SurfaceHeight` (2026-11-10 night)

The density pass's layer rule, from the 184271 decompile (constants: 128 = a layer's reach above its reference,
0.25 = smooth-union factor), was checked against the emulated Far pass with `emu_layers.py` (caves removed): all 144
nodes within 9 mm for the planet's real layers and with each Offset type, Subtract, SmoothRadius, HeightOffset, both
WaterFade modes, layer 0 off and an extra All layer; also layer gating is LOD 0 (MaximumLOD >= 0). 44 of those nodes
are the `PlanetSurfaceChecks` vectors in ArtifactX.Core.Checks. `WaterFadeInDistance` (100 in all 31 settings, Min
and Max) is now in the catalog (format 76).

Real beacons through `SurfaceHeight`:

| Beacon | Real | `Height` | `SurfaceHeight` |
| --- | --- | --- | --- |
| 505200FF67B291 (Arkans) | 281.2 | 267.7 | **281.24** |
| 242700FF67B291 | 411.1 | 407.6 | 422.5 |
| 2409005B6DA013 | 73.3 | 76.3 | 83.1 |
| 20FE00011476E2 | 20.0 | 20.0 | 21.3 |

(The 105200FF67B291 entry at 88.1 m is an ArtifactX test beacon written at `Height` + 0.2, not ground truth.)

The two misses are 130,048 m planets whose layers use Offset All (layer 2, Height 128) and Zero. The combination is
exact given the game's columns, so either Core's per-layer value differs from the game's column there, or those
beacons do not stand on open ground. **Next:** with the owner at the 242700FF67B291 beacon (same region as Arkans),
read its planet and Far regions and run `emu_layers.py`; compare the captured columns with Core's layer values.
Beacon placement keeps using `Height` until that is settled.

### Riwor V (242700FF67B291), owner at the beacon, 2026-11-10 night

- The beacon stands beside a crashed ship. The ship's site flattens the ground around it (building pads, column
  radius up to ~105 m), and that flattening is what sets the beacon's height; the game's own surface there is
  411.45 m (real 411.1). Neither `Height` nor `SurfaceHeight` models that flattening, so this beacon is not a test
  of the layer rule.
- Away from the site (four regions 30-250 candidates out, caves removed) `CombineLayers` matches the game within
  1 cm at LOD 0 with this planet's layers: layer 0 Offset Zero, layers 1-3 Offset All, layer 2 Height 128 (the 128
  m cap binds), layer 3 MaximumLOD 1. (An earlier mismatch was the harness defaulting to LOD 2.)
- Core's per-layer values equal the game's columns at the same directions (base 6 mm, layers within 2.5 cm).
- Open: world positions from the region transform (0x141291310) sit a constant ~13-15 m sideways from the density
  pass's column directions (Riwor V (-8.0, 5.65, 8.8), Arkans (6.8, 12.1, -5.9)), so on steep ground a point's
  height depends on which frame it is in (up to ~8 m here). Which frame the save's `wMC` uses needs a game-built
  beacon on sloped open ground, away from buildings.
