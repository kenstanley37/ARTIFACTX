# ArtifactX Issues Log & Gameplay Feedback

Tracks player gameplay feedback, research gaps and planned features. Sources include `ARTIFACTX_NOTES.odt` and `ARTIFACTX_MAUI-10-26-2026.odt` (notes dated 2026-10-26, incorporated 2026-10-27). Existing GitHub links are retained; newly logged local entries do not imply a GitHub issue has been created. User reports and planned behavior are distinguished from verified implementations.

---

## 1. Already Completed / Resolved

The following items from the notes are already fully addressed in the codebase on branch `feature/planet-details-drilldown`:

| Note # | Original Note | Resolution in ArtifactX |
| :--- | :--- | :--- |
| **#4** | *PlanetDetails page missing from src; PlanetsPage too massive; need dedicated drill-down page.* | **Completed**: Split out `PlanetDetailsPage.xaml` and `PlanetDetailsPage.xaml.cs` into a dedicated drill-down page under `Views/ExplorePages/`. Removed ~400 lines of procedural UI from `PlanetsPage.xaml.cs`. |
| **#15** | *Make portal glyphs always on the bottom of cards so they look standard across all planets.* | **Completed**: Glyph strip is anchored at the base of every planet summary card on `PlanetsPage.xaml.cs`. |
| **#29** | *On details page, fish could be far right because it grows, and everything else to the left.* | **Completed**: `PlanetDetailsPage` now uses a multi-column body with the manual details editor, save facts, and bases in the left column, and the auto-sizing `FISH` card in the dedicated far-right column (460px width). |
| **#10** | *Support procedural curiosities & expedition items in inventory editor.* | **Completed**: Added `ProceduralProducts` to cargo grids, enhanced item search with relevance ranking, automatic 5-digit seed generation (`#NNNNN`) and full icon caching directly in the standard "Add item..." search picker, plus an in-slot "Re-roll seed" context action on branch `feature/procedural-items-editor`. |
| **#27** | *Show user.db database size in UI.* | **Completed**: Added database size calculation (including WAL breakdown), schema version, full file path, and an "Open data folder" button directly to the ArtifactX Data card on `SettingsPage` on branch `feature/user-db-size-display`. |
| **#22** | *Inventory sorting: sort by category/type then by name.* | **Completed**: Enhanced `InventoryGridViewModel.SortItems` with category taxonomy (Technology, Raw Materials/Substances, Crafted Components, Curiosities & Artifacts, Consumables/Navigation, Cooking & Edibles, Trade Goods, Construction Parts) and secondary A-Z alphabetical sorting on branch `feature/inventory-category-sorting`. |
| **#26** | *Manual "Capture Screenshot Now" button for OCR reader.* | **Completed**: Added contextual manual capture buttons to `SystemViewFrame` and `CURRENT SYSTEM` header on `PlanetsPage`, and inline on creature cards + fauna header on `PlanetDetailsPage`. Contextual buttons serve as true manual overrides that bypass OCR detection heuristics to unconditionally crop and save system and creature pictures with offline fallback and immediate card refresh on branch `feature/manual-capture-button`. |
| **#28** | *Dual planet classification display (Biome Group + Specific Descriptor).* | **Completed**: Added Schema Migration 20 to `user.db` adding `descriptor` column to `planet_details`. Integrated descriptor extraction from Discoveries pop-ups (`BodyKindsService`), flight cards, visor scans, and full planet pages (`PlanetPageReader`), with clean fallback to broad biome group (`NmsPlanets.BiomeGroup` / `FormatClassification`, e.g. *Crimson Planet (Red / Exotic)* or *Paradise Planet (Lush)*), manual descriptor viewing & editing in `PlanetDetailsPage`, and dual classification display across `PlanetsPage`, `PlanetDetailsPage`, `BasesPage`, `FindsPage`, and `FishingPage` on branch `feature/dual-planet-classification`. |
| **#21** | *Collapsible categorized bases page (Freighter, Planets, Stations, Settlements, Placed Equipment), galactic regions, and BaseDetailsPage drill-down.* | **Completed**: Grouped `BasesPage.xaml` into 5 collapsible categories: 1) Freighter Base (pinned at top), 2) Planetary Bases, 3) Claimed Space Stations & Outposts, 4) Settlements, and 5) Placed Equipment (No Base) to track standalone refiners, save points, and beacons counting toward the 21,000 construction cap. Resolved page transition lag with instant visual navigation and background `Task.Run` processing. Added galactic region names to location telemetry (`UniverseInfo.LocationWithRegion`). Decoupled itemized building parts into `BaseDetailsPage.xaml` with comprehensive icons and styled fallback glyph badges for all station and base components on branch `feature/collapsible-bases`. |

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
- **Details**: Scans Exosuit, Starship, Freighter, and Base Storage containers, automatically merging partial stacks of the same substance or product up to maximum stack sizes. See Feature 34 for consistent per-container coverage and the follow-up from the 2026-10-26 notes.

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

### Feature 12: Dual Planet Classification Display (Biome Group + Specific Descriptor) [Resolved]
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

### Feature 16: Space Station & Outlaw Station Vendor Inventory Scanning
- **Description**: Add screen reader parsing and database tracking for three key specialized station vendors:
  1. **Space Station Scrap Dealer** (e.g. *Righteous Thief Dekoureut - Vy'keen Scrap Dealer*):
     - Sells: Suspicious Packets (Arms, Tech, Goods), Emergency Signal Scanners, Repair Kits.
     - Currency: **Tainted Metal** (traded from derelict freighters).
     - Goal: Catalog scrap dealer inventory offerings per system address.
  2. **Outlaw Station Salvage Dealer** (e.g. *Salvage Dealer Inpudarz - Aftermarket Upgrades*):
     - Sells: Aftermarket/black market X-class technology upgrade modules (Hyperdrive, Pulse Engine, Deflector Shield, Boltcaster, Mining Beam, Hazard Protection, etc.).
     - Currency: **Nanites**.
     - Goal: Record which X-class upgrade module types are stocked at each pirate/outlaw station.
  3. **Outlaw Station Contraband Vendor** (*Contraband Agent*):
     - Sells: High-value illegal trade commodities (GekNip, First Spawn Relics, Moon Ether, Prismatic Feathers, Banned Weapons, etc.) and Suspicious Packets.
     - Currency: **Units**.
     - Goal: Integrate contraband price and availability data with galactic trade routes and smuggling profit calculations.

### Feature 17: Exact Procedural Multi-Tool and Crashed Starship Locations and Details
- **Related notes: Original requests #5 and #6; clarified by the player on 2026-10-24.**
- **Description**: Reverse-engineer the game's procedural data and generation rules so ArtifactX can calculate the exact multi-tools and crashed starships available at each location on every planet and show them on Planet Details. This is an app-generated reference, not a player-submitted Finds workflow.
- **Required details**: the exact planet and location/building (for example, each Minor Settlement cabinet or Sentinel Pillar), XYZ coordinates where the game defines them, tool name and image, class, stats, and all other identifying details the game data can provide.
- **Data quality rule**: Do not show possible tools, candidate pools, odds, or predictions as the actual tool at a location. Each displayed tool must be the exact result of the decoded game rules/data for that location. Where the game does not define a value such as fixed coordinates, show that honestly rather than inventing one.
- **Goal**: Users can look up exact multi-tool locations for any system without needing the Finds page to supply the tool data.

---

- **2026-10-24 clarification**: Planet Details must omit equipment pools, odds, hypothetical S-Class stats and unlocated building possibilities until actual generated items, their actual class/stats and precise site coordinates are resolved. Saved site markers alone do not establish their equipment contents. This is procedural lookup for any system, without requiring Finds.
- **Station reference examples (owner screenshots)**: Geometric Angle Focuser, C class, displayed damage potential 333.3 / scanner range 209.8; Mass Needler Z-C11, B class, 443.3 / 252.3. Owner bought Mass Needler on DELETEME for read-only save inspection. Station shown as Lukinog III Rendezvous, Gek, Research // Advanced, At War. Do not assume a seed or pool mapping from screenshots; match the purchased record before deriving rules. Displayed equipped stats must be distinguished from intrinsic bonuses.

### Feature 18: Squadron Pilots, Editing and 3D Ships

- **GitHub**: [Issue #25](https://github.com/kenstanley37/ARTIFACTX/issues/25), expanded 2026-10-24 using the owner's four Squadron Pilots screenshots.
- Show each ship with its correctly associated pilot: name/title, race, S/A/B/C rating, actual skill labels and ratings, confirmed kills and notes where available.
- Allow editing pilot and ship information through the real saved fields or procedural inputs. Research generated values so edits produce the intended in-game effect.
- Reuse the Starships page's actual 3D ship preview capability.
- Design around the four squadron slots, using the game's compact 2-by-2 ship selection layout and selected pilot/ship details area. Show empty slots clearly.

### Feature 19: Storage Hangar

- **GitHub**: [Issue #49](https://github.com/kenstanley37/ARTIFACTX/issues/49), requested 2026-10-24 with two in-game Storage Hangar screenshots.
- Add a dedicated page for archived starships, with names, class ratings, available details and a 3D view of the selected ship.
- Provide Restore ship to return an archived ship to the active fleet, respecting capacity and preserving the game's stored ship/technology data.
- Provide Delete ship to remove an archived ship using the normal staged edit and confirmation flow.
- The page contains stored ships only. Active Starships remain on the existing Starships page.
- Verify the archive schema and restoration/deletion transitions with controlled test-save changes. The game's description says technology is preserved while cargo is removed when a ship is archived.

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
---

## 6. Latest Player Notes (2026-10-25)

Synthesized from the latest `ARTIFACTX_NOTES.txt`. Cross-referenced with open GitHub issues (see links). All items now have a tracking GitHub issue.

### Feature 20: Race Language Word Tracking Accuracy
- **GitHub**: [Issue #31](https://github.com/kenstanley37/ARTIFACTX/issues/31) (bug)
- **Note #1**
- **Problem**: The app reports that Gek (and possibly other race) words remain learnable, but in-game the player can no longer learn them. Word-count / learnability state in ArtifactX is out of sync with the game.
- **Proposed Solution**: Audit language-word tracking against a controlled in-game diff (learn words, save, compare) and reconcile the "remaining words" calculation with the game's actual learnable-word state. Reuse the existing language-words page and `NmsLanguageWords` paths.

### Feature 21: Expedition Rewards Editor — Include Latest Expedition
- **GitHub**: [Issue #32](https://github.com/kenstanley37/ARTIFACTX/issues/32)
- **Note #2**
- **Problem**: The expedition rewards editor does not list the most recent expedition, so players cannot edit rewards for it.
- **Proposed Solution**: Add the latest expedition's reward entries to the expedition rewards data source/catalog and editor list; verify against the current save's expedition history.

### Feature 22: Offline AI-Generated Corvettes
- **GitHub**: [Issue #38](https://github.com/kenstanley37/ARTIFACTX/issues/38) (long-term)
- **Note #3**
- **Problem**: Players want to generate Corvette (and other) ship builds from a text spec (e.g. "Star Wars Falcon") without an online service.
- **Proposed Solution**: Long-term: train/run an offline model that produces ship part layouts from user specs. Requires research into feasible local model sizes and the Corvette save format (see Feature 19 / archived-ship schema work).

### Feature 23: Always Show Player's Current In-Game Location
- **GitHub**: [Issue #33](https://github.com/kenstanley37/ARTIFACTX/issues/33)
- **Note #4**
- **Problem**: The player's current location (galaxy, region, system, planet/space station/freighter) is not consistently visible across the app.
- **Proposed Solution**: Surface the live location (from `LiveSaveWatcher` / `UniverseInfo`) in the title bar / a persistent status area on every page, including "On Freighter", "On Planet", station name, galaxy and region.

### Feature 24: Planet Details — Exact Multi-Tool (and Ship) Locations and Details
- **GitHub**: [Issue #35](https://github.com/kenstanley37/ARTIFACTX/issues/35) (also #34)
- **Note #5**
- **Problem**: Planet Details does not yet show every multi-tool location with its full details (XYZ coords, stats, image, class, etc.), nor equivalent details for ships.
- **Proposed Solution**: Extend the procedural multi-tool/ship location research (Feature 17) so Planet Details renders each exact generated item with coordinates, class, stats and image, and the same for crashed ships. No candidate pools/odds — only decoded exact results.

### Feature 25: Planet Details — Greyed Placeholders for Undiscovered Fauna, Flora and Minerals
- **GitHub**: [Issue #36](https://github.com/kenstanley37/ARTIFACTX/issues/36)
- **Note #6**
- **Problem**: Creatures, flora and minerals are not shown until discovered, so the player cannot see how many remain.
- **Proposed Solution**: Render all expected species/flora/minerals as greyed-out placeholder cards on Planet Details (extending Feature 4's fauna slot placeholders to flora and minerals), filling in colour once discovered.

### Feature 26: Current System — Likely NPC Starships and Encounter Odds
- **GitHub**: [Issue #37](https://github.com/kenstanley37/ARTIFACTX/issues/37)
- **Note #7**
- **Problem**: The Current System page does not show which NPC-driven ships can land in the system and their encounter chances.
- **Proposed Solution**: Add a section listing possible NPC ships with their % chance to appear, derived from system data (research required).

### Feature 27: Alliance Tracking
- **GitHub**: [Issue #29](https://github.com/kenstanley37/ARTIFACTX/issues/29)
- **Note #8**
- **Problem**: No tracking of alliance members, individual contributions, or overall totals.
- **Proposed Solution**: Research the NMS.exe save structure for alliance data (may require reverse engineering), then surface members, per-member contributions and totals in a dedicated page.

### Feature 28: OBS Streaming Overlay
- **GitHub**: [Issue #39](https://github.com/kenstanley37/ARTIFACTX/issues/39)
- **Note #9**
- **Problem**: Streamers cannot show live game details (portal glyphs, XYZ coords, planet, ship, freighter, station, galaxy/region/system) in OBS.
- **Proposed Solution**: Provide a configurable overlay module whose visible fields the user can pick (glyphs, coords, planet, starship, freighter, station, galaxy, region, system).

### Feature 29: Discord Bot as Background Worker in the API
- **GitHub**: [Issue #40](https://github.com/kenstanley37/ARTIFACTX/issues/40)
- **Note #10**
- **Problem**: Discussion needed on a Discord bot running as a background worker in the ArtifactX API.
- **Proposed Solution**: Design discussion first; then implement bot commands surfacing live game data via the API.

### Feature 30: Code Signing Research
- **GitHub**: [Issue #50](https://github.com/kenstanley37/ARTIFACTX/issues/50)
- **Note #11**
- **Problem**: Need the cheapest and best method for code signing releases.
- **Proposed Solution**: Research free/low-cost options (SignPath.io OSS program is already deferred in project memory) and document a recommendation.

### Feature 31: Auto-Explore (Automated Region Mapping)
- **GitHub**: [Issue #43](https://github.com/kenstanley37/ARTIFACTX/issues/43) (long-term)
- **Note #12**
- **Problem**: Manual region mapping is tedious.
- **Proposed Solution**: Automate: warp into a system, scan it, upload results, warp to the next system, repeating across the region (long-term).

### Feature 32: Star Discovery Testing
- **GitHub**: [Issue #16](https://github.com/kenstanley37/ARTIFACTX/issues/16)
- **Note #13**
- **Problem**: Star discovery behaviour in saves is not yet understood.
- **Proposed Solution**: Controlled test: back up save, fly to and discover a star, save, then diff the save files to identify what changed. Document findings.

### Feature 33: Group Storage Pages into a Single "Cargo" Page
- **GitHub**: [Issue #51](https://github.com/kenstanley37/ARTIFACTX/issues/51)
- **Note #14**
- **Problem**: The left nav is growing too large; storage pages (cold storage, base storage, ingredient storage, corvette cache, etc.) are scattered.
- **Proposed Solution**: Consolidate all storage-type pages into one "Cargo" page listing the cargo types. Freighter, Starships, Exosuit and Multi-Tool remain separate. Load each cargo inventory only when the user selects it (reuse existing UserControl).

### Feature 34: Sort and Merge Duplicates on All Inventory Pages
- **GitHub**: [Issue #52](https://github.com/kenstanley37/ARTIFACTX/issues/52)
- **Note #15**
- **Problem**: Inventory pages lack a consistent sort + "merge duplicates" action.
- **Proposed Solution**: Add a sort and merge-duplicates button to every inventory-type page, building on Feature 7 (deduplicate stacks) and Feature 8 (category sort).
- **2026-10-26 notes update**: Provide a clearly named "Merge All" action on every inventory/cargo page and container that supports stackable items. Audit all container types rather than limiting this to the main equipment pages; unsupported technology-only inventories should not offer a meaningless merge action.
- **Implementation / validation**: Reuse shared stack-merging rules and inventory controls; stage edits through `SaveSessionManager.StageEdit`. Preserve item metadata and total quantities, respect each container's stack limits, and leave incompatible items separate. Verify partial stacks, full stacks, empty containers and containers with no mergeable items. This extends existing Features 7/34 and GitHub issue #52; it is not a new duplicate request.

### Feature 35: Cold Storage — Search by Rarity and Show Rarity on Each Fish
- **GitHub**: [Issue #54](https://github.com/kenstanley37/ARTIFACTX/issues/54)
- **Note #16**
- **Problem**: Cold Storage cannot be filtered by fish rarity, and rarity is not shown per fish.
- **Proposed Solution**: Add rarity-type search/filter to Cold Storage and display each fish's rarity.

### Feature 36: Fishing Page — Planet-Type-First Ordering
- **GitHub**: [Issue #53](https://github.com/kenstanley37/ARTIFACTX/issues/53)
- **Note #17**
- **Problem**: Fishing lists "anywhere" fish mixed in; players want fish for the current planet type listed first.
- **Proposed Solution**: Order the fishing list so the current planet type's fish come first, then "anywhere" fish, keeping each group in its own domain.


### Feature 37: Station Multi-Tool Technology Slots
- **Status**: Grid implemented on `fix/station-multi-tool-coverage`; user confirmed the displayed slots match perfectly on 2026-10-26. Installed module-stat details remain follow-up work.
- **Problem**: Current System lists station multi-tools without their technology grids, leaving players unable to inspect the complete offered inventory.
- **Proposed Solution**: Show each tool's installed technologies and upgrade modules in their actual slot positions, empty unlocked slots, supercharged slots and total unlocked-slot count. Reuse the existing inventory UserControl and catalog names/icons. Distinguish unavailable inventory details from an empty inventory, and preserve the saved-reload context.
- **Validation**: Compare the displayed grid with the same cabinet in game, including installed module values and supercharged-slot positions.

### Bug 38: Station Multi-Tool Damage Depends on Unresolved Runtime Animation State
- **Status**: Station work accepted for merge by the owner on 2026-10-27. Guarded runtime support handles absent and present-but-inactive overheat definitions; Himizumi and the missing Nakaok-Anj case are confirmed in game. Active overheat handles and differing camera-name durations remain unsupported, not guessed. Reopen for a new observed mismatch.
- **Problem**: A station tool can show a different damage potential from the app despite matching identity, class, inventory and scanner range. Himizumi's Return of War matches at 1347.8, while Kiss of Naryev-VI shows 400.0 in game versus 333.3 with the app's standard-duration assumption.
- **Evidence**: Native execution reproduces both supplied damage values with a shared zero animation duration; the ordinary duration reproduces the lower laser value. The live preview reads an animation controller shared by both comparisons. The actual cause of the zero-duration lookup is not yet verified.
- **Native follow-up (2026-10-26)**: Resource initialization leaves FrameEndGame=0 when the animation handle is absent and fills it to 160 when present. Eight native checks reproduce the resource-absent/present results for Return of War, Kiss of Naryev-VI, Bane of Silence and Sleep of War. This establishes an engine mechanism, not the live state in the screenshots.
- **Follow-up**: Station scope is closed for now; retain guards for unsupported runtime states and investigate fresh evidence if reported. Planetary cabinets/buildings proceed separately on `feature/planet-multi-tool-locations` (Feature 24). Bug 39 remains an open page-refresh UX issue. See `docs/research/PIRATE_STATION_MULTITOOLS.md`.

### Bug 39: Unchanged Current System Updates Reset Scroll and Redraw Previews
- **Status**: Owner confirmed on 2026-10-27 that autosaves no longer reset the page after the session-identity correction (`741330f`). The newly reported missing tools in another system are tracked separately as Bug 51.
- **Problem**: The Current System page refreshes even when nothing has changed, returns the user to the top of the page, and repeatedly redraws the 3D previews. User reported 2026-10-27. This interrupts browsing and is poor UX.
- **Expected**: Apply only meaningful changes in place. Preserve scroll position, expanded/collapsed sections and model rotation. Unchanged snapshots must not recreate cards, replace the page or rebuild previews.
- **Implementation**: Current System now reconciles planet rows by address, retains unchanged orbit-body/data/unmatched lists and system-picture bytes, and rejects stale asynchronous picture results. Station rows use site + model identity; a damage-only update retains the existing card, technology grid and model. Preview reloads reuse an already-built model, and unchanged market/comparison lists are retained. The shared save feed is unchanged; no new watcher or forced scroll restoration was added.
- **Automated validation**: Shared reconciliation passed 100 unchanged updates, in-place stat changes, reorder, model replacement, duplicate-key rejection and 1,000 mixed updates with no Reset events. App build passes; scroll/focus/rotation still require owner testing.
- **Autosave root cause / regression**: `SaveFolderWatchService.Apply` calls `AdoptSlot` with a newly indexed metadata object. Using that object as display identity cleared and hid station cards on every autosave. The panel now tracks `SaveSessionManager.SessionIdentity`, which survives metadata adoption but changes for a real load/close. A harness compiling the production station view-model reproduces failure with the old metadata identity and passes 50 metadata replacements with the corrected identity; damage-only updates, stale async results and real session/system changes also pass.
- **Validation**: Stay scrolled down with a rotated model while repeated unchanged snapshots arrive; confirm no jump, reflow or preview rebuild. Then verify real system/stock changes still appear.


### Bug 40: Station Cabinets with X-Class Scanner Upgrades Missing
- **Status**: Confirmed working by the owner on 2026-10-27 after testing the updated build. Implemented on `fix/station-tool-damage-runtime`; catalog format 41 is required.
- **Evidence**: Nakaok-Anj's two A-class Overload of Vimestvie-IX cabinets share a model/name but have different inventories. The missing cabinet's quality-4 scanner upgrade caused inventory generation to fail. Both physical sites were already located correctly.
- **Correction**: Extract the illegal high-grade chance, reproduce the native extra draws and high-grade intervals, and require verified standard runtime conditions before displaying this cabinet.
- **Validation**: 609 native scanner-module rolls match exactly; composed cabinet results match screenshot damage/scanner values 440.0/221.5 and 3402.9/319.4. Owner returned to the previously missing-tool case and confirmed "it is working" on 2026-10-27. This confirms the reported case, not every possible station or special upgrade.


## 7. Companion and Helper-App Notes - 2026-10-26

Source: `ARTIFACTX_MAUI-10-26-2026.odt`, incorporated 2026-10-27. These are backlog requirements, not completed features. The MAUI library requirement also comes from the owner's accompanying chat request. Preserve existing edits and validate companion save behavior in Slot 8 before enabling new writes.

### Feature 41: Editable Companion Area Records
- **Source note**: "Companion Area Records Editable".
- **Request**: Allow users to inspect and edit companion area records.
- **Research / scope**: Identify the exact records and their in-game meaning before choosing fields or controls. The note does not establish a schema, valid ranges or safe write behavior; do not infer these from raw key names.
- **Acceptance**: Confirm the fields through controlled save comparisons, present meaningful labels, stage edits with revert support, and verify persistence in game. Put verified schema/validation in Core and reuse existing companion controls.

### Feature 42: Companion 3D Preview and Actual Creature Name
- **Source note**: "Companions to show 3D visual and real name of creature".
- **Request**: Display the selected companion's actual creature name and a rotatable 3D preview representing that creature.
- **Implementation**: Reuse the existing preview pipeline where compatible; investigate creature model, descriptor, seed, scale and colour inputs. Distinguish the creature/species name from the player's pet nickname. Do not invent names or substitute a generic model as an exact preview.
- **Acceptance**: Compare names and appearance with the same companion in game; retain preview rotation while unrelated fields change and avoid rebuilding the model on every keystroke. Track unresolved model types explicitly.

### Feature 43: Egg Sequencer Helper
- **Source note**: "Egg Sequencer helper".
- **Request**: Help players understand egg sequencing inputs and outcomes.
- **Research / scope**: Establish supported ingredient effects, dose behavior and egg/parent inputs using extracted game data and controlled in-game tests. Separate known effects from uncertain predictions; exact outcome generation remains research until verified.
- **Architecture**: Put reusable sequencing rules and result models in the shared libraries so both WinUI and a future MAUI helper can use them. Keep platform UI and local save access separate.
- **Acceptance**: Compare supported predictions with in-game sequencer results; do not require a save edit merely to use the helper.

### Feature 44: Bookmark Generated Tools, Starships and Entire Systems
- **Source note**: Bookmark multi-tools or starships from Current System / Planet Details, or bookmark the entire system; reopen the bookmark to see all its details.
- **Request**: Add persistent bookmarks at item and system level, with a details view reachable from each bookmark.
- **Implementation**: Reuse Finds/bookmark infrastructure where it fits. Retain galaxy/system address, item/site identity, reload context, seed and available details needed to identify the same offering. Avoid deduplicating distinct cabinets solely by shared name or appearance. Keep recorded details available even if the game/catalog context later changes, and distinguish recorded facts from regenerated results.
- **Acceptance**: Bookmark two same-name tools separately, a starship and an entire system; restart the app and reopen each with its details and location intact. Only offer bookmarking for records actually available on the relevant page; do not introduce speculative located items.

### Feature 45: Complete Planet Creature List, Tameability and Verified Quality
- **Source note**: List all creatures on a planet, show whether they are tamable, and highlight S-class creatures once creature logic is perfected.
- **Request**: Show the planet's creature roster with verified tameability and, once supported, S-class highlighting.
- **Dependency**: Feature 48. Existing fauna placeholders/captured discoveries (Features 4 and 25) are not proof that the full procedural roster, class or tameability is known.
- **Acceptance**: Verify roster identity/counts and tameability against the game. Establish what "S-class" means for creatures before implementing labels or filters; do not transfer equipment quality rules to fauna without evidence. Until verified, show unknown states and retain captured records rather than fabricating missing creatures.

### Bug 46: Companion Edits Are Not Detected Until Focus Leaves the Field
- **Source note**: "Companions page doesn't detect changes until the user leaves the field".
- **Status**: User-reported; reproduction and correction pending.
- **Expected**: Valid edits update the staged/dirty state promptly while typing, without requiring focus loss. Handle incomplete numeric text without discarding input or staging invalid values; avoid a full companion reload per keystroke.
- **Investigation / validation**: Check binding update triggers, validation and change notifications in companion controls/view-models. Type, paste, clear and revert values while staying in the field; confirm dirty-state feedback and the staged value are correct.

### Bug 47: Companion Editing Is Laggy
- **Source note**: "Companions is laggy to edit".
- **Status**: User-reported; profiling pending. Related to Bug 46 but not yet proven to share its cause.
- **Investigation**: Measure input latency and identify synchronous catalog/save work, repeated JSON walks, control reconstruction or preview regeneration in the edit path. Keep disk/catalog work off the UI thread and update only affected state.
- **Acceptance**: Editing text, numbers and selections remains responsive with a populated companion record; selection, focus, caret and preview rotation survive unrelated changes. Verify staged edits and revert behavior remain correct.

### Feature 48: Companion and Creature Logic Reverse Engineering
- **Source note**: "Deep research is required on the Companions. We need total Reverse Engineering".
- **Request**: Research companion/creature generation and save behavior deeply enough to support reliable editing, previews, naming, egg sequencing and planet creature information.
- **Approach**: Track each subsystem separately: identity/names, model descriptors/parts, colours/scale, traits and area records, egg inheritance/sequencing, roster generation, tameability and any actual quality mechanic. Use extracted tables, native reference comparisons and controlled Slot 8 tests; record supported builds and unresolved runtime inputs.
- **Architecture**: Put verified portable algorithms/models in Core and extraction in GameData. Expose tested inputs/results to each app through shared contracts; keep Windows process reads and UI code in platform services. Do not declare complete creature generation from OCR or a few matching seeds.
- **Acceptance**: Maintain an evidence-backed support matrix and targeted regression references before enabling each dependent feature. Features 41 through 45 depend on their respective verified portions, not a single all-or-nothing research milestone.

### Feature 49: Shared Libraries for a Future MAUI Helper App
- **Source**: Owner's 2026-10-27 chat request accompanying the notes; the MAUI project will be added to this solution later as more NMS logic is understood.
- **Current baseline**: Core, Almanac and GameData are separate `net10.0` library projects. WinUI references all three. This is a reuse foundation, not proof of mobile compatibility; GameData includes extraction/native dependencies that need platform review.
- **Requirement**: Continue implementing and updating reusable NMS rules in libraries rather than duplicating them in pages or WinUI services. Reuse Core for portable generation/save-domain rules, Almanac for its existing shared reference models, and GameData for local game-data extraction/catalog building. See [architecture guidance](ARCHITECTURE.md#6-shared-libraries-and-future-maui-helper).
- **Plan**: Before adding MAUI, define the helper's target platforms and supported workflows, then identify portable catalog/result contracts and platform adapters. Keep Windows process reading, game-install discovery, OCR, renderer controls and WinUI types outside portable domain APIs. Do not assume the helper can read a PC install/process or share desktop save-write capabilities.
- **Acceptance**: The future helper consumes shared algorithms through project references with no dependency on WinUI; supported target builds and native package compatibility are verified. Avoid copying NMS logic or adding an empty MAUI project before the intended workflows/platforms are established.
- **Intended workflows (owner, 2026-10-27)**: A phone helper aimed at console players. Point the camera at the in-game portal glyphs to identify the current system, then show everything generation can resolve for it (system, planets, station stock, later ships/buildings). Also reference tools: cooking recipes, refining recipes, organic frigate feeding results, etc. Galaxy Search (Feature 53) is a natural fit.
- **Constraints to design for**: (1) Console players have no PC install, so the phone cannot build its own catalog; it needs a prebuilt catalog shipped or downloaded per game build. Check redistribution of extracted game data before shipping it. (2) No save access: station tool loadouts depend on known technology, progression level and active food. The phone must show save-independent facts (name, class, model, site) as exact and either ask for the few inputs or mark loadout/stats as approximate. (3) Glyph recognition is a 16-symbol, 12-position classification from the camera, feasible on-device; keep it in a platform adapter, outside Core.


### Feature 50: Distinct Class Badges and Colours for Tools and Ships
- **Source**: Owner's 2026-10-27 screenshot of the game's purple A-class emblem.
- **Request**: Use recognizable class images/badges and matching class colours so tools and ships stand out, rather than giving every class the same plain text treatment.
- **Implementation**: Reuse catalog-sourced game class emblems where available and provide a shared badge UserControl for tool/ship cards. Use consistent class colours (including the purple A-class treatment shown), readable contrast in both themes and a text/accessibility label so class is never communicated by colour alone. Retain a clear fallback if an icon is unavailable.
- **Acceptance**: Compare the badges with the game's S/A/B/C indicators on representative tool and ship cards, check both themes, and keep badge updates independent of 3D preview generation.


### Bug 51: Unrelated Food Bonus Hides All Station Multi-Tools
- **Status**: Correction implemented on `fix/station-tool-food-guard`; requires catalog format 42 rebuild and in-app confirmation.
- **Report**: Iljeark-Ats V, Euclid, portal `0090FF67B292`: both C-class Poem of Supplication cabinets disappear from the app, although present in game. Screenshot references are damage/scanner 1362.7/200.1 and 1362.7/206.6.
- **Cause**: The saved FoodUnitItem is nonempty. The prior conservative runtime guard rejected every food product, even when its bonus targets a stat unrelated to displayed weapon damage or scanner radius.
- **Correction**: Extract each product's FoodBonusStat into the catalog, preserve the normalized saved food ID, and allow generation only when its known target is outside all 11 displayed-stat inputs. Unknown food IDs, missing tables and relevant bonus targets remain guarded; their strength is not guessed.
- **Validation**: Native execution confirms 605 exact unchanged results when the food target differs from the evaluated stat, and confirms that a matching target invokes the bonus calculation. With the actual saved food context the composed pipeline reproduces both supplied screenshot pairs. Default references and session-transition checks remain part of regression validation.


### Bug 52: Station Multi-Tools With X-Class Upgrades Missing
- **Status**: Correction implemented on `fix/station-tool-food-guard` (2026-10-27); requires in-app confirmation at a cabinet whose tool carries an X-class weapon upgrade (pirate stations are the likeliest). No catalog rebuild needed beyond format 42.
- **Report**: Codex handoff: pirate/X-class technology installed in station tools still caused problems.
- **Cause**: The module roller only accepted UP_SCANX among the high-grade qualities, so any cabinet that rolled UP_LASERX, UP_BOLTX, UP_SHOTX, UP_SMGX, UP_RAILX, UP_CANNX, UP_GRENX, UP_TGRENX, UP_SENGUN, UP_LASER0 or UP_BOLT0 was dropped whole. X-scanner tools additionally required a live read of the running game; that guard read the pending game mode (image+0x6E8D718) instead of the active mode (+0x6E8D714).
- **Correction**: Native quality branch 0x140ec45de applies the high-grade draw to qualities 4/5/6 and not to 0..3/7; the roller now follows that rule for every module. The alternative chance applies only in Unspecified/Seasonal mode, which save load (0x1404967F0..0x1404968B4) selects only for active season data, so the Main save context establishes the standard roll. Expedition context still requires the live guard (address corrected).
- **Validation**: 3,900 native rolls (13 special-quality weapon modules x 100 suffixes x chance 0/10/100) plus 3,900 at selection-seed suffixes, and the 7,403 ordinary rolls: all bit-exact. Native ranking/insertion regenerated without early stops: 500/500 loadouts match, 161 of them with special-quality modules. All station screenshot references, pirate references and pipeline guards still pass. See `docs/research/PIRATE_STATION_MULTITOOLS.md`.
- **2026-10-27 follow-up**: Owner's Slot 1 at Kettsibu hid the whole station section: its season block has UseLegacyStationsNearStartUA=true, which was treated as unknown. The native legacy predicate returns false outside Unspecified/Seasonal game mode, so Main now resolves standard. Both cabinets then match the owner's screenshots, including Rizamakho's Waveform Focuser (A, UP_LASERX, 480.0 / 224.8), the first in-game confirmation of a non-scanner X-class module.


### Feature 53: Galaxy Search - Find Items Near the Player
- **Status**: Planned (owner request 2026-10-27). Not implemented.
- **Request**: Search a range around the player, not just the current system, for generated content. First use case: the nearest station whose cabinet tool has an X-class upgrade, instead of flying around blindly.
- **Design**: A new page (Finds remains confirmed, player-recorded finds; offer "Save to Finds" once confirmed in game). Range as same region / nearby regions / hyperdrive light-years, results sorted by distance and streamed from a background scan. Filters only on what generation already resolves exactly: system attributes, planet profiles, station multi-tools (class, upgrade type, kind, stats). Unsupported cases show as unknown, never guessed. Each result shows system name, region, portal glyphs, distance and a direction hint. New reverse-engineering results (ships, planet cabinets, crashed ships) become new filters.
- **Prerequisites**: (1) Establish how many systems each region actually contains, so the search never lists nonexistent systems. (2) Establish whether station stock depends on arrival versus save reload (current generation uses the save's reload address); label results accordingly.
