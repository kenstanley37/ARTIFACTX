# Creature and flora species names (2026-11-04, solved)

This covers Feature 63 (the names on Fauna and Flora records) and Feature 42 (companion species names). It comes from static disassembly of NMS.exe build 184271, checked against the owner's Slot 8 screens.

## Status

**Solved: 26 of 26 names match.**

- All 14 Fauna records, including the one record that appears twice.
- All 8 Flora records.
- 4 companions: Witchilly "Z. Wingeebus", Ana-Ana "A. Rainrobeus", Beary-Beary "F. Lobcineus" and Bihi-Bihi "L. Leafuivateum".

The code is Core `ArtifactX.Core.Procedural.SpeciesNames`. Where the seeds are in the save is in `NmsSpeciesSeeds`. The check is `tests/ArtifactX.Core.Checks --species-name-vectors <words.json>`.

Mineral record names come from a third routine (`0x140eb48a0`). It hasn't been read or checked yet, because there are no in-game mineral names to check against.

## Seeds

**Fauna** (`0x140eb39f0`)
- The names come from the creature's discovery entry in `fDu/ETO/OsQ/?fB` (8P3, type "Animal").
- The name seeds are the 3rd and 4th values of its `bEr` list (`seedA = bEr[2]`, `seedB = bEr[3]`). The discovery namer at `0x140524d3b` reads `+0x18` and `+0x20`.
- The Fauna record itself (`MZ1` UqY) keeps only the planet address and `bEr[0]`, so the app matches each record to its discovery.
- That's why the first attempt failed: it used the record's own pair as the seeds. That attempt got "Must" right for "J. Mustlimii" only by chance.

**Flora** (`0x140eb4190`)
- A single seed: `bEr[0]`, which is the Flora record's (`fp9`) UqY[1].

**Companions** (`0x14075733b`)
- The two seeds are `m9o` and `JrL`.
- If the companion has a custom species name (`HhX`), the game shows that instead.

## Generator

**1. Seed.**
- Fauna: CityHash `Hash128to64(low = seedB, high = seedA)`, then StartState.
- Flora: `Mix64(seed)`, then StartState.

**2. Root word.**
- Fauna: `NAMEGEN_CRE_((w * 300) >> 32 + 1)`. The list has 350 entries; only the first 300 are used.
- Flora: `NAMEGEN_FLORA_ROOT_((w * 250) >> 32 + 1)`.

**3. Generated word.**
- The maximum length is drawn first:
  - Fauna: `(w * 3) >> 32 + 4`.
  - Flora: `(w >> 31) + 4`.
- `0x140e9f9f0` then builds the word with `{alphaset 6, min 3, max}`. It is the same word builder as Core `NameGenerator.Generate`.
- The word is added after the root.

**4. Ending, by the last letter.**
- 'a' or 'o': add "e".
- 'e' or 'i': draw once; odd gives "a", even gives "um".
- 'u': draw once; odd gives "m", even gives "s".
- Anything else: draw once and take an ending from the Latin table at `0x144b7cda0`, `(w * 14) >> 32`. The table is: ium ima ica osa eus era eum ae ea ia is ii us um.

**5. Rude-word filter.** It runs twice (`0x140ea0600`, Core `NameGenerator.Clean`).

**6. Consonant fix.**
- After three consonants, the next letter is checked.
- If it isn't a vowel or 'y', one drawn vowel from "aeiou" goes in two places back, unless that position is index 2.
- Example: "Appglutera" becomes "Aippglutera".

**7. Genus letter.** One draw gives `'A' + (w * 26) >> 32`. The output is `"%c. " + word`.

## Words

The catalog step `AddMultiToolNameWords` (format 69) now also keeps the lists `CRE` and `FLORA_ROOT` from the player's own English language files.
