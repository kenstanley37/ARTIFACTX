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

## Next

1. Find the job that fills a slot (writes `dword [slot+0]` and the `+0x200` container):
   start from functions that reference the pool's slot array (`[pool+0x38]`/`[pool+0x40]`).
2. Port N's computation (likely density noise from the region coordinates), or emulate it
   for many regions and fit/port it.
3. Validate against the owner's complete Noturbo Spur mapping (Euclid; in progress
   2026-10-27): every visited index must be <= N, and the mapped total should equal N
   (+ purple if any were visited).
