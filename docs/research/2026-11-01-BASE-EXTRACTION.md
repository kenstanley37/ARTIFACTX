# Base extraction audit

Status: read-only investigation. No extraction quantities are included in the
recipe planner's owned inventory. No automatic hotspot resource claim is made.

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
screenshot's time-until-full also does not equal a simple remaining-stock /
displayed-rate calculation; avoid shipping such a time estimate as exact.

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
