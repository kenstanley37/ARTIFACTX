# Planetary Multi-Tool Locations

## Scope and current status (2026-10-27)

Branch: `feature/planet-multi-tool-locations`, separate from the accepted station
work. First target: ordinary minor-settlement/shop cabinets. No new planetary
stock or coordinates are shown in the app by this work. The station results do
not prove planetary building placement, orientation or availability.

The existing site hash needs the actual interaction entity's world origin,
rounded by the native rule. A chart/scan marker, building centre or latitude and
longitude does not establish that origin. Do not hash one of those substitutes
and present the resulting inventory as an actual cabinet.

## Installed asset survey

`PLANETBUILDINGTABLE.MBIN` is decoded as `GcBuildingDefinitionTable`. Its
`BuildingFiles` array is indexed by the vendored race enum; each row's building
files are indexed by `GcBuildingClassification`. Shop roots are L-systems with
separate Normal and AbandonedSystem entries. The inspector uses these enums,
not hand-maintained index/name mappings.

The tested install resolves the ordinary shop roots through SHOPTRADER,
SHOPWARRIOR and SHOPSCIENTIFIC L-system files. The rules refer to layout models,
locator types and weighted target models; a flat scene walk alone is insufficient
to generate a whole shop.

The new `PlanetShopWeaponSurveyService` in GameData follows the rules' referenced
models and embedded scene references to actual `WeaponTerminal` components. It
retains source rule/model/locator, entry probability and restriction count,
attachment path, reseeding flag and interaction-specific data. This is an asset
survey, not execution of global restrictions, rule selection or building generation.

Results from the current install:

| Race enum | Normal rule references | Abandoned-system rule references |
| --- | ---: | ---: |
| Traders | 4 | 4 |
| Warriors | 2 | 1 |
| Explorers | 7 | 7 |
| Total | 13 | 12 |

All 25 references resolve to the shared ordinary CRATE_WEAPON interaction entity,
with no unresolved paths. That interaction has no specific-data override and
`ReseedAfterRewardSuccess` is false. These are **25 rule references, not 25
physical cabinets**. Some layouts embed the crate through SCENEGRAPH while an
L-system rule also references it. Selection/replacement semantics must be traced
before counting actual instances or deduplicating paths.

For example, the ordinary Warrior `Roof_Shop` rule reaches the crate through
FLOORLAYOUT_NPC at entry-model position approximately (3.974135, 3.734138, -5).
The corresponding Explorer layout entries have different local origins. These
are relative to the referenced entry model, not the generated building or planet.
An entry probability of 100 is metadata at that rule edge, not a guarantee of a
cabinet at every building marker.

Sentinel pillars were also inspected separately: the SentinelHive root exposes
an encounter component and a DroneHive interaction, rather than the ordinary
shop's terminal chain. The Korvax monolith references its own terminal scene.
Their interaction flow, gates and pool selection require independent tracing;
neither is enabled by the ordinary shop survey. Settlement rotational cabinets,
WFC buildings, additional tool families and other special interactions remain
separate research areas.

## Shared implementation and verification

Scene-local scale, X/Y/Z rotation and translation composition now uses one
GameData helper, `SceneTransformMath`, shared with station extraction. The
complete extracted station tables were compared before/after this refactor and
were structurally identical. No catalog format change is needed for this survey.
No catalog, save or user database is edited by the research command.

Run the repeatable inspection against a local install:

```powershell
dotnet run --project src/ArtifactX.Tools/ArtifactX.Tools.DataCataloger -c Release -- planet-shop-survey <output.json> <game-folder>
```

The JSON contains unselected definitions and the source install fingerprint.
Treat unresolved paths as incomplete evidence. Keep the generated JSON local;
do not commit extracted game data.

Synthetic checks (no game files required):

```powershell
dotnet run --project tests/ArtifactX.GameData.Checks -c Release
```

Checks cover nested scale/rotation/translation through a reference chain,
interaction metadata, one decode per asset, missing references, recursive
references, missing building tables and non-weapon rejection. The actual-install
CLI reproduced 25 references with zero unresolved paths (fingerprint
`312B17D65F8E258C`). Existing full station address-to-record references still
match all four named/class/stat cases; the 100 pirate descriptor comparisons
and composed runtime-damage checks also pass after the shared transform change.

## Saved marker inputs inspected

The vendored `GcScanEventSave` definition has BuildingLocation (Vector3f) and
BuildingSeed (GcSeed), plus building class and galactic address. It has no saved
orientation field. Read-only inspection of the current Slot 8 scan-event array
shows valid nonzero seed-shaped values for Portal/GraveInCave markers and invalid
zero seed values for several non-building mission events. There is no active
Shop marker in that snapshot. The apparent seed key and its exact native meaning
still need serializer/reference verification before a new public save reader is
introduced. Native scan-event deserialization at 0x1420e4e4a supplies struct+0x30
to the BuildingSeed reader, and 0x1420e4ec2 supplies struct+0x00 for
BuildingLocation, agreeing with the vendored layout. Several other native
classes use the same field names at different offsets; string references alone
must not be treated as identification of the scan-event structure. A saved
building seed must not be relabelled as a cabinet inventory seed; they are
separate inputs until proved otherwise.

## Next native work

1. Trace the Shop resource/L-system seed and choice sequence from planet building
   data. Establish how global restrictions, templates, locator rules and embedded
   SCENEGRAPH references combine or replace each other.
2. Establish the building spawn slot, cluster layout and actual building origin,
   terrain adjustment and orientation. Distinguish the scan-event marker origin
   from a fully generated building pose.
3. Compose the selected layout and cabinet transforms and match the rounded world
   hash against native execution or a read-only real cabinet reference.
4. Only then reuse the verified item selection/inventory/stat stages with an
   explicitly established reload context. Keep ordinary, special-pool and
   time-dependent interactions distinct.
5. Add a Planet Details UserControl only for proven located records, with the
   same details/cards/technology grids used by station tools where appropriate.
   Full planet-wide enumeration remains unsupported until placement is verified.

## Vy'keen shop rule tree and two real cabinets (2026-11-05)

`SHOPWARRIOR.LSYSTEM.MBIN` (build 184271): template `Base` -> `PARTS/WARRIORPARTS/LSYSTEM/BASEWARRIOR.LSYSTEM.MBIN`;
base rule `GroundFloor` (GROUNDFLOORMODULE_BASE) fills its `ROOF_` locators with ROOFMODULE_SHOP (100) and its
`WALLSHORT_` locators with WALLSHORTMODULE_TECHSHOP (100); `Roof_Shop` puts FLOORLAYOUT_NPC at `LAYOUT_`, and
`Layout_NPC` puts CRATE_WEAPON at `WEAPONSCRATE_` (3.974, 3.734, -5 from the roof module). Eight global
restrictions cap counts (nShopRoof, nRooms, nTerminals, nExteriorDoors, nLandingPads, Lab, nWeaponTechStation,
nTechShop), so which ROOF_ locator gets the shop roof - and the base layout from the template - decide the cabinet.

Two real cabinets on Kaim 30/19 (see MULTITOOL_GENERATION.md "Planet shop cabinets reproduced") give exact world
points to fit: 17.9 m / 1.8 m and 29.4 m / 4.1 m (sideways / up) from Core's ground point under each Shop site.
Next: extract BASEWARRIOR and its module scenes, list every ROOF_ locator transform the base can produce, and find
the building facing and locator choice that land the crate on both points; then trace the native choice.

`BASEWARRIOR.LSYSTEM.MBIN` (template of the shop, itself templating `COMMONPARTS/LSYSTEM/BASECOMMON`): 28 rules, a
recursive module grammar. A ground-floor module's WALLLONG_/WALLSHORT_ locators take caps (with terminals, generators,
silos...) or corridors; corridors lead (Long_CorridorIn / Short_CorridorIn) to further GROUNDFLOORMODULE_BASE rooms, so
one shop can have several rooms; each room's ROOF_ locator gets a roof module (ROOFMODULE_BASE with themed layouts,
or ROOFMODULE_SHOP once, nShopRoof). Probabilities use MergeProbabilityOptions Balance and per-entry NoMoreThan
restrictions. The cabinet's place is therefore the product of a seeded expansion of this grammar plus the building's
facing - a port of the native L-system expander (and its RNG and restriction rules) is needed, not a fixed offset.

### Native L-system manager (build 184271, 2026-11-05)

RTTI names `cTkLSystemManager::GenerateStep(TkHandle, const cTkSeed&)`, `GenerateLSystemSampleByID(...,
const cTkMatrix34&)` and `GetLocatorNode`; its code sits around 0x142D70000-0x142D90000 (401 .pdata starts, many
chained fragments). Strings anchor: 0x142D75B60 node/registration ("cTkLSystemGenerationNode", "Generated LSystem",
"Game/Managers/L-Systems Manager"); 0x142D768B0 restriction checks ("Chain restriction"); 0x142D7A690 `_IN_`/`_OUT_`
locator matching; 0x142D7A010 variants ("LSystemVariant-%d"); 0x142D79260 / 0x142D87640 "INTERIOR_"; 0x142D83800 and
0x142D8459D the ImGui debug window (stored L-systems, per-sample size/seed/bounding box, "GENERATE SAMPLE n" sets
+0x118/+0x120 on the manager; rule view prints "- Locator: %s", "Probability", "%s, No More Than: %d"). The large
fragments 0x142D7E3C0 / 0x142D7E810 / 0x142D7F230 walk the manager's node list (`DAT_146EFD7E0`+0x60) and are the
likely expansion. A no-analysis Ghidra import mis-bounds them; full analysis is needed before porting.

### GenerateStep found (2026-11-05, full Ghidra analysis of build 184271)

`cTkLSystemManager::GenerateStep(TkHandle, const cTkSeed&)` is **0x142D79260** (body continues at 0x142D792B8):
the `std::function` built there has vtable 0x1435E0160, the `_Func_impl_no_alloc` of GenerateStep's lambda_1
(TypeDescriptor 0x14520B860). It returns early once the generation queue index (+0x238) reaches its count (+0x21C).
For the queued node it walks the node's locators (+0x1AC count, +0x1B0 array); for each locator it resolves the
locator name (`_strupr`'d, compared with `"INTERIOR_"`), finds the matching rule (`FUN_142D851D0`), and picks the
entry with ONE draw of the usual MWC from the step's `cTkSeed` (lo, hi, valid flag):
`carry = rotl16(lo) ^ hi ^ lo` (0 when the seed is invalid, lo then 1), `state = carry + (lo ? lo : 1) * 0x5A76F899`,
`index = (entryCount(+0x10C) * (uint)state) >> 32` into the rule's entry list (+0x110). So the entry list is built
already weighted (MakeRule / MakeLocatorEntry: next to trace), and the seed passed per step decides every locator of
that node. `INTERIOR_` locators take a separate attach path. Next: MakeRule's list building (probabilities, Balance
merge, NoMoreThan / global restrictions), how each child node's seed is derived, and the building root seed and pose
from the planet building site.

Correction and the real pick (same day): the GenerateStep draw above picks a model **variant** of the already
chosen entry (`entry+0x110` variant handles, count `+0x10C`; MakeRule 0x142D7DB00 builds them, variant i seeded
from `base + 100*i` through two MWC steps and a 64-bit mix `*0x64DD81482CBD31D7`, `*0xE36AA5C613612997`).
The entry itself is chosen by **0x142D851D0** `(manager, innerRule, node)`:

- per entry (stride 0x138, model name at +0, Probability float at +0x120, restrictions at +0x130/count +0x12C as
  {type, amount}: type 0 = maximum ("No More Than"), types 1 and 2 add to a minimum), and per matching global
  restriction (manager +0x150, count +0x14C, stride 0x198, matched by model name; same types);
- local count = `FUN_142D7A980(manager, node, name)`, global count = manager map +0x178 by name (value +0x104);
- below a local or global minimum -> that entry is returned at once (first such entry in file order);
- below both maxima -> a candidate with its Probability; otherwise excluded and its Probability pooled;
- if any candidate: one MWC step on the **manager's running state** (+0x23C lo, +0x240 hi:
  `s = lo * 0x5A76F899 + hi`), `r = (uint)s * 2^-32 * 100`; walk candidates in file order with
  `cum += pooled / n + p_i` and return the first with `r < cum`; none -> -1 (locator left empty).

Constants checked in the exe: 0x144BF46C8 = 2^-32 (double), 0x144BF61AC = 100.0. Still to trace: where the
manager state (+0x23C/+0x240) and each GenerateStep seed come from for a planet building (the site key?), the
queue order (+0x220 queue, +0x238 index), locator transforms, and the building's pose on the site.

Generation start **0x142D82630** `(manager, parentNode, const cTkSeed* seed, const cTkMatrix34* pose)`: clears the
queue (+0x218/+0x220) and the count maps (+0x178, +0x1C8), sets the manager state from the seed
(`+0x23C = lo ? lo : 1`, `+0x240 = rotl16(lo) ^ hi ^ lo`; invalid seed: 1 and 0), picks the root rule's (+0x130)
model variant with one MWC draw `(variantCount(+0x18C) * (uint)(carry + lo * 0x5A76F899)) >> 32` from list +0x190,
creates the root node at `pose` and queues it. Every caller (0x142D7A010, 0x142D7C3C0, 0x142D86890 ...) passes the
same `manager+0x248` seed to the start and to each GenerateStep, so **one seed per building** drives it: entry picks
consume the manager's running MWC in queue order, variant picks re-draw from the building seed. Open: who sets
+0x248 for a planet building (the Shop site key is the first candidate), FUN_142D7A980's counting scope, the
child-node creation (FUN_14187F220: model variant + locator transform), the `INTERIOR_` path, and the site pose.

### Buildings use three pre-built samples per L-system (2026-11-05)

The manager update (0x142D86890) and 0x142D7A010 / 0x142D7C3C0 are debug paths (the ImGui window's requests,
32 preview variants, "TempBuilding"). Gameplay places buildings with
**`GenerateLSystemSampleByID(handle, lsystem, n, const cTkMatrix34& pose)` = 0x142D78B20**: it copies the stored
sample `lsystem * 15 + n % 3` (stored list +0x170, stride 0x2D0 per L-system, 0x30 per sample: node transforms
+0x188 (0x40 each), node count +0x194, model handles +0x198) and instantiates it at `pose`. Its only outside caller
is 0x1412B9BA0 (callers 0x1412BAA70, 0x1412B7E90). The three samples are generated once per stored L-system with
seeds `stored+0x100 + 0 / 100 / 200` (the debug window's "Sample 1/2/3 seed"). Registration is
**0x142D70960** `(manager, name, const cTkSeed* seed, LoadType, colour, ...)`, storing `*seed`; the planet building
loader **0x14163C300** passes a seed drawn from the planet-side running MWC `param_5`: two MWC steps
(`s = lo * 0x5A76F899 + hi` twice) then `x = ((s_lo >> 1) ^ (s << 32 | prev_lo)) * 0x64DD81482CBD31D7;
x = (x >> 33 ^ x) * 0xE36AA5C613612997; seed = x >> 33 ^ x` (valid). Abandoned systems use the "ABANDONED" colour
palette. 0x14163C300 is called from **0x14163B270** (the planet's building-table setup, per race x class, with
param_4 the planet RNG; it also draws two `rand * 10000 + 1` values into the planet data +0x33DC / +0x33E0 first).
So each planet has three layouts per shop L-system, decided by the planet RNG and registration order, and each
building picks one with `n`. Next: the seed of 0x14163B270's RNG and its draw order, and how 0x1412B9BA0 chooses `n`
and the pose from the building site.

Per building class the planet setup 0x14163B270 makes `GcBuildingDefinitionData.NumModelsToGenerate` variant
records (planet data +0x3398, stride 0xE0): for each index `i` it calls the setter **0x14163C1F0** (class byte
+0xD3, sample `n = i` at +0xAC, density share +0xA8, flatten/heights/override radius from the definition) and then
0x14163C300, which registers that record's own L-system with the next seed from the planet RNG (+0xB0 = handle).
Build 184271 table: **Shop 3 models**, Shelter/Abandoned/RadioTower/Observatory/Factory/Harvester/Ruins/Monolith 3,
Terminal/Outpost/Beacon/Depot/DistressSignal 1, Settlement_Small/SmallIndustrial/Large/Farm/Factory 10. So a planet
has exactly three shop layouts (three seeds, samples 0/1/2) and every shop site uses one of them at its own pose.
Next: 0x14163B270's RNG (`param_4`) seed and its draws before each class, how a site picks its variant record, and
the site pose (rotation about the ground normal).

Planet RNG for buildings: 0x14163B270 <- 0x141638D00 <- the planet generator **0x1416C5A40** (and 0x1416C7DB0),
which keeps one MWC at generator+0xBA4/+0xBA8 and re-seeds it before each stage from one of 26 stage seeds derived
from the planet seed (`param_3+0x20`, a cTkSeed). Derivation (13 rounds, two seeds each, from
`lo = seed.lo ? seed.lo : 1, hi = rotl16(lo) ^ seed.hi ^ lo`, M = 0x5A76F899, C1 = 0x64DD81482CBD31D7,
C2 = 0xE36AA5C613612997, `mix(a, b) = { x = ((uint)b >> 1 ^ (b << 32 | a)) * C1; x = (x >> 33 ^ x) * C2;
return x >> 33 ^ x; }`):

    s1 = lo*M + hi;  a = (uint)s1;  s2 = a*M + (s1 >> 32);  seedEven = mix(a, s2)
    s3 = (uint)s2*M + (s2 >> 32);  b = (uint)s3;  s4 = b*M + (s3 >> 32);  z = mix(b, s4)
    seedOdd = (z & 0xFFFFFFFF00000000) | (((z >> 32) >> 1) ^ (uint)z);  lo = (uint)s4; hi = s4 >> 32

Stage seeds sit at local_248 - 16k; the building stage re-seeds from **local_218 = stage seed 3** (round 1's odd
seed) just before 0x141638D00, so the planet building setup's RNG is `MWC.Start(stage seed 3)`. Inside
0x14163B270, two draws go to planet data +0x33DC / +0x33E0 (`(uint)s * 10000 >> 32` + 1), then every variant
record's 0x14163C300 call takes two MWC steps (before checking for an L-system path), in the setup's loop order.
Next: read 0x14163B270's loops (race x class order, gating FUN_14167F2F0 / FUN_1416445F0 and any other draws) to
get the shop records' registration indices, then check the three seeds against the two Kaim 30/19 cabinets.

Record counts in 0x14163B270 (class order 0..62): records(c) = overrides(c) + (allowed(c) ? NumModelsToGenerate(c) : 0),
where overrides = NumOverridesToGenerateWaterworlds when >= 0 on biome 14 else NumOverridesToGenerate, counted only
when `EnabledWhenPlanetHasNoNPCs || planet has NPCs` and (the +0x31D8 planet value > 0 and not one of two sentinel
values, or FlattenType != 3); **allowed** (0x1416445F0) = Density[planet density level] > 0 (same EnabledWhen rule) or
the class is flagged by a cluster layout, NumModelsToGenerate > 0 and the planet is not type 7 - or the class is
Shelter/Waypoint/LandingPad (mask 0x200084); a class whose third building-table path (WFC) is set gets none.
0x14167F2F0 gates a class by its PlanetRestrictions bytes (+0x94..+0x97) against planet flags. Every record
consumes two MWC steps in 0x14163C300. So on a normal planet the shop records follow Shelter (1 override + 3),
Abandoned (3 if allowed) and Terminal (1 if allowed): the first shop seed is draw pair 2 + R with
R in {4, 5, 7, 8} - few enough to brute-force against the measured cabinets.

### Site to building: record and facing (2026-11-05)

A regular grid site goes through the cluster generator 0x1412B6BA0: FUN_142DEC840 finds the cell (site key
`local_940`, position, up), an MWC is started from the key (`MWC.Start(key)`), and **0x141635570** places it:

- yaw: `index = (uint32)(rotl16(lo) ^ hi ^ lo + (lo ? lo : 1) * 0x5A76F899) >> 29` - the first MWC output of the
  key - and `angle = index * 45` (0x144BF6138 = 45.0), so **eight facings**, turned about the site's up vector
  (sin/cos FUN_140201620 / FUN_140201540 of that angle, Rodrigues form);
- draw 1 (from the key's MWC state as saved by 0x1412B6BA0): slot = `(int)(u * 2^-32 * 999)` (0x144BF62F8 = 999.0)
  into the 1000-slot table (planet data +0x33A8: record index, chance, enabled), draw 2 the chance test - Core's
  `PlanetBuildingSites.ClassAt` already reproduces this;
- the **main building takes the slot's record**, which 0x14163B270 wrote as the class's first record for all ten of
  its slots - so every main Shop on a planet uses the first shop record (its L-system, sample 0) at its own facing;
- cluster members (the class's cluster layout, e.g. Shop -> SHELTER_WAYPOINT) each pick a random record of their
  class (`first + (u * (count + 1) >> 32)`) and get their own derived seeds (Shelter, Waypoint and LandingPad from a
  separate stream), placed by FUN_1416360A0.

If that holds, the two Kaim 30/19 cabinets are the same crate of the same layout, so they must sit at the same
distance from their buildings' origins; the measured 17.9 m vs 29.4 m from Core's ground point says either the origin
is not that ground point (cluster offset, terrain snap) or a record other than the first is used. Next: port the
L-system with the shop scenes' locator transforms and test against both cabinets with the eight facings.
