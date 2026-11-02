# Base extraction audit

Status: read-only investigation. No extraction quantities are included in the
recipe planner's owned inventory. No automatic hotspot resource claim is made.

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

Pole generation, spacing/overlap rejection and closest-hotspot selection remain
unported and unverified. Consequently no actual extractor resource is inferred.

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
