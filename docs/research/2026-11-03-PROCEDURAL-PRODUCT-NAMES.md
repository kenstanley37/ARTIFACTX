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

## Open: the seed

The only caller (0x140ee1377, in the description builder starting before 0x140edfed0)
passes `Mix64((w2 << 32) | w1)`, where w1, w2 are the next two outputs of its own
generator, which has already drawn many values (age, value...) since it was seeded.
So the name seed is not the `#NNNNN` number directly: plain decimal, hex, FNV-1a of
the id and several other guesses all fail on the four names above (test harness:
the session's scratchpad `seedsearch`/`procname` projects). Next step: trace the
description builder's generator from its entry (or emulate 0x140edfed0..0x140ee1377
with stubs, as the title-unlock oracle does) to the name call, then check all four.
