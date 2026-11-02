# Base extraction audit

Status (2026-11-02): base ingredient-source discovery is integrated in the
planner using generated hotspot resources. It lists placed extractors at saved
bases; no extraction quantities are included in owned inventory. Power,
current output stock and production-rate prediction remain excluded.

## Native stock and resource selection helpers

Subsequent inspection located the active hotspot-table pointer at 0x146EF1DA0
and its consumers in the inspected executable's 0x1412F4AC0-0x1412F694F range.
These addresses are executable-specific research references, not runtime
application dependencies.

Native 0x1412F5400 converts stock by integer division FIRST:
`(stored internal units / AmountCost) * SubstanceYeild`. It does not multiply
first and then floor individual units. `ExtractionStock` ports that operation
and rejects unsupported negative/overflow cases. The read-only Unicorn oracle
executes the helper with synthetic definitions; all 36 output vectors matched
Core. Game bytes and generated vectors remain outside the repository.

For the two preserved Paraffinium saves, the earlier raw sum 10033886 converts
to 6750 displayed units and the later raw sum 10134456 converts to 7000. These
match the owner's two observed amounts. The continuous internal growth rate
is exactly 565 internal units/second, corresponding to 1412.5 item units/hour.
Using continuous stock for time remaining gives roughly 02:08:48 for the older
save, about nine seconds after the screenshot's 02:08:57. This explains the
earlier apparent disagreement: the displayed stock is quantized, while the
underlying accumulator is not. Offline updates, power loss and collection
resets still need separate checks.

Native 0x1412F5EF0 selects a resource once the actual hotspot type is known:
Mineral1 uses the planet's common mineral, Mineral2 its biome-specific mineral,
Mineral3 its rare mineral; Gas1/Gas2 use the two gas entries for the biome.
`HotspotResources` ports that selection. A separate native oracle supplies
synthetic planet resources and gas tables, stubbing only world/address service
calls. Core matched all 119 outputs (seven type cases across 17 biome slots).

This is not automatic resource identification yet. The actual hotspot type at
the extractor's position remains to be resolved. Nearby native routines use
spherical pole placement, seeded MWC draws, hotspot weights, range and class
tables. Do not select the biome mineral merely because a base contains a
mineral extractor, or the first gas merely because it contains a gas extractor.

## Native hotspot attribute draws

`HotspotAttributes` ports the type, range, class and strength draws inside
0x1412F6580. It accepts six caller-supplied category definitions; it contains
no extracted game content. The float random conversion uses a double reciprocal
of 4294967295 before rounding to float, not 4294967296. Two coordinate-jitter
draws occur between range and class and must advance the stream even when only
attributes are needed.

The read-only `hotspot-roll-oracle.py` executes that native helper with 256
deterministic synthetic seeds and varying category/class weights and ranges.
The sine and two geometry services are stubbed, so this is strictly an
attribute test. Core matches type flag, class, both strength outputs, exact
range float bits and final MWC state for every vector. Positive finite weights
are the supported input domain of this initial port.

### Address orientation and spherical rotation

0x1413524B0 generates a direction from the address masked to 52 bits;
0x1412F59A0 applies spherical rotations. An initial trial produced non-finite
directions because its vector at 0x1472E1F40 is zero in the file image. Follow-up
xrefs found initializer 0x140033520: it copies (0,0,1,1) from 0x144BF4BE0.
Executing that initializer removes the missing runtime-state dependency;
no guessed axis is substituted.

`hotspot-roll-oracle.py --geometry` now runs that initializer and all geometry
services, including native sine/cosine. It records the address orientation,
rotation input and output direction. The 256 cases vary addresses (including
zero, low-word zero and masked-high-bit boundaries), pole indices, planet
radii, attribute weights and random states. `HotspotOrientation.Direction`
matches all orientation component bits. `Rotate` uses Numerics quaternion
arithmetic with the game's trig; its maximum measured direction error is
2.9200194E-7, about 3.8 cm at a 130048-unit radius. The test rejects non-finite
results and errors above 1E-6. This is a measured rotation tolerance, not an
exact-coordinate claim or a full placement check. Native rotation inputs are
radians; the apparent half-angle constant is 0.5, not degrees-to-radians.

### Candidate construction and the single-pole acceptance pass

`HotspotCandidate` now constructs the pole angles and coordinate jitter from
the radius, spacing and random state. Candidate latitude/longitude match the
native float bits in the 256 geometry vectors; resulting directions retain the
rotation tolerance above. The range-plus-ten-unit margin is subtracted from
half the pole spacing before jitter is applied.

`HotspotPole` ports the address/pole hash, initial MWC state, count draw and
rejection loop from 0x1412F6110. Power is one category, the three mineral types
share a second, and both gas types share a third. For equal categories, summed
ranges minus chord distance must not exceed negative minimum spacing; for
different categories it must not exceed maximum overlap. Rejected proposals
still consume all five candidate draws. Tests use caller-supplied synthetic
definitions, not committed game content. The Core port bounds attempts and
fails if spacing does not converge; it never returns a truncated partial pole.

The oracle's `--geometry --poles` mode executes the complete native acceptance
pass with preallocated output storage (no allocator stub). In 256 varied cases,
780 proposals produced 613 accepted hotspots: 167 rejected proposals exercise
the retry path. Core matches all accepted counts, type flags, classes, strengths
and range values; maximum direction error is 3.2236142E-7. Decisions extremely
close to a distance threshold are still subject to the measured rotation
tolerance; this is not a proof for all possible input tables.

### Inverse coordinates and the nearby query

`HotspotOrientation.Inverse` and `HotspotNeighborhood.Around` now port the inverse
coordinates at 0x1412F55A0 and four-pole selection at 0x1412F4AC0. The inverse
uses the native acos polynomial (now `GameTrig.Acos`), not MathF.Acos. Native
longitude wrapping selects the last lower pole when its index is non-positive,
while retaining the upper pole of one. Degenerate tangents fail explicitly.

The oracle's `--geometry --near` mode executes the native nearby query and all
hotspot/geometry code. Only world-layout initialization, the supplied planet
size service, memset and scalar log2/ceil/pow services are replaced. The size
service supplies one of 15/63/96/127, and the query computes its radius via its
native operations (scaling by 32 twice). The logarithm is base TWO; an initial
log10 service substitution was wrong and was corrected before comparisons.
With varied synthetic definitions and points, all 256 Core neighborhoods match
native accepted counts and ordered attributes across 2423 hotspots. Maximum
direction error is 4.0094923E-7. Candidate and inverse angle checks also pass.
This does not validate world generation or a general extractor selection rule.

The oracle accepts `--reference <external-json>` with case arrays containing
Address, Radius, Point, Configs and Rules. No saves or extracted definitions
are committed. A read-only check used the preserved Paraffinium base snapshot:
20 extractor positions, actual extracted hotspot settings, radius 98304 and
an orthonormal candidate base basis derived from radial up and saved forward.
Native and Core agree on 20 neighborhoods / 200 hotspots, maximum direction
error 2.6656008E-7. All 20 extractors' closest mineral candidate has flag 4
(Mineral2), within its 212.9924-unit range; example chord distances are 7.50,
4.42 and 4.39 units. That is consistent with the owner's Paraffinium reference.
The native filtered selector 0x1412F4EB0 is also ported as
`HotspotNeighborhood.Closest`. It filters by hotspot flags and chooses minimum
horizontal distance: subtract squared radial projection from squared delta,
clamp to zero, then sqrt. It does not itself enforce a hotspot range limit.
The native closest-mineral choice matches Core in all 256 synthetic cases
(including same-type identity via direction comparison). The 20 preserved
Paraffinium positions also select Mineral2 under that native rule. The test
oracle supplies only world/radius/scalar services as above; selection executes
unchanged with its inline output storage.

The base coordinate basis, generated world-resource mapping and the link-grid
consumer's range/power handling still need independent confirmation; do not
promote this candidate survey to automatic resource identification or count
extraction stock as owned yet.

## Catalog extraction and generated resource reference (2026-11-02)

`CatalogSteps.Extraction.cs` now builds the `EXTRACTIONTABLES` row from the
installed region hotspot and base building tables. Its portable Core record
contains the six ordered hotspot definitions with enum-derived names/flags,
17 biome gas pairs, substance batch yields and resource-network connection
settings. The installed-file scratch-catalog check found 6 categories, 17 gas
pairs, 33 substance yields and 5 resource-link definitions. It verifies the
known mineral/gas extractor and depot definitions, and converts the reference
Paraffinium stock to 6750 using the extracted yield (no copied yield list).

`ExtractionCatalogChecks.World` accepts external reference cases and reads the
scratch catalog read-only. For the preserved base address it runs the existing
system/body/profile generators, selects the previously native-confirmed
Mineral2 resource and checks the generated radius. Both match the supplied
reference: LUSH1 (Paraffinium), radius 98304. Biome gases remain extracted inputs,
not an inference that a gas extractor necessarily collects the first gas.

At that checkpoint the row had no application consumer and catalog format was
not bumped. The subsequent integration below moves the format to 57. No real
catalog or save was modified by checks; they wrote a new scratch database.

### Recipe base-source integration

`BaseExtractionSites` reads HomePlanetBase entries, resolves their generated
worlds once per address, transforms placed extractor coordinates and applies
the native-filtered nearby query. Only matching mineral/gas hotspots within
range produce a source entry. It groups by base, item and extractor kind.
Unsupported body sizes and missing coordinate data are excluded. It does not
claim equipment is powered, infer a production rate or add stored output.

Read-only Slot 1 Working save inspection on 2026-11-02 independently exercises
the second base orientation: Oxygen Farm - Portal has 25 gas extractors and all
resolve to OXYGEN, matching the owner's Oxygen depot reference. The Paraffinium
base resolves to LUSH1 with 20 extractors. An external reference JSON asserts
both results; the check also asserts discovery leaves the parsed save unchanged.
The Oxygen total describes every placed extractor at the base, including the
previously identified disconnected networks, not just the 18-extractor main
network in the capacity screenshot.

The shared recipe details control shows matching sources under 'Extractors at
your bases'. Catalog and save walks run off the UI thread. The existing
app-wide snapshot feed updates strings on existing item ViewModels; there is
no new watcher or planner rebuild. Without a save, that section is hidden.
The app needs catalog format 57, built from the owner's installed game files.
UI behavior still requires the owner's Visual Studio build and in-app check.

## In-game reference and fresh-save comparison

The owner supplied a Supply Depot screenshot showing Paraffinium, 6750/10000
overall storage, network extraction rate 1412/hr, Filling, and 02:08:57 until
full. They saved immediately afterwards, then observed 7000 without another
manual save. File timestamps nevertheless showed that the game's autosave was
updating. The original 6750 moment was not preserved in the two files copied
for the comparison; do not claim an exact same-moment amount match.

Copies of the two real Slot 1 save files were made in the external research
scratch directory and decompressed with SaveInspector's existing read-only
SaveStreamProcessor pipeline. The live saves were not changed. For the same
25 equipment records, unchanged part IDs, positions and lower words:

- Earlier object timestamp: 1793555548; candidate stock 6967.976388888889.
- Later object timestamp: 1793555726; candidate stock 7037.816666666667.
- Elapsed: 178 seconds; candidate increase 69.84027777777737.
- Observed growth: 1412.5 units/hour, consistent with the displayed 1412/hr.
- Table-based capacity: 10000, matching the screenshot.

This supports the upper-word stock interpretation and table-derived units for
this one setup. It does not verify rounding, disconnected networks, offline
production, capped storage, power failure, or automatic resource identity. The
screenshot's time-until-full does not equal a simple calculation using the
rounded displayed stock and rate; the later native findings above explain why.

## Confirmed inputs

The owner confirmed on 2026-11-01 that the mineral extraction setup at
"Amazing Planet Biomes - Portal" produces Paraffinium. The Slot 1 reference
save has 20 mineral extractors and five supply depots in that base's object
array. This confirmation applies to this setup, not arbitrary bases or all
mineral hotspots on that planet.

Object placement is in PersistentPlayerBases (`F?0`), Objects (`@ZJ`), with
ObjectID (`r<7`), Timestamp (`b1:`), UserData (`CVX`) and transform vectors.
The vendored GcPersistentBaseEntry confirms the structural types; the real save
confirms the corresponding fields and part IDs. UserData is a packed unsigned
64-bit integer, not an ordinary inventory container.

Read-only inspection across the available Working saves found extraction
equipment in Slots 1, 2, 3, 5 and 6; Slot 4 had a depot. Lower UserData words
were usually zero, with one 131072 value in Slot 2. Upper words varied and some
mineral extractors had 360000. These observations alone do not map flags,
production, power status or elapsed-time behavior. Working files can change
while the app follows the game; the audit hashes exactly the bytes it parses.

## Local game definitions

The existing DataCataloger `dumpfile` command decoded the installed game's
basebuildingobjectstable.mbin and regionhotspots table. GcBaseBuildingEntry's
LinkGridData contains DependsOnHotspots, Rate, Storage, resource-network
connection settings and dependent connections. Mineral and gas extractors have
distinct hotspot dependencies; depots have no hotspot dependency of their own.

The inspected extractor definition has Storage 360000 and Rate 100; the depot
has Storage 1440000 and Rate 0. The inspected Paraffinium hotspot substance
definition has AmountCost 360000 and SubstanceYeild 250. These are extracted
inputs, not constants to copy into application logic.

Candidate conversion: upper UserData word multiplied by SubstanceYeild and
divided by AmountCost. Applying the same ratio to the definition's storage
gives 250 units per extractor and 1000 per depot, or 10000 for this equipment
set. Capacity and growth have the supporting comparison above; an exact
same-moment stock/rounding comparison remains outstanding. The configured Rate is not a verified
site production rate; hotspot density, diminishing returns, power and network
connectivity must be accounted for before displaying that claim.

## Reproduction

`scripts/research/base-extraction-audit.py` reads a decrypted save and local
table dumps. It requires an exact base name and explicitly confirmed resource
ID. It reports the save hash, equipment, packed words, catalog inputs and
candidate counts. It does not infer the resource, write to a save, modify
user.db, or add quantities to inventory. Do not commit its game-data inputs
or generated reports.

## Resource-network reconstruction

A deeper `dumpfile basebuildingobjectstable.mbin 9 5000` includes the main
connection network enum and dependency settings. Use this depth for graph
audits: a depth-6 dump omits the network enum and cannot distinguish every
resource generator from unrelated network types.

The reference base contains 24 saved pipelines. For those parts, the saved
Position is one endpoint and Position + At matches the other endpoint. At is
a displacement, not the normalized forward vector used by an extractor.
The table supplies Resources network identity, masks, subgroups and distances.
Mask compatibility prevents nearby equipment from being joined without pipes.

The candidate graph initially used the greater connection radius and missed
one Oxygen extractor. The owner's second depot screenshot shows Oxygen,
500/10500 capacity and 1342/hr. The missing endpoint was approximately 3.082
units from its extractor. Using the sum of the extracted 3 and 0.1 radii
connects it. The other isolated extractor's nearest endpoint is about 3.146
units away and stays disconnected.

With summed radii:

- Paraffinium: one group, 20 mineral extractors + 5 depots + 24 pipelines;
  10000 capacity matches the screenshot.
- Oxygen: main group, 18 gas extractors + 6 depots; 10500 capacity matches
  the screenshot. Other candidate groups are one gas extractor and six gas
  extractors. Their capacities/connectivity have not been checked in game.

`Core/Recipes/BaseExtractionNetworks` now reconstructs these equipment groups
from caller-supplied extracted rules without UI, installation lookup or libMBIN.
Members retain raw link-grid stock/capacity units and object indices. It does
not identify substances, round item counts, predict production or modify saves.
Other resource generators and unsupported UseMinDistance rules are excluded or
rejected rather than silently treated as supported industrial extraction.
Application wiring and GameData/catalog integration are not implemented yet.

Core.Checks covers masks, summed radii, endpoint displacement, nearby unconnected
equipment, separated groups, duplicated pipes, unique stock counting, mutation
protection and unsupported rules. Local reference vectors from the extracted
rules and saved objects reproduce the two displayed main-network capacities.
This verifies the aggregate reference cases, not every individual edge or
general native-network equivalence. Vectors stay outside the repository.

Use `--previous-save` to compare an earlier decrypted save of unchanged
equipment. The comparison requires matching part IDs, positions, lower-word
state and common increasing timestamps, and reports a base equipment total,
not an inferred connected-network total.

Create the inputs with the existing DataCataloger:

```text
dumpfile basebuildingobjectstable.mbin 6 5000
dumpfile regionhotspots 5 500
```

Run the audit with `--save`, `--building-dump`, `--hotspot-dump`, `--base-name`
and `--resource`. The resource argument is a catalog ID, used only by this
developer diagnostic. Production UI must continue to show ordinary item names.

Validation exercised the real installed table parser and synthetic empty/full
packed endpoints, expedition context selection, missing-base rejection and
input immutability. The real base audit found 25 equipment objects. These tests
check the diagnostic's arithmetic and input handling, not the game's storage
semantics. No app was launched, and no save was edited.

## Next evidence and implementation

The requested in-game reference is supplied and recorded above. A controlled
collect/save comparison can establish
the packed quantity word and rounding. Resource identity still needs a verified
hotspot-generation path or an explicit user-maintained site record; do not
assign Paraffinium to every mineral extractor because this one base has it.

Once verified, put data extraction in GameData, portable definitions and supply
calculations in Core, and the save/platform adapter in WinUI services. Show
saved site stock separately from ordinary cargo and avoid counting each depot
as a separate copy of a connected network's displayed total.
