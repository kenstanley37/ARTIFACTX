# Creature and flora species names (2026-11-04, in progress)

This work is for Feature 63 (Wonders Fauna and Flora names). It also covers Feature 42, because companions use the same generator. The findings come from static disassembly of NMS.exe build 184271.

## Status

**Not solved.** One known name ("J. Mustlimii") doesn't reproduce: the seed and prefix fit, but the generated middle doesn't. More in-game names are needed (see the last section).

## The generator

The fauna name routine is `0x140eb39f0(names, seedA, seedB, out, outGenus)`. The flora routine is `0x140eb4190`, and `0x140eb48a0` is a third, probably mineral.

1. **Seed.** The seed is CityHash `Hash128to64(low = seedB, high = seedA)` (`0x140eb3a27`). Then StartState, then one step.
2. **Prefix word.** The routine looks up loc text `NAMEGEN_CRE_%i`, with `i = ((w * 300) >> 32) + 1`. The table has 350 entries in `nms_loc4_english.mbin`: "Canis", "Bison", "Must"...
3. **Generated word.** The next word sets `max = ((w * 3) >> 32) + 4`. It is followed by `0x140e9f9f0(names + 0xc0, state, {alphaset 6, min 3, max}, out)`. That is the same word builder as `ArtifactX.Core.Universe.NameGenerator.Generate`: the struct is `{alphaset, min, max}` and the alphaset sizes match Core's (0x178c, 0xe0d ... 0xf00 for alphaset 6). The generated word is appended to the prefix.
4. **Ending.** The ending depends on the last letter:
   - 'a' or 'o': add "e".
   - 'e' or 'i': one step, then "a" if the word is odd, otherwise "um".
   - 'u': one step, then "m" if odd, otherwise "s".
   - Anything else: one step, then the Latin table at `0x144b7cda0`, indexed `(w * 14) >> 32`. The table is: ium ima ica osa eus era eum ae ea ia is ii us um.
5. **Clean-up.** The rude-word filter (`0x140ea0600`, Core's `NameGenerator.Clean`) runs twice. Then, at the first run of four consonants ("aeiou", with 'y' ending a run), one random vowel from "aeiou" goes in at `i - 2` (`0x140eb3e3f`).
6. **Genus letter.** One step gives `'A' + ((w * 26) >> 32)`. The output is `"%c. " + word`.
7. **Second output.** An optional second output builds a genus word with `{6, 5, 5 + (w >> 30)}` plus ".". It isn't needed for the short name.

## Callers

- **Discovery naming** (`0x140524c81`, "! ERROR ! NO PROC NAME !"). It switches on the discovery type (+0x38). For a creature it passes `seedA = seeds[2]` and `seedB = seeds[3]` (zero if there are three seeds or fewer).
- **Companions screen** (`0x14075733b`, next to "%0.1fkg / %0.1fm" = scale x 40). It passes the pet record's two seeds at +0x23a8 / +0x23b0. Getting this right gives companion species names as well (Feature 42).

## The one known name

- **The record.** Slot 8's Largest Herbivore record (`vLc/6f=/MZ1[0]`) has UqY = [0x40FE00011476E2, 0x70C0A2A5046D89F0]. The game shows "J. Mustlimii".
- **What fits.** With `seedA = UqY[0]` and `seedB = UqY[1]`, the first draw picks `NAMEGEN_CRE_117` = "Must". That fits "Mustlimii".
- **What doesn't.** The rest gives "L. Mustsuosusia". No alphaset, skipped draw or min/max pattern within 12 draws gives the chunk "lim" (alphaset 6 index 1842). So either the record values aren't exactly the discovery seeds (and "Must" is a 1-in-300 coincidence), or the builder for this call differs from Core.

## Needed

Screenshots of the in-game Fauna and Flora record names (Slot 8 or Slot 1), each with its record slot. Companion species names from the Companions screen would work too, since that screen passes the pet's own two seeds.
