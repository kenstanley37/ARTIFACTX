# ArtifactX - Agent Rules

No Man's Sky save editor and companion app for ordinary players (modest PCs, some with poor eyesight). WinUI 3 + .NET 10, MVVM. These rules win over your defaults. This is the only rules file: `CLAUDE.md` and `GEMINI.md` import it, Codex reads it. Dated research and handoff notes go in `docs/` (`docs/AGENT_HANDOFF_NOTES.md`, `docs/research/`), not here.

## 1. Working with the owner

- Challenge wrong, fragile or worse-UX ideas plainly and recommend the better path before building.
- UX first: conventional, discoverable, keyboard accessible, plain words, no surprise state changes. Recommend one choice, not a menu.
- Act on clear tasks without check-ins. Ask first for: merging into `development`, releases, PRs; anything writing a real save or deleting player data; ambiguous requirements that change what you build.
- Report honestly what you verified and how. Compiling is not working.
- Never launch or click through the app to self-verify (simulated clicks once hit another game). The owner tests. `dotnet build` with 0 errors/0 warnings is fine.
- Never hand-repair data (`user.db` rows); fix the code, the owner retests.

## 2. Code architecture

- **No mega pages.** Code-behind wires events and navigation only. UI = XAML `DataTemplate`s/`UserControl`s bound with `x:Bind` to ViewModels (`ObservableObject`). Use `[ObservableProperty]` on `public partial` properties and `[RelayCommand]`, never the field form (`MVVMTK0045`, not AOT safe). C#-built cards (`new Border {...}`) are a smell, except custom-drawn controls (`ShipPreview` renderer, `InventorySlotGrid` canvas). Split oversized pages you touch.
- **Folders:** `Views` (page `.xaml` + its one `.xaml.cs` only, in domain subfolders `Views/<Domain>Pages/` - SaveEditor, Account, Progression, Explore, Reference, Inspection, App - namespace `ArtifactX.WinUI3.Views.<Domain>Pages`; never flat in `Views/`), `Controls` (UserControls), `ViewModels` (state/logic), `Services` (I/O), `Models` (data), `Helpers/<Feature>` (small stateless presentation helpers, namespace `ArtifactX.WinUI3.Helpers.<Feature>`; not a catch-all, no UI building, no data/save I/O). Portable domain logic goes in `ArtifactX.Core`.
- **No partial-file mega pages:** `SomePage.Helpers.cs`/`.Mapping.cs`/`.Save.cs`, `MainWindow.GameData.cs` etc. are forbidden. Extract into a service, ViewModel, model or helper.
- Data loading and save logic live in `Services` or `ArtifactX.Core`, never in pages.
- **No god-services.** `CatalogService` is split by domain (`CatalogService.cs` core cache/lookup, `.Equipment.cs`, `.Universe.cs`, `.Gameplay.cs`); new static caches are cleared in `CatalogService.Reload()`. `CatalogSteps` (GameData) is split as `CatalogSteps.<Domain>.cs` and shares `GetPakEntries`, `GetIndex`, `ExtractBytes`, `DecodeMbin`, `GetEnglishLoc1Lookup`, `GetMergedEnglishLookup` - never a new `PakDiscoveryService().Discover(...)` loop or re-decoding `language/nms_*_english.mbin` in a step. Nav tag/page routing lives in `NavigationService`, not `MainWindow.xaml.cs`.
- **Reuse, don't rebuild:** `LoadoutTemplatesPanel` (+`ILoadoutTemplateHost`), `TechStackCopyConfirmation`, `ClassPicker`, `EquipmentStrip`, `FoldingCard`, `StatBonusBoxes`/`StatBonusService`, `NumberFieldViewModel`, `InventoryCard`+`InventoryLayout`, `SeedBox`, `SaveArrays.StageEntry`, `RawFieldsPanel`, `NavigationService`, `StarshipService`/`ShipPartsService`/`ShipPartsPanel`/`ShipBuilderService`, `MultiToolService`/`MultiToolKinds`, `FreighterService`, `SpaceStationService`, `CompanionService`, `SettlementService`.
- **Strict domains:** each entity's logic stays in its own service (Starship, MultiTool, Frigate, Freighter, Companion, Base...); never cross for convenience. Shared utilities (seed parsing, hashing, formatting) go in neutral `ArtifactX.Core` classes (e.g. `NmsSeedGenerator.TryParse`); shared engine mechanics (progression, known tech, slot grids) get neutral names.
- **Layers:** `ArtifactX.Core` = save models/path helpers (no UI, no libMBIN); `ArtifactX.GameData` = catalog builder, the ONLY code reading PAK/MBIN; `ArtifactX.WinUI3` = UI.
- **Never hardcode extractable data.** Game content comes from the catalog (`nms_catalog.sqlite`, built from the player's install by GameData); structural enums from libMBIN (`Enum.GetNames<...>()`).
- **libMBIN is ours** (`src/ArtifactX.Tools/NMS.Tools.libMBIN`): fix/extend it directly after checking real saves; LGPL-3, note changes in `THIRD-PARTY-NOTICES.md`.
- Smooth UI: navigation shows immediately; disk I/O, SQLite, big JSON walks go to `Task.Run`. Never block the UI thread.
- Dev/confidence notes go in C# doc comments (how and when a save field was confirmed), never in the UI.
- Zero errors and zero warnings across the solution (nullable, `MVVMTK0045`, `NU*`, `WMC1506` included). Match surrounding style and comment density.

## 3. UI

- Use theme brushes (`ThemeTextBrush`, `ThemeTextDimBrush`, `ThemeAccentBrush`, `ThemeDoneBrush`, `ThemeLineBrush`, `ThemePanelAltBrush`, `ThemeVoidBrush`...) and styles (`ThemeCardStyle`, `ThemeEyebrowStyle`...). Both themes (Station Core, Galaxy Map) must work; check a key exists in `Themes/*.xaml`/`App.xaml` before using it.
- Contrast WCAG AA (4.5:1+); carry meaning with colour (green good / yellow close / red over), not more grey.
- `AutomationProperties.Name` on every icon-only button, panel-content button and input; same-looking buttons get distinct names. Records in a ComboBox override `ToString()`.
- Never show raw save keys/ids (`3Nc`, `^UP_SCAN0`); fallback names plain and sequential ("Storage Container 1").
- Use the game's words (fish quality Rare = "Uncommon", Epic = "Rare"; biomes 8-10 are "anomaly" worlds).
- Equipment pages: details card full width above the tech grid, collapsible; right sidebar = Templates only.
- Live updates come from `FishCatchRecorder.SnapshotSeen`; never a page's own save watcher. Update in place: no reloads, keep scroll, animate new items.

## 4. Saves

- Never write a save while NMS runs (Save is blocked; don't work around it).
- Steam Slot 8 ("DELETEME") is the test save. Slot 1 is the owner's real save: read only.
- Stage edits via `SaveSessionManager.StageEdit / RevertEdit / RevertEditsUnder / HasStagedEditsUnder`; stage whole arrays (`SaveArrays.StageEntry`). A page's Reset reverts everything it staged.
- Expeditions (`XTp == "Season"`): live state is under `2YS`; `SaveSessionManager` remaps - always write `vLc` paths, no expedition copies of pages.
- Templates/shares always include and apply unlocked slots (`hl?`).
- Every save-format claim needs evidence (controlled in-game diff or decrypted real save), written in the doc comment.
- The save stores almost nothing about planets/systems; the generator port and screen capture provide the rest.

## 5. App data

- `%AppData%\ArtifactX`: `GameData\nms_catalog.sqlite`, decrypted copies in `Working\`, `logs\`.
- `user.db`: additive numbered migrations, backup before upgrade, soft deletes (`deleted = 1`), never shipped.
- Never commit game data (`*.sqlite`, `*.sqlite.bak`, `*.hg`, `*.hg.json`, extracted icons). Check diffs.
- Desktop ships no game data; builds all from the player's install. Mobile may ship a tables-only bundle (numbers/text for generators), never assets (models, textures, icons, images, audio); exported at release time, kept out of git, records its game build, rebuilt each NMS update.

## 6. Git, changelog, releases

- Never commit to `main` or `development`. Branch off `development` (`feature/`, `fix/`, `refactor/`, `docs/`) before touching files; hotfixes branch off the release tag. Commit and push once verified; merge only with owner approval (the merge commit is the only commit on `development`).
- Keep `.gitignore` current with any new project/tool output.
- `CHANGELOG.md`: a plain player-facing line under "Unreleased" in the same commit as each user-facing change.
- Releases: owner decides; no fix-only releases. A `development -> main` PR only when cutting a release, merged at once. Tag every release (`git tag -a vX.Y.Z -m "Release vX.Y.Z"`).
- Issues live in GitHub `kenstanley37/ARTIFACTX`; add to existing issues, no duplicates. Issue #1 tracks features unconfirmed in game.
- Never create Copilot instruction files.

## 7. Build and debugging

- Build: `dotnet build src/ArtifactX.WinUI3 -r win-x64`. VS `x64|Debug` uses a separate cache (`bin\x64\Debug`); the owner runs the VS build. A running app locks the output.
- Crash with no readable exception: suspect XAML first (undefined `ThemeResource`, `x:Bind` type mismatch, keyed `Geometry`). Check `logs\crash.log`, then the Windows Application event log (Id 1000).
- Never pass a WinRT control's method group to `DispatcherQueue.TryEnqueue` (use a lambda). A navigated page must have a XAML file.
- Slowness: compare Ctrl+F5 with F5 first (the debugger froze save reads ~9 s).
- `UserControl.Padding` is ignored: wrap content in a padded `Border`.
- `NumberBox` rounds to 8 decimals and echoes through TwoWay binding: use `NumberFieldViewModel`.
- `LostFocus` handlers must not stage unchanged values.

## 8. Repository

- Root holds only the solution (`ARTIFACTX.slnx`), repo files (`.gitignore`, `README.md`, `LICENSE`, `CHANGELOG.md`, `Directory.Packages.props`, agent stubs) and `src/`, `tests/`, `docs/`, build/release support. No new top-level folders; no project folders in the root.
- Every project in `src/` and `tests/*.Checks` is registered in `ARTIFACTX.slnx`; no orphan or duplicate folders. Update `tests/*.Checks` when a signature they use changes.
- Namespaces: `ArtifactX.<Layer>` (`ArtifactX.Core`, `.GameData`, `.WinUI3`, `.Tools.DataCataloger`, `.Core.Checks`); vendored libMBIN keeps `libMBIN`.
- Test what can be tested in isolation (Core logic, generation, seed/hash parsing, staging, ViewModel state, service math) with `tests/*.Checks` or xUnit (built-in asserts or Shouldly). Put file/DB/network/game access behind small interfaces for fakes. Run synthetic checks before committing. No FluentAssertions or MediatR (commercial licences).
- New CLI tools use System.CommandLine; don't convert existing DataCataloger commands.
- Future crowd-source API (ASP.NET Core): thin layer over Core services, explicit versioning, DI via interfaces, plain services (no MediatR), global `ProblemDetails` errors, AOT friendly where practical.
- In proposals, name the target files and where each piece lives.
