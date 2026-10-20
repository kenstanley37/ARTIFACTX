# ArtifactX Issues Log & Gameplay Feedback

Synthesized from player gameplay notes (`ARTIFACTX_NOTES.odt`). Each item has been evaluated against current functionality, cross-referenced with existing GitHub issues, and articulated with clear problem statements and proposed technical solutions.

---

## 1. Already Completed / Resolved

The following items from the notes are already fully addressed in the codebase on branch `feature/planet-details-drilldown`:

| Note # | Original Note | Resolution in ArtifactX |
| :--- | :--- | :--- |
| **#4** | *PlanetDetails page missing from src; PlanetsPage too massive; need dedicated drill-down page.* | **Completed**: Split out `PlanetDetailsPage.xaml` and `PlanetDetailsPage.xaml.cs` into a dedicated drill-down page under `Views/ExplorePages/`. Removed ~400 lines of procedural UI from `PlanetsPage.xaml.cs`. |
| **#15** | *Make portal glyphs always on the bottom of cards so they look standard across all planets.* | **Completed**: Glyph strip is anchored at the base of every planet summary card on `PlanetsPage.xaml.cs`. |
| **#29** | *On details page, fish could be far right because it grows, and everything else to the left.* | **Completed**: `PlanetDetailsPage` now uses a multi-column body with the manual details editor, save facts, and bases in the left column, and the auto-sizing `FISH` card in the dedicated far-right column (460px width). |

---

## 2. Updates & Extensions to Existing GitHub Issues

These items directly extend existing open GitHub issues:

### Issue #18: Auto-capture system and planet details while you play
- **Note #1 & #5 — Improve System Image Capture Reliability & Heuristics**:
  - **Problem**: `ScreenCaptureLogService` frequently fails to detect the galaxy map or system view without visiting all planet details first and moving the mouse excessively.
  - **Proposed Solution**: Loosen mouse-movement diffing thresholds for system view frames and add direct visual anchor detection (e.g. system name banner or star icon) to trigger capture promptly upon opening the map.
- **Note #25 — Discoveries UI Parallax Tilt Compensation**:
  - **Problem**: In-game mouse parallax causes the Discoveries interface to tilt and shift, causing OCR bounding boxes to misalign.
  - **Proposed Solution**: Implement dynamic landmark anchoring (anchoring relative to the Discoveries header banner or glyph strip) rather than static screen coordinate offsets.

### Issue #15: Sharper 3D previews
- **Note #7 — Ship Designer Accent Paint Contrast & Lighting**:
  - **Problem**: Accent paint colors selected in the Ship Designer are washed out and barely noticeable in the preview window, despite looking vibrant in-game.
  - **Proposed Solution**: Adjust the 3D preview shader/diffuse lighting and ambient light values in `ShipPreview.cs` to increase specular/diffuse contrast on secondary color channels.

### Issue #9: Record which items each system sells & Issue #20: Trade routes
- **Note #8 — Space Station Vendor Inventory Scanning**:
  - **Problem**: System sales tracking is limited to Galactic Trade Terminals; station tech vendors, cartographers, and scrap dealers are uncataloged.
  - **Proposed Solution**: Extend the screenshot reader to parse technology vendor and cartographer stock lists, filing blueprint/upgrade inventories per system address.
- **Note #17 — Outlaw Station Contraband Vendor Support**:
  - **Problem**: Outlaw/Pirate systems lack trade terminals and trade through Contraband Agent NPCs.
  - **Proposed Solution**: Teach the trade data cataloger and OCR to recognize contraband dealer shop menus for trade route profit calculations.

### Issue #21: Base building limits: parts used vs the game's caps
- **Note #21 — Collapsible Categorized Bases Page (Freighter, Planets, Stations, Settlements)**:
  - **Problem**: BasesPage only lists planetary bases; freighter bases, claimed space stations, and settlements also consume building parts (`LooseObjectsPath`).
  - **Proposed Solution**: Group `BasesPage.xaml` into collapsible categories:
    1. *Freighter Base* (pinned at top)
    2. *Planetary Bases*
    3. *Claimed Space Stations & Outposts*
    4. *Settlements*
- **Note #23 — Base Object Cleaner / Reclaim Part Limits**:
  - **Problem**: Claiming space stations adds dozens of default interior objects to the save's loose objects table, consuming global build limits.
  - **Proposed Solution**: Add a "Clean / Delete Base Objects" button on each base/station card on `BasesPage` to purge loose objects (preserving the core terminal).

---

## 3. New Bug Reports

### Bug 1: Active System Reverts on Page Navigation
- **Notes #2 & #20**
- **Problem**: Navigating from "Current System" (`PlanetsPage`) to "Finds" or other navigation destinations causes the active system selection to revert to the previous system or an arbitrary system, triggering redundant "system changed" events and UI reloads.
- **Root Cause Area**: System address state synchronization between `PlanetsPage`, `FindsPage`, and `SaveSessionManager`.
- **Proposed Fix**: Centralize the active system selection into a shared service or view model so navigation preserves the selected system consistently.

### Bug 2: Player Location Coordinates Discrepancy on Finds Page
- **Note #3**
- **Problem**: The "Use My Location" filter/sort on `FindsPage` appears inaccurate or fails to update based on live in-game coordinates.
- **Proposed Fix**: Audit `FindsPage.xaml.cs` coordinate retrieval against `NmsPlanetCoordinates` and `LiveSaveWatcher.FromSession().Coordinates` to ensure fresh surface coordinates are used for distance calculations.

---

## 4. New Feature Requests

### Feature 1: Manual Fauna Management (Delete / Edit Incorrect OCR Scans)
- **Note #6**
- **Description**: Add delete and edit actions for fauna records stored in `user.db`.
- **Details**: When screenshot OCR misinterprets stylized NMS creature names or traits, players currently have no way to remove the record. Add an inline delete/edit button on creature cards.

### Feature 2: Support Procedural Curiosities & Expedition Items in Inventory Editor
- **Note #10**
- **Description**: Enable adding procedural items (e.g. Timeless Rings, excavated skeletons, historical documents, frigate expedition trade goods) to cargo inventories.
- **Details**: Currently only fixed catalog items appear in the item picker. Provide procedural item templates or manual ID/seed entry for curiosities and expedition rewards.

### Feature 3: Seed Placeholders for Unvisited System Planets
- **Notes #11 & #13**
- **Description**: Populate system planets using seed/system JSON placeholders prior to landing or analysis visor scans.
- **Details**: Freighter planetary probes and arrival in a system record base planet records without full discovery JSON. Use `UniverseInfo.GeneratedPlanetName` and known seeds to display all planets immediately, seamlessly merging OCR/discovery details as the player explores.

### Feature 4: Full Fauna Slot Placeholders (e.g., 0/11 Species Cards)
- **Note #14**
- **Description**: Show placeholder cards for all species on a planet based on `FaunaTotal`.
- **Details**: If a planet is known to have 11 species, render 11 slot cards on `PlanetDetailsPage` (scanned ones populated with photos and traits, remaining slots displayed as "Undiscovered Species"), making it clear how many scans remain.

### Feature 5: Dedicated NavigationService Architecture
- **Note #16**
- **Description**: Decouple frame navigation, breadcrumbs, and page parameter passing out of `MainWindow.xaml.cs`.
- **Details**: Extract a centralized `NavigationService` to manage page transitions, parameter propagation (e.g. passing `KnownPlanet` to `PlanetDetailsPage`), and history stack management.

### Feature 6: Categorize Standard vs. Pirate (X-Class) Tech in Install Menus
- **Note #18**
- **Description**: Distinguish legitimate space station technology modules (C, B, A, S class) from Outlaw station black market modules (X-class Suspicious tech) in installation dialogs.
- **Details**: Add visual badging and category filters to avoid accidental installation of illegal/unpredictable pirate upgrades.

### Feature 7: Consolidate / Deduplicate Stacks Across All Inventory Containers
- **Note #19**
- **Description**: Add a one-click "Deduplicate Stacks" action across all inventories.
- **Details**: Scans Exosuit, Starship, Freighter, and Base Storage containers, automatically merging partial stacks of the same substance or product up to maximum stack sizes.

### Feature 8: Inventory Sorting: Sort by Category/Type Then by Name
- **Note #22**
- **Description**: Update inventory auto-sort routines to group items primarily by category (Elements, Minerals, Technology, Consumables, Curiosities, Trade Goods) and secondarily alphabetically.

### Feature 9: Research Procedural Planet Naming Divergence
- **Note #24**
- **Description**: Investigate discrepancies where `UniverseInfo.GeneratedPlanetName` differs from in-game naming (e.g. "Woodlari VIII" vs. in-game "Woodlari Prime").
- **Details**: Verify whether the Worlds Part 1/2 update modified seed generation hash tables or roman numeral suffix conventions.

### Feature 10: Manual "Capture Screenshot Now" Button for OCR Reader
- **Note #26**
- **Description**: Add a manual camera/capture button in the ArtifactX title bar or page headers to force immediate OCR parsing when auto-detection does not trigger.

### Feature 11: Display `user.db` Database Size in UI
- **Note #27**
- **Description**: Show local database storage footprint (e.g. `user.db: 4.2 MB`) in Settings and/or the status bar to give players visibility into screenshot/discovery cache growth.

### Feature 12: Dual Planet Classification Display (Biome Group + Specific Descriptor)
- **Note #28**
- **Description**: Display both the broad biome group and the specific planetary descriptor on planet pages (e.g. *"Crimson Planet (Red / Exotic)"*).

### Feature 13: Mission Target Tracking: Industrial Cleanup
- **Note #30**
- **Description**: Track planets containing mission targets for the ongoing "Industrial Cleanup" mission and display live completion metrics from player save milestones.

### Feature 14: Customizable Player Dashboard with Modular Widgets
- **Note #31**
- **Description**: Provide a customizable home dashboard page with drag-and-drop or selectable widgets (current system telemetry, active fishing goals, global build limit tracker, coordinate clipboard, active missions).

### Feature 15: Custom Theme Creator & Palette Editor
- **Note #32**
- **Description** *(Low Priority)*: Enable players to create, save, and share custom color themes in Settings by editing primary theme brushes (accent, panel background, text, borders).

---

## 5. Architectural Standards (from Code Review Notes)

1. **Domain Directory Grouping**:
   - Organize WinUI 3 views and view models into logical domain folders:
     - `Views/ExplorePages/` (`PlanetsPage`, `PlanetDetailsPage`, `FindsPage`, `BasesPage`, `NearCorePage`, `FishingPage`)
     - `Views/InventoryPages/` (`ExosuitPage`, `StarshipPage`, `FreighterPage`, `BaseStoragePage`, `ExocraftPage`)
     - `Views/CraftingPages/` (`CraftingPage`, `CataloguePage`, `RecipePage`)
2. **Componentization First**:
   - Favor focused custom controls and shared services over monolithic page code-behind files.
3. **Stepwise Refactoring**:
   - Always perform architectural refactoring in discrete, verifiable stages on dedicated feature branches with zero-error build verification before merging.
