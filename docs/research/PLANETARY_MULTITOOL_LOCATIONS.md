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

### The cabinet is fixed in the shop's frame; the building origin is not the site point (2026-11-05)

Vy'keen scenes (build 184271): GROUNDFLOORMODULE_BASE has one `Roof_Out_0` at (0, 7.742, 0) (plus WallShort_Out_N/S
at (0, 2.217, -/+6.756), WallLong_Out_E/W at (+/-3.974, 2.216, 0)); ROOFMODULE_SHOP's `Roof_In_` is at its origin and
its `Layout_` at (0, -7.742, 0); FLOORLAYOUT_NPC's `WEAPONSCRATE_` reference is at (3.974, 3.734, -5.000), RotY -90.
The shop rule puts ROOFMODULE_SHOP on the root ground floor's only roof locator, so **the cabinet is always at
(3.974, 3.734, -5.000) in the building's root frame**, whatever the layout seed does elsewhere - 6.4 m from the origin.

Building frame (0x141635570 with basis 0x140576380): coarse axes from the up vector's dominant component
(y>0: A=+Y, B=+Z; y<=0: A=-Y, B=-X; x>0: A=+X, B=+Y; x<=0: A=-X, B=+Z; z>0: A=+Z, B=+X; z<=0: A=-Z, B=+Y);
`X = normalize(A x B)`, `Z = X x A`; forward = `normalize((n x Z) x n)` turned about n by `index * 45` degrees.
Solving each measured cabinet back to an origin with that frame puts the origin at ground level (273.3 / 250.5 m
above the base radius vs Core's ground 275.2 / 250.1) but 14-36 m sideways from Core's site point, for every sign
convention tried - so the main building is moved off the cell point. The shop's cluster layout SHELTER_WAYPOIN
(Waypoint 1, Shelter 1, AlignmentJitter 10, AlignmentSteps 4) and its arranger FUN_1416360A0, which receives the main
building's position and forward by pointer, are the next thing to read.

The arranger **0x1416360A0** (8706 bytes) runs when a cluster has members (shop + waypoint + shelter): settlement
classes 36/57 snap the main position to a grid; other classes build per-member bounding boxes from each record's
model bounds (record +0xC8/+0xCC model handles, +0xA4 margin, up to 20 boxes), lay the members out around the cell
(the layout's AlignmentJitter / AlignmentSteps), separate overlaps and write the main building's final position and
forward back through `param_8` / `param_9`. Porting it (with the bounding boxes of Shop/Shelter/Waypoint models) is
the remaining step for the main shop's origin; everything inside the shop is already fixed for Vy'keen shops.
Trader and Explorer shops reference the crate from several layouts (4 and 7 rule references), so for them the
L-system expansion does matter.

### The arranger does not move the main building; the offset is systematic (2026-11-05)

In 0x1416360A0 only cluster members (index >= 1) are placed and moved: on a spiral around the main building
(`sqrt(n * k)` radius, a random angle per member, two or three MWC draws each), with up to 16 overlap passes using
FUN_141638B70 on the members' boxes (record +0xC0 radius plus +0xA4 margin). Index 0, the main building, keeps the
cell point FUN_1412B6BA0 passes in. So the shop's origin is the game's cell point - yet the three measured cabinets
sit 17.9, 29.4 and 31.2 m sideways from Core's Shop site points (Kaim 30/19, local frame at each site: north +17.7 /
+22.0 / +30.3 m, east +3.0 / -19.4 / -7.7 m), where the cabinet is only 6.4 m from its building's origin at a random
facing. The consistent northward part points at Core's site position, not the layout: either Core's Voronoi feature
point differs from FUN_142DEC840 / FUN_1419C2CB0 for these cells, or the cell lookup FUN_1412B6BA0 makes for a site
(query point, divisions param_9[7], radius scale param_9[8] * 0x144BF56F8) is not the one Core reproduces. Crashed-ship
checks only had the visor's 0.01 degree precision (~20 m here) plus one beacon test, so a shift this size was never
excluded. Next: trace FUN_1419C2CB0 against PlanetBuildingSites.Site for one of these keys.

### Core's cell maths is exact; the cluster is re-centred after the arranger (2026-11-05, build 184271)

**Cell lookup checked.** `scripts/research/region-generator/voronoi184271.py` runs the game's wrapper 0x142DEC840 /
worker 0x1419C2CB0 in Unicorn on the three Kaim 30/19 shops (radius 130048, 546 divisions, sector seed 3733: Core's
inputs, equal to the live building block). Same site key and point A as `PlanetBuildingSites.Site` on all three, within
0.7 cm, querying from the shop or from the cabinet. 0x1412B6BA0 passes exactly these inputs (sector `+0x48`, radius
`+0x38`, divisions `+0x40` x 0.5), and 0x141635570 copies point A into slot 0 unchanged. So the offset is not in Core.

**The missing step.** After 0x141635570 / the arranger return, 0x1412B6BA0 moves the **whole cluster, main building
included**, by minus the radius-weighted mean of the members' offsets from point A:
`shift = -sum(r_i * (p_i - A)) / sum(r_i)` over every member with a record (main term 0), `r_i` = the building record's
`+0xC0` radius. (The earlier "index 0 keeps the cell point" holds inside the arranger only.) The site record in memory
(stride 0x530, `scripts/diagnostics/read_site_clusters.py`) keeps the positions from **before** the shift: `+0x10` point
A, then the members; `+0x2A0` the main forward, then each member's direction from the main building.

Live check (read-only, game on Kaim 30/19). Radii from the live type records: Shop 50.412, Waypoint 10.0, Shelter
21.379 / 23.329 / 21.255 / 21.379, LandingPad 13.599. Shop 1 has one member (the Waypoint, 60.4 m south); predicted
shift `10 / 60.412 = 0.1655` of it, (0.59, 9.87, 1.50) m. The spawned shop's root node (forward equal to the record's
main forward, 1.40 m below Core's terrain) sits at horizontal (0.60, 9.89, 1.50) from A, and the Waypoint's parts at
50.5 m instead of 60.4 m. Shops 2 and 3 (Waypoint + Shelter) shift 25.5 m. Their nodes were not loaded, so they are
checked only through the cabinets below.

**Facing: the angle is in radians.** The main forward is `rotate(f0, n, index * 45)` with f0 the projected axis from
the 0x140576380 basis (+Y on face +X), index = top 3 bits of the key's first MWC word, and 45 passed to the game's
sin/cos **as radians** (270 rad = -10.15 deg, 90 rad = 116.62 deg). It reproduces all three stored forwards to 4
decimals. The earlier pose fits treated it as degrees.

**Crate in the building frame.** With the shift and that facing, the Vy'keen crate lies at the same horizontal spot
in all three shops despite three different facings: (n x F, F) = (-2.68, 8.32), (-2.82, 8.28), (-2.60, 8.04) m
(cabinet points are whole metres, so +-0.5 per axis; best with Shelter radius 21.379 or 21.255). That is 8.7 m from the
root, not the root room's (3.974, -5.000), so the shop roof is not on the root room in this planet's shop layout. All
main shops share the layout (first record, sample 0), so one L-system expansion gives every cabinet on the planet.
Height is still open: taking the crate as 4.15 m above the root (as measured on shop 1), the roots sit -1.40, +0.15 and
+1.83 m from Core's height at the shifted point. No ring min/mean/max (8-64 m) explains it (spreads of 3 m or more).
The game re-grounds loaded buildings with the full voxel terrain (see PLANET_BUILDING_PLACEMENT.md, visited planets).

Next, in order: (1) port the arranger's member placement (0x1416360A0: spiral radius, angle draws, overlap passes on
record radii + `+0xA4` margins, chance-gated members as in 0x141635570) and check it against the pre-shift member points
in the live records; (2) the building's vertical placement rule (0x1412B9BA0 and its callers, LowerIntoGround,
flattening); (3) the L-system expansion for the shop roof's room (0x142D82630 / 0x142D851D0 above), checked against
(-2.7, 8.2). With all three, the cabinet's rounded world point and so its exact tool follow from the address.

### Cluster members ported: choice and placement exact (2026-11-05, build 184271)

`scripts/research/region-generator/clusterport.py`, checked against the live pre-shift records of the three Kaim
shops: **members chosen 3/3 from the site key, positions 5/5 within 3 mm**, main-building shift as measured.

- **Choice** (0x141635570, layout `SHELTER_WAYPOIN` = Waypoint 1-1 p 1, Shelter 1-1 p 1). After the slot and chance
  draws the site MWC (`*param_2`) continues; a side copy of it (not written back) gives one word that gates every
  Shelter entry: `word * 2^-32 < GcBuildingGlobals.ChanceOfAddingShelter` (+0xB94, 0.75; globals at 0x14535B140 in
  this build). Other entries with Probability < 1 take a draw from the main stream instead. Then per entry
  `count = Next(Max - Min + 1) + Min` and per member `record = first + ((u * (sameClassCount + 1)) >> 32)` over the
  planet's type list. Kaim: shop 1 rolled 0.959 (no shelter), shop 2 0.300 -> shelter record 2, shop 3 0.467 -> 3.
- **Placement** (0x1416360A0, first loop, up to three members, **no random draws**): `D = r0 + a0 + ri + ai`,
  `t = clamp((ri + ai) / D, -1, 1)`, the angle advances by `asin(t)` (inlined acos polynomial, in degrees) before the
  member and again after it (0x14163720C); member = `A + D * normalize(cos(th) T1 + sin(th) T2)` on the tangent plane,
  `T1 = n x v`, `T2 = n x T1`, `v` from the start-up face tables 0x146F82840 / 0x145A019F0 / 0x145A01AB0 (read live):
  dominant axis X or Y -> (0, 0, 1), Z -> (0, 1, 0). Only face +X is checked. Members beyond three use random draws
  and the overlap passes (not needed for shops).
- **Radii.** Record `+0xC0` = the table's OverrideRadius when > 0, else `max(0.5 * XZ diagonal of the record's bounds,
  GcBuildingGlobals.MinRadius)` (0x14163C960 at 0x14163CD39; MinRadius +0xC8C = 10, the Waypoint's value). The bounds
  come from the record's generated L-system sample (0x1418809F0), so the Shop's 50.412 and the Shelters' 21.255 /
  21.379 / 23.329 need the L-system port. It is the same port the crate's room needs, so both now wait on it.
  Neighbouring global `BuildingYOffset` (+0xB90) = 3.0 is a lead for the open height rule.

Remaining for an exact cabinet: (1) L-system expansion of the planet's first shop record and its sample bounds (gives the
radii, hence the shift, and the crate's local point); (2) the vertical rule; (3) the building basis on other cube faces.

### L-system expander ported: every Vy'keen building layout and radius from the address (2026-11-06, build 184271)

`scripts/research/region-generator/lsysport.py` (expander + sample bounds), checked by `lsyscheck.py` against the
L-system manager's stored samples read live on Kaim 30/19 (`scripts/diagnostics/read_lsystem_samples.py`, read-only;
data from the `LsysDump` tool): **24/24 layouts** (all 8 stored Vy'keen L-systems x 3 samples, 1,056 nodes in order,
positions 0.0000 m, rotations 2e-6) and **22/24 radii exact**.

- **Seeds.** The manager's stored list is static at 0x1472E8AF0 (+0x170, 0x2D0 per L-system; layout in the reader's
  header). The registration seed of every path is the mixed seed of its first building record (stage seed 3 MWC, two
  Voronoi draws, then two steps per record, as traced above): Shelter pair 0, Abandoned 4, Shop 8, RadioTower 14,
  Observatory 17, Factory 21, Harvester 24, UnderwaterAbandoned 59 - 8/8. Samples use seed lo + 0 / 100 / 200.
- **Rule merge.** Templates load first, the file on top; rules match by model (name kept from the first); Balance
  appends new models to an inner rule, Prioritize puts them first, Replace removes the inner rule and appends it at the
  end; the first BaseRule is the root (read off the manager's live merged table +0x140 / globals +0x150).
- **Chain restrictions.** Every AtLeast global adds AtLeast 1 to its only producer reachable from the root, then to
  that producer's producer, up to the root - appended even when the model already has a minimum. That is why the
  shelter always builds two shacks: WEAPONTECHSTATION AtLeast 1 -> WALLPROPLINK -> SHACK gets a second AtLeast 1.
- **GenerateStep.** Breadth-first queue. Per inner rule, the first active locator that is a direct child of the node
  (or of its `Interior_` locator) and whose upper-cased name contains the LocatorType (filter 0x142D703B0) gets one pick
  (0x142D851D0: first entry below a local or global minimum, else one draw `u * 100` on the manager's MWC over the
  candidates weighted `p + pooled / n`; float32) and is switched off. The child goes on the locator; if it has a
  matching locator of the opposite `_IN_`/`_OUT_` type (0x142D7A690; any type for plain names) it is aligned so that
  locator lands on the parent's (`parent @ L @ inverse(C)`, no flip) and that locator is switched off (0x142D7AE60).
- **Bounds and radius.** Each node model's meshes (scene MESH `AABBMIN/MAX`, mesh-local) are flattened into the model
  frame with references included; that box's corners go through the node; Euler order R = Rx Ry Rz. Radius = max(half
  the XZ diagonal, MinRadius 10). The stored sample is re-centred on its XZ bounds (live root node at minus the centre).
  Bounding hulls and per-reference boxes were tried and are worse. Only the shelter's records 0/2 miss, by 5 mm
  (21.383 / 21.260 vs 21.379 / 21.255; a cable mesh, rotated -77 deg about Z inside a -120 deg locator).

**The shop.** Kaim's first shop record: seed `C9BC0BFCE72F16EF`, root room with CAPGROUND / CORRIDOR (long walls),
INTERIOR / TECHSHOP (short), ROOFMODULE_SHOP (forced minima), radius **50.412 m** on all three samples (the XZ bounds
are the fixed parts: landing pad door and corridor). Root offset (-6.348, 0, +13.389), so the crate, FLOORLAYOUT_NPC's
`WEAPONSCRATE_` reference at (3.974, 3.734, -5.000) in the root room, sits at **side -2.374, forward +8.389** in the
building frame (side = n x F). `shopcrate.py` runs the whole chain from the planet seed: shop 1 shifts **10.000 m**
(measured 10.02) and the three predicted crates miss the cabinets by **0.31, 0.57, 0.37 m** horizontally (cabinet
points are whole metres). Caveat: with only the Waypoint as member, the shift equals the Waypoint's radius whatever
the shop radius is, so shop 1 checks the Waypoint; shops 2 and 3 (Waypoint + Shelter) are the ones that depend on the
shop and shelter radii, and they agree through the crate.

**Height: BuildingYOffset does not explain it.** Root heights needed (cabinet height above the base point minus the
crate's 3.734) are 273.26 / 250.46 / 266.81 m; Core's `PlanetTerrain.Height` at the shifted building points gives
274.24 / 249.90 / 264.55 (offsets -0.98, +0.56, +2.26). No constant (the 3.0 global included) fits a 3.2 m spread, and
neither does min / mean / max of a 16-point ring at any radius 0-60 m plus a constant (best spread 2.45 m, beyond the
~1 m whole-metre slack). The remaining height is most likely the game's full voxel terrain, which buildings re-ground
on, not the heightfield Core reproduces.

Remaining for an exact cabinet: the vertical rule (needs the voxel surface or the grounding call traced), other races'
shops (Trader/Explorer layouts reference the crate several times: now just a run of `lsysport` with their L-system), the
building basis on cube faces other than +X, and a Core (C#) port of the expander and the scene data in the catalog.

### Where the building height comes from (2026-11-06, build 184271, static)

- The spawn record defaults (constructor 0x1422ACBA0): `MaxXZRotation` 0, `AlignToNormal` false, `LowerIntoGround`
  true; the setter 0x14163C1F0 copies only the table fields. So buildings never tilt; the crate's height spread is not
  a tilt.
- `cGcRegionDecoratorBuildings::PollToUpgradeLandmarks` (0x1412BAA70, named by its own debug string) grounds each loaded
  building with **physics ray casts**: one along the up vector through the pose (+-`DAT_14533d4f4`), and if that
  misses, five (the corners of the record's box and the centre); the building moves to the hit nearest its pose. The
  height is therefore the collision surface of the generated voxel terrain, not a formula on the site.
- Every building class has `FlattenType = Flatten` (PLANETBUILDINGTABLE, build 184271), and the voxel generator takes
  `TkNoiseFlattenPoint`s (radius, density, turbulence; no target height). So the ray lands on terrain flattened
  around the building, to a level the voxel generator computes. Neither Core's `Height` nor `BaseHeight` at the site
  or the shifted point gives the needed roots (273.26 / 250.46 / 266.81 m vs H(P) 274.24 / 249.90 / 264.55,
  B(P) 246.95 / 221.65 / 238.03).

Next for the height: trace the voxel generator's use of the flatten points (`FlattenTerrainPoints`,
`TkVoxelGeneratorRegionData`) to get the flatten target and blend; if it depends on the voxel density beyond the
noise layers Core has, the vertical position stays out of reach offline.

### The voxel flatten under buildings (2026-11-06, build 184271, static trace)

The terrain a building's ray lands on is made in the region's 2D column pass, not by `TkNoiseFlattenPoint`s:
`0x1412B3770` (region voxel driver) -> `0x14128DB30` (column table: all 8 noise layers, features, caves per column) ->
`0x14128F0A0` (per column: runs the cluster generator `0x1412B6BA0` itself to find the buildings whose cluster covers the
column, then writes the column's pad entry `+0x390`, 16 bytes: height, distance, cluster extent, column radius) ->
`0x1412B1D60` / `0x1412B13F0` (voxel densities: shapes the surface round the pad with TkVoxelGeneratorData
`BuildingSmoothingHeight` +0x1124 / `BuildingSmoothingRadius` +0x1128 and writes `BuildingVoxelType` +0x1130).

Pieces decoded in `0x14128F0A0`:
- the column value starts at `NoSeaBaseLevel` and takes layers 0-3 (max / subtract) as Core's `Height` does, then
  adds and removes the layer 6/7 base term, and is clamped to [0, 128] at the end;
- `0x141290180`: at the column point, the base term B (`0x141290840` = Core's `BaseHeight`: layers 6 and 7 at the point
  projected to the planet radius) at the centre and 8 points on a 64 m ring (or the cluster extent when the record's
  +0x4D flag is set): returns the largest |B0 - Bi| / 64 (slope) and the ring maximum;
- non-settlement classes (all but 36-50 and 57-59, `0x1412B9050`) are clamped between `extent * slope` and
  `128 - extent * slope`, kept 5 m above the sea when the class is not a water type; the settlement classes are set
  to the ring maximum (full flatten);
- `0x14128EBB0`: floor = max B over every cluster member's centre and 6 points (60 deg) at half its record radius;
  below it the column goes to floor + 5 (gated by the member check `0x141635F00`: water placement, Min/MaxHeight);
- constants: 64 (`0x144BF6160`), 1/64, 128 (`0x144BF61D8`), +5 (`0x144BF5FF0`), 0.05, ring 0.6 for flagged builds.

Numbers do not close by hand yet: for Kaim's three shops the BaseHeight floor + 5 sits 17.4 / 19.3 / 18.9 m below the
needed roots (no constant), so which branch a shop takes and the units of the column value need the native run.
Next: read Kaim's planet object live (`read_planet_object_live.py`), emulate `0x14128F0A0` for the columns under the
shops, check against grounded building roots read live, then port.

### Building height solved: the cluster is flattened to Core's Height at the site point (2026-11-06, build 184271)

`scripts/research/region-generator/emu_columns.py` runs the game's column pass (0x1412B5F70 + 0x14128DB30 ->
0x14128F0A0) in Unicorn on Kaim's planet object read live, with NMS.exe's .data copied from the running game. Inside a
cluster the surface (pad height + the layer 6/7 base term) is flat and equals **Core's `PlanetTerrain.Height` at the
site's cell point A**: 275.19 / 250.09 / 261.90 for the three surveyed Kaim shops (Core H(A) 275.19 / 250.08 / 261.90).

Checked live, read-only, with the owner standing in a Kaim shop (site key 404DA563E9C5EC88, record 8 = first shop
record): the grounded scene nodes (state 4, node transforms read through the scene manager 0x146EFD7E8: handle table
+0xE8/+0xF0, 0x40-byte matrices +0x60) sit at 272.185 / 272.183 / 272.189 m (shop, waypoint, shelter) vs the emulated
surface 272.189 and Core H(A) 272.190; a Monolith 261.099 vs H(A) 261.104. The instance's node is the sample's root room:
all **23 nodes of the generated layout, crate included, match the live children within 7 mm**, and the root room predicted
from the address alone (site, members [Waypoint 12, Shelter 1], shift, radian facing, height H(A), root offset) lands
**1.2 cm** from the live node with the same forward and up.

So the vertical rule is: building origin = shifted point at the base radius raised to `130048 + Height(A)`. The earlier
"unexplained" height offsets came from two of the three old cabinet points: cabinet 2 is the predicted crate rounded on
every axis, cabinets 1 and 3 are 1.9 m and 4.9 m off vertically (found by a near-player search; likely false matches).

### The cabinet seed uses the building's spawn-time height (2026-11-06, live check in the owner's shop)

The owner's cabinet in shop 404DA563E9C5EC88 offered **Parallel Neutron Reflector, C, 333.3 / 203.7, 4 tech
(SCAN1, SCANBINOC1, LASER, STUN_GREN)** (screenshot, reload body = planet 1). The predicted crate (grounded) rounds
to (-123844, 83305, -137661), which gives the right tool, class and damage but 249.2 scan / 5 tech. The only whole-metre
point within 6 m that reproduces the screenshot exactly is (-123846, 83303, -137661): the crate placed with the
instance's **stored spawn pose** (record +0x00, 2.357 m below the grounded node) instead of the ray-grounded one. So the
cabinet is generated when the building spawns (0x1412B9BA0), before `PollToUpgradeLandmarks` moves it onto the ground.

Spawn-time heights: buildings outside clusters are stored at Core's `Height` at their point (17/17 within 0.01 m); the
three buildings of this shop's cluster at 269.828 / 269.619 / 269.680 while the final surface is 272.19. They come from
`0x141299710`, which bilinearly samples the region's generated voxel height grid (+0x1D0, region resolution +0x84..+0x88)
and then runs the cluster generator. The column pass itself is flat at 272.189 at every LOD tried (0, 1, 3, 4, 5), so the
2.4 m comes from the region grid's coarse sampling of the smoothed building pad (`0x1412B13F0`, smoothing radius 150 m /
height 200 m on Kaim). Next: dump the live region objects around the shop (grid, LOD, dimensions) and emulate
`0x141299710` on them; the old cabinets 1 and 3 (1.9 / 4.9 m) are the same spawn-height effect.

Spawn height, ruled out so far (live, 2026-11-06): the loaded terrain regions are `cGcRegionTerrainNear/Mid/Far/VeryFar`
(vtables 0x144BBEC70 / 0x144BBEAD8 / 0x144BBEE20 / 0x144BBEFB0; cube-space origin at +0x50, scale 1/2/4/8 m, grid bytes
+0x80.., height grid +0x1D0 in local units above Core's `BaseHeight`, i.e. surface = grid * scale + B). At the cluster
they give 272.34 / 272.80 / 274.6 / 277.1 m, never the stored 269.6-269.8; the column pass gives 272.189 at every LOD
and with default record radii. `0x141299710` (region grid sampling + cluster check) is a scatter placer, not the
building spawner. Remaining candidate: the spawner estimates the surface from the shaped density (`0x1412B13F0`,
smoothing radius 150 / height 200) along the vertical with a coarse step. Until then a shop's crate is exact across the
ground and its height is known only to the cluster's offset (here -2.36 m from Core's `Height(A)`).

### Spawn height solved: buildings spawn on the Far region's surface (2026-11-06, live)

The instance creator is **0x1412B7F70** (decorator, calls this build's height function **0x14128E850** and
0x141290180, then, when its region's byte +0x89 > 6, samples the region's height grid +0x1D0 at the building by two
triangles and converts back with 0x141291310). The world -> grid mapping 0x141291DF0 is: gnomonic projection on the
cube face, height = |p| - R - BaseHeight(p) along the face normal, then the region's inverse transform (+0x20, origin
+0x50, scale 1/2/4/8 m for Near/Mid/Far/VeryFar; grid cell = scale * (+0x84) / (+0x86 - +0x88); no border offset).
Sampled this way, the **Far** region's grid (16 m cells) gives the stored spawn heights of the owner's shop cluster:
269.852 / 269.607 / 269.681 vs 269.828 / 269.619 / 269.680 (shop, waypoint, shelter; the cm are bilinear vs the
game's triangles). So cluster buildings - and their cabinets - are made by the Far region's decorator.

`emu_far.py` regenerates a live Far region with the game's code (0x141294A00 -> 0x1412B3770(region, voxel, block,
0x80)) on the planet object read live: with live-backed pages (`LIVE=1`, read-only) its height grid equals the game's
**exactly (max diff 0.0)**; without them (planet object only) the whole shop zone is still exact and only a corner
touched by a larger cluster differs (member choice / arranger reading live heap data: 0x1416353AA, 0x141635558,
0x1416357F9, 0x14163617F, 0x140611B85). Reading `BaseHeight` and the column pass, the inputs are the planet seed, the
voxel settings and the building table - all address-derived.

So the cabinet pipeline from the address is: site -> layout -> cluster -> **Far-region surface at the building** ->
crate (root offset + crate locator) -> whole-metre point -> seed. Porting it to Core needs the Far-LOD voxel region
generation (density from the noise layers incl. grid/feature layers if they reach the surface, building pads and the
`0x1412B13F0` smoothing, and the grid's height extraction) - the emulator is the reference to port against.

### Spawn height from the address: 3/4 cabinets exact (2026-11-06)

The emulated Far region's column table (hooked at 0x1412B1D60) shows its nodes: node (i, j) sits at cube z = origin +
(i - 1) * 16 m and cube y = origin + (j - 1) * 16 m (the driver's border cell; Core's `BaseHeight` there equals the
table to 0.001), and inside the cluster's flat zone the node surface is exactly `Height(A)` (pad + base term, e.g.
20.26 + 251.93 = 272.19). The grid stores surface minus the node's own base term, and the density is linear across the
8 m layers (crossing 0.5 where the grid says), so the extraction 0x1412B3370 adds no error. The 2.4 m comes from the
creator: it reads grid index floor(local / 16 m), which holds the node **one cell back**, interpolates on the plane of
corners (i, j), (i+1, j), (i, j+1) and adds `BaseHeight` at the building. In Core terms:

    spawn = Height(A) + BaseHeight(p) - plane(BaseHeight at nodes (i-1, j-1), (i, j-1), (i-1, j))

`shopspawn.py` (with the `HeightProbe` tool for Core heights) runs it end to end from the planet seed: spawn heights
within 2.6 cm of the live instances, and the shop crates reproduce **3 of 4 owner cabinets exactly** - the live shop,
old cabinet 2 and old cabinet 3 (whose spawn is 4.5 m above H(A)); cabinet 1 lands 1 cm across a rounding boundary
(x -107157.51 vs -107157; the game works in float32, ~0.8 cm at these coordinates). Open: float32-faithful arithmetic
for boundary cases, buildings near the edge of the flat zone (shaped pads), faces other than +X, and the Core port.

Interpolation residual (2026-11-06): on the live Far grid with exact `BaseHeight`, the three members need
6.5129 / 5.4641 / 4.1038 (local units). The plane over (i,j),(i+1,j),(i,j+1) gives +1.9 / -2.3 / +0.5 cm; the
decompiled nesting `lerp(lerp(h00, h01, fz), h10, fx)` gives +0.3 / -40 / -0.1 cm; a two-triangle split (upper triangle
when fx + fz > 1) gives +1.9 / -0.8 / +0.5 cm. No single reading of the decompile fits all three to a millimetre, so the
exact sampling should be emulated (0x1412B7F70 between its 0x141291DF0 and 0x141291310 calls, float32) rather than
re-derived. Until then about one crate in ten lies within 2 cm of a whole-metre boundary on some axis and should be
reported with both candidate points.

Exact sampling from the machine code (0x1412B82B3..0x1412B859F, 2026-11-06): the creator divides the local point by
(cell, cell height, cell, 1) to get the index g, reads corners (i, j), (i, j+1), (i+1, j), builds points P = (index *
cell, value * cell height, index * cell), and interpolates the whole point as lerp(lerp(P00, P01, fz), P10, fx) with
fx, fz = frac(g / cell) - the index is divided by the cell size a second time - before 0x141291310 converts it back;
so `BaseHeight` is added at the lerped, unshifted position, not at the building. With the live Far grid this gives the
members to -1.8 / -3.3 / -0.0 cm; in Core terms (`shopspawn.py`) the live shop spawns at 269.821 vs 269.828, and the
cabinets stay 3/4 exact with cabinet 1 2 cm across its boundary. The last centimetres still need the float32 path.

Grid quantization (2026-11-06): in a flat zone a Far node stores the building density 0.75 * (s - y) * (-1/128) offset
to 0.5 (s = node surface above its BaseHeight, y = layer height, 8 m per layer from the base), quantized per layer as
trunc(((0.5 + d) / 5 + 0.5) * 65535) - **exact** against the emulated region's stored words (3/3 flat-zone nodes, 14
layers each). The extraction (crossing of 0.5) then puts the grid up to ~1.3 cm below s. `shopspawn.py` now models it
(`far_grid_value`); with the game's own sampling on the live grid (`emu_far.py SAMPLE=`) the members come out at
269.8203 / 269.6094 / 269.6797 against the stored 269.828 / 269.619 / 269.680 - the last mm-cm is the float32
system-space position the instance is stored in. Cabinets stay 3/4 exact; cabinet 1 still needs +2 cm in x (likely
Core's Height(A) there, or float32 position) and needs a live check at that shop.

### Moon verification on Eolul Sigma (cube face -Y, 2026-11-06)

Two shops tested in game on Eolul Sigma (Moon 2 of Ihweyf, `21FA016452DB`, radius 15,360 m, cube face `-Y`, reload on
Eolul Sigma):
- `0x2CA7C9B233C0867D` (`-66.19, -164.07`, building at `-66.25, -164.16`): in-game cabinet offers **Improvised Molecule
  Focuser**, A-Class, 333.3 dmg / 452.4 scan, 16 slots, 3 SC, 6 tech (including Rusted `!` and A-class scanner modules)
  -> exact match at `(-369690, 98921, -7965)`.
- `0x30521C5EE7F897F4` (`-52.59, -123.72`, building at `-52.69, -123.72`): in-game cabinet offers **Song of Tuyma-VII**,
  B-Class, 1347.8 dmg / 259.4 scan, 12 slots, 2 SC (`(2,1), (6,1)`), 4 tech (`SCAN1, SCANBINOC1, LASER, BOLT`)
  -> exact match at `(-375751, 100759, -7172)`.

Live inspection of `cTkLSystemManager` (`read_lsystem_samples.py`, `dump_planet_buildings.py`) and the spawned moon
instances (`read_site_clusters.py`) on Eolul Sigma established two details required on secondary bodies and small-radius
moons:
1. **System-wide L-system registration (`0x142D70960`):** `cTkLSystemManager` registers each `.LSYSTEM.MBIN` path once per
   star system, seeded by the first planet in index order (`0..planetIndex`) whose `RecordClasses` includes that building
   class. On Eolul Sigma (`planetIndex = 1`), both `SHACK_WARRIOR.LSYSTEM.MBIN` (handle 0, Shelter radii
   `21.379, 23.329, 21.255`) and `SHOPWARRIOR.LSYSTEM.MBIN` (handle 2, Shop radius `50.412`) reuse Planet 0's seeds,
   bringing `PlaceMembers` within 1–3 mm of the live member positions.
2. **Spawned building basis re-orthogonalization:** While the site cluster record (`+0x2A0`) stores `f_site` from
   `Facing(sitePoint, siteKey)`, the spawned building instance stands at `buildingPoint` with `up = Normalize(buildingPoint)`,
   `side = Normalize(Cross(up, f_site))`, `forward = Cross(side, up)` (matching all 9 components of the live 3x3 instance
   matrix to 4 decimal places on both moon shops). On a moon (`R = 15,360 m`), the 25 m cluster shift tilts `up` by
   1.6 mrad, shifting the crate by ~3 cm and flipping `0x30521C5EE7F897F4` across the `(Y, Z)` half-metre boundary to the
   exact in-game point `(-375751, 100759, -7172)`.

