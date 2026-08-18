# No Man's Sky Save Editor (NMS-SE)

A modern, open-source save editor for No Man's Sky, built with .NET 10 and WinUI 3.

## Features

- **Real-time, in-memory editing:** Save data is parsed once into a live JSON
  tree; every page reads and stages edits against that same shared session,
  so changes made on one screen are instantly visible everywhere else.
- **Nothing touches disk until you say so:** Edits are staged in memory and
  only written to your actual save files when you click Save — with a Reset
  available at both the per-page and global level.
- **Verified write path:** Confirmed byte-for-byte identical to the game's
  own save format on an unmodified round-trip, and confirmed in-game that
  edited values (currency, inventory slots) load correctly after saving.
- **Game-running protection:** Detects when No Man's Sky is actively running
  and disables saving to prevent conflicting writes; also detects if the
  save changed externally (e.g. an autosave) while the app had it open.
- **Real item names and icons:** Item data extracted from your own local
  game files via the included cataloging tool — see
  [docs/DATACATALOGER.md](docs/DATACATALOGER.md).
- **Game-authentic dark UI:** Built with WinUI 3, styled to resemble the
  in-game interface so editing feels familiar rather than like reading raw
  JSON.

## Architecture Overview

The solution is split into a few decoupled layers:

1. **`ArtifactX.Core`** — Save file container format (LZ4 block decompression and
   the matching writer), plus the schema/path-constant classes documenting
   what each obfuscated JSON key means.
2. **`ArtifactX.WinUI3`** — The application itself: pages, view models, the
   `SaveSessionManager` singleton that holds the active save's parsed tree
   and staged edits, and the reusable UI controls (like the inventory slot
   grid) shared across Exosuit/Starship/Multi-Tool/Freighter.
3. **`ArtifactX.Tools.DataCataloger`** — A separate CLI tool that extracts item
   names and icons from your local game files into a SQLite database, which
   the main app reads read-only at runtime.
4. **`ArtifactX.Tools.SaveInspector`** — A CLI tool for investigating unfamiliar
   save file formats (hex dumps, round-trip tests, targeted field edits)
   without needing the full app.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full technical
write-up, including the reasoning behind the container format, the
generic-tree editing approach, and what's confirmed vs. still unverified.

## ⚠️ Platform Support Status

This project has been developed and verified against **Steam (PC)** saves
only so far — including a full round-trip test (decompress → edit a live
value → recompress → load in-game successfully, with the `mf_*` manifest
left untouched).

| Platform | Save Files | Status |
|---|---|---|
| Steam (PC) | `save*.hg` + `mf_save*.hg` | ✅ Verified — read, edit, and write path confirmed against the live game |
| GOG (PC) | `save*.hg` + `mf_save*.hg` | ⚠️ Untested — same file pattern as Steam, likely compatible, not confirmed |
| Microsoft Store / Xbox (PC) | Opaque `wgs` container | ❌ Untested — different, opaque save container; folder can be located but contents haven't been decoded |
| PlayStation | `savedata*.hg` | ❌ Not yet supported |
| Nintendo Switch | `manifest*.dat` | ❌ Not yet supported |

If you can test on a platform other than Steam, or want to help decode one
of the untested formats, see `CONTRIBUTING.md` — this is one of the most
valuable ways to contribute right now.

## Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 (17.12+) or later, with the **Windows App SDK** /
  **WinUI application development** workload installed
- Windows 10/11 (WinUI 3 is Windows-only)

### Build and run the editor
```bash
git clone https://github.com/kenstanley37/NMS-Save-Editor.git
cd NMS-Save-Editor
```
Open `NmsSaveEditor.slnx` in Visual Studio, set `ArtifactX.WinUI3` as the startup
project, and run. On first launch, it auto-detects Steam/GOG/Microsoft Store
save folders, or you can browse for your own.

### (Recommended) Generate real item names and icons
Without this step, the app still works, but items display as raw internal
ids (e.g. `ASTEROID2`) instead of real names (`Gold`). To fix that:
```bash
cd src/ArtifactX.Tools/ArtifactX.Tools.DataCataloger
dotnet run                # full extraction, ~15 minutes, one-time
dotnet run -- trim        # produces a small, ship-ready catalog file
```
Then copy the resulting `Working/nms_catalog_dist.sqlite` to:

<ArtifactX.WinUI3 build output folder>/Data/nms_catalog.sqlite

Full details in [docs/DATACATALOGER.md](docs/DATACATALOGER.md).

## Contributing

Contributions are welcome — especially testing on GOG, Xbox, or console
platforms (see the support table above), and help mapping more of the save
file's structure (`docs/ARCHITECTURE.md` documents what's confirmed so
far). See `CONTRIBUTING.md` for how to get started.