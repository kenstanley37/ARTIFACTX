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

## Quick Start
*(Instructions for setting up local builds will be populated during Phase 5)*