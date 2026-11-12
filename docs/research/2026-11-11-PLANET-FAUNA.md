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

## Size class and genus: 84% (2026-11-11, later)

`0x1416d9650` builds a role's spawn data (GcCreatureSpawnData) from its role record. With the creature data's
`MinScale` and `MaxScale` (capped at GcCreatureGlobals `MaxCreatureSize`), the role description's size range picks a
band: lo = min + (max - min) * 0.25 * MinSize, hi = min + (max - min) * 0.25 * (MaxSize + 1) (0.25 is `0x144bf4f70`).
One draw from the role seed (StartState, one step) gives lo + (hi - lo) * f, clamped to [min, max], written to both
spawn MinScale and MaxScale, so the scale is per species. `0x1412345f0` buckets it against `CreatureMedMinSize` /
`CreatureLargeMinSize` / `CreatureHugeMinSize` (GcCreatureGlobals, loaded at `0x145358C80`: offsets match libMBIN);
babies are divided back by `GroupBabyScale` first. Ported as `PlanetProfileGenerator.SpeciesScale`; the catalog keeps
the creature scales, role size ranges and the four globals (format 78).

| 1,496 discoveries whose creature seed is predicted | Predicted exactly |
|---|---|
| Size class (1,330 whose type is right) | 1,261 (95%) |
| Species seed bEr[2] | 1,306 (87%) |
| Genus seed bEr[3] | 1,261 (84%) |
| Both, so the exact species name | 1,198 (80%) |

Those misses turned out not to be spawn-time swaps; see the next section.

## Creature picks and species ids: 97% exact names this year (2026-11-11, later)

`bEr[1]` is the djb2 hash of the creature's scene file exactly as `GcCreatureFilenameTable` writes it
(`MODELS/PLANETS/CREATURES/ANTELOPERIG/ANTELOPE.SCENE.MBIN`), so every saved discovery says which creature the game
spawned. Comparing that with the port's picks showed two things:

- **The port's weighted pick was wrong for roles with a RequireTag.** The game (GenerateCreatureRoles, both passes)
  weighs a creature by the RarityOverride of the required tag (`GcCreatureTagAndRarity`), not by its own rarity,
  and applies the herd penalty whenever no tag is required. ANTELOPE vs TRICERATOPS, STRIDER/COW vs
  TWOLEGANTELOPE, RODENT vs BLOB were all this. Now `PlanetProfileGenerator.PickWeight`; the catalog keeps each
  tag's rarity. Fauna totals unchanged (983 / 983, the pick doesn't change the draw count).
- **The species seed uses the scene's creature component Id** (`GcCreatureComponentData.Id`, the +0x40 that
  0x14123e740 reads): the CRAB and HERMITCRAB scenes say SPIDER. Weird creatures share scenes holding one component
  each, so the creature's own id wins when its scene has it. The catalog keeps it as `CreatureEntry.SpeciesId`.

| Discovered | Discoveries | Species | Genus | Both (exact species name) |
|---|---|---|---|---|
| 2026 | 890 | 869 | 870 | 867 (97.4%) |
| 2025 | 397 | 391 | 381 | 381 (96%) |
| 2020-2024 | 209 | 203 | 86 | 85 |

Older discoveries keep the genus seed their game version made, with other GcCreatureTypes numbers (ANTELOPE as
21/22 where today's game says 25/26), so they can't match today's generator; that's expected. The remaining
current misses are single wrong picks spread across kinds.

**Confirmed in game, 2026-11-11 (Slot 8):** Planet Details showed the unscanned creature on Orduenev D27 as
"P. Rainaemera (Predicted)"; scanned at 20:06 the game named it P. Rainaemera. B. Warloxigesea (Gakiraw Yonez) also
matched the generator from a fresh decrypt. Shown in the app since `aa66a59` (catalog format 79).

Next (done: names on Planet Details), and match them one by one against
the owner's captured creatures; then traits (rarity, gender, activity, height and weight from the scale).

## Creature traits: rarity, activity, genders, weight and height (2026-11-12, overnight)

Ghidra on 184271, measured with `fauna-check` (`TRAITS=1`) against the owner's captured creatures
(`planet_creatures`), matched to predictions by species name on the same planet (479 of 519 matched):

| Trait | Where it comes from | Exact |
|---|---|---|
| Rarity (discovery list) | spawn data `0x1416d9650`: groups per km / max(scale, 0.75), over the domain's very-dense groups per km (birds: air); below 0.33 Rare, below 0.66 Uncommon | 477 / 479 |
| Activity | the role's `ActiveTime`, words `UI_CRE_ACTIVE_*` | 479 / 479 |
| Gender + weight/height | `0x1416cfcc0`, see below | 407 / 446 whole forms (every complete capture) |

`0x1416cfcc0` runs once per creature, in order, drawing from the planet generator's own stream (+0xba4: the
stream `GenerateCreatureRoles` takes its two root draws from, i.e. `RngStack.Root`). Each text pick
(`0x1416cdfb0`) counts the numbered keys PREFIX1..n and makes one draw. For a normal creature: behaviour and diet
picks (one draw each), then Height1 = min(scale, 10) + f * 0.4 - 0.2, Weight1 = min(scale, 10) * 40 + f * 50,
Height2 = Height1 + f * 0.2, Weight2 = Weight1 + f * 15; notes (one draw); gender 1 over `SEX1..16`, gender 2
redrawn until different; age (one draw, <= 0.3). Robots (diet Robot, ROBOT worlds) pick on their own seed; with
ROBOTANTELOPE and WALKINGBUILDING also notes and genders: on the creature's own stream (StartState(seed)) five text
picks (three on a normal world), the robot note and its description, then gender 1 and 2 over `UI_ROBOT_GENDER_1..9`
(no "must differ"). The owner's four robots (robot worlds) all show draw 8, gender 1.

A species comes in **two forms**: gender 1 with weight/height 1, gender 2 with weight/height 2. The Discoveries
page shows the form you scanned; the analysis visor shows the individual, close to one form. The 39 misses are all
incomplete captures (gender without size or the reverse; the part recorded matches a form). Spelling is compared
loosely: the owner's game is in US English ("Vectorized") while the catalog reads UK English ("Vectorised"). Sizes are
formatted through double, as printf rounds the float's exact value (146.9499 -> 146.9).

**Behaviour, diet, notes** (the analysis visor's lines): the same picks, now with their words. GcCreatureGlobals
names a key prefix per role (`Temperments`: TEMPERAMENT_PREY...) and per diet (`Diets`, `WaterDiets` for creatures
in water - the game tests the role's TileType 4, taken as "in water", unconfirmed); the pick draws only when keys
exist. Diet text fills %BIOME_PLANT% / %BIOME_MEAT% from `DietVeg` / `DietMeat` by biome (swamp and lava sub-biomes
use 12 / 13). Notes are `UI_CREATURE_NOTE_<n>`. The owner's three scans on a Hikovsky planet (2026-11-11
screenshots) are exact: Hasty / Solar Vine / Photosensitive, Methodical / Solar Vine / Drinks blood, Peaceful /
Solar Vine / Can sense magnetic fields. Robots' behaviour (draw 1, UI_CREA_ROBOT_BEHAVE_: binary words), diet
(draw 4, UI_CREA_ROBOT_DIET_) and notes (draw 6, UI_CREA_ROBOT_NOTE_; draw 4 for a robot antelope or walking building
on a normal world) follow the gender-confirmed draw order but aren't checked against a scan yet. Not yet: Age words.

**Produces** is per kind: `0x1412345f0` looks the spawned creature's id up in GcCreatureGlobals `HarvestingProducts`
and keeps the index; `0x1412431d0` gives FOOD_V_MILK (Wild Milk) to kinds not listed. The owner's four scans match:
SIXLEGCAT Leopard-Fruit, FLOATSPIDER Crab 'Apple', TWOLEGANTELOPE Wild Milk, WEIRDFLOAT Hexaberry. The catalog keeps
the map and each product's NameLower ("Leopard-Fruit").

Catalog format 80: gender, behaviour, diet and note words, the role's `ActiveTime` and its words. Fauna totals (986 / 986) and species names
(881 / 904 this year) unchanged.

## Next layers

1. ~~Waterworld air rule~~ (solved above).
2. Habitat split: 17 planets where recorded habitats don't fit (e.g. recorded flying x4, predicted flying x1).
   Some are capture noise (blank habitat rows).
3. ~~Creature identity seed~~ (96%, above); species names exact for 97% of this year's discoveries (above). With the seed pair
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
