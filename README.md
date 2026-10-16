# ArtifactX

A save editor and companion app for **No Man's Sky**, for Windows. Built
with .NET 10 and WinUI 3.

ArtifactX edits your saves, and it also follows your game while you play:
it records where you catch fish, keeps notes on the planets and systems you
visit, and shows what a starship seed gives before you commit to it.

> **Status: early alpha (v1.0.0-alpha1).** No release has been published
> yet, so for now you build it from source (see [Building](#building)).
> Tested on Steam saves only. **Back up your saves** - ArtifactX makes a
> backup every time it writes, but it's still young software.

## What it does

### Save editor
- **Character and gear:** General (units, nanites, quicksilver, save name,
  difficulty), Exosuit, Multi-Tool, Starships, Freighter, Freighter Upgrades,
  Frigates, Squadron and Exocraft. Each inventory grid has a list of every
  item or technology that fits beside it, with adjacency arranging for tech.
- **Starships from seeds:** see which parts a seed gives (by the game's own
  part names and icons), search for a seed with the parts you want, and
  preview the ship's 3D shape, built from your own game files.
- **Storage:** Base Storage, Cold Storage, Ingredient Storage, Corvette
  Cache, and Dismantled Base Components (display only, with a reminder
  badge when items are waiting).
- **Templates:** save a tech stack or a whole build, share it as a file, or
  add it as a new ship, multi-tool or freighter.
- **Progress:** Milestones, the five alien languages, Catalogue (known
  technology), Fishing Records, Companions, Settlements (including what each
  building stage needs and where to buy it) and Space Stations.
- **Account data:** catalog, Guide, Expedition and Twitch rewards
  (`accountdata.hg`).

### Explore (follows your game - never edits your save)
- **Planets:** every system and planet you've discovered, with your
  coordinates on the planet, your own notes, names for planets renamed by
  their discoverer, and the fish caught on each.
- **Fishing:** every fish, which planet types it lives on, and where to catch
  it - including the planets where ArtifactX recorded you catching it.
- **Finds** (great ships, multi-tools, shops, fishing spots - with the
  portal address to get back), **Near Core** and **Bases**.
- While ArtifactX is open - on any page, even minimised - it notes where you
  catch each fish. The game's save only keeps how many you caught, so keep
  ArtifactX open while you play for the most detail.

### Reference
- **Recipes**, **Crafting** (full crafting trees down to raw materials),
  **Item Values** and **Guides** (your own list of creator videos).

### Safety
- **Nothing is written until you click Save**, with Reset per page and for
  the whole save.
- **A backup on every save**, to any folder you choose (a cloud-synced folder
  works), with a restore picker.
- **Saving is blocked while No Man's Sky is running**, and ArtifactX notices
  when the game writes a newer save and offers to switch to it.

### No game data shipped
Item names, icons, recipes and the rest are read from **your own copy of No
Man's Sky** the first time ArtifactX starts (about a minute and a half), and
again after game updates. ArtifactX ships none of the game's files. See
[docs/DATACATALOGER.md](docs/DATACATALOGER.md).

## Platform support

| Platform | Status |
|---|---|
| Steam (PC) | ✅ Read, edit and save confirmed in game |
| GOG (PC) | ⚠️ Same save format as Steam; untested |
| Xbox app / Microsoft Store (PC) | ❌ Save folder found, but its container isn't decoded yet |
| PlayStation, Switch | ❌ Not supported |

Testing on GOG or Xbox is one of the most useful ways to help - see
[docs/CONTRIBUTING.md](docs/CONTRIBUTING.md).

## Building

You need:
- Windows 10 or 11
- the [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio with the **WinUI application development** workload

```bash
git clone https://github.com/kenstanley37/ARTIFACTX.git
```

Open `NmsSaveEditor.slnx`, set **ArtifactX.WinUI3** as the startup project
and run (x64). ArtifactX finds your No Man's Sky install and save folders on
its own; you can also point it at them yourself.

## How it's put together

| Project | What it is |
|---|---|
| `ArtifactX.WinUI3` | The app: pages, the shared save session, inventory grids and item lists |
| `ArtifactX.Core` | The save format (decompress, read, write) and what the save's obfuscated keys mean |
| `ArtifactX.GameData` | Builds the item catalog from the player's game files; ship models for the preview |
| `ArtifactX.Almanac` | Fixed reference data (starship, multi-tool and freighter types) |
| `ArtifactX.Tools.DataCataloger` | Command-line front end to the catalog builder, for development and research |
| `ArtifactX.Tools.SaveInspector` | Command-line tool for digging into save files |
| `NMS.Tools.libMBIN` | Our copy of libMBIN, which reads the game's data files |

More in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Credits

ArtifactX builds on the work of others - see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for licences:
- **[libMBIN / MBINCompiler](https://github.com/monkeyman192/MBINCompiler)**
  (LGPL 3.0) - reading the game's data files.
- **[NMS Shipwright](https://github.com/S-T-0-7/NMS-Shipwright)** (MIT) -
  how a seed picks a starship's parts, and its 3D model.
- **[nms_namegen](https://github.com/hadsh/nms_namegen)** (MIT) - systems
  (names, star, economy, planet layout) from their addresses.

No Man's Sky is a trademark of Hello Games. ArtifactX is a fan project and
isn't affiliated with or endorsed by Hello Games.

## Licence

ArtifactX is free software under the [GNU GPL 3.0](LICENSE): you can use,
change and share it, and anything built from it must stay open source under
the same licence.

## Contributing

Bug reports, platform testing and help mapping more of the save file are all
welcome - see [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md).
