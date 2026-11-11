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

## Creature seeds: 96% of this year's discoveries (2026-11-11)

`GenerateCreatureRoles` writes each creature's `GcCreatureRoleData.Seed` (+0x5B0) right after picking it: the next
two draws of the role's stream, mixed with the same Mix64 a child stream starts from (`0x1416d60e0`, around the
`_stricmp(.., "ROBOT")`). Roles 1 and 7 then roll a diet in a child stream (not for ROBOT ground life). The port now
keeps that seed on each `PredictedCreature`.

The save's creature discovery (`fDu.ETO.OsQ.?fB`, `8P3.<Dn` "Animal") stores it as `bEr[0]`. `fauna-check` with the
Working folder as third argument compares them (read-only), by discovery year (`ksu.3I1`):

| Discovered | Seed predicted |
|---|---|
| 2016-2019 | 0 / 38 (older game versions generated creatures differently) |
| 2020-2024 | 209 / 241 |
| 2025 | 397 / 415 |
| 2026 | **888 / 925 (96%)** |

307 of 335 planets have every recorded creature predicted. Next: the 37 current-version misses (which planets,
which roles), then the name seeds `bEr[2]`/`bEr[3]` that `SpeciesNames.Fauna` turns into the species name.

## Name seeds bEr[2] / bEr[3]: not from the planet (2026-11-11)

A creature discovery's `bEr` is [creature seed (predicted, above), type value, name seed A, name seed B, optional 5th].
`SpeciesNames.Fauna` makes the species name from A and B (`bEr[2]`, `bEr[3]`). From the 1,494 discoveries whose seed is
predicted (`fauna-check` "probe" and "name seeds" lines):

- `bEr[1]` is fixed per creature kind across all planets (every BIRD: E7CAD69E2F26FF55), but not unique per id
  (CRAB and SPIDER share one): probably the model or body plan.
- `bEr[3]` is never shared between planets and mostly differs per creature; on 100 of 156 planets with several 2026
  discoveries, creatures with the same `bEr[1]` share it.
- Neither A nor B equals the planet seed, any of its 26 child seeds, the address, the creature seed or Mix64 of it.
- `GcCreatureRoleData.Info` (GcCreatureInfo) holds only descriptive traits, no seeds.

So the name seeds are added when the live creature is discovered.

## Name seeds: formula found, species 87%, genus needs the size class (2026-11-11, later)

Traced in Ghidra (184271; new helper scripts `FindStores.java`, `StringRefs.java`, `ReadData.java`):

- The discovery namer `0x1405248f0` (case 3 = Animal) passes key seeds [2] and [3] to the fauna namer
  `0x140eb39f0`. Pets store the same pair as `SpeciesSeed` / `GenusSeed` (`0x141230ad0` copies runtime
  +0x23a8 / +0x23b0 into GcPetData +0x170 / +0x150), so **bEr[2] = species, bEr[3] = genus**.
- The Animal key builder is `0x14044e1e0`: type 3, the planet address, then [creature seed, djb2 hash of a
  resource name (bEr[1], fixed per kind), species, genus, optional 5th]. It gets species and genus from
  `0x14123e740`, all CityHash `Hash128to64(low, high)`:
  - **species** = H(creature seed, H(CreatureID bytes 8-15, bytes 0-7)) (ID = the spawned creature's data, +0x40);
  - **genus** = H(int @+0xac, H(int @+0xa0, planet address)) on the live creature component.
- +0xa0 is the role data's `Type` (GcCreatureTypes), set in `GenerateCreatureRoles` as the creature's `RealType`,
  else its `ForceType`, else role + 22 (Predator, PlayerPredator, Prey, Passive). The same code sets `Diet`: Robot
  on ROBOT worlds, Carnivore for role 2, roles 1 and 7 roll (Omnivore when the draw is above 0.5, constant
  `0x144bf56f8`), else Herbivore. The port now keeps both on `PredictedCreature` (`CreatureType`, `Diet`), and the
  catalog keeps `RealType` (format 78).
- +0xac is **not the diet**: it is the creature's **size class** (GcCreatureSizeClasses Small/Medium/Large/Huge,
  0-3), written in `0x1412345f0` by bucketing its scale (+0xe34) against three globals (`0x14535a7a8`, `a784`,
  `a73c`; very likely `CreatureMedMinSize` / `LargeMinSize` / `HugeMinSize` in GcCreatureGlobals), the scale first
  divided by `0x14535a988` when flag +0x116c bit 0 is set; Sentinel-type creatures are forced. The scale comes from
  the spawned scene node (`0x14530fe50` table, stride 0x28), i.e. the spawner. Checked against the owner's
  captured creatures (named from their real seeds, matched by name): class 0 = 0.3-1.4 m, 1 = 1.1-2.5 m,
  2 = 2.6-4.2 m, 3 = up to 7.3 m (fish measured differently).

Measured with `fauna-check` on a format-78 catalog (1,494 discoveries whose creature seed is predicted):

| | Predicted exactly |
|---|---|
| Creature type (+0xa0, brute-forced from bEr[3]) | 1,494 / 1,494 |
| Species seed bEr[2] | 1,304 (87%) |
| Genus seed bEr[3], size class taken as the diet (wrong) | 243 (by chance) |

Species misses are kinds whose spawned creature data has another ID than the role's pick, e.g. ANTELOPE spawning
as TRICERATOPS (28), CRAB as SPIDER (27), STRIDER as TWOLEGANTELOPE (14), RODENT as BLOB (11): the model/descriptor
choice at spawn (bEr[1] is that resource's djb2 hash, so the two travel together).

Next: (1) find how the spawner sets a species' scale (deterministic per species, since a discovery's genus is
fixed; likely MinScale/MaxScale of the creature data and the role seed) and port the size class, which gives
genus; (2) the resource choice behind the species misses; then predicted creatures get their real species names.

## Next layers

1. ~~Waterworld air rule~~ (solved above).
2. Habitat split: 17 planets where recorded habitats don't fit (e.g. recorded flying x4, predicted flying x1).
   Some are capture noise (blank habitat rows).
3. ~~Creature identity seed~~ (96%, above); name seeds: species 87%, genus needs the size class (above). With the seed pair
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
