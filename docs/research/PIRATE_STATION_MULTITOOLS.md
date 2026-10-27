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

## Native resource-readiness reproduction (2026-10-26)

The duration functions read **FrameEndGame** (+0xC8), not configured FrameEnd
(+0xC4). Both extracted standard overheat entries have FrameEnd=160,
FrameStart=0, FrameEndGame=0 and Speed=1. `0x142D555D0` uses FrameEndGame for
an unresolved animation handle; `0x142D55C80` also uses it on a resolved active
animation. Consequently, the raw catalog FrameEnd calculation assumes runtime
initialization that the preview cannot establish from an offered inventory.

The native initialization fragment `0x142D3C29F`..`0x142D3C30F` checks the resource
handle array at owner+0x40. A zero handle skips the FrameEndGame assignment. A
nonzero handle asks `0x14183F6C0` for frame count and writes FrameEndGame as the
configured end when valid, otherwise frameCount-1. With the extracted 161-frame
resource and configured end 160, this writes 160. The alternate initialization
path `0x142D5715F`..`0x142D57188` applies the same clamp.

The scratch `station-damage-readiness-references.py` executes that actual native
initialization fragment, the actual animation duration lookup/fallback, and the
complete native weapon-mode maximum. The host supplies resource readiness,
frame count, lookup handles, and inventory/module data; this is not a live-game
capture. The generated Kiss/Return inventory and module rolls come from the
previous exact system diagnostic, including their supercharged coordinates.

| Reference | Resource absent, runtime end 0 | Resource present, runtime end 160 |
| --- | --- | --- |
| Return of War | 1347.8 | 1347.8 |
| Kiss of Naryev-VI | 400.0 | 333.3 |
| Bane of Silence | 420.0 | 352.3 |
| Sleep of War | 400.0 | 333.3 |

All eight native checks passed. This establishes a concrete engine mechanism
for the observed discrepancies without changing class, technology, adjacency,
supercharged slots or camera resources. It does **not** establish the live
resource/controller state at the time of any supplied screenshot. Both camera
resource files have the same 161 frames, so no camera-only correction follows.

A static generated cabinet cannot establish this runtime readiness. Keep the
unverified damage guard until a reliable runtime source or clearly differentiated
preview-state presentation is implemented. Do not replace all laser values with
the resource-absent maximum: that would break confirmed resource-present values
such as Sleep of War 333.3 and Mass Needler 443.3. No production formula or
presentation was changed during this follow-up.

### Read-only live diagnostic

`scripts/diagnostics/read_multitool_damage_state.py` uses only process query/read
access (0x410), never injection, game-function execution, remote allocation or
memory writes. It verifies the traced maximum routine's prologue before following
build-specific pointers, limits the animation count to 2048, and prints only the
primary mode and OVERHEAT/OVERHEAT_3P frame fields. Errors are caught without a
native application-error dialog. Run with Python on Windows while NMS is open:

```powershell
python scripts/diagnostics/read_multitool_damage_state.py
```

The initial local attempt reported that NMS was not running or not visible.
The diagnostic is ready for the next live cabinet observation. A zero runtime
frame end with a present configured end would directly support the initialization
mechanism; other results require following the active animation-handle path.

### Live observation and guarded correction

The user started NMS during this follow-up. The read-only diagnostic observed
mode 20 (Gravity, from the current vendored enum), 34 animation entries, and no
OVERHEAT or OVERHEAT_3P. Names included IDLE, GRV_EQUIP, GRV_TARGET_IDLE, GRV_GRAB,
GRV_GRAB_IDLE, GRV_IDLE, GRV_THROW and GRV_PUSH, confirming the sampled table's
meaning. The user reported Geometric Loop Needler at 1370.2 damage and 206.9
scanner in the currently open cabinet. That projectile-dominated value does not
distinguish the two duration states; no direct live observation of Bane/Kiss's
higher value was obtained in this follow-up.

The production input service now supports exactly the unambiguous absent-name
case. It opens the process with query/read access only, validates the complete
executable SHA-256 and the running comparison-call bytes, validates animation
names/counts and resamples identity fields to reject an interrupted switch. It
returns zero only when both overheat names are absent. Present animations,
unsupported binaries, inaccessible processes and changing/invalid layouts return
unknown. No camera state is guessed and no game functions are executed.

The Core runtime accepts an explicit validated nonnegative duration; the station
service supplies it only after the guarded read succeeds. The output marks live
preview-state usage, and the hint explains that damage reflects the current game
preview state. A proven duration also permits pirate damage to be displayed.
Without it the existing standard assumption/pirate guard remains unchanged.

The C# input service was run separately against the live game and returned zero.
Composed pipeline tests reproduce Bane 420.0, Kiss 400.0 and Return 1347.8 with
that input; negative, NaN and infinite durations are rejected. Existing default
reference and native regression checks still pass. Present-animation readiness
remains unresolved and is deliberately not read as a fabricated duration.


### Inactive animation durations and missing X-scanner cabinet (2026-10-27)

The runtime reader also supports present but inactive overheat definitions. Native
handle lookup uses the active mask at table+0xE4 and definition indices in 16
slots at table+0x244 with stride 0x180. When neither requested definition has an
active handle, duration uses FrameStart, FrameEndGame, Speed and the 30/60 Hz
period. All 128 native timing vectors matched exactly. Active overheat handles,
differing camera-name durations and changing samples remain unsupported. This
supersedes the earlier absent-name-only limitation above. The user subsequently
confirmed the corrected build matches both Himizumi cabinets in game.

Nakaok-Anj (0083FF67B291) has two distinct cabinets sharing model seed
0x18F03D972E6840A1 and name Overload of Vimestvie-IX. Site
0x47878890288B0A9B was dropped because its UP_SCANX module has quality 4;
site 0xF56F67CEEDE57303 resolved normally. This was not name deduplication.

Quality 4 takes an extra chance draw at 0x140ec4847 before the ordinary count
roll. The threshold is GcPlayerGlobals.ChanceHighGradeIllegal, extracted as
IllegalModuleHighGradeChance in catalog format 41 (10 in the tested install).
Successful high-grade draws maximize count while retaining RNG consumption;
each stat then consumes a second uniform draw and uses the interval selected
by its weighting-curve index. Native comparisons cover 203 suffixes at each
of chance 0, extracted chance 10 and chance 100: 609 exact count/order/level/
float matches. Missing or invalid catalog chances remain unsupported.

The live read-only guard validates the executable and native branch bytes,
rejects forced-best debug mode and establishes that the network/perk branch
at 0x140ec4602..0x140ec4636 is inactive. Without that proof the composed
located-record path continues to omit X-scanner tools. No game function is
called and no game memory, save or installed catalog is modified.

Using the extracted format-41 tables, the full located-record diagnostic now
resolves site 0x47878890288B0A9B as A-class, 26 unlocked slots, seven installed
technologies, damage 440.0 with live duration zero and scanner 221.5. Its
UP_SCANX suffix is 70472 and has fauna/flora/mineral rewards, no radius bonus.
The second cabinet remains 3402.9 / 319.4. Both match the supplied screenshots.
The runtime guard also returned true against the running game. In-app
confirmation of the newly included cabinet still requires the owner to rebuild
game data and run the new build. Existing standard/pirate regression checks pass.


### Unrelated food target guard (2026-10-27)

After the autosave session-identity fix, Iljeark-Ats V (0090FF67B292) showed no
station tools. Read-only Slot 8 inspection found FoodUnitItem `^FOOD_DNUT_AJAM`.
The product table maps this product's FoodBonusStat to stat 61 (fauna rewards),
which is not an input to the displayed damage maximum or scanner radius.

Native 0x14057a9ed..0x14057a9f6 compares ProductData+0x1BC against the exact
stat ID at the evaluated stat entry+8. A mismatch skips food-bonus computation.
605 native executions (all 11 displayed-stat inputs, 55 values each) returned
bit-identical input values with food stat 61. A matching stat-61 control invoked
the food-bonus helper, proving that the mismatch check is being exercised.
No claim is made about the amount/formula for a matching food target.

Catalog format 42 appends ProductFoodBonusStats to the multi-tool technology
metadata, extracted from product data without hardcoded food IDs. Saved runtime
inputs retain the normalized product identity. Both station and individual-record
guards permit only a catalog-known target outside the damage/scanner input set.
Missing metadata, unknown products and relevant targets remain unsupported.
An explicit special-modifier flag without an identified food remains guarded.

Using the actual saved food context, the full address-to-record pipeline returns
two C-class Poem of Supplication records: 1362.7/200.1 and 1362.7/206.6. Both now
have owner-provided cabinet screenshots confirming those values. This does not
replace the need to test the newly visible cards after the format-42 data rebuild.
