# Pirate station multi-tool cabinet coverage

## Scope and evidence (2026-10-26, installed NMS build 180836)

The ordinary TypeB cabinet path does not apply to pirate stations. The pirate root
is selected ahead of the legacy-station predicate in the native station dispatcher
(`0x1416377CA`, `0x14163C5E6`). Pirate inventory class/technology selection uses native
wealth row 3, rather than ordinary wealth minus one.

The pirate root descriptor chooses one of four outer shapes. Each shape has a
separate accumulated cabinet transform, even though the interior assets are shared.
Catalog extraction retains those four branches and selects exactly one using the
station spawn seed. It does not combine their cabinets or reuse TypeB origins.

The verified reference chain is:

1. `SPACESTATION_PIRATE.SCENE.MBIN`, selected outer shape.
2. `LAUNCHTUBE_PIRATE.SCENE.MBIN` (directly or through the shape's exterior scene).
3. `BACK_SECTION_PIRATE.SCENE.MBIN` / `RefBackSectionAband`.
4. `PIRATES/SHOPWEAPON.SCENE.MBIN` / `ShopMissionREF1`.
5. `MODELS/PLANETS/BIOMES/COMMON/BUILDINGS/CRATE/CRATE_WEAPON.SCENE.MBIN` / `WeaponsCrateREF`.
6. `CRATE_WEAPON/ENTITIES/CRATE_WEAPON.ENTITY.MBIN` / `Crate_Weapon`.

The final attachment contains a WeaponTerminal interaction, no interaction-specific
pool override, and ReseedAfterRewardSuccess=false. Its pool is Standard. The shared
crate lies outside the station directory, so the extractor permits that exact
additional scene for pirate branches. It does not traverse arbitrary planet assets.

## Screenshot reference

User-provided Onamir-Zuang, Euclid, system `0x7A00FF67B291`, portal `007AFF67B291`:

- Generated name: Bane of Silence, exact screenshot match.
- Class: C, exact screenshot match.
- Scanner: 208.7, exact screenshot match.
- Selected model seed: `0x5A11F24AF5066F6E`.
- Actual cabinet position hash: `0x2EDBA7C129F6E326`.

The existing default-OVERHEAT damage calculation produces 352.3 for this generated
inventory; the supplied game screenshot shows 420.0. This discrepancy is unresolved.
Pirate records therefore set HasSupportedDamagePotential=false, and their UI cards
show scanner range only. No camera-dependent explanation is established. Ordinary
station damage presentation remains unchanged.

## Validation

The scratch stationcheck harness compares all 100 pirate root choices against the
native descriptor selector at `0x142D6B5A0`. All four extracted shapes contain one
WeaponTerminal. The composed pipeline matches the reference name, class and scanner,
rejects missing/ambiguous geometry and contradictory system inputs, and preserves
pirate-over-legacy priority. Existing ordinary station references are also checked.
No game save, user database or installed catalog is modified by these checks.

Catalog format 40 adds PirateRootDescriptor/PirateVariants and each variant's
RootDescriptorId. A game-data rebuild is required. Missing old-catalog fields yield
no pirate records. Legacy and abandoned station cabinet coverage remains unsupported.

## Damage follow-up: runtime animation input

The native maximum routine at `0x14140F400` reads the animation controller from its
owner at +0x23D0, resolves OVERHEAT through `0x1413F8420`, and obtains its duration
through `0x140206900`. The latter returns zero for an absent/out-of-range animation
index (`0x1402069A4`). The comparison UI calls the same owner at manager+0x72CAD0
for both offered and owned inventories (`0x1406AD2B4` through `0x1406AD305`). Thus
this duration is a runtime owner input, not a property inferred from the offered
inventory's class or technology grid alone.

A native execution with the reference Bane inventory gives 352.25806 with the
standard 160/60-second duration, and exactly 420.0 with duration zero. Executing
the real duration routine with a missing animation index also produces 420.0.
This reproduces the displayed number but does not establish which live runtime
condition caused the missing lookup in the user's session.

Additional user reference: Himizumi, Euclid, portal `00DDFF67B291`, system
`0xDD00FF67B291`. Both generated inventories were checked with the same duration
choices:

| Tool | Class | Standard duration | Zero duration | Game screenshot |
| --- | --- | --- | --- | --- |
| Return of War | B | 1347.8 | 1347.8 | 1347.8 |
| Kiss of Naryev-VI | A | 333.3 | 400.0 | 400.0 |

Return of War site/model seeds: `0x8B5B8B775B86FCE1` / `0x13608D84289710A3`.
Kiss of Naryev-VI site/model seeds: `0xB7B14844E37680A8` / `0xB26E5B15F7E54ECA`.
This pair is consistent with a shared zero-duration runtime input while a
projectile mode dominates Return of War's maximum. No class-specific correction,
pirate multiplier, fixed 1.2 scaling, or camera-dependent explanation is justified.
The saved-reload pipeline still lacks a proven source for the runtime duration.

### Additional exclusions and saved-mode trace

Both `MULTITOOLLAZOVERHEAT.ANIM.MBIN` and its `_3P` counterpart contain 161 frames;
the corresponding entity entries both use frames 0..160, speed 1, and 60 Hz.
The resource metadata therefore does not explain the difference as a simple
first-person versus third-person duration change.

The rod animation entities have neither OVERHEAT nor OVERHEAT_3P. This is one
possible missing-animation controller, not a verified explanation for the user's
session. Do not label the discrepancy as fishing-related without a controlled
runtime observation.

Native save/load traces also establish that StartingPrimaryWeapon at raw player
+0x866F8 is copied to runtime player+0x1C938 (`0x140573B7B`), and initialization
reads manager+0x28278 into weapon-owner+0xF74 (`0x1413F8CD2`). The save writer copies
manager+0x72DA44 back to that raw field (`0x14057B556`). Initialization can reject
an unsupported mode and fall back to mode 5. This helps trace the owner but does
not establish animation-resource readiness. The controller at owner+0x23D0 is
assigned from an entity component lookup at `0x1413FA31C`..`0x1413FA32B`.

No readable running NMS process was available to the diagnostic environment in
this follow-up; no process memory, saves or databases were changed. The open
question is the actual live controller/name/resource state when the preview
returns zero duration. Standard-duration and zero-duration arithmetic are both
reproduced, but selecting one from class, station type or the offered grid alone
would be an unsupported inference.
