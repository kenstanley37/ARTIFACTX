# Catalog publication and stale WAL state

Owner report, 2026-10-31: Update game data returned after a completed update.
The build log ended successfully at 07:59 local, with 22,005 items. Read-only
inspection of the installed catalog showed different metadata through SQLite's
normal reader and a main-file-only immutable reader:

- Normal: format 51, built 2026-10-29, 22,004 items.
- Main file: format 52, built 2026-10-31T11:59:06Z, 22,005 items.

GameDataBuilder previously moved a completed database over the destination file
while CatalogService still had the destination open. Destination WAL/SHM state
survived the replacement, so ordinary readers saw old metadata. Clearing pooled
connections does not close active catalog connections.

CatalogPublication now uses SQLite's backup API to publish through a transaction
instead of replacing a live database file. This incorporates the source WAL and
updates the destination using SQLite's own locking and journaling. No manual
catalog or user database repairs were made.

The GameData checks use synthetic databases with an open destination reader and
uncheckpointed WAL data on both databases. After publication both the existing
connection and a fresh connection see the new value. A deferred reader transaction
retains its snapshot during publication and sees the next version after commit.
The isolated WinUI build passed with zero errors. App testing remains with
the owner; restart the rebuilt app and run the update once to replace old state.
