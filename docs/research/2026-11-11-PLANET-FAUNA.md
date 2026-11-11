# Planet fauna: where the creature generator stands (2026-11-11)

Issue #3, remaining part: each planet's individual creatures (and later flora and minerals), not just totals.

## Measured

`scripts/research/fauna-check` runs `PlanetProfileGenerator` (the existing port of NMS.exe `0x14169f4a0`, child
seed 10, added in `665c4e4`) over every planet the owner captured from the game, read-only on the catalog and
`%AppData%\ArtifactX\user.db`:

    dotnet run --project scripts/research/fauna-check -- "%AppData%\ArtifactX\GameData\nms_catalog.sqlite" "%AppData%\ArtifactX\user.db"

| Check | Result (catalog format 77, game build 185034) |
|---|---|
| Fauna total per planet vs the game's "x / y fauna" (`planet_details.fauna_total`, 982 planets) | 978 exact (99.6%); **983 / 983 after the waterworld fix** |
| Planets the game says have no fauna | 136 / 136 |
| Recorded creatures' habitats fit inside the predicted list (`planet_creatures`, 99 planets) | 82 / 99 |

All four total misses are waterworlds (biome 14): 5 of 9 waterworlds exact, the rest predicted 3 to 7 too few.

## Waterworld fauna: SOLVED (983 / 983)

Found by decompiling build 184271 in Ghidra (project copied to `%AppData%\ArtifactX\Research\ghidra-184271`,
scripts in `scripts/research/ghidra`). The creature routine is `cGcPlanetGenerator::GenerateCreatureRoles`, found with
`FindByScalars.java` by the constants the port uses (0xBA2E8BA3 = "% 11", 0x16, 0xE, 0x18): `0x1416d60e0`.

- `0x1416d87e0` picks from one optional list: a probability roll, then ground (unless biome 14), water (sea level),
  air, cave (if caves); then EMPTY for unset domains only when the list's own `OverrideAllDomains` (+0x44) is set.
  The game files set it on every biome, sub-biome, empty and abandoned list and off on the purple list, which is
  what the port had assumed per call. Now read from the catalog (older catalogs keep the assumption).
- The biome's own list applies only when **all four** domains are still unset (and the list isn't empty). The port
  applied the waterworld list whenever ground was unset. Every waterworld is in a purple-star system; the purple list
  never sets ground on a waterworld but may set water or air, which the waterworld list then overwrote (and its
  override turned unset air into EMPTY). Fixing the condition: all 9 recorded waterworlds exact, fliers exactly where
  the owner recorded them (3 / 3 / 3 / 4).
- `0x1416d85f0` (weighted pick) pushes and draws even for an empty list; the port now does too (no measured change).
- Checked and matching the port: table collection `0x1416d8a70` / `0x1416d8c20` (main tables, then additional
  entries with MaxTablesToAdd), domain order ground-air-water-cave, hemisphere counts (they only mark a creature's
  hemisphere, inside a popped stream: no effect on the count), all 77 referenced ecosystem tables extracted.
- Small difference left alone: the game stops adding creatures once more than 22 exist (so up to 23); the port stops
  at 22. No recorded planet comes close.

## Next layers

1. ~~Waterworld air rule~~ (solved above).
2. Habitat split: 17 planets where recorded habitats don't fit (e.g. recorded flying x4, predicted flying x1).
   Some are capture noise (blank habitat rows).
3. Creature identity: the port gives an archetype id per creature, not the creature's seed. With the seed pair
   that `SpeciesNames.Fauna` takes, predicted creatures get their real species names, and can be matched one by
   one against the 510 recorded creatures by name.
4. Traits: rarity, gender, activity, size.

Also to fix: `PredictedCreature.DefaultDisplayName` is a hand-typed English table; names must come from game data.

## Tooling state

- Ghidra 12.2 at `%LOCALAPPDATA%\Programs\ghidra_12.2_PUBLIC` (needs JDK 25).
- The October planet-data emulator scripts (resources/getters/validate) are gone: they lived in a session
  scratchpad. Only `emu_sys.py` and its extracted game files survive (scratchpad of session ffdfbde9), plus
  `scripts/research/region-generator/emu`. Addresses in old notes are for builds 180836/184271, not 185034.
- Keep new research tooling under `scripts/research/`, not in a scratchpad.
