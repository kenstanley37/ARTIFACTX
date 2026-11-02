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
  instructions using all English language files. It is not yet wired into the
  app's catalog build or a page; no catalog format bump needed at this checkpoint.
- `--title-unlocks-catalog-check` checks six Colossus references against current
  installed metadata. Passed on 184271, with no generated data committed.
- Next: extract vehicle/part presentation associations, map actual ownership
  lists without conflating eligibility, and add a reusable read-only unlock panel.
  No Exocraft UI change yet. Native grant discrepancy must remain explicit in
  development notes and must not turn into false unlocked badges.
