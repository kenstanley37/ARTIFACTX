# Multi-tool generation research

## Verified 2026-10-24 (NMS build 180836)

Owner authorized publishing DELETEME reference data. No game or save writes were used.

- Screenshots: station Geometric Angle Focuser, C, damage 333.3, scanner 209.8; Mass Needler Z-C11, B, damage 443.3, scanner 252.3. Station shown as Lukinog III Rendezvous.
- Read-only Working Slot 8 `save15.hg.json` and `save16.hg.json`: packed reload-body address `0x980906A7A427` (galaxy index 9, station/body 0). Saved location label says Aboard Lukinog III Prime; do not substitute this for the screenshot's station name.
- Purchased `SuJ[4]`: model seed `0x4101B7E8CB7DAFD9`, inventory seed `0x10662FF4655D0E43`, B, 12 unlocked slots; SCAN1, SCANBINOC1, LASER and UT_SURVEY. The existing production name generator returns Mass Needler Z-C11 exactly.

## Pool construction

`0x1416d1750`, called by preparation phase `0x1413b9277`, obtains its seed from packed universe address routine `0x140583a90`. Includes galaxy and reload-body nibble; not a portal code or planet model seed.

1. Mix64 the packed address, initialize MWC as elsewhere.
2. Independent probability/count streams start at this state. Probability consumes one word per ordered pool definition. Count consumes a word only when probability passes, selecting MinDraw..MaxDraw inclusively.
3. Model stream starts at the same state and consumes one word before any model pairs. Each model consumes low then high words; Mix64 that pair.
4. Preserve GcSimulationGlobals.MultitoolPool order, probability, min/max and model paths from the player's catalog. There are more types than Standard/Royal. SettlementRotational uses a separate day-dependent stream and is omitted unless supplied a rotation day. The hash input is the UTC whole-day number (`UnixSeconds / 86400`), as shown by the quotient at `0x1416d1818`; the constructor separately stores the day-start timestamp (`day * 86400`) at pool offset `0x20`, but does not hash that timestamp.

Production port plus actual extracted definitions yields the following computed entries (rotational entries are omitted without a rotation day). Only the first two have screenshot references; computing another pool member does not prove stock at a cabinet:

| Order | Type | Seed | Name |
|---|---|---|---|
| 1 | Standard | 0x3D4A291F239C0CBA | Geometric Angle Focuser |
| 2 | Standard | 0x4101B7E8CB7DAFD9 | Mass Needler Z-C11 |
| 3 | Standard | 0xF3EF209C654EF9A7 | The Hand of Arechno-XVII |
| 4 | Exotic | 0x498FAE64909C7F5B | Legacy of Okronega-IX |
| 5 | Sentinel | 0x15D70B834586F3FA | Burning Riyskoy Clarifier |
| 6 | Atlas | 0xFDAD7A4FA09A7EB4 | Dreams of Certainty |

Both names match screenshots; second seed matches the bought tool. This validates pool construction for this reference, not all locations or all pool types. The former implementation incorrectly used an unmixed portal/planet seed, fixed count, no initial word, and cited unrelated code addresses.

## Site selection and inventory

- `0x1416d2470`: filter generated entries by requested pool type, initialize MWC from interaction/site seed, take one scaled integer draw.
- `0x1404cfda0`: class roll from the inventory/site seed and current economy probability row. Cumulative float probabilities, one uniform draw; leftover probability stays C.
- For inventory seed `0x10662FF4655D0E43`, the production selection is Mass Needler and economy row 2 gives B. Both match.
- `0x1404ceab0`: intrinsic bonuses use the inventory seed, **not model seed**. Using the actual Pistol/B table gives damage 0, mining 10.274667739868164, scan 26.141597747802734, exactly matching all three saved floats. Hypothetical S-class model-seed rolls are not actual located stats.
- The actual intrinsic-stat API requires a resolved weapon kind and a nonempty catalog row. Unknown/Standard kinds return no result; they do not inherit Rifle values. Atlas, AtlasYellow and AtlasBlue use distinct enum indices 6, 7 and 8 respectively, verified against the vendored `GcWeaponClasses` definition and full table extraction.
- Equipped Damage Potential/Scanner Range require technology effects, supercharged slots and other inventory calculations. Do not label intrinsic percentages as those display values.

## Position hash

`0x140504eb0`: interaction entity world XYZ are floats; each gets sign-preserving +/-0.5 then truncates to signed int. Hash X/Y with Hash128to64, then combine the result with Z. The interaction initialization call is `0x140ff16f8`.

Searching integer positions within 100 units of the saved station player found exactly one position `(-854, 6892, -401)` whose hash equals the purchased inventory seed. This corroborates the hash. It recovers a **rounded position cell**, not the exact floating transform or a procedural placement algorithm. Do not display it as a solved exact location.

The usual crate entity has ReseedAfterRewardSuccess=false. That alone does not prove that all cabinet types, planets, reload states or updates preserve contents.

## Remaining work (feature is open)

- Reproduce actual cabinet transforms from station layout generation and planetary building placement, terrain, rotation and child scene transforms. A scan marker/building centre is insufficient for the cabinet position hash.
- Derive class/slots/technology and equipped stats for each located item; verify the C example as well as B.
- Establish reload-state and pool-type rules for each cabinet/pillar/camp; verify beyond one system and galaxy.
- Decode crashed ship site placement and its own item/class/inventory generation. Multi-tool results do not prove ship results.
- Keep Planet Details pools and unlocated equipment hidden until site-specific identity, class/stats and precise coordinates are established. No Finds dependency, no inferred stock from a marker, no guessed placements.

## Saved reload-address input (2026-10-26)

The pool constructor has one direct call in this installed executable, at
`0x1413B9277`. Preparation reads the player object at manager `+0xB940`, calls
`0x140583A90`, then passes its full packed address to the constructor. The address
packer reads player `+0x860..+0x874`, including PlanetIndex and RealityIndex.
Ordinary save loading copies the 24-byte `GcPlayerStateData.UniverseAddress` at
raw `+0x86658` into this runtime address at `0x140573CAA..0x140573CCE`.
The subsequent load branch can replace the address for a local/network override
(`0x140573D40..0x140573D75`), so the ordinary-copy result is not universal.

`NmsMultiToolGenerationPaths.ReadSavedReloadAddress` reads `yhJ` from an already
selected saved player-state snapshot. It preserves the body nibble, unlike the
existing system-address reader, and rejects missing, noninteger or out-of-range
components rather than generating a plausible address from defaults. It supplies
an address for a proposed ordinary reload of that snapshot. It does not establish
the active pool of a running game or select the normal/expedition context itself.

A native slice oracle copied 300 saved address structures into runtime state and
packed each through the actual native routine. The production reader matches all
300 results; 3,000 incomplete/malformed address cases are rejected. All four
located reference records still match in the complete harness. Read-only Slot8
inspection found Main context, galaxy 0, system 52, body 0 at (657,-1,1659),
demonstrating that its current saved address differs from the historical station
reference. No current-session pool identity is inferred from that snapshot.

## Explicit reload context retained through cabinet generation (2026-10-26)

`StationMultiToolContents.SelectForReload` accepts `MultiToolReloadContext` with a
system address, full reload-body address and established body count. It rejects
cross-system/cross-galaxy addresses, nonzero reserved bits, a body beyond that count,
and missing pool definitions. It builds one pool, selects using the actual cabinet
site seed, rolls class, and retains the supplied context on the resulting contents.
Station presence and geometry remain separately established caller preconditions.
The context describes the specified reload scenario, not an observed running session.

When retained context is present, `LocatedMultiToolGeneration` repeats selection
from that address and checks model seed, pool index/type/source, model resource and
class before emitting a final record. Existing low-level pool-selection callers
remain available with their explicit caller preconditions. The reference harness
now uses the context API for all four complete reference records; names, actual
classes and displayed stats still match. Seven invalid reload contexts and altered
retained-context/pool-index cases are rejected. No player-facing stock UI is enabled.

## Saved food-effect guard (2026-10-26)

The extra per-stat adjustment at `0x14057A9C7` reads player `+0x18820`, performs
an item lookup, compares the item's bonus stat type, and adds its resolved bonus
before applying the stat's combination rule. This is not an independent saved
boolean: save load `0x140573AF9..0x140573B0A` copies the 16-byte FoodUnitItem
at raw `+0x83C90` into that runtime field. Native named deserialization at
`0x142A04BD7..0x142A04BF0` confirms the field name and offset. Five native
load fixtures, including an empty ID, preserve all 16 bytes.

The obfuscated key is `nsH`, confirmed against the field mapping in
[libNOM.map](https://github.com/zencq/libNOM.map/blob/master/libNOM.map/Resources/mapping_mbincompiler.json)
and read-only Slot8 inspection. Its saved value is `"^"`, the empty game-ID
encoding. The two-argument `ReadStandardGameState` normalizes a leading caret
and supplies the existing conservative extra-effect guard. Empty IDs permit the
ordinary path; every nonempty ID remains unsupported without guessing its
bonus, expiry or catalog lookup result. Missing/malformed fields return null.
The service now has a save-state overload that reads this guard automatically;
the explicit runtime override remains available for native research contexts.

This snapshot input does not establish live consumable state. All four reference
records now use the save-derived guard, progression and known-tech set. Native
wealth, station/season state and reload context still require established inputs.

## Active saved-context selection (2026-10-26)

`ReadActiveSavedState` selects exactly the context named by `XTp`: Main uses
`vLc/6f=`, Season uses `2YS/6f=`. It returns the selected saved reload address
and progression/known-tech/food runtime inputs together. Missing or unknown
selectors, absent selected contexts or incomplete selected inputs yield null;
there is no fallback to another populated context. A populated inactive expedition
must not contribute its inventories or generation inputs to Main.

Verified against the read-only actual Main snapshot and a dual-context fixture
with distinct progression and galaxy values. Selecting Season switches both;
removing its player state rejects the request rather than borrowing Main.
The complete harness still matches all four reference records.

This establishes saved-context ownership, not active seasonal station behavior.
Tracing runtime seasonal flag `+0xD792` found it assigned from loaded season
milestone data count at `+0x66B0` (`0x1405DECBD..0x1405DECD2`). The validity
helper additionally checks dates and other season flags. Therefore neither
XTp alone nor an inactive expedition block proves the native legacy predicate.

## Address-to-record station composition (2026-10-26)

`StationMultiToolGeneration.Generate` composes the complete supported path from a
system/reload address and established station/runtime context: ordinary solar layout,
station pose, selected scene, cabinet origins, pool/class selection, model resolution,
inventory and displayed stats. It cross-checks body count, native wealth and system
flags against generated attributes, and omits unsupported models rather than emitting
partial records. The request still requires established station/legacy/session inputs.

The full pipeline matches all four reference names, classes, damage values and scan
ranges from their two system addresses, retaining reload provenance. Sixteen negative
context cases produce no stock. These are composed-path checks against the existing
game references, supplementing the individual native numerical oracles.

`LocatedMultiToolService.GenerateStationAsync` loads the station, inventory, technology,
model and name tables off the UI thread, snapshots known technologies, and runs this
pipeline. Cancellation is preserved, and results are discarded if catalog rebuilding
or a changed catalog timestamp invalidates the loaded inputs. Planet Details remains
unwired until context resolution and supported-case UX are ready. This service entry
point does not add live session tracking or prove seasonal station behavior.

## Supported saved seasonal early return (2026-10-26)

The save includes CommonStateData at `<h0`, with SeasonData at `Rol`.
Read-only Slot8 inspection places `EqZ=false` at serialized index 128, matching
the named schema's UseLegacyStationsNearStartUA. Adjacent indices are the known
Sks/AlwaysUseSeasonalStationTradeData (127), ;wI/UseLegacyStationsRadius (129),
and uTm/UseSpookHazardOnly (130). Native named decoding at `0x142995EC0` and
`0x142998890` confirms the long field name and boolean offset `+0x6B92`.
This key identification uses inspected save ordering plus named schema/native
metadata; it is not a controlled in-game toggle experiment.

Native ordinary season loading at `0x1404961E9` and `0x140496823` passes
saved CommonStateData+0x50 to `0x1405DE8B0`. For a reload using that saved
configuration, a false near-start flag establishes the legacy predicate's
false early return irrespective of season validity, unless the debug override
forces legacy. A true or missing flag remains unknown: no radius/address
branch has been ported, and online season-data replacement is not covered.

`ReadSavedLegacyStationRule` exposes this narrow saved-reload rule.
`ReadSavedStationRequest` combines it with strict active-context/address/runtime
selection and generated system attributes to prepare the station pipeline request.
Tests resolve both historical station-address fixtures, preserve the forced-legacy
override and reject true/missing seasonal flags. Existing full-pipeline references
continue to test the four known tool identities/classes/stats. No live pool is inferred.

## First saved-reload UI connection (2026-10-26)

Planet Details now contains a separate "Station multi-tools after reloading this
save" section. It is conditional on an ordinary reload at the saved location and
does not label its output as the running game's current stock. Supported records
show name, actual class, damage potential, scanner range and system-space position.
Unsupported contexts/models are omitted; the hint states that only supported
cabinets are shown. This section is separate from planetary save markers and Finds.

The existing shared live-save reader creates a slim independent equipment-input
snapshot off the UI thread. It contains selected-context address, progression,
known tech, food ID and the saved seasonal flag, not full inventories. Initial
loading obtains the latest disk snapshot if the editing-session snapshot lacks
these inputs. The existing SnapshotSeen feed refreshes the section; no additional
watcher is started. Async generations carry a version and loaded-slot identity so
late results cannot replace newer page/slot results. Catalog generation stays in
the background service and rejects mismatched target systems or rebuilt catalogs.

The slim snapshot preserves all required address/runtime data and does not alias
its source, as checked by mutation isolation in the harness. Four full pipeline
references and sixteen unsupported contexts still pass. App/XAML builds are used
for code validation; visual layout and game behavior require the owner's testing.
Catalog format 39 is required for the complete current station input set.

### Hidden section on Urjarn: prime-body count correction (2026-10-26)

The owner's current Slot8 snapshot has saved body index 6 in system
`0x3400FF67B291`. Generated attributes have five primary bodies plus one prime
body. The saved-request builder and pipeline validator incorrectly treated primary
PlanetCount as the total addressable count, rejecting the sixth body and hiding
all records. `SystemGenerator.Bodies` already defines total as PlanetCount plus
PrimePlanetCount; reload validation now uses that same total.

Read-only inspection confirmed the installed catalog is format 39 and has all four
generation rows. Running the corrected production pipeline against copies of those
installed rows and the actual saved inputs produces two supported cabinet records.
These are calculated results for the saved-reload scenario; they have not yet been
compared to the owner's current in-game cabinet stock. The four historical game
references and sixteen unsupported-context checks continue to pass.

### Current System placement and new in-game comparison (2026-10-26)

The owner supplied both current cabinet screenshots: Geometric Loop Needler,
C-Class, damage potential 1370.2 in each, scanner range 206.9 and 209.7.
These match both calculated cards shown by ArtifactX for the saved body-six
reload scenario. This confirms names/classes/displayed stats for these two
additional results; the screenshots do not independently establish world XYZ.

Per the owner's correction, the station section now appears on Current System,
beside the station market area, and is removed from Planet Details.
`StationMultiToolsViewModel` and `StationMultiToolsPanel` own its display and
async refresh. CurrentSystemViewModel.Show supplies each shared save snapshot
and current system address; planetary equipment no longer loads station stock.
Generation calculations and saved-reload conditions are unchanged by this move.

### Additional cabinet reports and recovered scanner module (2026-10-26)

Nuhdi II (`0x6000FF67B291`, Euclid) screenshots show Light Balancer OC8-S,
C-Class, damage 2154.4 and scanner 203.2, matching the app. Sleep of War,
C-Class, scanner 228.8, initially appeared with game damage 400.0 versus app
333.3. The owner later supplied a 333.3 game screenshot and explicitly reported
333.3 in both camera views. Do not describe camera mode as the established cause.

The native potential evaluator, supplied with the generated Sleep of War loadout
(SCAN1, SCANBINOC1, LASER, STUN_GREN), the catalog overheat duration and zero
intrinsic damage, also returns 333.3333. That supports the arithmetic for those
inputs but does not explain the earlier 400.0 screenshot. The display path obtains
an animation duration from a runtime owner (`0x14140F417..0x14140F446`); its name
helper can append _3P, and a missing animation lookup returns zero at
`0x1402069A4`. Zero overheat duration would produce 400.0 for this loadout, but
the actual runtime lookup/loading state in the earlier screenshot is unverified.

Nakokus-Gegin (`0x4600FF67B291`, Euclid) has Messenger of the Abyss DR2/9-W93,
B-Class, 1445.1 damage and 213.9 scanner, matching the app. Its other cabinet,
Optimized Notins Engine (localized name varies with English word data), was omitted
because native selection chooses UP_SCAN0 with quality 7 (SeaTrash), outside the
previous module roller's supported qualities. Native ranking confirmed that choice
at site `0xEF3454F47873FD71`; the inventory seed generates suffix 31696.

Support is now limited to this verified recovered scanner entry in addition to
ordinary qualities. A new native stat-roll oracle covers 203 suffixes, including
all ranking-fixture suffixes. Its target stats are radius 1.0236437320709229,
flora discovery 2.897876739501953 and mineral discovery 2.497267961502075,
all native level 8. The complete 7,403-roll corpus matches with zero ULP error;
regenerated native ranking vectors also pass. The resolved scanner range is
210.43982, matching the game's 210.4 screenshot. Its earlier damage screenshot
shows 400.0 while the current standard-duration calculation gives 333.3, so that
display-context discrepancy remains open rather than being patched to the screenshot.

Onamir-Zuang (`0x7A00FF67B291`, Euclid) is pirate-controlled and remains
unsupported by the TypeB station pipeline. Its Bane of Silence screenshot
(C-Class, 420.0 damage, 208.7 scanner) is a reference for future pirate-scene
extraction, not evidence that ordinary TypeB geometry can be reused there.
