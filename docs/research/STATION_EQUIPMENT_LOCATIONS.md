# Station equipment location research

## Automatic moon-placement input and schema correction (2026-10-25)

The previously unresolved comparison at `0x14164ead5` reads GcDebugOptions.BootMode.
The native debug-options serializer at `0x14288ac05` passes member offset 0x70c
and the seven BootMode enum names to its enum reader. The enum starts
MinimalSolarSystem=0, SolarSystem=1. Native execution checked the member pointer
and labels, then all seven values of the moon comparison: moon placement is
enabled exactly when the mode is not MinimalSolarSystem.

The vendored schema had an extra TkGlobals.HmdVRSRateOverride integer at +0x44,
absent from the installed executable's serializer and actual debug-options MBIN.
It shifted later Toolkit fields and the enclosing debug options by four bytes.
BootMode was incorrectly decoded at 0x710 instead of 0x70c; the old
BootLogoFadeRate property read BootMode bits. Removing the unsupported field
restores all 110 checked Toolkit serializer offsets. Offset comments were
corrected and the library modification recorded in THIRD-PARTY-NOTICES.md.
The installed file now decodes BootMode=SolarSystem and BootLogoFadeRate=2.5.
This schema correction is scoped to installed build 180836.

Catalog format 38 records StandardMoonPlacementEnabled only for ordinary
SolarSystem mode. Other boot/debug modes remain unsupported by the automatic
address API, rather than extending normal generation to them merely because
their moon condition is nonzero. Older catalogs/missing metadata also yield no
automatic pose. The explicit bool overload remains for native reference/debug
inputs; the normal automatic overload no longer requires a moon flag.

All 286 supported address-to-pose cases match the native corpus through this
automatic catalog-gated API. All 14 prime-only/invalid address cases are rejected.
Both reference cabinet hashes and all four complete model/inventory/damage/scanner
records retain their expected results. This supersedes earlier notes that moon
placement has no semantic mapping. Station root dispatch, reload-state selection
and UI wiring remain separate work.

## Status and reference

**2026-10-25 continuation:** catalog format 25 now extracts the nested TypeB root descriptor and A/B/C launch chains separately. `StationSceneSelection` resolves one root launch branch; `StationCabinetLocations` transforms its own extracted cabinet origins through the generated pose. The reference spawn seed selects **LaunchTubeC**, correcting the earlier unverified assumption of A. Both hashes still match with the actual C geometry. Equipped stats, complete root-type dispatch and UI integration remain unfinished; no new game test is needed yet.

Read-only trace of NMS build 180836, 2026-10-24. **Both cabinet positions and selected tool identities are now reproduced for the reference station**, using native generated station pose plus extracted scene translations. The production catalog-to-location pipeline and wider station coverage remain unfinished; do not claim all station variants are solved.

Related evidence is in [MULTITOOL_GENERATION.md](MULTITOOL_GENERATION.md). The owner's purchased Mass Needler Z-C11 is in DELETEME Slot 8, with model seed `0x4101B7E8CB7DAFD9` and inventory/site seed `0x10662FF4655D0E43`. Its packed system/reload-body address is `0x980906A7A427` (body 0). Earlier brute-force inversion near the saved player found rounded world-position cell `(-854, 6892, -401)` for that seed. The later scene-to-world calculation below independently reaches that same hash from the generated cabinet origin; no position search is used to place the cabinets.

No game or save writes were used. The owner authorized publishing these reference details.

## Verified reference cabinet origins

`toolcheck/check6.log` records the production site hash/selection methods run with the native pose from scratch `station-native.json` and the extracted cabinet chain:

| Cabinet | Computed world XYZ (float32) | Site seed | Selected tool |
|---|---|---|---|
| GunBox1 | `(-853.83606, 6892.0063, -401.27625)` | `0x10662FF4655D0E43` | Mass Needler Z-C11 |
| GunBox2 | `(-849.3653, 6892.3306, -399.12704)` | `0x7906F5F0B4B0AA31` | Geometric Angle Focuser |

The first seed matches the purchased save entry exactly. Both selected names match the two station screenshots. This verifies cabinet placement and selection for this reference; it does not by itself verify the second tool's complete inventory, class or equipped display calculations.

Native reference pose:

```text
Position: (0, 6947.1064453125, 0)
Facing:   (0.8993499875068665, 0.06516798585653305, 0.43234556913375854)
Seed:     0x0ABE3A6694893A17
```

The harness normalizes Facing, builds `R = normalize(cross(UnitY, Facing))` and `U = cross(Facing, R)`, then computes `world = local.X*R + local.Y*U + local.Z*Facing + Position` using float32 vectors. `StationPoseGeneration` now reproduces the native pose from the exact incoming layout state and generated body centers. Generating those inputs in production remains unfinished.

## Verified reference scene chain

The extracted station chain used for the successful comparison is:

1. TypeB station scene.
2. LaunchTubeA reference with local translation `(0, 0, -860)`.
3. BACK_SECTION.
4. REFSuitWeaponShopArea with local translation `(-15, 5, -82.5)`.
5. GunBox1 with local translation `(6.717959, 1.496199, -2.478221)`, and GunBox2 with `(6.717959, 1.496199, 2.492832)`.

These are local scene translations. For this reference chain the successful harness composes them as `(-15+6.717959, 5+1.496199, -860-82.5+cabinetZ)` before applying the native station matrix above. An earlier suspected BACK_SECTION offset around `27.349` is not part of that successful chain and must not be added speculatively. Additional station variants may have other parent transforms, rotations, scales or descriptor conditions; extract and resolve those rather than applying this reference's translations universally.

Station loading at `0x14163b800` and onward reads `GcSolarSystemData.SpaceStationSpawn`, normalizes its facing, and builds the station matrix. The reference scene-to-world transform now matches both screenshot tool identities and the purchased inventory seed. Core now ports the pose stage, but does not yet produce its layout inputs from a system address. Existing station furniture positions are fixed station-local template coordinates.

## Station pose generation: `0x14164ff80`

The routine's `rdx` argument is an input bundle:

- `[rdx]`: generation input object; its field `+0x2e0c` controls station spawn mode.
- `[rdx+8]`: `GcSolarSystemData`.
- `[rdx+0x10]`: output matrix for a separate player spawn location.

The generator object in `rcx` has current MWC state at `+0x510/+0x514`. **Correction, 2026-10-25:** the main generator calls `0x14164d9e0` immediately before station placement, resetting this state from the system seed. The 300-case native capture confirms the address-expanded initial state at station entry. Earlier notes incorrectly assumed preceding layout draws remained in this stream.

### Relevant solar-system fields

| Offset | Field |
|---|---|
| `0x1ee0` | SpaceStationSpawn.SpawnFacing |
| `0x1ef0` | SpaceStationSpawn.SpawnPosition |
| `0x1f00` | SpaceStationSpawn.Seed |
| `0x1f10` | SpaceStationSpawn.SpawnMode |
| `0x20c0` | PlanetPositions (8 float vectors) |
| `0x2180` | PlanetGenerationInputs (stride `0x58`) |
| `0x21c0 + i*0x58` | PlanetGenerationInputs[i].PlanetSize |
| `0x2480` | SolarSystemData.Seed |
| `0x2544` | Planets (primary count) |
| `0x2548` | PrimePlanets |

Use the exact generated `PlanetPositions`; the save's `aHM` centers are rounded and are not substitutes for exact station orientation.

### Independent station appearance seed

This is separate from the mutable layout stream:

1. Initialize MWC directly from `SolarSystemData.Seed` using the usual low-word/rotated-low/high expansion. If the GcSeed enabled flag is false, use low 1, carry 0.
2. Draw two words, low then high.
3. Form `(high << 32) | low`, then Mix64 with `0x64dd81482cbd31d7`, `0xe36aa5c613612997`, and 33-bit xor shifts.
4. Store the resulting enabled GcSeed in `SpaceStationSpawn.Seed`.

The stored spawn mode is UseSeed (1) when the generation input's `+0x2e0c` value differs from 7, otherwise None (0). The field's semantic enum has not yet been identified; do not generalize this comparison into an unsupported station-type rule.

### Layout draws and station position

All fractions here use `Float01(word) = (float)((double)word * 2.3283064370807974e-10)`, with float32 multiplication/addition afterward.

1. First layout draw: `distance = 5000f + Float01(word1) * 5000f`.
2. Second layout draw: odd low bit selects negative distance, even selects positive.
3. Station spawn position is `P = (0, signedDistance, 0)`, with homogeneous W=1.

The routine writes this vector at stack `+0x50` at `0x141650160`, later copied into `SpaceStationSpawn.SpawnPosition`. Planet positions do not affect this initial station position; they affect its facing.

### Nearest planet and local basis

Search primary bodies first, minimizing squared XYZ distance from P to each generated planet center. Arithmetic is float32; strict improvement replaces the index, so ties retain the prior index. Only when no primary index was found does the routine search prime bodies.

Let C be the chosen planet center:

```text
D = normalize(C - P)
R = normalize(cross((0, 1, 0), D))
U = cross(D, R)
```

Normalization uses scalar SSE square root/division, not a reciprocal-square-root approximation. Before normalizing some vectors, the routine checks all XYZ absolute components against `1.52587890625e-05f`. Degenerate vectors use a global fallback at `0x14720bd50`; its initialized value remains to be confirmed. Do not silently substitute an assumed axis for that branch.

### Planet-relative target point

Third and fourth layout draws create a unit sphere direction:

```text
z = 1f - 2f * Float01(word3)
r = sqrt(max(0f, 1f - z*z))
theta = Float01(word4) * 6.2831854820251465f
S = (CosApprox(theta)*r, z, SinApprox(theta)*r)
```

The chosen body's PlanetSize goes to `0x14166bfa0`, which returns these units:

| PlanetSize | Normal | MassiveSolarSystems enabled |
|---|---:|---:|
| Large | 32 | 128 |
| Medium | 24 | 96 |
| Small | 16 | 64 |
| Moon | 4 | 16 |
| Giant | 128 | 512 |

The routine calculates an integer power-of-two bound using its custom log2 helper `0x1402c1b30`, CRT `ceilf`, and `powf(2, exponent)`:

```text
B = (int)powf(2f, (int)ceilf(Log2Approx(radiusUnits)))
shellUnits = B - max(1, B - radiusUnits)
T = C + S * (float(shellUnits) * 32f * 32f)
```

This is a generated targeting point; do not label it the planet's surface or radius without further evidence.

### Offset viewpoint and final facing

Fifth layout draw:

```text
phi = Float01(word5) * 6.2831854820251465f
Q = P + R * (CosApprox(phi) * StationSpawnAvoidRadius)
      + U * (SinApprox(phi) * StationSpawnAvoidRadius)
SpawnFacing = normalize(T - Q)
```

Operation order in assembly multiplies the trigonometric value, then the basis vector, then the global radius, with vector additions in that order. Preserve float32 rounding when reproducing exact cabinet hash cells. Q is a virtual viewpoint used for facing; the stored station spawn position remains P.

At `0x14165067a` the routine copies the completed spawn record into `GcSolarSystemData.SpaceStationSpawn`. An additional sixth draw after this selects a point between Q and T using `SpawnPointStationToPlanetInterpRange`; the final matrix output describes that separate player spawn. Do not confuse this output matrix with the station transform.

### Globals used

The globals in this executable trace correspond to `GcSolarGenerationGlobals` at runtime base `0x145215050`:

| Runtime address | Struct offset | Field |
|---|---|---|
| `0x145215230` | `0x1e0` | SpawnPointStationToPlanetInterpRange.X |
| `0x145215234` | `0x1e4` | SpawnPointStationToPlanetInterpRange.Y |
| `0x1452152d4` | `0x284` | StationSpawnAvoidRadius |
| `0x1452152dc` | `0x28c` | MassiveSolarSystems |

Extract these from the player's game data; the raw executable bytes at runtime globals are not reliable initialized configuration values. In particular, do not assume MassiveSolarSystems is false.

## Exact trigonometric helpers

`0x140206d80` is CosApprox, `0x140206e60` is SinApprox. These are float32 polynomial helpers, not CRT sinf/cosf. Both reduce an angle with:

```text
k = trunc(x * 0.15915493667125702f + (x < 0 ? -0.5f : 0.5f))
x = x - float(k) * 6.2831854820251465f
```

Then fold into `[-1.5707963705062866f, +1.5707963705062866f]`: above that range use `3.1415927410125732f - x`, below it use `-3.1415927410125732f - x`. Cosine's fold sign is -1 when folded, +1 otherwise. Sine does not apply an additional fold sign.

With `t = x*x`, evaluate each operation separately as float32:

```text
SinApprox:
  p = 2.752155751295504e-6f - t*2.3828544470916313e-8f
  p = p*t - 0.00019840782624669373f
  p = p*t + 0.008333330042660236f
  p = p*t - 0.1666666716337204f
  p = p*t + 1f
  result = p*x

CosApprox:
  p = 2.47604948526714e-5f - t*2.605161455448979e-7f
  p = p*t - 0.0013888378161936998f
  p = p*t + 0.04166663810610771f
  p = p*t - 0.5f
  p = p*t + 1f
  result = p*foldSign
```

The constants above were read directly from the executable's RIP-relative constant addresses. Do not replace the sequence with FMA or double-precision intermediates when testing exact rounding.

## Next checks

1. Complete a production C# port of the incoming layout state and all body centers, then supply those to the verified `StationPoseGeneration` stage.
2. Extract station root selection, complete scene transforms, descriptor conditions and cabinet interaction metadata into the player's catalog; no reference coordinates may be hardcoded as universal game content.
3. Retain the two successful world-position/site-seed/name comparisons above as regression references when replacing the scratch inputs with production generation and catalog rows.
4. Compare Geometric Angle Focuser's C class, inventory and displayed stats in addition to the now-matched placement/name. Complete equipped-stat calculations for both tools.
5. Verify further systems and station variants. A single station does not establish planetary building placement or crashed-ship generation.

## Production integration audit

### Root launch selection and extracted locations, 2026-10-25

Direct `System.Text.Json` serialization of `TkResourceDescriptorData.Children` loses nested groups because the declared element type is `NMSTemplate`. The scratch dumps showed `{}` rather than the root's `_ENTRANCE_` / `_TUBE_` / `_ATTACHPOINT_` choices. Production extraction now explicitly projects nested `TkModelDescriptorList` children into the existing ShipParts catalog JSON shape. The earlier ShipDescriptors extractor already uses explicit recursion; this was a gap in the scratch evidence and the new station extraction.

`shipprobe/station-descriptor-oracle.py` constructs the native runtime group/option layout and calls `0x142d6b5a0` for each root/nested group, with actual string CRT hooks and independent child seeds. Production `ShipParts` output matches all selected root IDs for **100/100 station seeds**, and `StationSceneSelection` resolves one branch in each case. This validates the weighted-selector stage, not a fully emulated scene-loading pipeline: child-seed recursion is supplied by the existing port/Python harness, and referenced scene descriptor traversal is deliberately omitted because it cannot change ancestor entrance choices.

The reference `0x0ABE3A6694893A17` selects `_TUBE_SQUARE` and `_ATTACHPOINT_LAUNCHTUBEC`. A/B/C now use independently traversed scene transforms; none borrows A's coordinates. Each extracts two actual WeaponTerminal attachments. The selected C origins are `[-8.282045, 6.496199, -944.9781]` and `[-8.282045, 6.496199, -940.007]` in station space. The full extracted transforms produce world positions `(-853.83594, 6892.0063, -401.2762)` and `(-849.36523, 6892.3306, -399.12698)`, respectively. Their hashes are exactly `0x10662FF4655D0E43` and `0x7906F5F0B4B0AA31`, preserving the purchased seed and both screenshot identities. Earlier simplified A-chain float positions differ slightly and must not be cited as the actual selected scene origins.

`shipprobe/stationcheck` references the production GameData/Core projects, extracts from the installed PAKs into scratch only, round-trips the catalog DTO through JSON, compares the native root-choice vectors, checks selected C cabinet hashes and rejects missing descriptors, duplicate matching branches and invalid facing vectors. It catches exceptions and exits normally with a failure code. Core and GameData compiled with no errors; existing unrelated warnings remain. No installed catalog, player database or save was written.

Root selection is separate from overall station-type dispatch and descendant descriptor/reward rules. `LocateStandardTypeB` explicitly requires the caller to establish a standard TypeB station. Pirate, abandoned, legacy and absent stations are not covered, and this method returns location hashes rather than complete weapon inventories. Do not enable a player-facing exact-stock card yet.

### Interaction pool rules and address-to-class check, 2026-10-25

`0x140ff1a25` calls `0x141003610` for the pool type, then passes that type and the interaction seed to `0x1416d2470`. The pool-type routine checks interaction type 0x15 (WeaponTerminal), resolves its specific data at component offset 0x2e8, and returns Standard when that data is absent. With `GcWeaponTerminalInteractionData`, UseSentinelWeapon selects Sentinel; otherwise a positive RespawnPeriodInSeconds selects SettlementRotational, and zero/nonpositive selects Standard. Production extraction records these rules and the two specific-data fields. Unknown specific-data templates yield no pool rule. Both actual TypeB cabinet attachments have no specific data and ReseedAfterRewardSuccess=false, so both resolve to Standard.

`StationMultiToolContents.Select` takes an explicitly resolved reload-body pool and economy probability row. It returns model plus actual class, and rejects missing pool rules, missing stock/class tables and reseeding interactions. It does not fabricate an inventory or call intrinsic bonuses equipped statistics.

The production source-referencing scratch harness now starts with portal code `0x9806A7A427`, galaxy 9 and the extracted `SolarLayoutTables`, plus the observed enabled moon gate. It produces the reference station pose exactly, selects C using the extracted root descriptor, transforms the extracted C cabinet origins and hashes them, then selects from the station reload-body pool and applies economy row 2. Results: **GunBox01 = Mass Needler Z-C11, class B; GunBox02 = Geometric Angle Focuser, class C**, matching both original screenshots. No saved player position, searched position cell or fixed cabinet coordinate is used by this combined path.

Next external check: a second ordinary inhabited station in Slot 8 DELETEME, after saving/reloading at that station. Obtain system glyphs and galaxy, both cabinet item screens (names, class and displayed stats) and a view of the station entrance shape. A purchase is not needed for this first comparison. Complete equipped inventory generation is still required before enabling exact-stock UI.

### Second station screenshot comparison, 2026-10-25

The owner supplied both cabinet screens and the station visor readout for **Modeysk Rendezvous**. Read-only inspection of the current Slot 8 working save identifies galaxy 9, coordinates `(1063, 6, -1414)`, system index 128, reload body 0. This gives portal code `0x8006A7A427` and packed system address `0x800906A7A427`. The generated Vy'keen race, Mining economy, wealth 2 and conflict 3 agree with the screenshot's Vy'keen / Extraction / Satisfactory / Alarming labels. No save was edited and no purchase was requested.

Production generation with the same extracted catalog tables and enabled moon gate selects LaunchTubeC. It predicts:

| Cabinet | World origin | Site seed | Model seed | Predicted and observed name/class |
|---|---|---|---|---|
| REFGunBox01 | `(-934.6468, 6958.0366, 139.32028)` | `0x8B6DCF59749AFA39` | `0x911527E231859BF7` | Iridescent Inertial Focuser, C |
| REFGunBox02 | `(-929.7239, 6958.013, 138.63051)` | `0x158005B0C4180212` | `0x5F7AD0297350073E` | Aspect of Aktot-XIV, B |

Both screenshot names and classes match without fitting any site coordinates to the screenshots. The new screenshots do not independently expose model/site seeds or confirm the entrance shape; those remain computed values, not purchased-save confirmations.

Read-only descriptor and actual weapon-tag resolution identifies Iridescent as Rifle and Aspect as Pistol. Iridescent's site/class-derived scanner bonus is `3.1785212%`, giving `206.4` range with the extracted TOOL base range of 200 and the existing scanner formula, matching its screenshot. Its observed damage potential is `1362.7`, with additional weapon technology visible; that equipped damage calculation remains unimplemented. Aspect's intrinsic scanner bonus is `29.741974%`, giving base range `259.5`; its screenshot shows `285.4` with a B scanner upgrade installed. Do not claim that equipped scanner total is reproduced until procedural technology effects are generated. Aspect's damage screenshot is `333.3`.

The scratch stationcheck harness now asserts both names/classes and Iridescent's rounded scanner range. No further user test is needed for this comparison. Initial slot layouts, procedural modules, supercharged positions and all equipped stats remain the next implementation work.

**Subsequent economy/technology correction:** Modeysk's native Average wealth is
index 1, obtained from SystemAttributes.Wealth (2) minus one. The second-station
harness now uses row 1 and both classes still match; its earlier row-2 match was
coincidental. Native ranking selects regular UT_SCAN for Aspect, whose extracted
1.1 scanner-radius factor reproduces the observed 285.4 when intrinsic bonuses
are applied. This does not complete general technology selection or equipped
damage. See the equipped-stat notes for native oracle limitations.

### Pose-stage comparison, 2026-10-25

The subsequent `SolarLayoutGeneration.PrimaryCenters` port reproduces all **1,088 primary-body centers across 287 systems**, including moon offsets, exactly in Debug and Release. `StationPoseGeneration.GenerateForSystem` combines existing address-derived body definitions, this layout and the reset station stream: **286/286 supported valid-address systems match the native pose exactly**. The expanded corpus contains 300 total addresses, including explicit star-index boundaries and galaxies 0, 9 and 255. Thirteen prime-only cases and one synthetic system-index-zero case are excluded from the address API and return null. Low-level pose comparison still matches 300/300 with native body inputs. Both reference cabinet hashes also still match.

Counts must be captured at station entry. The generator later combines primary and prime counts in the first count field; reading both fields after the entire main routine returns double-counts prime bodies. The reference station's counts at entry are three primary and two prime bodies, not five primary plus two prime bodies. This explained the first unsuccessful layout comparison.

Moon placement uses the nonzero native global gate at `0x14526da3c`. Its semantic mapping has not been independently verified; the new API requires an explicit value rather than assuming it from an unrelated catalog field. Forced layout modes, prime-only systems and gas-giant system generation remain unsupported by the address API. No size-4 bodies appeared in the expanded corpus. No UI is enabled, and no new in-game observations were made.

Scratch verification now catches exceptions at the top level, reports failures to stderr and exits with code 1. Unhandled exceptions previously opened Windows application-error dialogs; do not run that earlier harness version.

`StationPoseGeneration.Generate` ports `0x14164ff80` from its incoming layout MWC state, exact generated body centers/sizes, body counts and extracted solar globals. It independently produces the station appearance seed. It returns no pose for missing inputs, invalid selected sizes or unresolved degenerate-vector fallback cases. It does not select a station scene or expose equipment in the UI.

Scratch `shipprobe/station-pose-vectors.py` captured inputs at native routine entry and output poses after generation for the reference address plus 99 deterministic random 48-bit addresses. The source-referencing `shipprobe/posecheck` harness compared position, facing and appearance seed exactly: **100/100 in both Debug and Release**. These are executable-emulator comparisons, not additional in-game station observations. The oracle must load `emu/globals.py`; without that setup the reference facing becomes NaN. Its unrelated placeholder `GetTickCount64` imports are still reported.

Both reference cabinet hashes also match when the harness uses the production pose: `0x10662FF4655D0E43` and `0x7906F5F0B4B0AA31`. Their scene translations remain scratch regression inputs, not universal production coordinates.

Native normalization computes `1f / sqrt(lengthSquared)` once, then multiplies the vector. Componentwise division caused one-unit float differences and matched only 35/100 poses; reciprocal multiplication matched all 100. Keep that operation order. Full layout generation, descriptor selection and equipped stats remain open.

The narrow catalog path now reuses the existing `PlanetGeneration` category without a database schema change:

- `StationEquipmentTables` is a Core DTO for solar layout globals, all four station scene roots and verified cabinet definitions. Its vectors and matrices use scalar arrays/properties so default `System.Text.Json` serialization retains every component.
- `CatalogSteps.Planets.cs:AddPlanetGeneration` writes a `STATIONEQUIPMENT` row beside BIOMETABLES, PLANETPROFILE and INVENTORYGENERATION. `CatalogSteps.StationEquipment.cs` owns the PAK/libMBIN traversal.
- `CatalogService.GetStationEquipmentTablesAsync` reads the typed row on a worker thread; UI/Core do not query or decode PAK files.
- `CatalogItem.DescriptionEnglish` can hold the DTO JSON. Supply nonnull NameEnglish so `CatalogTrimService` keeps the row. A catalog format change/invalidation must include the new extracted data.
- A Core station-location generator can consume that DTO and produce strict cabinet records: scene/node identity, world origin, site seed, selected pool member, actual class and intrinsic bonuses. Missing required roots, transforms, attachment metadata or tables must yield no exact record, not a default cabinet or inferred stock.

Current extraction is intentionally narrow. It emits cabinet geometry only when `GcSimulationGlobals.SpaceStationFile` is the verified `SPACESTATIONTYPEB.SCENE.MBIN` root. It traverses A/B/C separately and accepts a location only when the referenced attachment contains an embedded `GcInteractionComponentData` whose type is `WeaponTerminal`. Root descriptors choose exactly one extracted launch branch. Legacy, pirate and abandoned roots receive no borrowed TypeB coordinates. The Core address-to-pose port consumes the extracted solar globals, but still requires the verified moon-placement gate explicitly. This establishes nothing about planetary cabinets or crashed ships.

### Existing pieces and their limits

| File/type | Reusable behavior | Limitation |
|---|---|---|
| `SystemMultiToolGenerator` | Native pool construction and selection by site seed/type | Does not place a cabinet |
| `MultiToolSiteGeneration` | World-position hash and class roll | Requires the exact interaction origin |
| `EquipmentGeneration.CalculateMultiToolBaseStats` | Actual-class intrinsic bonuses | Requires resolved weapon kind and inventory seed; not equipped display stats |
| `ShipParts` | Descriptor parsing and deterministic part selection | Station descriptors are not currently extracted; applicability must be verified |
| `CatalogSteps.Ships.cs:AddShipLooks` / ShipScenes JSON | Reference traversal pattern | Exports only name/material/reference/children; drops all transforms and ATTACHMENT metadata; starts at spacecraft `_PROC` scenes |
| `ShipModels.Local` / `Walk` | Scene transform construction and reference traversal in GameData | Private on-demand mesh loader, not a Core/catalog location API; cannot use its mesh-only pruning for interaction entities |
| `NmsSpaceStationPaths.DefaultFurniture` | Station-local fixed template furniture | Does not describe station pose or procedural cabinet placement |

Do not extend the old `SystemMultiToolItemViewModel` constructor into an exact-stock record without replacing its automatic hypothetical S-class calculation and generic location instructions.

### Original catalog gap checklist (historical)

The checklist below predates the extracted station tables and layout port described above;
it is retained as the original research scope, not the current completion status.

1. Solar layout fields: `SolarSystemSize`, angle/elevation change ranges, maximum radius fields, `GenerateForcedNumberPlanets`, `GenerateMaximumSolarSystem`, `MassiveSolarSystems`, `StationSpawnAvoidRadius`, and player interpolation range if producing that separate matrix. Extract actual fields needed by the eventual layout port instead of assuming defaults.
2. `GcSimulationGlobals` station root resources: SpaceStationFile, LegacySpaceStationFile, PirateSystemSpaceStationFile and AbandonedSpaceStationFile. The global root must lead to the selected station scene; TypeB reference coordinates are not a substitute for resource/descriptor selection.
3. Complete relevant scene-node transforms and reference chains, retaining ancestor nodes and any conditions that enable a cabinet. Export the exact interaction origins with stable scene/node identities; do not identify equipment merely by a visual mesh name.
4. ATTACHMENT entity/component data confirming weapon interactions, their pool/reward selection rules, enabled state and reseeding behavior. Relevant schemas include `GcInteractionComponentData` (InteractionType, InteractionSpecificData, ReseedAfterRewardSuccess), `GcWeaponTerminalInteractionData`, and `GcRewardWeapon` (PoolTypeProbabilities, ItemLevel, ForceFixed, UsePlanetSeed). The correct path for each cabinet must be traced rather than inferred from type names.
5. Descriptor trees for any station root or referenced scene whose generated choice changes equipment presence or location. Existing ShipDescriptors extraction includes spacecraft and multi-tools only.

Use DTO properties or explicit float arrays for serialized vectors/matrices. System.Numerics Vector3/Matrix4x4 store their components as fields, which default System.Text.Json serialization does not include; a naive DTO containing those types would silently serialize incomplete data unless field handling/converters are supplied.

### Station root dispatch (2026-10-25)

Build 180836 selects station roots at `0x1416377CA..0x141637814` and again at
`0x14163C5E6..0x14163C62C`. Priority is abandoned (`system+0x2E14`), then pirate
(`system+0x2E17`), then the runtime legacy predicate `0x1416434D0`; otherwise standard.
These load the simulation-global resource pointers at `0x144F3EB10`,
`0x144F3EC00`, `0x144F3EBB0`, and `0x144F3ED00`, respectively.

`StationRootSelection.Select` ports this priority. A native slice oracle checked all
eight boolean combinations using distinct resource pointers. The oracle substitutes the
legacy predicate's return value: it proves dispatch priority, not legacy-state resolution.
`StationCabinetLocations.Locate` requires separately established station presence and a
resolved false legacy predicate before using the supported standard geometry; absent,
abandoned, pirate, legacy and unknown states return no locations. The lower-level
`LocateStandardTypeB` retains its explicit caller precondition.

The legacy predicate is not a pure system-address rule. Its disassembly includes a global
override, player/session gates, stored universe addresses and address/distance comparisons
(`0x1416434D0..0x141643900`). Do not assume false from normal economy/race flags.
Resolving those runtime inputs and the reload-body pool remains necessary before UI use.
The ordinary moon-placement gate is now extracted from the supported debug BootMode;
the automatic station-pose overload no longer needs the caller to supply that flag.

### Legacy early-return gates and debug schema (2026-10-25)

The first instruction of the legacy predicate reads the debug override at
`0x14526F5A0` (`GcDebugOptions+0x2270`). Native debug serialization at
`0x14288AF8A` confirms the field name `UseLegacySpaceStations` and offset.
The vendored schema included `HmdShowEyeAimTarget`, absent from this installed
build's serializer; it shifted this boolean and subsequent flags by one byte.
Removed that field and corrected subsequent offset comments. A serializer scan
matches all 457 captured named offsets after this change (not every field or enum
was captured). This supplements the earlier embedded Toolkit four-byte correction.

Catalog format 39 stores nullable `ForceLegacySpaceStations`; missing debug data
remains unknown. `StationRootSelection.ResolveLegacy` resolves only proven early
returns: forced legacy is true, otherwise inactive seasonal rules or disabled
`GcSeasonalGameModeData.UseLegacyStationsNearStartUA` imply false. The native season
validity helper is `0x1405E16D0`; the legacy caller checks the seasonal flag at
`season+0x6B92`. A seven-case native oracle exercised these branches and the real
validity helper with controlled season gates. The active-season/legacy-enabled
address and distance branch remains unsupported and returns null in Core.

The production schema was also decoded against in-memory copies of the installed
debug MBIN with opposing bytes at `+0x2270/+0x2271`. Both toggles resolve to the
correct legacy and movement-stick fields; no installed file was changed. Catalog
extraction matches the installed raw legacy flag. The complete reference harness
still matches all four located multi-tools and 286 supported automatic station poses.
This does not establish a save-derived season-validity provider or reload-body source.

### Integration status audit (2026-10-26)

The generation service has no production UI caller yet. Verified model/inventory/stat
composition and saved progression/known-tech/food inputs do not establish a complete
player-facing feature. For the first supported standard-station flow, remaining work is:

1. Establish session or explicit reload-scenario inputs, seasonal/legacy state and the
   native economy row together, with clear snapshot/context validity.
2. Connect the service to Planet Details with supported exact records and graceful
   omission of unsupported cases; preserve the verified-versus-unknown boundary.
3. Validate the connected UI and game behavior with the owner after build/harness checks.

Planetary cabinet transforms, additional multi-tool model families and crashed-ship
generation remain separate unfinished work for the broader exact-equipment requirement.
Standard geometry dispatch now also rejects a catalog-forced legacy station even when
a caller supplies a contradictory false legacy flag. A targeted regression exercises
that conflict; the four reference records continue to pass.
