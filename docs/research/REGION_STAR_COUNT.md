# Region star count (Galaxy Search prerequisite 1)

Goal: know exactly which system indices exist in a region, so Galaxy Search
(ISSUES_LOG Feature 53) never lists a nonexistent system. NMS build 180836.

## Established (static disassembly, 2026-10-27)

- **Region voxel attributes** `0x14132AC60` (GcGalaxyVoxelAttributesData) carry guide,
  black-hole, Atlas and purple counts but **no total star count**. Purple systems are
  fixed: `PurpleSystemsCount = 0x41` (65), `PurpleSystemsStart = 0x3E8` (1000), written
  unconditionally at `0x14132AEBE..0x14132AEC5`. So purple indices are 1000..1064.
- **Loaded region objects** (galaxy-map pool, 175 slots of 0x280 bytes, initialised at
  `0x141354A00`) hold the stars. Lookup `0x141365420(map, &region, systemIndex, out)`:
  - finds the slot whose words at `+0x260/+0x262/+0x264` equal the region X/Y/Z;
  - `dword [slot+0x00]` is the **ordinary star count N**. An index above N that lies in
    the purple range [1000, 1065] is remapped to `N + (index - 1000)`;
  - `[slot+0x214]` is the total entry count; an index beyond it is replaced by 1
    (status 2): the game treats it as nonexistent;
  - `[slot+0x258]` is the per-star packed address array, `[slot+0x218]` 12-byte positions.
  - Indices are clamped to 1..0xFFF before lookup; index 0 returns status 2.
- The pool reserves 700 star entries per region (`edx = 0x2BC` at `0x141354AD1`), an
  upper bound for N + 65.

- The pool lives at `galaxyMap+0x50`; the galaxy-map object is constructed at
  `0x14132DC90` (pool init `0x1413549F0` is its only caller site, `0x14132DCF7`).
- Static helpers kept in the session scratchpad: `riprefs.py` (rip-relative xrefs),
  `callers.py` (direct call sites + containing function), `funcof.py` (.pdata bounds),
  `bytescan.py` (byte-pattern search), `nmsdis.py` (static disassembly).

### Star generator (found 2026-10-27)

- Per-star callback `0x141369C30(ctx, star, flag)`: the counter word at `[ctx->+0]` (capped
  below 0xFFF) gives the next system index (counter + 1). It calls `0x14132B3F0` for the
  star, appends the position via `0x14136DFF0(slot+0x210, ...)` and the packed address,
  with the index in bits 8..19 of the high dword, via `0x1413522F0(slot+0x250, ...)`, then
  increments the counter. So **N is the number of stars the generator emits**.
- Region generator `0x141367810` (one function, chunked to `0x141369A80`): calls the
  voxel attributes `0x14132AC60`, then emits stars through the callback in two early loops
  (`0x141367FED`, `0x141368253`, preceded by `0x14136A020`), then a **19 x 19 x 19 cell
  loop** (bounds `0x13` at `0x141369580..0x1413695A8`) using `0x142D74xxx`/`0x142D757D0`
  and `0x142D71BB0` (likely noise/density) before the third callback site `0x141369529`.
  A second caller of the callback is `0x141369A80` (`0x141369BD7`).
- Conclusion: the star count is the outcome of density sampling, not a closed formula.
  Plan: emulate `0x141367810` for a region (Unicorn harness in the earlier session's
  scratchpad `emu/`), confirm the emitted count and order against the owner's Noturbo Spur
  mapping, then port the loop.

### Emulated and validated (2026-10-27)

Unicorn harness (session scratchpad `regionemu.py`, `regionbatch.py`, on Codex's
`emu_sys.py` + `globals.py`): calls `0x141367810(regionAddress, slot, &offset)` with the
per-star callback replaced by a recorder that bumps the callback's counter word.

- The simplex noise reads a 16-entry float4 gradient table at `0x1459245E0` that is BSS
  in the file. Startup initializers fill `0x145924420..0x1459246D0`; run them first:
  `0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930,
  0x140199990, 0x1401999D0`. Without them every density is 0.5 and only the 122 fixed
  stars (120 guide + black hole + Atlas, callback flag 0) are emitted.
- Callback flag 0 = the fixed stars, flag 1 = density-grid and purple stars.
- **Correction:** purple systems are numbered **1001..1065** (counter + 1, like every
  star), not 1000..1064.
- Noturbo Spur (Euclid, region X 0x00C / Y 0x5C / Z 0x6DC): **N = 540** ordinary systems
  (1..540, contiguous) + 65 purple. All 165 systems in the owner's Slot 1 discovery
  records are in the emitted set.
- Every region in the Slot 1 discovery records (38 real regions across 7 galaxies):
  all visited indices are emitted. N ranges 242..579; several regions have visits close
  to N (506 vs 535, 474 vs 524). The only "misses" are the 0/0/0 placeholder address.

### Startup state correction and live ground truth (2026-10-27)

A read-before-write scan of `.data` during the generator (`regionreads.py`) found more
state the game fills at startup, zero in the file:
`0x146ED3DB0 = (0.5, 0.5, 0.5, 1)` and `0x146ED3DC0 = (-47.368, -47.368, -47.368, 1)`
(initializers `0x14003A060`, `0x14003A070`; also run `0x14003A110`) and the identity row
`0x14720BD50 = (0, 0, 0, 1)` (`0x140031340`). With them Noturbo Spur is **N = 539**, not
540, and many other regions shift by up to ~30. The "visited indices fit" test passed both
before and after, so it is too weak to prove an exact count.

Exact ground truth comes from the running game, read-only (`liveregions.py`, process
access 0x410, code bytes verified first): galaxy map = `[[image+0x6E8D708] + 0x95EFB8]`,
region pool = map + 0x50, active slot count `u32 [pool+0x54]`, slot pointers
`[pool+0x58]`. Each slot: N = `u32 [slot+0]`, total = `u32 [slot+0x214]` (N + 65),
region words at `+0x260` (X), `+0x262` (Z), `+0x264` (Y). The game had 175 regions
loaded; Noturbo Spur read **N = 539, total 604**, matching the corrected emulator.

**All 175 loaded regions match exactly** (N and total) between the running game and the
emulator with full startup state (`regionlivecheck.py`, 2026-10-27, owner's game in Euclid
around Noturbo Spur). The reader is kept as `scripts/diagnostics/read_region_star_counts.py`
for validating the C# port the same way.

Emission rule (for the port): cells pass a first density gate (`> [rbp+0x590]`, 0.55 in
the tested globals); when a per-region roll `rsi < r14` an extra block
`0x141368BB5..0x14136908E` runs; a star is emitted when `bl = density > [rbp+0x5D4]`
(scaled when `rsi < r14`, `0x141369111..0x141369143`). Cell positions include a per-region
random rotation (sin/cos at `0x140206D80`/`0x140206E60`), so the noise inputs must be
ported exactly; validate per cell against emulator hooks.

### Port structure (2026-10-27, scratch `regionport.py`)

- Region RNG = MWC seeded from the packed region address (lo or 1, carry =
  ror16(lo) ^ hi ^ lo); identical to Core `UniverseRandom`. Stored at `[rsp+0x30/0x34]`;
  float draws are `(double)word * 2^-32` narrowed to float.
- Parameters = `GcGalaxyGlobals.DefaultGeneration` (`GcGalaxyGenerationSetupData`, +0x1230),
  blended by `0x1421C2430` toward `FinalAnimationGeneration` (+0x13B0) with
  `FinalTransitionInterpolationValue` (+0x1FD8, 0 in normal play). Frame `rbp+0x460` = block
  start: `+0x130 BaseGenerationThreshold` (first gate), `+0x134/0x138/0x13C BaseTurbulence
  Gain/Lac/Scale` (4-octave density), `+0x16C SpiralFormChance` (truncated to int percent
  vs a Next(100) roll), `+0x174 StarGenerationThreshold` (emit test).
- Draw accounting (checkpoints matched exactly by counting MWC steps):
  1 setup draw; **fixed-star loop 1** places stars along segments read from
  `[slot+0x1D4/0x1D8]` (filled during generation, likely by `0x14136A930(slot+0x40, ...)`,
  ~2.5 KB with its own seeded stream): 6 draws per star; **loop 2** places the rest of the
  122 fixed stars at random segments: 7 draws per star; `0x14136A020` (star jitter/size) is
  always exactly 5 draws; `0x1413547E0` draws none. Noturbo Spur: loop 1 = 65 stars (390
  draws), loop 2 = 57 (399); region 012/5B/6DC: 55 + 67. Then 11 draws of grid setup
  (rotation angles via sin/cos, spiral roll, ranges from `+0xF0..0x10C`).
- Port order: (1) segment builder `0x14136A930`; (2) loops 1/2 draw counts; (3) grid setup
  draws and rotation; (4) simplex noise `0x142D75250` + fBm `0x142D746E0`; (5) cell loop and
  emission incl. spiral block; then C# with the same checkpoints and the live reader.

### Step 1 ported: galactic path segments (2026-10-27)

`0x14136A930(slot+0x40, galaxy, &regionWords, &edgeSet)` (scratch `segport.py`), exact for
60/60 regions (40 live, region 0, 19 random galaxies/negative coordinates):
- Region words are signed (12-bit X/Z, 8-bit Y). For each face f = 0..5 the shared face
  coordinate tuple is `(Z-1,X,Y) (Z,X,Y) (Y-1,Z,X) (Y,Z,X) (X-1,Z,Y) (X,Z,Y)`; a
  SuperFastHash-style mix of it seeds an MWC (`lo = h or 1`, `carry = ror16(h) ^ h`); two
  draws u, v give the face point `(u,v,-1) (u,v,1) (u,-1,v) (u,1,v) (-1,u,v) (1,u,v)`.
  Neighbouring regions therefore agree on shared face points.
- Face points closer than 1.0 are linked. A centre point comes from a second MWC seeded by
  `mix64(X | Z<<12 | Y<<24 | galaxy<<32)` (lo = h or 1, carry = ror16(lo) ^ hi ^ lo): three
  draws minus 0.5. Every still-unlinked face entry links to the centre.
- Edges are emitted once each (hash set), face rows 0..5 in column order, as
  `(P[i]*50, P[m]*50)` float4 pairs into `slot+0x1D0`.

### Step 2 ported: fixed-star draw consumption (2026-10-27)

Scratch `fixedport.py`: the region MWC state after the fixed stars matches the emulator for
59/60 regions; the miss is 0/0/0, inside the goal gap, where the generator returns before
any draw (`[voxel+0x8C]` InsideGoalGap -> `0x141367C1C` exits; N = 0). Voxel attributes
`0x14132AC60`: TransitPopulationDistanceRange (5, 10), PerpDistance 5, Atlas 1, black
hole 1, guide minimum 0x78, all zeroed inside the gap (integer distance
`sqrt(x²+y²+z²)` < `GcGameplayGlobals.GoalGapVoxelDist`, +0x15F4); RegionColourValue
from noise `0x142D768F0`. Draws: seed (galaxy byte as hi), 1 setup draw; loop 1 per
segment: `len` = f32 sqrt(z² + (y² + x²)), `min = 1/(len/5)`, `max = 1/(len/10)`, skip if
min > 1, `t = min`, per star 5 draws (`0x14136A020`) then `t += draw*(max-min) + min`
while `t <= 1`; loop 2: 7 draws for each of the remaining `122 - loop1` stars.

### C# port: exact (2026-10-27)

**Emission rule (verified):** a cell emits a star iff its density `> BaseGenerationThreshold`
and `> StarGenerationThreshold` (x 1.2 when the spiral roll `Next(100)` - the first grid draw -
is below `(int)SpiralFormChance`). The density at `[rsp+0x44]` is written once per cell and
nothing else in the loop gates emission, so rotations, spiral pull and positions do not
affect the count (`cellrule.py` reproduced the emulator count from per-cell densities).

Density input per cell (a, b, c = outer, middle, inner index 0..18):
`((idx * 100/19) + (-47.368423)) + region * 0.1`, times `BaseTurbulenceScale`, into the
derivative-damped fBm (4 octaves, lacunarity, gain) on 3D simplex noise with analytic
derivatives. Region coordinates are the signed words.

Core: `GalaxySimplexNoise` (simplex + fBm, SSE lane order and mask network ported),
`RegionPathSegments` (step 1), `RegionStarGeneration.Count(galaxy, x, y, z, settings)`
(gap exit, fixed-star draws, spiral roll, cell loop). Settings come from the catalog
(`GALAXYGENERATION`, format 44: `DefaultGeneration` + `GoalGapVoxelDist`; null if
`FinalTransitionInterpolationValue` is ever non-zero). **Exact on 225/225 regions:** all 175
regions read live from the game and 50 emulated references (random galaxies, negative
coordinates, 7 spiral regions, both sides of the goal gap). About 4.7 ms per region.
`tests/ArtifactX.Core.Checks` holds 12 of them as a regression check;
`scripts/research/region-generator/RegionCheck` re-runs the full comparison.

### Step 3 map: grid setup draws (positions only; not needed for the count)

`0x141368262..0x141368650`, 11 draws, `P` = generation-setup block at `rbp+0x460`:
1. d1: `rsi = Next(100)` spiral roll (vs `(int)SpiralFormChance`, `P+0x16C`).
2. d2: low bit picks the spiral direction constant stored at `[rbp+0x928]`.
3. d3: `lerp(SpiralInclusion.x, .y)` -> `[rbp-0x5C]` (`P+0x110`).
4. d4: angle `f*C - k` -> sin/cos (`0x140206D80`/`0x140206E60`) -> first rotation rows
   `[rbp+0x230/0x240]`.
5. d5: second angle -> rows `[rbp+0x140/0x2E0/0x90/0x260]`.
6. d6: `[rbp-0x54] = f*C + k`; `0x1401D14F0(rbp+0xC0)` combines the rotation.
7. d7..d11: lerps of ConnectionAttractorMax (`P+0xF0`) -> `[rbp-0x64]`, ConnectionAttractorMin
   (`P+0xF8`) -> `[rbp-0x60]`, BaseSize (`P+0xE8`) x C -> `[rbp-0x6C]`, SpiralFlex (`P+0x108`)
   -> `[rbp-0x58]`, ConnectionDistortion (`P+0x100`) -> `[rbp-0x70]`.

Then the 19³ cell loop (`0x141368650..0x1413695A8`): cell vector `(i,j,k)*step` vs the
(0.5,0.5,0.5) centre at `0x146ED3DB0`, density = fBm `0x142D746E0(pos * BaseTurbulenceScale,
octaves 4, gain, lacunarity)` on simplex `0x142D75250` (gradient table `0x1459245E0`,
permutation `0x14501D440`). Port these with per-cell checks of the density call's inputs and
output (hooks at `0x1413687D3`/`0x1413687D8`), then the emission rule above.

## Next

1. Find the job that fills a slot (writes `dword [slot+0]` and the `+0x200` container):
   start from functions that reference the pool's slot array (`[pool+0x38]`/`[pool+0x40]`).
2. Port N's computation (likely density noise from the region coordinates), or emulate it
   for many regions and fit/port it.
3. Validate against the owner's complete Noturbo Spur mapping (Euclid; in progress
   2026-10-27): every visited index must be <= N, and the mapped total should equal N
   (+ purple if any were visited).
