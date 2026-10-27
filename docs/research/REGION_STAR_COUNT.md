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

## Next

1. Find the job that fills a slot (writes `dword [slot+0]` and the `+0x200` container):
   start from functions that reference the pool's slot array (`[pool+0x38]`/`[pool+0x40]`).
2. Port N's computation (likely density noise from the region coordinates), or emulate it
   for many regions and fit/port it.
3. Validate against the owner's complete Noturbo Spur mapping (Euclid; in progress
   2026-10-27): every visited index must be <= N, and the mapped total should equal N
   (+ purple if any were visited).
