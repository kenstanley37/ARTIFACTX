# Catalog legacy-header null reference

The owner reported a null reference in `DeserializeBinaryTemplate(reader, string)`
while rebuilding game data and opening Current System (2026-10-31). The on-disk
crash log did not contain this event; the full debugger stack was unavailable.

A standalone main catalog pass reproduced the failing null-name path on
`metadata/inputtest.mbin` in `NMSARC.MetadataEtc.pak`. Direct inspection found
magic `00000000CCCCCCCC`, version `10706`, and apparent current-header name hash
`0000002E`. `MBINHeader.IsValid` is false. `MBINFile.GetData` previously passed
`GetXMLTemplateName()`'s null result into `StartsWith`, causing the reported
exception. The valid-header ship and planet extraction paths passed separately.
This establishes a reproducible catalog failure matching the report; it does
not establish a navigation concurrency fault.

The catalog now checks Load and IsValid before decoding, logging and skipping
incompatible headers. libMBIN also guards missing root names, preserves unknown
root hashes in its diagnostics, rejects unknown nonempty generic components
without passing null names, and retains empty component placeholders.

Checks: `tests/ArtifactX.GameData.Checks` uses synthetic binary records for the
missing-name, unknown root/hash, generic component and empty-placeholder paths.
The fixed main catalog pass against installed game archives wrote 90 categories
and 24,956 items in a scratch database, with inputtest skipped and no decode
failures. The installed catalog, user database and game saves were not changed.
App navigation and a rebuild under the VS debugger remain owner testing.

Isolated WinUI build passed with zero errors.
