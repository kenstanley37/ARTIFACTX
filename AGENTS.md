# ArtifactX - Agent Rules

ArtifactX is a No Man's Sky save editor and companion app for ordinary players (many on modest PCs, some with poor eyesight). WinUI 3 + .NET 10, MVVM. These rules come from months of work with the project owner. Follow them exactly; when one conflicts with your own defaults, the rule wins.

This is the single shared rules file for every AI assistant: `CLAUDE.md` and `GEMINI.md` only import it, and Codex reads it directly. Change the rules here, never in those stubs. Dated research and handoff notes go in `docs/` (see `docs/AGENT_HANDOFF_NOTES.md` and `docs/research/`), not in this file.

## 1. How to work with the owner

- **Challenge, don't just agree.** If an idea is wrong, fragile, or worse UX than an alternative, say so plainly and recommend the better path before building it.
- **UX first.** Pick what feels best to most players: conventional, discoverable, keyboard accessible, plain wording, no surprise state changes. Recommend one choice; don't offer a menu of options.
- **Act on clear tasks without check-ins**, but these decisions belong to the owner - ask, never assume:
  - merging a branch into `development`, cutting a release, opening a PR;
  - anything that writes to a real save or deletes the player's data;
  - genuinely ambiguous requirements where the answer changes what you build.
- **Report honestly.** Say what you verified and how. If something is built but not tested in the app or the game, say exactly that. Never claim a fix works because it compiles.
- **Don't launch or click through the app to self-verify.** The owner runs and tests it and reports back. `dotnet build` with 0 errors and 0 warnings is fine. Simulated clicks once landed in another game the owner was playing.
- **Don't hand-repair data.** When screen capture or a feature stores something wrong, fix the code only; never patch `user.db` rows by hand. The owner retests in game.

## 2. Code architecture (the BIG rule)

- **No mega pages.** Use UserControls and separate pages instead of piling code into one page. A page's code-behind wires events and navigation; it does not build UI.
  - New UI = XAML `DataTemplate`s / `UserControl`s bound with `x:Bind` to ViewModels (`ObservableObject`, CommunityToolkit `[ObservableProperty]` partial properties).
  - Use `[ObservableProperty]` on `public partial` properties and `[RelayCommand]` methods, never the older field form (it is not WinUI/AOT safe and causes `MVVMTK0045` warnings).
  - Keep the folders' jobs separate: `Views` (page XAML and matching code-behind only), `Controls` (reusable UserControls), `ViewModels` (state and logic), `Services` (I/O and infrastructure), `Models` (data), `Helpers/<Feature>` (small, stateless UI/presentation helpers).
  - **Keep `Views` / `Pages` clean and organized by domain in every UI project.** Never leave pages flat in the root of `Views/`. Place every page `.xaml` and its single matching `.xaml.cs` inside its domain subfolder (`Views/SaveEditorPages/`, `Views/AccountPages/`, `Views/ProgressionPages/`, `Views/ExplorePages/`, `Views/ReferencePages/`, `Views/InspectionPages/`, `Views/AppPages/`) with a matching namespace (`ArtifactX.WinUI3.Views.<Domain>Pages`). Only actual page `.xaml` files and their single matching `.xaml.cs` file belong in `Views/`; put page-support classes, formatting/mapping utilities and similar helper `.cs` files in the project's `Helpers/<Feature>/` folder, for example `src/ArtifactX.WinUI3/Helpers/Recipes/`, with a matching namespace such as `ArtifactX.WinUI3.Helpers.Recipes`. State belongs in `ViewModels`, data records in `Models`, I/O in `Services`, and portable domain logic in `ArtifactX.Core`; `Helpers` must not become a catch-all or a way around those boundaries.
  - **Don't hide a mega page or window in extra partial files.** Files such as `SomePage.Helpers.cs`, `SomePage.Mapping.cs`, `SomePage.Preview.cs`, `SomePage.Parts.cs`, `SomePage.Save.cs`, or `MainWindow.GameData.cs` are strictly forbidden alongside pages or windows. Extract the responsibility into an appropriately placed service, ViewModel, model, or helper class and compose it from the page or ViewModel. Helpers do not build UI or perform data loading/save I/O.
  - Building cards in C# (`new Border { ... }`, `new TextBlock { ... }`) is a smell. The exception is a custom-drawn control whose visuals are its job (`ShipPreview`'s renderer, `InventorySlotGrid`'s slot canvas). Antigravity previously produced 650-1,300 line code-behind pages after being told not to; they all had to be rewritten.
  - Data loading and save logic go in services (`src/ArtifactX.WinUI3/Services`) or `ArtifactX.Core`, never in the page.
  - When touching an oversized page, split it.
- **No god-services or monolithic files.** Keep services focused and split by domain rather than dumping unrelated features into a 2,000-line catch-all file:
  - `CatalogService` is split by domain (`CatalogService.cs` for core cache/lookup/search, `CatalogService.Equipment.cs`, `CatalogService.Universe.cs`, `CatalogService.Gameplay.cs`). Put new catalog queries in the matching domain file, and clear any new static cache fields inside `CatalogService.Reload()`.
  - `CatalogSteps` in `ArtifactX.GameData` is split by domain (`CatalogSteps.<Domain>.cs`) and shares the cached PAK index and localisation helpers in `CatalogSteps.cs` (`GetPakEntries`, `GetIndex`, `ExtractBytes`, `DecodeMbin`, `GetEnglishLoc1Lookup`, `GetMergedEnglishLookup`). Never instantiate a new `PakDiscoveryService().Discover(pcbanksPath)` loop or re-decode `language/nms_*_english.mbin` inside an individual step.
  - All sidebar `NavigationViewItem` tag-to-page and page-to-tag routing belongs in `NavigationService` (`src/ArtifactX.WinUI3/Services/NavigationService.cs`), never in switch statements in `MainWindow.xaml.cs`.
- **Reuse the shared pieces; don't rebuild them:** `LoadoutTemplatesPanel` (+`ILoadoutTemplateHost`), `TechStackCopyConfirmation`, `ClassPicker`, `EquipmentStrip`, `FoldingCard`, `StatBonusBoxes`/`StatBonusService`, `NumberFieldViewModel`, `InventoryCard` + `InventoryLayout`, `SeedBox`, `SaveArrays.StageEntry`, `RawFieldsPanel`, `NavigationService`, `StarshipService`/`ShipPartsService`/`ShipPartsPanel`/`ShipBuilderService`, `MultiToolService`/`MultiToolKinds`, `FreighterService`, `SpaceStationService`, `CompanionService`, `SettlementService`.
- **Strict domain boundaries; never mix domains:**
  - Keep domain concepts, services, and paths strictly separated by entity: Starship logic in `StarshipService`/`ShipPartsService`/`ShipBuilderService`, Multi-Tool logic in `MultiToolService`/`MultiToolKinds`, Frigates in `FrigateService`, Freighters in `FreighterService`, Companions in `CompanionService`, Bases in base services.
  - Never cross domains for convenience (e.g., starship naming or ship descriptor trees must never live in `MultiToolKinds`; frigate or companion seed parsing must never call `ShipPartsService`).
  - Shared domain utilities (such as hex seed parsing, formatting, or hashing) belong in neutral classes in `ArtifactX.Core` (e.g., `NmsSeedGenerator.TryParse`), not inside a specific equipment or part service.
  - When game engine mechanics share underlying structures or routines (e.g., player progression level, known tech lists, slot grid math), place them in neutral player/equipment classes rather than giving them equipment-specific names.
- **Layering:** `ArtifactX.Core` = save models and path helpers (no UI, no libMBIN); `ArtifactX.GameData` = the catalog builder (the ONLY code that reads the game's PAK/MBIN files); `ArtifactX.WinUI3` = UI.
- **Never hardcode extractable data.** Game content (names, icons, values, descriptor words, fish tables) comes from the catalog (`nms_catalog.sqlite`, built on the player's PC from their own game install by `ArtifactX.GameData`). Structural enums come from the vendored libMBIN directly (`Enum.GetNames<...>()`), not copied lists.
- **libMBIN is ours** (`src/ArtifactX.Tools/NMS.Tools.libMBIN`). Fix or extend it directly when a struct/enum is missing or wrong (verify against real saves first). It's LGPL-3: note modifications in `THIRD-PARTY-NOTICES.md`.
- **Smooth UI:** page navigation shows immediately; disk I/O, SQLite and big JSON walks go to `Task.Run`. Never block the UI thread.
- **Dev/confidence notes go in C# doc comments** (on the `Nms*Paths.cs` class or the page/service summary), never in the UI. Document how a save field was confirmed (which test, which date).
- **Zero compiler errors and zero warnings (`0 Warning(s), 0 Error(s)`) across the entire solution.** Match the surrounding code's style, naming and comment density. Never leave C# nullable/type warnings, `MVVMTK0045` field warnings, NuGet `NU*` warnings, or XAML `WMC1506` duplicate-property warnings in any project.

## 3. UI rules

- **Themes:** use the app's theme brushes (`ThemeTextBrush`, `ThemeTextDimBrush`, `ThemeAccentBrush`, `ThemeDoneBrush`, `ThemeLineBrush`, `ThemePanelAltBrush`, `ThemeVoidBrush`...) and styles (`ThemeCardStyle`, `ThemeEyebrowStyle`...). Both themes (Station Core, Galaxy Map) must work. Before using a new resource key, check it's defined in `Themes/*.xaml` or `App.xaml`.
- **Contrast:** aim for WCAG AA (4.5:1+). Grey text on the dark backgrounds is hard to read; carry meaning with colour (green good / yellow close / red over), not more grey.
- **Screen readers:** every icon-only button, every button whose content is a panel, and every input needs `AutomationProperties.Name`. Buttons that read the same need distinct names ("Generate a model seed", not three "Generate"). Records shown in a ComboBox override `ToString()`.
- **Never show raw save keys or ids** (`3Nc`, `^UP_SCAN0`, `STACONNECT`) to the player. Fallback names are plain and sequential ("Storage Container 1").
- **Use the game's own words.** Fish rarity: quality Rare reads "Uncommon", Epic reads "Rare". Biomes 8-10 (Red/Green/Blue) are "anomaly" worlds, not colours.
- **Equipment pages** (Starships, Multi-Tool, Freighter...): details card full width above the tech grid, collapsible; right sidebar = Templates only.
- **Live game updates:** pages follow the game through the app-wide feed `FishCatchRecorder.SnapshotSeen`; never start a page's own save watcher. Update in place - no page reloads, keep scroll position, new items animate in.

## 4. Saves - safety first

- **Never write a save while No Man's Sky is running.** The app blocks Save then; don't work around it.
- **Test saves:** Steam **Slot 8 ("DELETEME")** is the throwaway test save. **Slot 1** is the owner's real 100+ hour save - read it for reference only.
- **Edits are staged, never written directly:** `SaveSessionManager.StageEdit / RevertEdit / RevertEditsUnder / HasStagedEditsUnder`. Stage whole arrays (`SaveArrays.StageEntry`), not single elements. A page's Reset must revert everything that page staged (whole item: name, seeds, type, class, interior), not just its grids.
- **Expeditions:** when `XTp == "Season"` the live player state is under `2YS`, not `vLc`. `SaveSessionManager` remaps automatically - always write paths with `vLc`; never add expedition copies of pages or helpers.
- **Templates/shares always include unlocked slots** (`hl?`) for every inventory and apply them on import.
- **Every save-format claim needs evidence:** a controlled in-game diff (save, do one thing in game, save, compare) or direct inspection of a decrypted real save. Write the evidence in the doc comment.
- Planets/systems: the save stores almost nothing about them (type, rings, discoveries). Names, weather, resources etc. are generated by the game - that's what the system generator port and screen capture are for.

## 5. Data the app keeps

- App data: `%AppData%\ArtifactX` (catalog in `GameData\nms_catalog.sqlite`, decrypted save copies in `Working\`, logs in `logs\`).
- `user.db` (player's own SQLite: notes, finds, templates, captures): additive-only numbered migrations, backup before upgrading, soft deletes (`deleted = 1`). Never ship it.
- **Never commit game data** (`*.sqlite`, `*.sqlite.bak`, `*.hg`, `*.hg.json`, extracted icons). Check diffs before committing.
- **Desktop and mobile get game data differently:**
  - **Desktop** builds everything from the player's own installed game (`ArtifactX.GameData`) and ships no game data at all - not even tables.
  - **Mobile** has no game install, so it may ship a **tables-only bundle**: numbers and text the generators need (building layouts, multi-tool pools, inventory and tech tables, name words). It never ships game **assets** - no models, textures, icons, images or audio; use our own artwork or leave pictures out.
  - The mobile bundle is exported from the desktop pipeline at release time, is kept out of git (a release artifact, like the catalog), and records the game build it came from. Every NMS update needs a new bundle.

## 6. Git, changelog, releases

- **Never commit to `main` or `development` directly.** Create the task's branch off `development` (`feature/...`, `fix/...`, `refactor/...`, `docs/...`) before touching any file; a hotfix branches off the release tag on `main`. Commit and push the branch once the work is verified; merge into `development` only when the owner approves. A merge commit of an approved branch is the only commit that lands on `development`.
- **Keep `.gitignore` current.** When adding a project, tool, platform or build output, ignore what it generates (`bin/`, `obj/`, `.vs/`, `*.binlog`, platform build folders, local settings, user secrets) in the same change.
- **CHANGELOG.md:** add a player-facing line under "Unreleased" in the same commit as every user-facing change (plain words, no internal names). Skip internal-only changes.
- **Releases:** the owner decides when - no small fix-only releases; bundle fixes with features. Flow: a `development -> main` PR opened only when cutting a release and merged straight away (GitHub keeps every PR's commits forever - no speculative PRs). Tag every release (`git tag -a vX.Y.Z -m "Release vX.Y.Z"`); hotfix branches come off release tags.
- **Issues:** the owner's ideas and open work live in GitHub issues (`kenstanley37/ARTIFACTX`); add findings to the existing issue rather than opening duplicates. Issue #1 tracks features not yet confirmed in game.
- Never create Copilot instruction files.

## 7. Build and debugging lessons

- Build: `dotnet build src/ArtifactX.WinUI3 -r win-x64`. Visual Studio's `x64|Debug` build writes to a separate cache (`bin\x64\Debug`), so a CLI build doesn't update what VS runs, and vice versa. The owner runs the VS build.
- A running app locks the build output - close it before building.
- **Crash with no readable exception → suspect XAML first:** an undefined `ThemeResource` key, an `x:Bind` type mismatch, a keyed `Geometry` resource. Check `%AppData%\ArtifactX\logs\crash.log`, then the Windows Application event log (Id 1000) for native faults.
- Never pass a WinRT control's method group to `DispatcherQueue.TryEnqueue` (use a lambda). A page navigated to must have a XAML file.
- Slowness reports: compare a Ctrl+F5 run with F5 first - the VS debugger froze the app ~9 s during save reads.
- `UserControl.Padding` is ignored - wrap the content in a padded `Border`.
- `NumberBox` rounds to 8 decimals and pushes the value back through TwoWay binding - use `NumberFieldViewModel`, which ignores that echo, or Reset buttons appear without a real edit.
- `LostFocus` handlers must not stage a value that didn't change.

## 8. Repository layout and standards

- **Keep the root tidy.** The root holds the solution (`ARTIFACTX.slnx`), repository files (`.gitignore`, `README.md`, `LICENSE`, `CHANGELOG.md`, `Directory.Packages.props`, the agent stubs) and a few domain folders: `src/` (applications and libraries), `tests/` (check and test projects), `docs/`, plus build/release support. Put new things in the right domain folder with subfolders as needed; don't add new top-level folders. Never put a project folder directly in the root.
- **Keep `ARTIFACTX.slnx` complete and synchronized.** Every project under `src/` and every check/test project under `tests/` (`tests/*.Checks`) must be registered in `ARTIFACTX.slnx`. Never leave orphaned or duplicate project folders on disk outside the solution, and update `tests/*.Checks` in the same change whenever a signature they exercise changes.
- **One namespace root.** Every project uses the `ArtifactX` prefix plus its layer: `ArtifactX.Core`, `ArtifactX.GameData`, `ArtifactX.WinUI3`, `ArtifactX.Tools.DataCataloger`, `ArtifactX.Core.Checks`. The vendored libMBIN keeps its own `libMBIN` namespace because it is third-party code.
- **Testable code and unit testing where applicable.** Not everything in a WinUI/game-save app can be unit tested, but wherever logic can be tested in isolation (pure domain logic in `ArtifactX.Core`, procedural generation, seed/hash parsing, save-path staging, ViewModel state transitions, and service calculations), write unit tests or `tests/*.Checks` suites for it. Put logic behind small interfaces where it touches files, databases, the network or the running game, so it can be checked with synthetic/in-memory fakes without needing a live save or SQLite file. Keep and extend the `tests/*.Checks` programs (which compare against real game data and native-emulator results) and run all synthetic checks before committing. New pure-logic code may add xUnit tests with built-in asserts (or Shouldly). Don't add FluentAssertions or MediatR: both now need commercial licences.
- **CLI tools:** new command-line tools use System.CommandLine (clear help, POSIX-style verbs and options). Don't rewrite the existing DataCataloger commands just to convert them.
- **Future crowd-source API (ASP.NET Core):** a thin API layer over Core/application services, explicit versioning, dependency injection via interfaces, plain service classes rather than MediatR, global error handling returning `ProblemDetails`. Native AOT friendly where practical.
- **Show the structure in proposals:** when suggesting new code, name the target files and where each piece lives rather than one large file.
