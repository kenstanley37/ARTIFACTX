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

## Companion names (the name before a player renames it)

A companion the player never renamed has no name in the save (`fH8` is empty). The game makes one up each time it shows the companion: "Witchilly", "Ana-Ana", "Bigava-TO0-92L-SG6". The port is Core `CreatureNames`; the check is `--creature-name-vectors`.

**How it was checked**

- The game's routine (`0x140eaa420`) was run in the Unicorn emulator from the station multi-tool work. The game's text lookup was swapped for the catalog's word list, and `strncat`, `GetTickCount64` and `__stdio_common_vsnprintf_s` were stubbed.
- It gives the owner's four Slot 8 companions exactly.
- The C# port matches the emulator on 560 random seeds across 14 creature kinds.

**Seed** (`0x1412304cc`, filling the Companions screen)

- The CreatureID is read as 16 bytes, for example "PROTOROLLER". An ID of "QUAD" is read as "QUAD_PET".
- The seed is `Hash128to64(low = Hash128to64(low = bytes 8-15, high = bytes 0-7), high = BirthTime)`. BirthTime is the save field `XwC`.
- When the call's third argument is set, the routine uses LastEggTime (`tDR`) instead. That isn't the Companions screen's path.

**Routine**

1. Mix64, then StartState.
2. **A** is `NAMEGEN_CRE_((w * 300) >> 32 + 1)`.
3. **B** comes from the word builder (alphaset 5, min 2, max `(w * 3) >> 32 + 3`), with its first letter capitalised.
4. **C** starts as a copy of A, and **D** as a copy of B.
5. A, C, B and D, in that order, each get the same four steps (`0x140eabcd0`):
   - an ending;
   - the rude-word filter;
   - a vowel in a consonant run (`0x140ea0570`; inserted at the returned index + 1);
   - the filter again.
6. **Harsh endings** are for FIEND, BUGFIEND, SCUTTLER_PET, QUAD_PET, HORROR_PET and SPIDERQUAD_PET:
   - 'a' gives "st" (odd draw) or "sh";
   - 'e' gives "sh";
   - 'i', 'o' and 'u' give "ss";
   - 'x' and 'k' get nothing;
   - anything else draws from a table of 28 at `0x144b7cf30`: ak, ush and others.
7. **Soft endings** are for every other kind:
   - 'a' gives "" (odd draw) or "zi";
   - 'e' gives "e" or "enia";
   - 'i' gives "" or "sta";
   - 'o' gives "" or "tto";
   - 'u' gives "y" or "i";
   - anything else draws from a table of 31 at `0x144b7ce30`: illy, etta and others.
8. A final draw `f` picks the form, checking these in order:
   - **Robots** (QUAD_PET, ROBO_PET, HOVER_PET, ROBO_RODENT, SPIDERQUAD_PET): "A-code". The code has `(w >> 31) + 2` parts, joined by '-' or '/'. Each part is one of five kinds: "%c%i", letter letter digit, "%i%c", a letter, or a digit.
   - **A of 5 letters or fewer**: "A-A" or "A-C".
   - **B of 5 letters or fewer**: "B-B" or "B-D".
   - FIEND, SCUTTLER_PET, BUGFIEND and LANDSQUID_PET skip both of those.
   - **A of 8+ letters and B of 6+**: A.
   - Otherwise, by `f`:
     - `f < 0.35` gives A;
     - `f < 0.7` gives B;
     - `f < 0.9` gives "A-B";
     - anything higher gives "B-E", where E is a fresh word made the same way as B.
