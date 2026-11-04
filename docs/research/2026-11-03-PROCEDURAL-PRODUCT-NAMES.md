# Procedural product names (Wonders treasures)

Build 184271, read 2026-11-03 (static disassembly only; nothing was run). Goal: the
name the game gives a procedural product such as `PROC_LOOT#06686` ("Shiny Bones",
owner's Slot 8 Treasures screen). Ground truth from that screen:

| Item | Name |
|---|---|
| `PROC_LOOT#06686` | Shiny Bones |
| `PROC_BIO#15819` | Precious Hatchlings |
| `PROC_FOSS#41022` | Carbonized Beetle Antennae |
| `PROC_PLNT#00902` | Pulpoak Cultivar |

## Data

`metadata/reality/tables/proceduralproducttable.mbin` (GcProceduralProductData):
`NameGeneratorBase` (template word, e.g. `ITEMGEN_FORMAT_LOOT`), `NameGeneratorWordList`
(per placeholder: common/uncommon/rare word + `ReplaceKey` such as `%LOOTADJ%`) and
`NameGeneratorLegacyRolls` (LOOT 0, BIO 5, FOSS 8, PLNT 11). Words are localisation
keys with numbered variants `<word>_<n>` (n from 1): LOOT has 2 templates
("%LOOTCONT% of %LOOTADJ% %LOOTNOUN%", "%LOOTADJ% %LOOTNOUN%"), 27/48/46 words. Every
`NumOptions` in the file is 0; the game must fill the counts at load.

## Name routine: 0x140eb1d10 (chunks to 0x140eb2624)

Read in the disassembler, confirmed instruction by instruction:

1. `state = StartState(Mix64(seed))` - the same seeding as Core's MultiToolNames.
2. Step the generator `NameGeneratorLegacyRolls` times (0x140eb1dc8 loop).
3. For each word-list entry (0x98 bytes each, 0x140eb1e09): pick common/uncommon/rare
   by the rarity argument (r9d: 2 rare, 1 uncommon, else common), step once, and if
   that word's NumOptions > 0 take index `(low32 * NumOptions) >> 32`, key
   `"%s_%i"` (0x144b7a65c) = word + "_" + (index + 1).
4. Then the template the same way from `NameGeneratorBase` (0x140eb2203).
5. Replace each `ReplaceKey` in the template with its word.

## The seed: solved 2026-11-04

The description builder is one function, 0x140edee70 (chunks to 0x140ee165e, found
through the `.pdata` chained unwind entries). Read in the disassembler:

1. 0x140ee7050 splits the id at `#` and parses the seed with `atoi` ("06686" -> 6686;
   `0b`/`0x` prefixes are binary/hex).
2. 0x140edf14f hashes it (Wang style): `h = (s ^ 61) ^ (s >> 16); h *= 9; h ^= h >> 4;
   h *= 0x1b873593; h ^= h >> 15`, and seeds the generator with `StartState(h)`.
3. Two steps: w0 picks the value in BaseValueMin..Max, w1 the age in AgeMin..Max
   (of the rarity chosen next). The state goes to `[rsp+0x40]`, a copy to `[rsp+0x30]`.
4. 0x140ee6a20 steps the copy: the rarity, weighted by the three `ProceduralData[]`
   DropWeights (first running total >= `((word * total) >> 32) + 1`).
5. A jump table on the category: FreighterPassword, FreighterCaptLog,
   FreighterCrewList, FreighterTech* (0x10-0x16), DismantleData and MessageInBottle
   branch off; every other category reaches 0x140ee1229.
6. 0x140ea23b0 steps the real state (the same third word): the rarity's
   `Description` variant.
7. Two more steps, w1 and w2; the name routine gets `Mix64((w2 << 32) | w1)`, the
   product's table entry and the rarity.

The name routine mixes that seed **again** before StartState - the step the earlier
guesses missed. Port: `src/ArtifactX.Core/Procedural/ProceduralProductNames.cs`;
all four names above match (the catalog's British English spells "Carbonised").
In the routine, a rare or uncommon word whose key is empty falls back to the common
word, and every word takes one step even when it has no numbered variants.

Not yet checked against the game: products whose uncommon/rare lists differ
(PROC_SALV, PROC_BONE, PROC_STAR) and the description text (step 6), which the app
doesn't show.
