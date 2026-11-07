# Multi-tool equipped statistics

## Standard runtime inputs from initialization and save state (2026-10-25)

Inventory row cycling is now resolved for ordinary game-state initialization.
The caller at `0x140307df6` passes manager+0xe70 into constructor `0x140487ce0`.
That constructor addresses its player member at +0xaad0 and writes byte
player+0x1aa40 = 1 at `0x140487e0e`, exactly the manager+0x26380 byte read by
inventory layout generation. Native execution verified the caller pointer chain
and overwrote prior byte values 0/1/255 with 1. `ForStandardGameState` uses this
verified constructor behavior; the low-level constructor still accepts an explicit
cycling flag for test/debug contexts. This is not a guessed save/difficulty flag.

The technology eligibility level is also resolved: load code at `0x140573bf4`
copies raw GcPlayerStateData+0x866d4 (ProgressionLevel in the vendored schema)
into player+0x1a9d0, which the selector reads at manager+0x26310. Five native
load cases, including 0 and int.MaxValue, reproduce the copied value.
ProgressionLevel's save key is DtI, cross-checked using the
[published MBINCompiler mapping mirror](https://github.com/zencq/libNOM.map/blob/master/libNOM.map/Resources/mapping_mbincompiler.json)
and direct read-only inspection of the working Slot8 snapshot (integer 1).
The mapping mirror identifies itself as libMBIN 6.11.0.1; the native field copy
and current save observation are separate checks against the installed build.

`NmsMultiToolGenerationPaths.ReadStandardGameState` reads that level and the
established KnownTech array at 4kj from an already selected active player-state
snapshot. It removes the save's caret ID marker, rejects missing/malformed data,
and creates the standard cycling context. Slot8 currently has 156 known techs.
An additional 100 native ranking contexts populated with those IDs and level 1
match the Core selector: 78 complete supported selections, 22 unsupported module
stops. This supplements the earlier empty-known-tech ranking comparisons.
All four reference located records retain their expected inventory/stat results
when supplied this actual save-derived progression/known-tech snapshot.

The app service now accepts an active-state snapshot and resolves these inputs
without caller-supplied level, known-tech list or cycling value. The caller still
selects the active normal/expedition state and supplies established native wealth
and active-special status. Station type/reload source and moon-placement input
remain unresolved prerequisites for automatic location/UI wiring. No save was
written and no UI was launched.

## Automatic ordinary model-component resolution (2026-10-25)

Catalog format 37 adds `PlanetGeneration/MULTITOOLMODEL`: the actual main-model
descriptor tree, scene hierarchy and embedded custom inventory components.
Attachments are decoded as typed MBIN entities after normalizing their path
separators; no arbitrary entity-byte tag search is used by this exact path.
Each scene node records whether its ID is a descriptor option, its inventory
size/desired-tech/cool metadata, and unsupported dependency/platform conditions.

`MultiToolModelGeneration.Resolve` selects descriptor parts from the model seed,
then prunes unselected scene branches before collecting inventory components.
The ordinary normal branch must resolve to one unambiguous non-default size tag:
small gives Pistol, medium/large Rifle. Desired technology overrides are retained
as an explicit flag so the complete-record generator can reject them. Selected
unresolved links, scene references, platform exclusions, cool variants, malformed
descriptors and ambiguous size tags yield no resolved model. Other rare main-model
kinds and separate model files remain unsupported by this exact resolver.

All four reference model seeds now resolve their kinds/tags automatically, and
the composed located records retain the expected actual class, inventory and
damage/scanner pairs. This is reference-case validation of model component
resolution, not a native scene-loader comparison over arbitrary seeds. The
existing native algorithm regressions still pass. Additional checks reject missing
or malformed model metadata, selected unsupported nodes and conflicting sizes.

`LocatedMultiToolService` now has an overload that loads this model row alongside
inventory/technology tables and resolves components on the worker thread. Callers
no longer need to provide those facts for supported ordinary tools. The previous
explicit-model overload remains available for callers that already have verified
facts. It also declines results while the app's catalog builder is active, in
addition to checking the rebuild stamp before and after loading/generation.
The adapter is build-checked; it is not enabled in Planet Details yet.
Station/reload/runtime providers, including moon placement and inventory cycling,
remain prerequisites for automatic UI wiring. No game/save/catalog database was
modified by the reference harness.

## Located-record composition and app-service boundary (2026-10-25)

`LocatedMultiToolGeneration.Generate` now composes a selected cabinet item,
resolved main-model Pistol/Rifle component facts and explicit native runtime
inputs into one `LocatedMultiTool` record. It includes the actual class,
inventory, intrinsic bonuses, displayed damage maximum and scanner range.
The model seed/path must match the selected item; the world origin must hash
to the supplied site seed; class must agree with the runtime economy row.
Technology overrides, unsupported kinds/pools, active-special effects and any
unresolved inventory/stat stage return no record. Partial equipment is not
promoted to an exact result.

The composed record matches all four reference tools, including their displayed
damage/scanner pairs. Negative checks reject mismatched model seeds, class and
site hashes, unresolved kind, component overrides, missing damage metadata and
active-special runtime state. Native algorithm regression checks remain intact.

`LocatedMultiToolService.GenerateAsync` is the app adapter. It snapshots known
technologies, loads typed catalog tables concurrently, and performs catalog
stamp reads and generation on a worker thread. It supports cancellation and
discards results if the catalog rebuild stamp changes during loading or
generation. It performs no save/database mutations and no PAK reads.
The adapter is compile-checked with the app; it has not been exercised through
the UI. No Planet Details equipment panel has been enabled by this change.

Automatic UI wiring still needs a verified provider of station type/reload
state, moon placement, inventory cycling, progression/known technology state,
active-special state and complete selected model-component metadata. The
existing Planet Details panel continues to show recorded sites. There are no
fallback values for these inputs in the new adapter. This supersedes the earlier
statement that no app-side integration code exists, but does not claim that the
feature is ready for end-to-end use.

## Complete standard damage maximum (2026-10-25)

`MultiToolDamageGeneration.StandardPotential` now reproduces the complete
`0x14140f400` mode loop for supported standard inventories/default runtime flags.
The native routine evaluates all 21 modes unconditionally, including inactive
modes, and takes their maximum. It does not first filter to installed weapons.
Catalog format 36 extracts the complete enum-indexed weapon-properties array,
mode classes/base-stat filters, PLAYER and PLAYER_RAIL beam values, standard
OVERHEAT duration, and the shared increasing intrinsic source for laser and
projectile damage. Missing/incomplete modes, ambiguous intrinsic mapping or
custom-biome stat substitutions disable this aggregate result.

The native mode dispatcher has three contributing classes. Projectile uses the
existing rate/clip/reload formula; inactive zero-clip defaults yield zero when
the denominator is valid. Laser uses the existing heat/reload formula and
per-hit minimum-one clamp. Rail selects PLAYER_RAIL; other laser-class modes
select PLAYER. Charged projectile uses truncated equipped maximum-charge stat
24 times truncated per-hit projectile damage, times executable coefficient
2.015f, divided by equipped charge-time stat 28. Other classes return zero in
this routine, even where separate combat routines calculate their damage.
The coefficient is the float at `0x144b29a40`, not a fitted content default.

The native oracle loads actual projectile and weapon-properties MBINs, retains
the native per-mode potential, per-hit and equipped-stat routines, and executes
the full display maximum routine. Technology lookup, runtime group counts,
intrinsic-definition lookup, beam-map lookup and empty secondary inventory are
supplied. The animation lookup supplies the extracted standard duration.
No potential, per-hit or equipped-stat arithmetic result is substituted. Module
records use the separately checked native roll slices and procedural groups.
The 100 varied intact inventory fixtures include nonzero intrinsic bonuses and
charged cells; every one of their 2100 individual mode values and 100 complete
maxima matches. Eleven fixtures exercise nonzero Cannon/charged-projectile
potential, so that branch is not validated only at its inactive zero default.

The complete generated reference inventories reproduce displayed damage
443.3 (Mass Needler), 333.3 (Geometric Angle Focuser), 1362.7 (Iridescent Inertial
Focuser) and 333.3 (Aspect of Aktot-XIV). Their scanner checks remain 252.3,
209.8, 206.4 and 285.4. Earlier notes below that list the damage maximum as
unfinished are superseded for this supported standard/default-runtime scope.
Other model animation sets, active-special effects, overloaded or damaged
inventories and custom-biome substitutions remain unsupported. UI integration
and broader site/runtime resolution remain separate work.

## Equipped Boltcaster mode (2026-10-25)

`MultiToolProjectileGeneration.BoltPotential` now resolves the complete Bolt
formula from a supported generated inventory. Damage and clip-size contributions
add, rate and reload multiply, and extra bullets per shot use the maximum
contribution. The shared evaluator now supports that maximum mode: charged
values are adjusted, but adjacency does not modify maximum-mode contributions.
The native per-hit function truncates projectile damage without the mining
laser's minimum-one clamp. The final rate-times-clip arithmetic remains unchanged.

Catalog format 35 confirms the Bolt row in PLAYERWEAPONPROPERTIESTABLE is an
ordinary Projectile using Weapon_Projectile, with no custom-biome stat override.
It also verifies GcGameplayGlobals.UseMultiplierBonus and the inventory table's
single, increasing intrinsic source for Weapon_Projectile_Damage. Unknown or
changed mapping/mode metadata disables the Bolt rules. The dictionary overload
reads that catalog-resolved bonus ID from generated base stats; it does not fit
or assume a percentage from the displayed damage. The current extracted source
is WEAPON_DAMAGE, which also targets laser damage and grenade damage; mining
bonus separately targets Weapon_Laser_Mining_Speed with LessIsBetter=true.

The native harness loads the actual weapon-properties MBIN and retains the native
equipped evaluator, per-hit routine and projectile potential routine. Technology
lookups, runtime group counts and an empty secondary inventory are supplied.
It runs 750 stat comparisons across 150 inventories and five Bolt stats, then
300 potential comparisons (each inventory with suppressed intrinsic adjustment
and with a nonzero intrinsic damage bonus). Native intrinsic lookup uses the
actual inventory-table base-stat definitions and inventory base-stat records;
the percentage adjustment is not replaced by an arithmetic result hook.
All comparisons match exactly. Ordinary module records use separately checked
native rolls. Twenty-five inventories additionally supply deliberate fractional
extra-shot stat fixtures to exercise maximum selection, special slots and integer
truncation; those fixtures are not claims about actual generated module rolls.

The complete generated Iridescent Inertial Focuser inventory and intrinsic-stat
dictionary produce 1362.7, matching the screenshot. Its bonus is 1.115343 percent,
so the equipped 180 damage truncates to 182 before the firing formula. Missing
bonus keys, missing mode metadata and active-special contexts return no result.
All earlier laser, scanner, module, selection, layout and placement checks still
pass. Shotgun/charged projectile modes, other runtime stat substitutions, the
maximum across all weapon modes and exact-equipment UI integration remain open.

## Equipped mining-laser mode (2026-10-25)

`MultiToolStatGeneration.Evaluate` now holds the shared intact-inventory stat
walk previously used only by scanner range. It supports explicitly supplied
additive and multiplicative combination modes, base-stat filtering, generated
module bonuses, supercharged positions, group-limit rejection and adjacency.
`MultiToolScannerGeneration.Range` uses this shared walk; its existing 300 native
comparisons and four screenshot checks still pass. This is not a universal
stat-metadata resolver: callers must establish the stat's combination mode and
any applicable intrinsic adjustment.

The native mining-laser path combines damage stat 2 additively, and heat stat 4
and reload stat 6 multiplicatively, filtering by the laser base-stat group.
`MultiToolLaserGeneration.StandardPotential` composes these equipped values,
the caller-resolved laser damage percentage, integer per-hit truncation/clamp,
PLAYER beam DefaultDamage/HitRate and the standard OVERHEAT duration. Catalog
format 34 extracts the PLAYER laser row, the OVERHEAT animation's explicit
frame bounds/speed/frame rate, and the two additive adjacency coefficients.
The animation duration is calculated from those extracted fields; no fitted
cooldown is used. Other model animation sets are outside this standard helper.

The expanded native harness runs 600 stat evaluations across 150 inventories
and stats 2/4/6/57, using explicit base-stat filters. It retains native equipped
evaluation, adjacency and special-slot adjustment, with technology lookup,
group-count cache, empty secondary inventory and inactive special technology
supplied as runtime inputs. All values match exactly. Seventy-five inventories
include ordinary native-rolled laser modules. The source module stats come from
the already checked native roll slices, with the generated procedural group.

A further 88 inventories containing LASER execute the native laser-potential
routine `0x141416930`, including its per-hit routine `0x141411bc0` and the actual
equipped evaluator. Beam lookup, default secondary inventory and runtime caches
are supplied, with explicit PLAYER beam data and standard overheat duration.
No per-hit/stat/formula result hook substitutes the values under comparison.
All 88 composed potential values match exactly. These cases suppress intrinsic
adjustments and request neutral difficulty; they do not establish the mapping
of nonzero inventory intrinsic bonuses to laser damage. The production helper
therefore requires the applicable percentage explicitly from the caller.

The actual generated Mass Needler inventory yields 443.3; the generated
Geometric inventory yields 333.3 for this laser mode. Both match the supplied
screenshots. This does not establish displayed Damage Potential for inventories
with other damaging modes: projectile evaluation and the maximum across all
weapon modes remain unfinished. The exact-equipment UI remains disabled.

## Equipped scanner evaluation (2026-10-25)

`MultiToolScannerGeneration.Range` now evaluates a supported intact initial
inventory, including regular bonus lists, actual generated module bonuses,
supercharged positions and orthogonal adjacency. It then applies the site's
intrinsic scanner percentage and extracted TOOL pulse range. Catalog format 33
adds regular bonus values/levels, technology groups and scanner gameplay rules.
Generated modules use their procedural definition's group, matching the native
overwrite at `0x140ec442b`, rather than retaining the template's group.

Scanner radius is multiplicative. A charged radius value below one is multiplied
by the reciprocal special-slot bonus; other radius values are multiplied by the
bonus. Adjacency is gated by the source's base-stat group. A source stat whose
parent occurs directly in the neighbor's stat list gets the child bonus; pairs
sharing parent groups without either directional parent relationship get the
same-type bonus. Only radius values greater than one receive multiplicative
adjacency. Float operations retain the native sequence in `0x1405a7240`.

The native harness calls the full equipped-stat evaluator `0x1405a7ea0`, with
explicit installed packets, generated module records, special slots and an empty
secondary inventory. Technology lookup and the runtime group-count cache are
supplied; the result cache is disabled and active special technology resolves to
none. Module stat records come from the previously checked native roll slices.
Intrinsic adjustment is disabled in this native factor comparison and checked
separately against screenshot results. The harness initially stalled on the
uninitialized native group-count cache; those runs were discarded. After supplying
that cache input, all 300 varied inventories match. The cases also vary the two
adjacency coefficients independently, so identical default values cannot hide
the distinction between the rules.

The combined generation/evaluation harness now reproduces all four reference
scanner ranges: Mass Needler 252.3, Geometric Angle Focuser 209.8, Iridescent
Inertial Focuser 206.4 and Aspect of Aktot-XIV 285.4. In particular, Geometric's
generated `UP_SCAN1#21318` rolls stat 63, value 10.390695, rather than a radius
bonus; its 209.8 comes from its intrinsic scanner bonus. This supersedes the
earlier unresolved 209.8 notes below. No technology factor is fitted to the
screenshots. Runtime active-special effects, overloaded technology groups,
damaged/player-edited inventories and extra inventory sources remain unsupported.
Unknown metadata returns no scanner result. Damage evaluation and UI integration
remain separate work.

## Seeded procedural module identity (2026-10-25)

`MultiToolProceduralModuleGeneration.InitialId` implements the ordinary,
unsuffixed `UP_` module path at `0x140ec38c0`. The supplied inventory seed is
expanded directly into a fresh MWC stream; the first word supplies
`(word * 100000) >> 32`. Native formatting is `%.9s#%05u`.
The helper accepts uppercase catalog IDs up to nine characters and rejects
already suffixed IDs and special dispatch requests. It does not resolve stats.

A scratch Unicorn oracle captured EAX at `0x140ec3a13`, before the native
formatter, for 100 seeds and three module IDs (300 comparisons). Cases include
zero, all bits set, and a zero low word. All suffixes matched the Core helper.
This proves the numeric suffix; native string formatting was inspected in
disassembly rather than executed by the oracle. The constructor's placeholder
GetTickCount64 import is overwritten by each explicit valid seed before drawing.
The Release stationcheck run also retained all prior descriptor, slot, layout,
candidate, selection, projectile and placement comparisons.

The materializer at `0x140ec3d10` uses a separate stream derived from the
numeric suffix. Its stat selection separates AlwaysChoose entries from optional
entries, shuffles the optional list, appends it after the mandatory entries,
and caps the selected total at four. Stat values call weighting-curve evaluation
at `0x142d6f5c0`, then interpolate ValueMin to ValueMax with float arithmetic.
`RollOrdinaryStats` now implements that isolated path for quality 0..3, with
default runtime flags. Catalog format 31 includes each eligible module's stat
ranges, mandatory flags, count range and weighting indices, plus the seven curve
types from `METADATA/REALITY/DEFAULTREALITY.MBIN`. Current curve values are
Linear, EaseInQuad, EaseInQuart, EaseInExpo, EaseOutQuad, EaseOutQuart and
EaseOutExpo; these values are extracted rather than assumed at runtime.

Suffix initialization: xor the suffix with `0x3d0000`, shift that result right
16, xor with the original suffix, multiply by 9, xor with its right shift by 4,
multiply by `0x1b873593`, then xor with its right shift by 15 (32-bit overflow).
The resulting word initializes MWC, with carry equal to its 16-bit rotation xor
itself. Two draws supply an independent name seed before the stat-count draw.
The requested optional count uses the weighted float draw, interpolation and
positive half rounding. Mandatory entries precede a backwards Fisher-Yates
shuffle of optional entries; the final count includes the mandatory count and
is capped by the available entries and four. Each selected stat consumes one
weighted draw, interpolated in native float operation order.

Verification: 100 suffixes matched the native state captured at `0x140ec3efe`.
An isolated execution of `0x140ec4703` through `0x140ec4d25`, supplied the
resolved definition, post-name-seed MWC state and default runtime flags,
produced 3600 comparisons (36 ordinary weapon modules x 100 suffixes).
Counts, ordered stat IDs, quality levels and values matched with zero ULP
differences in the Release harness. Native powf imports are replaced by the
oracle's rounded host power function, so these cases do not establish exact
agreement with the game's CRT powf for every possible suffix. No full module
materializer, naming, special-quality overrides or subsequent inventory ranking
was exercised by this isolated oracle.

## Module selection and inventory integration (2026-10-25)

The later ranking loop uses a selected module's actual rolled stat IDs when
updating its used-stat set. Substituting all template stat IDs changes subsequent
rankings. `SelectLoadout` now resolves ordinary module IDs and bonuses, uses those
actual stat IDs, and retains bonuses through `SelectAndPlaceLoadout` and
`InitialInventory`. The earlier explicitly regular-only methods retain their
failure behavior for procedural winners. Missing metadata, component overrides
and special-quality modules still return no complete inventory.

The native candidate record at `+0x10` copies the original inventory seed, and
the materialization call at `0x1404cfae6` passes that seed into the ID wrapper.
The rolled module's base-stat group comes from its technology template; its
generated stat list supplies the additional used-stat IDs. Catalog format 32
therefore extends the extracted parent-stat map to include procedural stat lists.

Expanded roll verification covers 7200 module/suffix combinations (36 ordinary
weapon definitions x 200 suffixes), with no float-bit differences. The 200 suffix
seed-state checks also pass. The ranking oracle now supplies generated technology
records populated from those independently executed native roll slices, rather
than retaining template bonus lists. Native ranking/insertion then runs normally,
apart from the pre-existing insertion/free-capacity hooks. Across 400 contexts
(100 inventory seeds, all four native wealth rows, varying slot budgets and
progression), 343 supported sequences match IDs and module stat records exactly;
57 stop at unsupported module types and correctly produce no Core result.
Placement preserves every module bonus and the established first-free order.
This is a composed ranking oracle, not execution of the complete native
materializer; module names, CRT differences and special runtime paths remain
outside its evidence. The app's exact equipment UI is not enabled by these APIs.

## Verified reference (2026-10-24, NMS build 180836)

The owner supplied station screenshots and purchased **Mass Needler Z-C11** in
Steam Slot 8 (DELETEME). Read-only inspection gives model seed
`0x4101B7E8CB7DAFD9`, inventory/site seed `0x10662FF4655D0E43`, class B,
intrinsic bonuses Damage `0`, Mining `10.274667739868164`, Scanner
`26.141597747802734`. Technologies are SCAN1 at `(0,0)`, SCANBINOC1 at `(1,0)`,
LASER at `(2,0)` and UT_SURVEY at `(1,1)`. LASER occupies a supercharged slot.
Screenshots before and after purchase both show Damage Potential **443.3** and
Scanner Range **252.3**. The other station tool, **Geometric Angle Focuser**,
class C, shows Damage Potential **333.3** and Scanner Range **209.8**.

These are equipped statistics, distinct from intrinsic percentage bonuses.
The mining percentage does not become the displayed Damage Potential.

## UI and executable call chain

- `0x1406ad2c7`: the multi-tool stat panel calls `0x14140f400` for full damage
  potential. A second call supplies the flag that suppresses intrinsic bonuses
  for the bar's base portion. The displayed numeric string uses `%0.1f`.
- `0x14140f400`: obtains the selected tool's OVERHEAT animation duration via
  `0x1413f8420` (first/third-person animation name) and `0x140206900`, then loops
  over 21 player weapon modes, evaluating `0x141416930` and retaining the
  maximum. It does not simply sum every installed weapon's damage.
- `0x141416930`: mode properties come from `GcPlayerWeaponPropertiesTable`.
  Different weapon classes use different formulas. The Laser branch begins at
  `0x141416991`; it reads PLAYER laser beam data, obtains per-hit damage from
  `0x141411bc0`, heat-time stat 4, reload-time stat 6, and computes the formula
  below. The mining laser is the maximum for the reference inventory, which
  has no other weapon installed.
- `0x141411bc0`: Laser branch reads stat 2, truncates to an integer, clamps that
  integer to at least 1, multiplies by laser beam DefaultDamage, then applies a
  difficulty multiplier unless the caller requests neutral difficulty. The
  damage-potential caller requests neutral difficulty.
- `0x1405a7ea0`: generic equipped-stat evaluator. It combines technologies and
  applies intrinsic bonuses only if requested. `0x14057aad0` walks installed
  technologies; `0x14057ad70` combines their stat values according to the stat
  metadata and caller flags. `0x1405a7240` also accounts for adjacency bonuses.
- `0x14057a970`: individual technology stat value with supercharged-slot
  adjustment. It checks the slot via `0x1404d3410`, reads
  GcGameplayGlobals.SpecialTechSlotBonus, and applies the appropriate additive
  or multiplicative rule. For the reference LASER values, damage and heat are
  multiplied by 1.25; reload time (a multiplicative value below 1) is divided by
  1.25. The code also supports a separate active-special-tech bonus, so the
  supercharged case alone is not the complete generic evaluator.
- `0x1406ad681`: Scanner Range reads equipped stat 57 (`Weapon_Scan_Radius`).
  At `0x1406ad748` it resolves the selected scan-data row, then multiplies by
  that row's PulseRange. The reference uses TOOL, whose PulseRange is 200.
- Animation duration path `0x140206900` -> `0x142d54740` -> `0x142d55c80`, or
  `0x142d555d0` for a base animation, uses the end/start frame difference,
  1/60 when Has30HzFrames is false (1/30 otherwise), and animation speed. Thus
  no fixed guessed cooldown is required.

## Extracted inputs (do not hardcode into production)

These are values from the installed game, suitable for extraction into the
catalog if this evaluator is implemented:

| Game file / field | Reference value |
|---|---|
| `metadata/reality/tables/nms_reality_gctechnologytable.mbin`, LASER / Weapon_Laser_Damage | 20 |
| same, LASER / Weapon_Laser_HeatTime | 8 |
| same, LASER / Weapon_Laser_ReloadTime | 0.6 |
| same, SCAN1 / Weapon_Scan_Radius | 1 |
| `gcgameplayglobals.global.mbin`, SpecialTechSlotBonus | 1.25 |
| `metadata/projectiles/projectiletable.mbin`, laser PLAYER / DefaultDamage | 1 |
| same, laser PLAYER / HitRate | 0.05 |
| `models/common/weapons/multitool/multitool/entities/multitool.entity.mbin`, OVERHEAT and OVERHEAT_3P | start 0, end 160, speed 1, Has30HzFrames false |
| `metadata/simulation/scanning/scandatatable.mbin`, TOOL / PulseRange | 200 |

The entity animation points to MULTITOOLLAZOVERHEAT(.ANIM.MBIN), or its _3P
variant. Both actual animation files contain 161 frames, and the configured end
frame is 160.

## Reference calculations and float order

The executable uses single-precision SSE arithmetic throughout, followed by
conversion to double for `%0.1f`. Preserve each float operation, the per-hit
integer truncation, and table/runtime data selection.

Laser branch, using `D` = per-hit integer damage times PLAYER DefaultDamage,
`H` = equipped heat time, `R` = equipped reload-time multiplier,
`A` = OVERHEAT animation duration and `T` = PLAYER HitRate:

```text
cooldown = float32(R * A)
denominator = float32(cooldown + H)
uptime = float32(H / denominator)
hitsPerSecond = float32(1 / T)
potential = float32(float32(hitsPerSecond * D) * uptime)
```

No-supercharge reference:

```text
D = trunc(20) * 1 = 20
H = 8
R = 0.6
A = float32(160 * float32(1/60)) / 1
potential = (20 / 0.05) * 8 / (8 + 0.6 * (160/60))
          ~= 333.3333 -> 333.3
```

Mass Needler, with LASER supercharged:

```text
D = trunc(float32(20 * 1.25)) * 1 = 25
H = float32(8 * 1.25) = 10
R = float32(0.6 / 1.25) ~= 0.48
potential = (25 / 0.05) * 10 / (10 + 0.48 * (160/60))
          ~= 443.2624 -> 443.3
```

Scanner for Mass Needler:

```text
technology radius factor = 1
intrinsic adjustment = float32(float32(26.141597747802734 * 1) * 0.01)
equipped radius factor = float32(1 + intrinsic adjustment)
range = float32(equipped radius factor * 200)
      ~= 252.2832 -> 252.3
```

## Scope and remaining verification

### Regular loadout selection and combined inventories, 2026-10-25

`MultiToolTechnologyGeneration.SelectRegularLoadout` now ports the default
ordinary-weapon selector at `0x1404cf569` onward. Catalog formats 29/30 add the
technology/template rarity, required level, prerequisite, PrimaryItem, BaseStat
and bonus stat IDs, player-global TechRarityData weights, structural weapon stat
parents and inventory-tag-to-layout mapping. The native stat-parent table starts
at `0x144a1b444`; its following word at `0x144a1b448` is a different metadata
field and must not be mistaken for the parent. Weapon enum-name prefixes match
the native parent mapping for all extracted Weapon technology bonuses in this build.

Selection takes explicit requested slots, native wealth, progression level,
known technologies and available capacity. The optional-tech budget is one
inventory-seed draw, bounded by round-away-from-zero `(requestedSlots / 5)`
times the native wealth factors `[0.3,0.6,1.0,0.6]`, clamped to 1..10. Always
rarity does not consume that budget. Stable score ties retain the earlier table
entry. Required levels/prerequisites and missing parent stats alter eligibility
and ranking. These factors are native code constants; rarity weights come from
the player's data, not a copied table.

The method deliberately returns null if a procedural candidate wins, because
its materialized stats may change later ranking. It never substitutes template
stats as the actual procedural module. Caller-specific DesiredTech overrides
are unsupported by this default path. `InitialRegularInventory` explicitly
rejects them and combines catalog inventory-tag lookup, initial layout, regular
selection, placement and supercharged slots only when all stages resolve.

The native ranking oracle now covers **400 cases**: 100 seeds across all four
native wealth classes, varying requested counts 9/12/18/24 and progression
levels 0/100, with no caller overrides and an empty known-tech set. Production
matches the ordered installed IDs in all **261 regular-loadout cases**, and
returns null in all **139 procedural-winner cases**. The native oracle uses
template materialization hooks only to detect those unsupported cases; their
later selected contents are not treated as validated outputs.

The purchased Mass Needler's combined production inventory reproduces its
selected/placed SCAN1, SCANBINOC1, LASER, UT_SURVEY, all 12 unlocked slots and
both supercharged positions. No supplied technology ID list is needed by the
combined path. The native context uses Wealthy=2, progression 0, enabled cycling
and no overrides for this comparison.

Read-only inspection of actual selected model parts confirms both _ToolBase_1
and the selected barrels have empty DesiredTechs lists in all three regular
references. The barrel inventory components provide WEAPON_SMALL for Mass
Needler and Aspect, and WEAPON_MEDIUM for Iridescent; WEAPON_DEFAULT on the
base does not override these positive size rows. The actual inventory table
maps the selected tags to WeaponSmall/WeaponMedium. Using these extracted rows
gives requested count 14 for both second-station tools. Their complete selected
and placed technology lists match the screenshot:

- Iridescent: SCAN1 `(0,0)`, SCANBINOC1 `(1,0)`, LASER `(4,0)`, BOLT `(5,0)`.
- Aspect: SCAN1 `(0,0)`, SCANBINOC1 `(1,0)`, LASER `(2,0)`, UT_SCAN `(3,0)`.

These reference checks use Average=1, progression 0 and enabled cycling.
Universal runtime-context derivation, model component traversal/overrides,
procedural materialization, the general equipped-stat evaluator and UI exposure
remain open. A full WinUI x64 build to isolated scratch output completed with
0 errors; 9,068 existing/general warnings were reported. The app was not launched.

### Placement and projectile display arithmetic, 2026-10-25

`MultiToolInventoryGeneration.PlaceTechnologies` ports automatic placement at
`0x1404e71c0`: visit unlocked cells in row-major order, excluding occupied cells
and Broken special slots. TechBonus/supercharged cells do not block installation.
The API takes an explicitly selected technology order and broken coordinates;
it does not infer selected IDs or procedural module seeds. It returns no partial
loadout when capacity is insufficient.

`shipprobe/placement-oracle.py` calls the native first-free-slot routine against
100 generated layouts, alternating ordinary layouts and layouts with broken
cells, and fills occupied native element records after each placement. Production
placement matches **100/100 cases**. The purchased Mass Needler's known native
selection order SCAN1, SCANBINOC1, LASER, UT_SURVEY placed into its generated
WeaponSmall/cycling layout yields `(0,0),(1,0),(2,0),(1,1)`, matching all four
saved positions. This combines independently verified layout and placement with
the observed/native-oracle loadout; Core loadout selection remains unfinished.

`MultiToolEquippedStats` now contains the native laser, projectile and scanner
display arithmetic on **already resolved equipped inputs**. It does not evaluate
technology effects, adjacency, procedural stats or the maximum across 21 modes.
It must not be used to label arbitrary inventories with full equipped statistics.

The projectile branch at `0x141416b20` evaluates these float32 operations:

```text
rate = float(trunc(Weapon_Projectile_BulletsPerShot) + 1) * equippedRate
activeTime = float(trunc(equippedClipSize)) * rate
uptime = activeTime / (equippedReloadTime + activeTime)
potential = uptime * (rate * perHitDamage)
```

The rate-times-clip term is the executable's actual display arithmetic; do not
replace it with a physically motivated magazine/fire-rate model. The bullets
input is the extra-bullets stat, not BurstCap or BurstCooldown.

For Iridescent, Rifle/C intrinsic damage 1.115343% applied to BOLT's extracted
180 damage gives an integer per-hit value of 182. Uncharged BOLT has rate 7.5,
clip 64, reload 0.8 and zero extra bullets. The formula gives 1362.7289, displaying
**1362.7**, matching the owner's screenshot. This is a formula/input comparison;
all upstream equipped-stat evaluation and weapon-mode dispatch remain separate.

`shipprobe/projectile-potential-oracle.py` calls native `0x141416930` with a
projectile-class properties record and hooks supplying explicit equipped stat
values/per-hit damage. Production arithmetic matches **100/100 float32 results**,
including fractional clip/bullets inputs to exercise native truncation. These
hooks deliberately bypass upstream technology evaluation and are not evidence
that the general evaluator is solved. Reference checks also retain laser
333.3/443.3 and scanner 206.4/285.4 using their established equipped inputs.

### Technology candidates and native selection trace, 2026-10-25

Catalog format 28 adds `MULTITOOLTECHNOLOGY` in PlanetGeneration. It preserves
the eligible regular technology IDs in file order (Weapon category, excluding
IsTemplate entries), followed by eligible procedural IDs in their own file order
(non-Biggs, with a resolvable Weapon-category template). Impossible and Always
rarity entries remain in the list: eligibility and later ranking are separate.
`MultiToolTechnologyGeneration.Candidates` restarts the inventory-seed MWC for
each of those two tables and records one float32 score per eligible entry.

`shipprobe/technology-candidates.py` runs the native candidate construction at
`0x1404cefe0`, capturing its list at `0x1404cf534`. It uses actual extracted
technology/procedural MBINs, actual CRT hooks and a direct template-ID lookup
hook in place of the engine's asset index. Production candidates, extracted
through GameData and round-tripped through JSON, match **100/100 seed cases**:
all 111 candidate IDs, order, procedural flags and float32 scores per case.
This includes zero, one and screenshot inventory seeds. Candidates are internal
ranking inputs and must never be displayed as installed cabinet stock.

The separate scratch `technology-select-oracle.py` runs the native ranking path
with explicit slot count 12, native wealth and progression inputs. It intercepts
final item insertion and uses template stats in place of procedural module
materialization. It therefore validates ranking observations only, not complete
procedural modules or final slot placement. It selects SCAN1, SCANBINOC1 and LASER
for all four references; optional results include UT_SURVEY for the purchased
Mass Needler, BOLT for Iridescent Inertial Focuser and UT_SCAN for Aspect of
Aktot-XIV. The original C tool produces a procedural scanner template (T_SCAN)
in this simplified oracle, which is not yet a validated final module.

**Economy correction:** SystemAttributes.Wealth uses player-facing 1/2/3 values;
native GcWealthClass uses Poor=0, Average=1, Wealthy=2, Pirate=3. Native class
rolling at `0x1404cfda0` indexes the probability table directly by that native
value. The first reference's Advanced economy uses row 2. Modeysk's Satisfactory
economy uses row 1, not 2. The scratch second-station check has been corrected
and both classes still match. The prior match using row 2 was coincidental and
must not be used as evidence for the mapping. Native technology selection also
uses that wealth enum; supplying Wealthy for Modeysk spuriously adds T_LASER.

Aspect's B-rated upgrade is the regular UT_SCAN blueprint; it need not be a
procedural module. Its extracted Weapon_Scan_Radius bonus is 1.1. Applying the
native float order to that factor and the site's Pistol/B scanner bonus
29.741974% gives 285.43234, displaying **285.4**, matching the screenshot. This
is a scratch equipped-scanner calculation with the natively selected UT_SCAN,
not yet a completed production equipped-stat evaluator. Iridescent's Bolt
Caster damage and actual technology placement still need implementation.

### Initial unlocked-slot layout, 2026-10-25

`MultiToolInventoryGeneration.InitialLayout` now ports category-3 count/dimension
selection at `0x1404ce5c0` / `0x1404ce6c0` and row placement at `0x1404cfeb0`.
Catalog format 27 extracts MinTechSlots/MaxTechSlots and all TechBounds thresholds
for WeaponSmall/WeaponMedium/WeaponLarge, preserving enum names and native order.
The API deliberately requires the selected row and cycling mode; neither has been
established universally from a cabinet's model/class yet.

Count rolling draws once on a fresh inventory-seed MWC stream. Dimensions select
the small rectangle when the requested count fits its area, otherwise standard,
otherwise large. Placement independently restarts the same seed and chooses a
contiguous run per row: length in `[1,min(remaining,width)]`, then its start in
`[0,width-length]`. The noncycling mode visits each row once and can leave fewer
unlocked positions than requested. The cycling mode at manager `+0x26380` instead
counts unique positions after each attempt and revisits rows until filled. It
retains a native off-by-one quirk: an invisible row at index Height consumes two
draws before wrapping to row zero. This is not a reason to expose that row.

`shipprobe/layout-oracle.py` calls the actual native layout routine against the
player's extracted inventory MBIN. Production code matches **600/600 cases**:
100 seeds, three weapon size rows, both runtime cycling modes. The oracle reports
placeholder GetTickCount64 imports from random-state initialization, which is
then overwritten by the explicit inventory seed; the placement stream is checked
against that seed. Catalog DTO extraction and JSON round-trip are part of the
comparison.

Read-only current Slot 8 inspection of the purchased Mass Needler confirms all
12 `OsQ.hl?` positions: `(0,0),(1,0),(2,0),(1,1),(4,1),(5,1),`
`(0,2),(1,2),(2,2),(3,2),(4,2),(5,2)`. They match **WeaponSmall with cycling
enabled** exactly, including a 6-by-3 rectangle. None of the other five tested
size/mode combinations matches. This is a reference-specific validation, not
yet the caller rule for selecting size and cycling in all cabinets.

Technology selection at `0x1404cefe0` is still being traced. Its candidate stream
walks the technology table in file order, applies category/never-procedural
filters and draws per eligible technology. Later selection uses rarity weights,
required-technology checks, runtime level/progression at manager `+0x26310` and
override inputs. Do not replace it with a fixed scanner/laser list or assume
the two screenshot loadouts are determined by intrinsic bonuses alone.

### Initial supercharged placement, 2026-10-25

`MultiToolInventoryGeneration.InitialSuperchargedSlots` now ports the initial-placement
path at `0x1404d2350` for inventory category 3 (multi-tool). Its executable mapping at
`0x144a60590` selects the **WeaponMedium** generation row, not WeaponSmall or the
actual tool model's size. The catalog now extracts `SpecialTechSlotMaxIndex` and
`MaxNumSpecialTechSlots` from that row (catalog format 26). The reference install
contains inclusive bounds X=6, Y=3 and maximum count 4.

The native routine starts a separate MWC stream from the inventory/site seed and
draws X in `[0, MaxX]`, then Y in `[0, MaxY]`, retrying duplicate supercharged
coordinates. Initial C/B/A/S inventories get `min(classIndex + 1, maximum)` slots.
Zero maximum indices skip placement. Generated special positions are not clipped
to the currently unlocked area; hidden positions may become available through
later inventory expansion. This API generates an initial list only; it does not
modify existing special slots or write any save.

Scratch `shipprobe/supercharge-oracle.py` loads the actual extracted inventory MBIN
into the executable emulator and calls the native routine with fresh runtime
inventories. A production-source check, using the JSON-round-tripped extracted
catalog rule, matches **400/400 outputs** (100 seeds times all four classes),
including zero/one seeds and all four screenshot site seeds. Read-only Slot 8
inspection also confirms the purchased B Mass Needler's `OsQ.MMm` positions are
exactly `(2,0)` and `(1,3)`, matching production generation. Its installed LASER
is at `(2,0)`, confirming that the previously observed supercharge is predicted
from the site seed rather than supplied as a display-stat assumption.

The workspace was restored from `stash@{0}` after Cline's fishing-list work.
The stash was applied without dropping it; the fishing change remains committed
on development. The restored station descriptor and both station identity/class
checks passed before the new work. Cline's additions to docs/ISSUES_LOG.md were
retained. The stash's build.binlog is a local artifact and must not be committed.

Installed technology choices, initial unlocked-slot layout and procedural module
stats are still unfinished. This step alone does not reproduce all equipped stats.

Second-station screenshots (Modeysk Rendezvous, 2026-10-25) add C Iridescent Inertial
Focuser: damage 1362.7, scanner 206.4; and B Aspect of Aktot-XIV: damage 333.3,
scanner 285.4. The address-to-location pipeline matches both names/classes. The C
tool's resolved Rifle class and actual site seed give scanner bonus 3.1785212%,
which reproduces 206.4 without a fitted percentage. The B tool's Pistol scanner
bonus 29.741974% gives base 259.5; a B scanner upgrade is visible, so 285.4 requires
its technology effects. The C tool also has another weapon installed. Neither
equipped damage total nor the B upgraded scanner total is newly validated by these
intrinsic checks. See STATION_EQUIPMENT_LOCATIONS.md for addresses and hashes.

The Mass Needler laser and scanner outputs match the owner's displayed values.
The uncharged laser calculation also matches the C screenshot. The C scanner
209.8 must still be reproduced from its actual site seed/intrinsic scanner
bonus rather than by deriving a percentage backwards from a rounded display.

This does not yet validate all 21 weapon-mode formulas, arbitrary combinations
of technologies, procedural upgrades, adjacency layouts, active-special-tech
bonuses, other tool families, or different game versions. A production
evaluator must read its extractable inputs from the player's catalog and retain
these distinctions. It must not label intrinsic percentages as equipped stats
or insert the reference display numbers as constants.

Site generation of initial unlocked slots, installed technologies and
supercharged positions is separate work. Correct equipped formulas alone do
not establish which item or inventory belongs at a location.
