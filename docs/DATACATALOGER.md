# DataCataloger

`NMS.Tools.DataCataloger` extracts item names and icons from your own local
copy of No Man's Sky's game files (`.pak`/`.MBIN`), and writes them to a
SQLite database that `NMS.WinUI3` reads at runtime to show real item names
and icons instead of raw obfuscated ids like `ASTEROID2` or `JET1`.

## Two-step process

### 1. Full build (slow - ~15 minutes)

DataCataloger.exe

On first run, it asks for the path to your NMS `PCBANKS` folder (inside your
game install directory) and remembers it for next time. It scans every PAK
file, classifies every table-shaped MBIN it finds, resolves localized names,
extracts and converts icon textures to PNG, and writes everything to
`Working/nms_catalog.sqlite` next to the exe.

This is a **working/dev artifact**, not something to ship. Out of roughly
1.25 million extracted rows, only ~4,500 have an actual display name - the
rest are internal/duplicate table entries with no name at all, present
because the extraction has to walk every row to find the ones that matter.

### 2. Trim to a distribution copy (fast - seconds)

DataCataloger.exe trim

Reads the working catalog and writes `Working/nms_catalog_dist.sqlite`,
containing only items with a real name, plus the icons and categories those
items actually reference. Everything else (LocalizedTexts, ~1.24M unnamed
rows) is dropped.

Confirmed result on a real extraction: **194 MB → 85.5 MB** (~56% smaller).

## Wiring the result into the app

Copy `nms_catalog_dist.sqlite` into `NMS.WinUI3`'s output directory as:

<app folder>/Data/nms_catalog.sqlite

`CatalogService` looks for it at exactly that relative path. If it's not
found, the app doesn't crash or block - it just falls back to showing raw
ids, the same behavior as before this feature existed.

## Why GameId lookups filter on `NameEnglish IS NOT NULL`

`GameId` is **not unique** in the catalog - e.g. `ENERGY` has 20 rows across
different source tables, only one of which has a real display name (the
rest are internal variants). Confirmed empirically: no `GameId` in the
catalog ever has two *different* non-null names, so
`WHERE GameId = ? AND NameEnglish IS NOT NULL` is a safe, unambiguous
lookup - never guess a matching row without that filter.

## A note on distributing extracted game assets

The trimmed catalog contains icon images and item names extracted from
Hello Games' own game files. This project bundles that data with the app
(the same approach used by other established community NMS save-editing
tools) rather than requiring every user to run a 15-minute local extraction
on first launch. This is used solely to help players identify items within
their own save data - not redistributed as standalone game assets. If you
fork or redistribute this project, you're responsible for your own
judgment call on this.

## Removability

The bundled catalog is entirely optional at runtime. `CatalogService` checks
for `Data/nms_catalog.sqlite` at startup; if it's missing, every lookup
returns nothing and the app falls back cleanly to showing raw item ids -
exactly how it behaved before this feature existed. If this data ever needs
to be pulled from a release (e.g. at Hello Games' request), that's a
one-file removal from the installer, not a code change.