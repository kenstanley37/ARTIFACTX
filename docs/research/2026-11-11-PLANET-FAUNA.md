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
on a normal world) follow the gender-confirmed draw order but aren't checked against a scan yet.

**Age** (the visor's, 0x1412e3f40): one draw w from the role seed (StartState, one step). A baby shows AGE
(w * 3 >> 32) + 1 (Infant, Young, Juvenile); an adult of a regular species (w * 3 >> 32) + 4 (Adult, Mature, Elderly);
of a species with GcCreatureInfo.Age Weird - the info routine's age draw, <= 0.3 - (w >> 30) + 7 (Indeterminate,
Constant, Fluctuating, Perpetual). The owner's five scans match: Perpetual x2, Elderly (adults), Juvenile, Infant
(young ones).

**Produces** is per kind: `0x1412345f0` looks the spawned creature's id up in GcCreatureGlobals `HarvestingProducts`
and keeps the index; `0x1412431d0` gives FOOD_V_MILK (Wild Milk) to kinds not listed. The owner's four scans match:
SIXLEGCAT Leopard-Fruit, FLOATSPIDER Crab 'Apple', TWOLEGANTELOPE Wild Milk, WEIRDFLOAT Hexaberry. The catalog keeps
the map and each product's NameLower ("Leopard-Fruit").

Catalog format 80: gender, behaviour, diet and note words, the role's `ActiveTime` and its words. Fauna totals (986 / 986) and species names
(881 / 904 this year) unchanged.

**Pet battle stats: SOLVED (2026-11-12).** Classes: seed = Hash128to64(low = genus, high = species);
XXH3-64 (seed 0, default secret; `System.IO.Hashing`) of StartState(seed) + "CORE_STAT_CLASSES", then per stat
XXH3-64 of StartState(that) + the 16-byte name (COMBAT, SPEED, HEALTH in GcPetBattlerCoreStat order MaxHealth,
Speed, CombatPotential), one generator step, weighted C/B/A/S pick (GcGameTableGlobals CoreStatClassRollTable).
Overall = highest of thresholds 0/1/4/7 the class sum reaches. The visor lists Combat Effectiveness =
CombatPotential, Agility = Speed, Health = MaxHealth. Element: the creature data's PetBattlerForcedAffinity (1-8),
else PetBiomeAffinities by biome (swamp 12, lava 13, anomaly 7). Four visor panels match exactly (Elephabatum,
Dasiythrodia, Scrofbuyae, Warloxigesea). `Core/Companions/PetBattlerClasses`; catalog format 82.

Earlier lead, kept for reference: the analysis visor's "Trait Potential" panel (Combat Effectiveness, Agility,
Health classes and the element) is drawn by `0x140757700` (`UI_PB_BINOC_HEADER_WILD`) from stats its caller
`0x1412da090` builds: pet data for the wild creature, then `0x1413cfa80` for the battler stats; the element is the
creature data's `PetBattlerForcedAffinity` (+0xcc) unless 0 or 9, else a per-biome table (`0x145328c9c`). Ground
truth: the owner's three 2026-11-11 visor screenshots (I. Elephabatum C/C/C Fire, N. Dasiythrodia B/A/S Fire,
H. Scrofbuyae S/B/S Fire; trait potential C, A, S).

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

## Hemisphere ("Found in the North / South"), 2026-11-12

NMS.exe 184271, Ghidra. Each domain archetype (and each additional-table group) has ChanceOfHemisphereLimit and
MaxToHemisphereLimit. 0x1416d8c20: one draw against the chance; when under it, MaxToHemisphereLimit draws each add
one to a per-table count (index = draw * table count >> 32). 0x1416d9190 stores that count with the collected table
(cGcRoleTableData +8). In the role loop of 0x1416d60e0, each role opens a child stream (the third push, which the port
already discarded): f = one draw; if 1.0 (0x144bf5c48) / the table's role count < f and the table's count > 0, one
more draw x gives the hemisphere (~x & 1) + 1 (1 Northern, 2 Southern) and the count drops by one. The child stream is
popped, so the picks are unchanged (fauna totals still 987/987, activity 487/487).

Words: UI_CRE_HEMISPHERE_NORTH / _SOUTH (catalog format 84). Ground truth so far: Doroug XV (13500FE0E2923 planet 3)
I. Aippglutera predicted South, seen South in game 2026-10-22 (issue #6). The issue's "T. Milkvigneum: Always Active /
Found in the South" is most likely Aippglutera's scrolling row: Milkvigneum's captured activity is Diurnal, and the
port says it is found anywhere. More checks wanted. Harness: `HEMI=<system hex>:<planet> dotnet run` in
scripts/research/fauna-check.

CONFIRMED in game 2026-11-12 (owner, Slot 8): Haka Beta (BBFAFCFFA004 planet 3, AC Colony Star) predicted only
T. Airchedeisium as South; the game's Discoveries list shows "... Active / Found in the South" on T. Airchedeisium and
no hemisphere on the other 9 (10/10). Unst Prime (planet 2): none predicted, none in game (14/14). Two planets, 2/2 South.

## Habitat word, 2026-11-12

The discovery line is built by 0x14077b2e0 ("%s / %s / %s / %s": rarity, habitat, activity, hemisphere). Habitat =
UI_CRE_TERRAIN_<TileType> (GcTerrainTileType order: AIR, BASE, ROCK, MOUNTAIN, WATER, CAVE, DIRT, LIQUID, SUBSTANCE),
where TileType is the role data's (+0x5CC), copied from its GcCreatureRoleDescriptionTable (+0x1C) in 0x1416d60e0;
creatures of GcCreatureTypes 1-4 (Bird, FlyingLizard, FlyingSnake, Butterfly) or 48 (ProtoFlyer) always read AIR.
Owner's Unst Prime (BBFAFCFFA004 planet 2) list: large butterflies and a small bird from ground tables show "Flying" -
now predicted the same. Catalog format 85 (TileType per role table, habitat words). The water diet list now keys on
TileType 4 too. Habitat fit vs captures 87 -> 94 of 104 planets; the rest are blank captures and a few Underground vs
Ground cases still unexplained.

## Body parts (issue #15), 2026-11-12

A creature's body parts are picked by the same descriptor selector as ships and multi-tools (Core ShipParts, ported
from NMS.exe's selector) on its scene's part tree with its **role seed** (discovery bEr[0], PredictedCreature.Seed).
Evidence (owner's seven saves, fauna-check `PARTS=1`): a companion's CreatureSeed WTp equals its species' discovery
bEr[0] for 132/132 companions that have a discovery; ShipParts with WTp reproduces the stored descriptor list (osl)
exactly for 90 of 182 companions (112 with any of its seeds), the rest mostly whole rerolls (seed or parts changed
after adoption). Creature part trees are in the catalog from format 87 (ShipDescriptors, UsageCategory Creature).

Open: the anomalous rig (WEIRDRIG) takes a role **Filter** ("FLOAT", "ROLL", "CRYSTAL", from the role description
table) that forces its first choice; neither "take it without a draw", "narrow then draw" nor "redraw until it
matches" reproduces all the owner's floaters and rollers - read the selector's filter handling (0x142d6b5a0 area).
Colours are not done: the owner's reroll tests say WTp also drives the colours (palettes, not ported).

**Correction, same evening (and reverted):** bEr[4] = Mix64(bEr[0]) on 363/365 records, but it is **not** the parts
seed: for G. Towerravusosa it picks a frilled triceratops head with one nose horn, where the game shows the hippo head
with two horns, turtle body with back spikes and triceratops tail that the role seed bEr[0] picks (owner's screenshot
2026-11-12). The role seed stays; bEr[4] is maybe the colour seed. The app's flat colour guesses (orange shell and
accessory materials) made the right parts look wrong at first - colours are the next step.

**Confirmed in game 2026-11-12 (owner, Slot 8):** G. Towerravusosa walking on Menuscuryd Setaj has exactly the parts the
role seed picks (hippo head with head plate, turtle shell with spikes, triceratops tail) - "the app matches the
creature walking around perfectly". The game's own Discoveries page draws the same species differently (horned head,
other back spikes): the game is inconsistent there, not the port. The rig's pet harness (PETHARNESSMAT, not a
selectable part) is now left off wild creatures. GameData.Checks `--creature-part-probe` shows what each part is.

**Anomalous rig solved 2026-11-12:** the WEIRDRIG bodies are named after the exotic sub-biomes (GcBiomeSubType:
FractCube, Bubble, Shards, WireCell, HydroGarden floaters; Structure, Contour, Beam rollers). The role's Filter (FLOAT /
ROLL / CRYSTAL, role description table) forces the first choice and the planet's sub-type the variant - not a draw,
which is why no draw rule fitted the owner's anomalous companions. Owner's B. Bloatlexia on the HydroGarden planet
Azzaviu XII (4F8FBFFA005 planet 4): glowing orbs in game = _FLOAT_HYDROGARDENXOR (VORTEXLIGHT / CORELIGHT materials);
the seed alone picked _FLOAT_SHARDS. Catalog format 88 (role Filter, sub-type names); CreatureTables.ForcedPartTerms /
ForcedOptions feed ShipParts' stored-parts path. Crystal bodies (Pillar / Gem) have no sub-type name: seed-picked.

**Roller confirmed in game 2026-11-12 (owner, Slot 8):** K. Larvalamia on the Structure planet Ailsdoni
(Angeym-Kuhovd II, portal 3045FBFFA005, found with fauna-check EXOTIC) is the _ROLL_STRUCTURE body in the world and in
the app. Two of two anomalous checks (HydroGarden floater, Structure roller); crystals (Pillar / Gem) still unchecked.

**Crystals use their own scene (2026-11-12, to confirm in game):** the owner's R. Gentdiffoe on the BoneSpire planet
Aoedesdo (Iladdu-Bulga, portal 116EFBFFA005) is a tall block in the world; the app drew WEIRDRIG's _CRYSTAL_GEM (an
"egg"). WEIRDCRYSTAL's creature row has an ExtraFilename scene, MODELS/PLANETS/CREATURES/WEIRD/WEIRDRIGGROUND, whose
_CRYSTAL_ group holds _Crystal_Beam, _Crystal_BoneSpire, _Crystal_Hexagon, _Crystal_Pillar and _Crystal_ShardSingle -
sub-type names again. Their ids are cut to 15 characters (_CRYSTAL_BONESP, _CRYSTAL_HEXAGO, _CRYSTAL_SHARDS), so
ForcedOptions matches on option names, and "Shards" also matches "ShardSingle". Catalog format 89 stores the scene
(CreatureEntry.ModelScene); Planet Details draws from it when present. fauna-check PICK for that creature now gives
_CRYSTAL_BONESP (448 triangles, BONEFORMSTATIC materials) where the seed alone gave _CRYSTAL_BEAM. Not yet seen by the
owner; the block in the screenshot could also be _Crystal_Pillar (MINERALMAT), which no sub-type names.

**Crystal = two scenes (owner, 2026-11-12):** the live R. Gentdiffoe is a green crystal pillar with bone spires
coming out of the ground round its foot; the Discoveries page shows only the spires. The game draws the creature's own
scene (WEIRDRIG: _WEIRD_CRYSTAL -> _CRYSTAL_PILLAR / _CRYSTAL_GEM) and spawns its ExtraFilename scene with it
(WEIRDRIGGROUND: the sub-type's ground piece, _Crystal_BoneSpire here). The flock scenes (BIRDFLOCK, FISHFLOCK,
BUTTERFLYFLOCK) and WEIRDDIGGER have no geometry of their own (--creature-part-probe "*"), so "draw both" also gives a
flock's single animal and the digger's GROUNDCREATURE. Pillar vs Gem: taking the filtered _WEIRD_CRYSTAL without a
draw gave the Gem; narrowing the group and still drawing gives the Pillar seen in game, and leaves the HydroGarden
floater and Structure roller as confirmed (ShipParts drawForced). Mix64(seed) also gives the Pillar, but it was
already ruled out for ordinary bodies (G. Towerravusosa). One crystal so far: a second one where the two disagree
would settle it.

## Colours (issue #15), 2026-11-13

Every creature material is a procedural texture list like a ship's or a multi-tool's: layers tinted from the palette
table - Fur, Scale, Underbelly, Rock, Leaf, Feather, Metal, Custom_Head (GameData.Checks `--creature-part-probe` now
lists each material's palette slots; cow, cat, triceratops and bird scenes checked). Only the pet harness and a few
eyes are fixed textures. So creatures now go through the same painter (ShipModels.Painted): the seed picks layers and
patterns (PickLayers), the palette table (ShipLook.PaletteTable) gives the colours. CreatureModelService.PaintAsync.

Inputs: the role seed (WTp on a companion) for both; a companion's ColourBaseSeed (uAX) when its flag is on replaces
the colour seed only - the owner's 2026-07-28 tests (NmsCompanionPaths) showed WTp changes pattern and colour, uAX only
colour. **Not confirmed:** whether the palette table takes the seed itself (starships) or its first child seed
(multi-tools, ModelPreviewService.ColourSeed); the port uses the child seed. Also unknown: whether wild creatures take
any planet palette, and what GcCreatureSpawnData SwapPrimaryForRandomColour / SwapPrimaryForSecondaryColour do.
Ghidra lead not followed yet: the readers of GcPetData +0xF8 (ColourBaseSeed) lead to the creature's palette table.
Ground truth wanted: screenshots of live creatures (not Discoveries) next to the app's model.

**Owner test on Jinnie II, 2026-11-13 (Kostima-Kuloft, 000412F6FCFFB005, Slot 8):** seed-palette colours don't match:
every live creature there is red-brown / tan like the planet (N. Radiotaylae red-brown with stegosaurus back plates;
I. Homobocamosa tan with quills; K. Bearxiumica yellow-olive fur, sky-blue spotted back frill), the app drew green,
yellow-black stripes, red. The base palettes (colours/basecolourpalettes.mbin) hold only muted earth tones, so a vivid
purple fur (an earlier live shot) can't come from them. Each biome has its own 66-palette file (GcBiomeData
ColourPaletteFile, e.g. biomes/lush/lushcolourpalettes.mbin, GcPlanetColourData: 64 colours per palette, far richer -
lush Fur has purple, gold, orange). **Next hypothesis:** creature (and flora) colours come from the planet's colour table
= the palette-table generator over the planet's biome palette file with a planet seed; the creature seed picks texture
layers and colour slots only. To port: the planet's biome file (biome + sub-type -> GcBiomeData), its palette file, the
planet colour seed (raw / child of the planet seed - check against a few planets' flora and creatures).
Parts are also off on Jinnie II (Radiotaylae back plates drawn as a small frill; Bearxiumica gets horns), so parts need
fixing before colours can be judged. GameData.Checks `--mbin-dump <path part> [n]` lists / dumps game files.

**Parts vs gender form (2026-11-13, owner's Jinnie II scans, planet 000412F6FCFFB005 index 2):** the two individuals
scanned in their species' **first** form match the role-seed parts: K. Bearxiumica ("Radical") has the narrow scalloped
back fin _SBACK_6 (FINFLAPS; Mix64 would give bulbs), I. Homobocamosa ("Radical") the hunched quilled body. The one in
its **second** form, N. Radiotaylae ("Female"; forms "Exotic or Female"), has big stegosaurus plates (cat _BCA_11 /
_BLBACK_7, BACKSPINE material) that neither the role seed (_HEAD_CAT, _BCA_14 fur tuft) nor Mix64 (_HEAD_WOLF,
_BCA_X7 side fins) gives. Hypothesis: the second form is built from another seed. No simple transform (+-1, ^1, swap,
child seeds 0-2, Hash128(seed, 0/1/2), Mix64 twice...) reproduces the companions the role seed misses (fauna-check
PARTSGENDER=1) - but those misses may be rerolled or Egg-Sequencer pets. Ghidra: the wild -> pet copy (0x1405110b0,
called from the visor's 0x1412da090) copies the body from the live creature, so the choice is made at spawn, not there.
Not found yet: the code filling GcCreatureSpawnData.FemaleResource (seed at +0x78) next to Resource (+0xC0).
Owner's photo-mode shot of the same Female N. Radiotaylae: back plates, a hog tail with two blade fins (_TAIL_HOG +
_THA_X2, ACCFINS), lizard-like bumpy skin. fauna-check PICKSEARCH over 235 derived seeds (Mwc draws and child seeds
0-31, Hash128 both ways with 0-31, seed +-^ 0-31, planet / species / genus mixes) for "hog tail + blade fins + plates
(_BCA_11 / _BCW_4 / _BLBACK_7)": 6 hits, about chance (~2%). The suggestive ones: seed+3 (= seed^3: _HEAD_WOLF, _MESH_WOLF
_BCW_4) and seed-3 (_HEAD_CAT, _MESH_LIZ _BLBACK_7) - if each individual's parts seed were the role seed plus its index
in the group, individuals of a species would differ. Open question for the owner: do individuals in one herd look
alike?

**Two looks per species = the two gender forms (owner's herd shots, 2026-11-13).** An I. Homobocamosa herd shows
exactly two looks: quilled ones scan "Radical" (form 1, 180.5 kg / 4.3 m) - the role-seed parts, which the app draws;
smooth hunched ones without quills scan "None" (form 2, 193.7 kg / 4.4 m). So individuals don't vary freely: form 1
is built from the role seed, form 2 from something else. Candidate: role seed - 3 (= seed ^ 13 for both seeds tried)
fits both form-2 sightings - Radiotaylae plates + bladed hog tail (_HEAD_CAT _MESH_LIZ _BLBACK_7 _TAIL_HOG _THA_X2),
Homobocamosa the same turtle head on a diplo body with a big smooth back plate (_BODY_DIPLO _DBACC_2,
ACCPLATEBODY) - but it reproduces none of the companions the role seed misses on rich trees (cat, grunt, trex,
triceratops, rodent, strider), so it is unproven (likely chance; those pets are probably rerolled or egg-born).
Next: Ghidra for where a spawned individual's gender picks its model seed (GcCreatureSpawnData Resource +0x90 /
FemaleResource +0x48, seeds at +0xC0 / +0x78); FindAccess on those offsets plus the swap-colour flags found only
unrelated code (0x1404761a0...). The app shows form 1 - correct for half of each herd.

**Planet colours (2026-11-13):** a wild creature's palette table is its planet's - ShipLook.PaletteTable over the
planet's biome palette file (GcBiomeData.ColourPaletteFile; catalog format 91 "BiomePalettes", biome x sub-type ->
file), seeded with ColourSeed(planet seed) (the first child seed). Jinnie II (biome 10 HugeToxic, base palettes, planet
seed F9D9E98106914B45): K. Bearxiumica's back fin FINFLAPS (Scale Primary + Alternative1) comes out brown #402E18 and
teal #2E706C - the owner's shot shows a sky-blue frill with brown spots; the creature's own seed gives dark blue +
olive, the raw planet seed grey + olive (no blue). Fur brown / olive fits Bearxiumica and Homobocamosa. One planet so far:
check another biome (one with its own palette file, e.g. lush) next. Companions keep their own seed on base palettes.

**Pattern layers use the creature seed's child (2026-11-13):** GameData.Checks `--creature-layer-probe <material>
<seeds...>` lists the layers a seed picks. I. Homobocamosa (owner: plain brown, no stripes): the role seed picks
TAPIRBODYMAT MARKINGS=BELTS (the stripes the app drew), its first child (ColourSeed) GRADIENT on body and head.
K. Bearxiumica (owner: frill with spots): child gives FINFLAPS MARKINGS=SPOTS; role seed GRADIENT, Mix64 SPIKES.
So: parts from the role seed, pattern layers from ColourSeed(role seed), colours from the planet's table
(ColourSeed(planet seed) over the biome palettes). Preview lighting moved to linear light (ShipPreview.Lit).

**Colour slot offset per creature (2026-11-13, owner's close-ups):** with the planet table (Jinnie II, ColourSeed(planet
seed), base palettes) every sighting fits "slot s reads palette entry (s + k) mod 5" with a per-creature k (Unique
read as slot 3): I. Homobocamosa k=3 - Fur[3] #5F3624 orange-brown body, Underbelly[3] #4C5E5D grey-teal belly patch
(close-up); K. Bearxiumica k=1 - Fur[1] #5F521D yellow-olive, fin base Scale[1] #2E706C teal with spots Scale[2]
#704F05 brown; W. Gentaylatica k=4 - petals Grass[4] #245E71 blue and Leaf[(3+4)%5=2] #A64D00 orange. The app uses k=0,
which gives the white bellies (Underbelly[0] #BEDBB6). Likely GcCreatureSpawnData.SwapPrimaryForRandomColour. k not
found: Mwc draws 0-15 of the role seed / its children / Mix64 (x5 multiply-high, mod 5, float), direct mod 5, list
position (2/3/5) and role (3/4/1) - only chance-level hits. Next: Ghidra for where a creature's palette alt index is
offset (reads of the spawn data's +0x146/+0x147 swap flags), or more sightings to test candidates.

**Colour remap search, 2026-11-13 (later):** the remap structure exists - GcProceduralTextureColourIndices
{ int Alts[8] } (enum GcPaletteColourAlt: Primary, Secondary, Alternative3-5, Unique, MatchGround, None): slot ->
palette entry, used by no game file (runtime only). Its type-hash / "Alts" references in NMS.exe 184271
(0x141b96dc0-0x141b9d8d0, 0x141ba5cd0) are the reflection serializer only. Another reading of the sightings:
Bearxiumica = SwapPrimaryForSecondaryColour (Primary <-> entry 1: teal fin, olive fur; Secondary -> entry 0: brown
spots), Homobocamosa / Gentaylatica = SwapPrimaryForRandomColour (Primary -> entry 3 / 4). Those flags live in
GcCreatureSpawnData, which only biome files' hand-placed GcEnvironmentSpawnData use; procedural roles fill it at run
time. Dead ends: identity-table stores (0x142c55620 is a BC7 block decoder), "gDiffuseMap" users in 0x1416a37f0 (planet
atmosphere / terrain textures). Next lead: the creature spawner that copies a role into GcCreatureSpawnData and sets
+0x146 / +0x147.
