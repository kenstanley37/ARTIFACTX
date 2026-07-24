# No Man's Sky Save Editor (NMS-SE)

A high-performance, modern save editor for No Man's Sky built using .NET 9, Entity Framework Core, and WinUI 3.

## Features
* **Stateless Parsing Layer:** High-speed processing using `System.Text.Json`.
* **SQLite Cache Engine:** Relational database mapping to prevent in-memory JSON bloat.
* **Differential Patching:** Minimizes save corruption by altering only modified bytes/nodes.
* **Modern Interface:** Built with WinUI 3 featuring a game-authentic dark aesthetic.

## Architecture Overview
The solution is divided into three distinct decoupled layers:
1. `NmsSaveEditor.Core`: Parsing logic, file mapping, and diff engines.
2. `NmsSaveEditor.Data`: SQLite mapping and Entity Framework Core entities.
3. `NmsSaveEditor.UI`: WinUI 3 presentation layer.

## ⚠️ Platform Support Status

This project has been developed and verified against **Steam (PC)** saves only so far — including a full round-trip test (decompress → edit a live value → recompress → load in-game successfully, with the `mf_*` manifest left untouched).

| Platform | Save Files | Status |
|---|---|---|
| Steam (PC) | `save*.hg` + `mf_save*.hg` | ✅ Verified — read, edit, and write path confirmed against the live game |
| GOG (PC) | `save*.hg` + `mf_save*.hg` | ⚠️ Untested — same file pattern as Steam, likely compatible, not confirmed |
| Microsoft Store / Xbox (PC) | Opaque `wgs` container | ❌ Untested — different, opaque save container; folder can be located but contents haven't been decoded |
| PlayStation | `savedata*.hg` | ❌ Not yet supported |
| Nintendo Switch | `manifest*.dat` | ❌ Not yet supported |

If you can test on a platform other than Steam, or want to help decode one of the untested formats, see `CONTRIBUTING.md` — this is one of the most valuable ways to contribute right now.


1## Quick Start
*(Instructions for setting up local builds will be populated during Phase 5)*