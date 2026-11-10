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
