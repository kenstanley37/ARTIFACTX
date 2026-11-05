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
