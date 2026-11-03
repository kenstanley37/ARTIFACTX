# Exocraft unlock guidance: Colossus investigation

## Evidence and limits

Read-only inspection of installed NMS build 184271 on 2026-11-02. No save or
account file was changed. Work is on `feature/exocraft-unlock-guide`.

The Catalogue page currently filters to known technology. Cosmetic descriptor
groups and title rewards require a separate catalog projection and read-only
presentation on each relevant Exocraft tab.

`charactercustomisationdescriptorgroupsdata.mbin` links Colossus options to
product/special IDs. `playertitledata.mbin` links those IDs to titles and declared
requirements. These are eligibility definitions, not confirmation of ownership.

| Part | Title | Stat | Threshold |
|---|---|---|---:|
| Widescreen Cabin | Big Rig | CARGO_DESTROYED | 100 |
| Furnace Module | the Colossus | CARGO_DESTROYED | 200 |
| Off-Road Wheels | Rusty | CARGO_SCRAP | 75 |
| Tracked Treads | Loadmaster | CARGO_EXP | 50 |
| Caterpillar Track | Hazmat Specialist | CARGO_TOX | 50 |
| Superelastic Wheels | Fuel Handler | CARGO_RAD | 50 |

Localized title instructions explicitly say manually process scrap, explosive,
toxic and radioactive waste. Cabin and furnace instructions say recycle a large
or vast amount of industrial waste. Do not substitute recovered units/value for
the `CARGO_DESTROYED` count. These six title definitions do not reference travel
distance. They include season exclusions; retain those conditions when extracting.

## Owner observations

- Owner manually sorted waste, usually transporting it in a corvette rather than
  Colossus because objects fell out. Automatic sorting was used roughly once.
- Snapshot counters: scrap 452, explosive 112, radioactive 95, toxic 76,
  destroyed 781, automatic 46. Later screenshot: scrap 559, explosive 136,
  radioactive 119, toxic 98. The parts remained locked.
- Account `B89/Bgb` contains titles `T_RECYCLE1` through `T_RECYCLE8`. Inspection
  found expedition parts in other account reward lists but several linked
  recycling parts absent. Do not equate account lists without mapping their roles.
- Owner found and selected Rusty at the Appearance Modifier. Colossus parts
  remained locked afterward. Selection is not a verified recovery method.
- Empty/default counters include `TRUCK_CUST`, `CARGO_DS_THROWN`, `CARGO_SWARM`.
  No evidence identifies any as an unlock switch. JSON field adjacency is not a
  dependency: lifetime stats are an ID-keyed collection, not ordered logic.

## Native grant early exit

Build-specific addresses (never reuse after an update without reinspection):

- `0x140614BD0`: title eligibility. Looks in account's unlocked-title set at
  object +0x140, returns false immediately when title is present. If absent,
  checks the declared season exclusions and nonempty title ID.
- `0x1406153A0`: title grant. Calls that eligibility helper at `0x1406153F3`.
  False jumps to return before inserting the title or entering the linked
  specials loop. The list at title record +0x90/+0x98 matches the vendored
  `GcPlayerTitle.TitleUnlocksSpecials` layout.
- `0x1406154E0`: loop invokes `0x1405B5AB0` for each linked special, followed by
  product lookup/notification on success. Its complete downstream behavior is
  not yet emulated.

`scripts/research/title-unlock-oracle.py` runs the real eligibility and grant
machine code with synthetic memory. Lookup stubs model an existing title and a
valid catalog row. Both return false; only expected lookup calls occur. PASS on
184271. This validates the existing-title early exit only, not first-time reward
grant, all callers, a global absence of backfill, or a general NMS bug.

This mechanism fits an earned-title/missing-part discrepancy. How this account
entered that state remains unknown. Do not advise additional driving, further
sorting, title selection, or changing a JSON flag as a verified fix.

## Implementation checkpoint

- Portable `TitleUnlockRule` preserves requirements and special associations.
- `CatalogSteps.AddTitleUnlocks` extracts 15 title reward rules and localized
  instructions using all English language files. It now also follows the
  enum-indexed customization UI into descriptor and texture definitions: 593
  choices, including 17 Colossus cabin/chassis/running-gear/paint options.
  Common decal groups with no unlockable choices are omitted from the UI.
- Catalog format 58 wires this extraction into the normal build. A scratch full
  build on 184271 completed without decode errors: 22,009 named items.
- `--title-unlocks-catalog-check` checks six Colossus references against current
  installed metadata. Passed on 184271, with no generated data committed.
- `VehicleUnlockPanel` is a reusable folding card above the inventories on
  every Exocraft tab. XAML templates bind to ViewModels; file/database work is in
  `VehicleUnlockService`, and portable catalog records stay in Core. The panel
  starts folded and retains its folding preference. Vehicles without extracted
  unlockable choices say so instead of inventing data.
- Account observations use fresh read-only account data: B1h/fyX/d4U membership
  is labeled "Reward recorded" or "Reward not recorded", not inferred unlocked
  state. Bgb titles are separate. The owner screenshots support flatbed and
  expedition options being recorded, and six recycling reward options absent.
  Full account-list semantics and all native customization gate branches are not
  established, so the panel deliberately avoids promising in-game availability.
- Progress uses the ID-keyed global-stat group from the loaded session or the
  app-wide snapshot feed. It updates existing cards in place. No new watcher,
  reload, save edit, automatic repair or title-selection advice is introduced.
- Build passed with zero errors and no new warnings. App appearance, narrow
  layouts, both themes and live progress still require the owner's check; the
  agent did not launch the app. Unknown acquisition routes remain explicit.


## Staged editor follow-up

The owner authorized account-wide changes on 2026-11-02. The compact panel now
uses checkboxes with hover text and screen-reader help. Enabling stages the part
in player KnownSpecials and account UnlockedSpecials, its linked account title,
and verified integer global-stat minimums. Higher counters remain untouched.
Disabling removes only the selected ID from observed account reward lists and
player specials/products; titles and statistics remain earned. Defaults cannot
be disabled. Unknown prerequisite shapes and restricted expedition contexts are
rejected before staging. Unknown acquisition routes do not invent counters.

The account companion joins title-bar Save/Reset; Save validates the original
account content and refuses conflicts with the separate Account Data editor.
Account backups precede writing. Slot and account writes are sequential, not a
cross-file transaction: a failure after slot saving explicitly leaves account
edits pending for retry. No real save/account file was written during development.
The panel reset restores only its changed entries and preserves later edits to
those counters. Unlock changes no longer recreate the vehicle inventory grids.

Core synthetic checks cover detached edits, duplicate special removal, minimum
thresholds, higher-counter preservation, unsupported prerequisite rejection,
float-counter rejection and selective Reset preserving later/unrelated counters.
UI appearance and actual in-game unlock persistence still require owner testing.


## Fresh Slot 1 / Slot 5 comparison

Read-only fresh decompression through SaveStreamProcessor.ExtractRawJsonAsync on
2026-11-02, after owner supplied both UI/game screenshots. Both members of each
slot pair agreed, and file timestamps were stable across each read:

| Counter | Slot 1 (save.hg / save2.hg) | Slot 5 (save9.hg / save10.hg) |
|---|---:|---:|
| CARGO_DESTROYED | 998 | 41 |
| CARGO_SCRAP | 571 | 14 |
| CARGO_EXP | 136 | 1 |
| CARGO_RAD | 119 | 1 |
| CARGO_TOX | 98 | 3 |
| EXO_SMASH | 2548 | 2828 |

All six investigated part IDs are absent from Slot 1 KnownSpecials (828 entries),
and present in Slot 5 KnownSpecials (891 entries). Neither KnownProducts list
contains these six. Shared account fyX/B1h/d4U contain none of these six IDs;
Bgb contains T_RECYCLE1 through T_RECYCLE8. Thus the observed availability
corresponds to per-save KnownSpecials membership, not shared account membership
alone. Slot 1 exceeds every declared requirement; Slot 5 is below the six
recycling thresholds despite owning the parts. This does not establish whether
Slot 5 received them through prior editing, migration, a different grant path,
or another mechanism. Do not infer its acquisition history from the counters.

The existing-title early exit remains a plausible explanation for why repeated
sorting does not repair Slot 1, not a proven root cause. Account-wide records
must not be described as guaranteeing availability or removal in every existing
save: those saves can retain their own KnownSpecials. No real data was changed.


## Owner Furnace save test and crash

Owner enabled Furnace on Slot 1 and clicked Save. Fresh post-crash read-only
comparison with the hash-verified pre-test snapshot showed:
- save2.hg: only vLc/6f=/24< changed, adding TRUCK_BODY03 (828 -> 829).
- accountdata.hg: only B89/fyX changed, adding TRUCK_BODY03 (663 -> 664).
- save.hg had additional baseline differences because the baseline slot pair
  had different saved timestamps/states; those differences cannot be attributed
  to the Furnace edit. The new Furnace ID is present in both written slot files.
- KnownTechnology (4kj) was identical to its own baseline in both slot files.
  No catalogue technology, including broken placeholders, was enabled.

crash.log at 2026-11-02 21:09:56 reports COMException 0x8001010E from
Application.Resources -> ThemeResources.Brush -> VehicleUnlockItemViewModel.Refresh
-> VehicleUnlockPanel.SessionChanged. SaveEditSession.CommitAsync runs under
Task.Run and raises EditsChanged on that worker after writing; the panel's
async-void event handler touched XAML directly. SessionChanged and SnapshotSeen
now dispatch onto the UI queue and check IsLoaded, matching CatalogReloaded.
The file comparison establishes persisted edits, not in-game acceptance.
Agent did not launch the app or write any real save/account data.
