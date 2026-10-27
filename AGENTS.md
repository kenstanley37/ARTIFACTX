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
- **Don't launch or click through the app to self-verify.** The owner runs and tests it and reports back. `dotnet build` with 0 errors is fine. Simulated clicks once landed in another game the owner was playing.
- **Don't hand-repair data.** When screen capture or a feature stores something wrong, fix the code only; never patch `user.db` rows by hand. The owner retests in game.

## 2. Code architecture (the BIG rule)

- **No mega pages.** Use UserControls and separate pages instead of piling code into one page. A page's code-behind wires events and navigation; it does not build UI.
  - New UI = XAML `DataTemplate`s / `UserControl`s bound with `x:Bind` to ViewModels (`ObservableObject`, CommunityToolkit `[ObservableProperty]` partial properties).
  - Use `[ObservableProperty]` on `public partial` properties and `[RelayCommand]` methods, not the older field form (it is not WinUI/AOT safe and causes the MVVMTK0045 warnings).
  - Keep the folders' jobs separate: `Views` (pages), `Controls` (reusable UserControls), `ViewModels` (state and logic), `Services` (I/O and infrastructure), `Models` (data).
  - Building cards in C# (`new Border { ... }`, `new TextBlock { ... }`) is a smell. The exception is a custom-drawn control whose visuals are its job (`ShipPreview`'s renderer, `InventorySlotGrid`'s slot canvas). Antigravity previously produced 650-1,300 line code-behind pages after being told not to; they all had to be rewritten.
  - Data loading and save logic go in services (`src/ArtifactX.WinUI3/Services`) or `ArtifactX.Core`, never in the page.
  - When touching an oversized page, split it.
- **Reuse the shared pieces; don't rebuild them:** `LoadoutTemplatesPanel` (+`ILoadoutTemplateHost`), `TechStackCopyConfirmation`, `ClassPicker`, `EquipmentStrip`, `FoldingCard`, `StatBonusBoxes`/`StatBonusService`, `NumberFieldViewModel`, `InventoryCard` + `InventoryLayout`, `SeedBox`, `SaveArrays.StageEntry`, `RawFieldsPanel`, `StarshipService`/`MultiToolService`/`FreighterService`, `ShipPartsService`/`ShipPartsPanel`, `SpaceStationService`, `CompanionService`, `SettlementService`.
- **Layering:** `ArtifactX.Core` = save models and path helpers (no UI, no libMBIN); `ArtifactX.GameData` = the catalog builder (the ONLY code that reads the game's PAK/MBIN files); `ArtifactX.WinUI3` = UI.
- **Never hardcode extractable data.** Game content (names, icons, values, descriptor words, fish tables) comes from the catalog (`nms_catalog.sqlite`, built on the player's PC from their own game install by `ArtifactX.GameData`). Structural enums come from the vendored libMBIN directly (`Enum.GetNames<...>()`), not copied lists.
- **libMBIN is ours** (`src/ArtifactX.Tools/NMS.Tools.libMBIN`). Fix or extend it directly when a struct/enum is missing or wrong (verify against real saves first). It's LGPL-3: note modifications in `THIRD-PARTY-NOTICES.md`.
- **Smooth UI:** page navigation shows immediately; disk I/O, SQLite and big JSON walks go to `Task.Run`. Never block the UI thread.
- **Dev/confidence notes go in C# doc comments** (on the `Nms*Paths.cs` class or the page/service summary), never in the UI. Document how a save field was confirmed (which test, which date).
- Match the surrounding code's style, naming and comment density. Zero compiler errors; fix warnings you introduce.

## 3. UI rules

- **Themes:** use the app's theme brushes (`ThemeTextBrush`, `ThemeTextDimBrush`, `ThemeAccentBrush`, `ThemeDoneBrush`, `ThemeLineBrush`, `ThemePanelAltBrush`, `ThemeVoidBrush`...) and styles (`ThemeCardStyle`, `ThemeEyebrowStyle`...). Both themes (Station Core, Galaxy Map) must work. Before using a new resource key, check it's defined in `Themes/*.xaml` or `App.xaml`.
- **Contrast:** aim for WCAG AA (4.5:1+). Grey text on the dark backgrounds is hard to read; carry meaning with colour (green good / yellow close / red over), not more grey.
- **Screen readers:** every icon-only button, every button whose content is a panel, and every input needs `AutomationProperties.Name`. Buttons that read the same need distinct names ("Generate a model seed", not three "Generate"). Records shown in a ComboBox override `ToString()`.
- **Never show raw save keys or ids** (`3Nc`, `^UP_SCAN0`, `STACONNECT`) to the player. Fallback names are plain and sequential ("Storage Container 1").
- **Use the game's own words.** Fish rarity: quality Rare reads "Uncommon", Epic reads "Rare". Biomes 8-10 (Red/Green/Blue) are "anomaly" worlds, not colours.
- **Equipment pages** (Starships, Multi-Tool, Freighter...): details card full width above the tech grid, collapsible; right sidebar = Templates only.
- **Live game updates:** pages follow the game through the app-wide feed `FishCatchRecorder.SnapshotSeen`; never start a page's own save watcher. Update in place - no page reloads, keep scroll position, new items animate in.
- Never mention the competing save editor NomNom anywhere (UI or comments) - say "a reference tool".

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
- **One namespace root.** Every project uses the `ArtifactX` prefix plus its layer: `ArtifactX.Core`, `ArtifactX.GameData`, `ArtifactX.WinUI3`, `ArtifactX.Tools.DataCataloger`, `ArtifactX.Core.Checks`. The vendored libMBIN keeps its own `libMBIN` namespace because it is third-party code.
- **Testable code.** Put logic behind small interfaces where it touches files, databases, the network or the running game, so it can be checked without them. The `tests/*.Checks` programs compare against real game data and native-emulator results; keep them and extend them. New pure-logic code may add xUnit tests with built-in asserts (or Shouldly). Don't add FluentAssertions or MediatR: both now need commercial licences.
- **CLI tools:** new command-line tools use System.CommandLine (clear help, POSIX-style verbs and options). Don't rewrite the existing DataCataloger commands just to convert them.
- **Future crowd-source API (ASP.NET Core):** a thin API layer over Core/application services, explicit versioning, dependency injection via interfaces, plain service classes rather than MediatR, global error handling returning `ProblemDetails`. Native AOT friendly where practical.
- **Show the structure in proposals:** when suggesting new code, name the target files and where each piece lives rather than one large file.
