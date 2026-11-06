# Handoff: planet shop cabinets, Core port (2026-11-06, Claude to Antigravity)

Claude is out of usage for a few days. This file is the brief for continuing the work. Read `AGENTS.md` first; every
rule there applies. The full research record is `docs/research/PLANETARY_MULTITOOL_LOCATIONS.md` (from "Vy'keen shop
rule tree" to the end). Read it before changing any maths.

## Goal

Work out, from a planet's address alone, where each planet shop's multi-tool cabinet stands and which tool it
offers. The mobile app needs this as well, so all the maths goes in `ArtifactX.Core`. It takes plain data tables and
never reads game files, the save or the running game (see "Mobile app constraints" in `docs/AGENT_HANDOFF_NOTES.md`).

## Where things stand

Branch: **`feature/building-layout-generator`** (off `development`; the research branch
`research/planet-cabinet-arranger` is merged into it). Work on this branch. **Do not merge it into `development`
and do not open a PR**: the owner approves merges.

The research pipeline is in Python under `scripts/research/region-generator/`. Each step below is already proven
against the live game. The port has to reproduce the Python, not reinvent it.

| Step | Python reference | Proven | Core port |
|---|---|---|---|
| 1. Building layout (L-system expander), radius, root offset | `lsysport.py` | 24/24 live layouts | **Done**: `Core/Universe/BuildingLayoutGenerator.cs` |
| 2. Building record seeds | `shopcrate.py` `record_seeds`, `buildingseeds.py` | live records | **Done**: `PlanetBuildingSites.RecordSeeds` |
| 3. Cluster members, placement, re-centring | `clusterport.py` `choose_members`, `place_members`, `recentre` | exact vs live | **Done**: `Core/Universe/BuildingClusterPlacement.cs` |
| 4. Facing (forward vector) | `shopcrate.py` `facing` | exact vs live | **Done**: `BuildingClusterPlacement.Facing` |
| 5. Spawn height | `shopspawn.py` `far_grid_value`, `spawn_inputs` | 4/4 within 2.6 cm, **cube face +X only** | **Done**: `PlanetShopCabinetGenerator.FarGridValue` / `SpawnInputs` / `SpawnHeight` |
| 6. Crate point and cabinet seed | `shopspawn.py` `__main__`, `MultiToolSiteGeneration.SeedForPosition` | 3/4 cabinets exact, 4th 1 cm across a rounding edge | **Done**: `PlanetShopCabinetGenerator.ForShopSite` |

### What step 1 added (commit a23c219)

- **Data shapes** (`Core/Universe/BuildingLayoutTables.cs`): the records for the new catalog row.
- **Expander** (`Core/Universe/BuildingLayoutGenerator.cs`): `Generate(tables, lsystemPath, seed)` returns the nodes,
  bounds, `Radius` and `RootOffset`. `SampleSeed(seed, n)` gives the seed of record sample n.
- **Transform helper** (`Core/Universe/LayoutTransform.cs`): a double-precision affine transform.
- **Catalog step** (`GameData/CatalogSteps.BuildingLayouts.cs`): writes the `BUILDINGLAYOUTS` row in category
  `PlanetGeneration` (about 600 KB). It is called from `CatalogSteps.AddPlanetGeneration`.
- **Check** (`tests/ArtifactX.GameData.Checks/BuildingLayoutChecks.cs`).

Run the check like this (the scratch catalog must be a new file; the samples file is the owner's local read, never
commit it):
```
dotnet run --project tests/ArtifactX.GameData.Checks -- --building-layout-check "D:\SteamLibrary\steamapps\common\No Man's Sky\GAMEDATA\PCBANKS" <new scratch .sqlite> "%APPDATA%\ArtifactX\Research\live_samples.json"
```
Expected: `24/24 layouts, 22/24 radii exact` and `PASS`. Two shelter samples are 4-5 mm off; the Python had the same.
**Run this after every change** to the generator or the catalog step.

## Next tasks, in order

1. **Record seeds.** Port `record_seeds` (the planet seed gives stage seed 3, which gives two Voronoi draws, then two
   MWC steps per record mixed into the L-system seed). Reuse what Core already has: `PlanetBuildingSites.VoronoiSeeds`
   and its `Mwc`. Don't write a second MWC. Check: Kaim 30/I9's planet seed `0x07E3F9990696DCC9` must give the shop
   record seed `seeds[8]` and the shelter `seeds[0]` (print them from `shopcrate.py` and compare).
2. **Cluster.** Port `choose_members`, `place_members` and `recentre`. The cluster layout entries and
   `ChanceOfAddingShelter` (0.75) and `MinRadius` must come from the catalog (`GcBuildingGlobals`, the cluster layout
   data), **not hardcoded constants**. Add them to an existing or new planet-generation row. Radii come from step 1:
   the shop's radius is `Generate(...).Radius` (50.412 m on Kaim) and the Waypoint's is `MinRadius` (10 m).
3. **Facing.** Port `facing(A, key)`. Note: the angle `(word >> 29) * 45` is passed to sin/cos **as radians**, not
   degrees. That is how the game does it. Don't "fix" it.
4. **Crate point in the layout.** The crate is the `CRATE_WEAPON` REFERENCE inside the `FLOORLAYOUT_NPC` node of the
   shop layout (`shopcrate.py`, `crate_root`). The catalog step stores locators only, so it needs to also store that
   reference's transform for the scenes that hold one. Keep the extraction in GameData and the maths in Core.
5. **Spawn height.** Port `far_grid_value` and `spawn_inputs` exactly, **float32 where the Python uses `F32`**
   (C# `float`), using Core's `PlanetTerrain.Height` / `BaseHeight` (HeightProbe calls the same functions). Only cube
   face +X is proven. Write the other faces with the same rule, but say in the doc comment that they are unverified
   and add them to GitHub issue #1. Don't claim them.
6. **Cabinet.** Crate = spawn point + root offset + the crate's place in the layout, in system space (planet-relative
   point + floored planet centre), rounded to whole metres; seed = `MultiToolSiteGeneration.SeedForPosition`. Check
   against the four shops in `shopspawn.py` (`shops` list): 3 must match exactly. The fourth (`8924A2E5C39F854D`) lands about
   2 cm across a rounding edge in x; report it, don't tune constants to force it. For such near-edge results the app
   should later show both candidate tools rather than guess.

Put each check in `tests/ArtifactX.GameData.Checks` or `tests/ArtifactX.Core.Checks`, next to the layout check.

## Rules that matter most here

- **No UI yet.** The cabinet does not go on any page until the whole chain is checked. When it does: a
  ViewModel/UserControl, never C#-built cards or a mega page.
- **Never hardcode game data.** Numbers and lists come from the catalog, built from the player's own install.
- **Catalog format:** `GameDataBuilder.CatalogFormat` is still 73. Bump it only in the change that first makes the app
  *read* the new rows (it forces every player to rebuild).
- **Don't launch the app or click through it.** `dotnet build src/ArtifactX.WinUI3 -r win-x64` with 0 errors and no
  new warnings is the bar; the owner tests in game.
- **Never write a save, and never write to the game's memory.** The live research readers are read-only; you don't
  need them for this port.
- **Report honestly.** Say which checks you ran and their exact output. "Builds" is not "works".
- Commit and push to this branch when a step's check passes. Internal maths needs no CHANGELOG line; anything a
  player can see does.

## Pitfalls already found (don't repeat them)

- Euler order is X, then Y, then Z (R = Rx·Ry·Rz), translation · rotation · scale. A scene root's own transform
  is **not** applied.
- The bounds use every MESH node's AABB taken through the full transform chain, including referenced sub-scenes. Don't
  box a sub-scene and then rotate its box (that grows it).
- The pick maths is float32 (`r = (float)(u * 2^-32) * 100f`, weights summed in float). Doubles change picks.
- The spawn reads the Far grid **one cell back** (`(j + dj - 1)`, `(i + di - 1)`), and the lerp weights are
  `frac(g / 4)`: the index divided by the cell size again. That looks like a bug but the game's machine code does it.
- `lsysport.py` reads LsysDump JSON; the C# port reads the catalog row instead. If you need the Python to compare,
  `LSYS_DATA` must point to a fresh LsysDump output (run LsysDump from the repo root).
