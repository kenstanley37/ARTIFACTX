# Contributing Guidelines

Thank you for helping improve the NMS Save Editor! 

## Branching Strategy
* `main`: Production-ready, stable releases.
* `develop`: Active integration branch for upcoming features.
* `feature/*`: Specific feature branches carved off `develop`.

## Code Style Requirements
* Follow official Microsoft .NET coding conventions.
* Use meaningful, strongly-typed naming conventions for game state properties.
* All asynchronous methods must append the `Async` suffix and accept `CancellationToken` where appropriate.

## Platform Testing Needed

The save-reading and save-writing pipeline (`NMS.Core.SaveStreamProcessor`) has only been verified against **Steam (PC)** saves. If you play on GOG, Xbox/Microsoft Store, PlayStation, or Switch, testing and reports are extremely valuable — this is currently the single biggest gap in the project.

**How to help, safely:**
1. Use the `NMS.Tools.SaveInspector` CLI (`src/NMS.Tools/NMS.Tools.SaveInspector`) to inspect your save files — it's read-only and never modifies your originals.
   - `SaveInspector inspect <path> --full` dumps a file's raw bytes and checks whether it matches the known container magic number.
   - `SaveInspector roundtrip <input> <output>` decompresses and recompresses a file unmodified — a good first check that the container format matches what Steam uses.
2. **Always work from a backup**, never your live save folder directly.
3. Open an issue with what you find — even "this doesn't match the Steam format at all" is useful, since it tells us a platform needs its own container-parsing path rather than reusing the Steam one.
4. If you get as far as a working round-trip, the next step (matching what we did for Steam) is a real in-game load test: edit one known value (e.g. Units), leave any manifest/index files untouched, and confirm the game still loads the save correctly.

Please don't assume another platform's format matches Steam's without testing it end-to-end — we found real surprises even within Steam saves (see `mf_save.hg` manifest investigation in the project history) that only showed up once we actually inspected real files.

## Pull Request Process
1. Fork the repo and create your branch from `develop`.
2. Ensure all local tests pass (Phase 5 framework).
3. Submit a PR targeting `develop` with a clear breakdown of changes.