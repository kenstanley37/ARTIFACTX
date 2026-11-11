# Planet fauna: where the creature generator stands (2026-11-11)

Issue #3, remaining part: each planet's individual creatures (and later flora and minerals), not just totals.

## Measured

`scripts/research/fauna-check` runs `PlanetProfileGenerator` (the existing port of NMS.exe `0x14169f4a0`, child
seed 10, added in `665c4e4`) over every planet the owner captured from the game, read-only on the catalog and
`%AppData%\ArtifactX\user.db`:

    dotnet run --project scripts/research/fauna-check -- "%AppData%\ArtifactX\GameData\nms_catalog.sqlite" "%AppData%\ArtifactX\user.db"

| Check | Result (catalog format 77, game build 185034) |
|---|---|
| Fauna total per planet vs the game's "x / y fauna" (`planet_details.fauna_total`, 982 planets) | 978 exact (99.6%) |
| Planets the game says have no fauna | 136 / 136 |
| Recorded creatures' habitats fit inside the predicted list (`planet_creatures`, 99 planets) | 82 / 99 |

All four total misses are waterworlds (biome 14): 5 of 9 waterworlds exact, the rest predicted 3 to 7 too few.

## Waterworld air: cause found, rule not yet known

The game's creature lists give waterworlds (`ArchetypeLists.Biomes[14]`) only water archetypes (WATERWORLD 0.7,
WATERGLOW 0.3) and nothing for air. `SelectArchetype` then forces every unset domain to "EMPTY", so the port
predicts no flying creatures, while the owner recorded fliers on 3 waterworlds.

Tried: leave air unset there (`forceEmpty: false`), so the fallback picks `AirEmpty` (ONLYAIR). Waterworlds then get
fliers, but totals drop to 975 / 982 (overshoots of 2 to 6). Reverted: the game adds air only under a condition the
port doesn't have. Next: read the routine in Ghidra (build 185034) rather than guess.

## Next layers

1. Waterworld air rule (above).
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
