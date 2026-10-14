# Game data (the item catalog)

ArtifactX shows real item names, icons, recipes, fish, tech stats and more
instead of raw ids like `ASTEROID2`. All of that comes from **the player's own
copy of No Man's Sky**: ArtifactX reads the game's files on the player's PC
and builds its catalog there. **No game data ships with ArtifactX.**

## In the app

- **First run:** ArtifactX opens on a *Set up ArtifactX* page instead of Save
  Selection. It finds the game (Steam, GOG or Xbox app installs; or the player
  chooses the folder), then **Read my game files** builds the catalog with a
  progress bar, the current step and a plain-words activity feed. A save can't
  be opened until this is done - editing relies on the catalog (stack limits,
  slot counts, which technology goes where).
- **After a game update:** a bar offers *Update game data*. The app stays
  usable meanwhile with the previous data.
- **Settings → Game data:** where the game is, which build, when the data was
  read, Update / Change game folder / Open build log.

The catalog is stored in `%LOCALAPPDATA%\ArtifactX\GameData\nms_catalog.sqlite`
(build log alongside it).

## How ArtifactX knows the game updated

Nothing is typed in by hand. When the catalog is built it records, in a
`GameDataInfo` table:

- the **game fingerprint**: `NMS.exe`'s file version plus the name and size of
  every `.pak` in `GAMEDATA/PCBANKS` - it changes with every patch;
- the **catalog format** (`GameDataBuilder.CatalogFormat`): bumped when
  ArtifactX starts collecting something new, so every player rebuilds;
- `NMS.exe`'s version, the Steam build id when it's a Steam install, the time
  and the item count.

On start, ArtifactX compares the installed game's fingerprint with the
catalog's. The title bar shows the installed game's own build
(e.g. "NMS build 180836").

## Code

- **`src/ArtifactX.GameData`** - the builder, used by both the app and the
  developer tool:
  - `GameInstall` - finds an install and fingerprints it.
  - `GameDataBuilder.Build` - the whole build: the main build
    (`CatalogBuildService`), every step in `CatalogSteps` (language words,
    guide topics, glyphs, freighter paints, corvette parts, tech stats,
    system/planet descriptions, recipes, item values, fish, crafting,
    procedural products), the trim to the small catalog the app reads, and
    the `GameDataInfo` stamp. Works in a scratch folder and only replaces the
    old catalog once the new one is complete.
- **`src/ArtifactX.WinUI3/Services/GameDataService.cs`** - the app side:
  where the catalog lives, its state against the install, and the background
  build.

The main build only decodes tables under `METADATA/` (about 1,000 files):
every table the app uses lives there, and decoding all 81,803 MBINs - mostly
3D models - was most of an 18-minute build. `CatalogBuildService.ScanAllFolders`
turns the full scan back on for research.

## Developer tool

`src/ArtifactX.Tools/ArtifactX.Tools.DataCataloger` is a command-line front
end to the same library, plus research commands.

```bash
cd src/ArtifactX.Tools/ArtifactX.Tools.DataCataloger
dotnet run -- build-all out.sqlite                    # what the app does
dotnet run -- build-all out.sqlite "D:\Games\No Man's Sky"
dotnet run -- add-recipes out.sqlite                  # one step, on an existing catalog
dotnet run -- locid UI_BONES   # / locval / grep / dumpfile / dumptable / inspect
```

The research commands read the game folder from `config.json` (the
`PCBANKS` path).

Developer builds of the app (Debug) still fall back to a hand-built
`src/ArtifactX.WinUI3/Data/nms_catalog.sqlite` if one is there; release builds
never include it.

## Why GameId lookups filter on `NameEnglish IS NOT NULL`

`GameId` is **not unique** in the catalog - e.g. `ENERGY` has several rows
across different source tables, only one of which has a real display name.
No `GameId` ever has two *different* non-null names, so
`WHERE GameId = ? AND NameEnglish IS NOT NULL` is a safe, unambiguous lookup -
never guess a matching row without that filter.
