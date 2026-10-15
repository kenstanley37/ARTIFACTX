# Third-party notices

ArtifactX includes work from the following projects.

## nms_namegen

ArtifactX's star system generator (`src/ArtifactX.Core/Universe/`: star colour,
economy, wealth, conflict, dominant race, system flags, planet layout, and
region/system/planet names) is a C# port of **nms_namegen**, and its letter
data (`namegen-data.json.gz`) comes from that project.

- Project: https://github.com/hadsh/nms_namegen
- Authors: Stuart Coyle, had.sh, GoodGuysFree
- Licence: MIT

```
MIT License

Copyright (c) 2026 Stuart Coyle & had.sh & GGF

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The C# port was checked against nms_namegen's own golden test vectors (443
systems, every field identical) and its 1,000-system hand-recorded ground
truth (same accuracy as the original: star colour 99.1%, race 99.1%,
economy 99.0%, wealth 98.9%, conflict 99.0%, region names 100%).
ArtifactX and nms_namegen are fan projects and are not associated with
Hello Games.

## NMS Shipwright

How ArtifactX works out a starship's parts from its seed
(`src/ArtifactX.Core/Procedural/ShipParts.cs`: the game's random generator as
seeded for models, weighted part picks, nested part seeds) is a C# port of the
part selector in **NMS Shipwright**'s `nms_procgen/generator.py`. No code from
it runs in ArtifactX and no files from it are included; the part trees come
from the player's own game files.

- Project: https://github.com/S-T-0-7/NMS-Shipwright
- Author: Shikhar Tiwari
- Licence: MIT

```
MIT License

Copyright (c) 2026 Shikhar Tiwari

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

---

This software is not affiliated with, endorsed by or connected to Hello Games.
No Man's Sky is a trademark of Hello Games. No game files are included in this
repository or in the released application; everything the app needs is read
from the user's own installed copy of the game at runtime.
```

The port was checked against the original on 2,000 seeds across 8 ship types
(every part identical) and against starships seen in game.

The starship paint colours (`src/ArtifactX.Core/Procedural/ShipColours.cs`) are
a C# port of the palette picker in NMS Shipwright's `nms_procgen/textures.py`,
checked against the original on 3,000 seeds (every colour identical, bit for
bit).

## MBINCompiler / libMBIN

ArtifactX's game-data reader (`src/ArtifactX.Tools/NMS.Tools.libMBIN`) is
libMBIN from MBINCompiler. It reads the game's own data files.

- Project: https://github.com/monkeyman192/MBINCompiler (originally emoose/MBINCompiler)
- Authors: monkeyman192, emoose and contributors
- Licence: GNU LGPL 3.0. Full text in `licenses/LGPL-3.0.txt`, with the GNU
  GPL 3.0 it builds on in `licenses/GPL-3.0.txt`; both ship with the app.

ArtifactX keeps its own copy of libMBIN and may change it (for example to
add game data structures newer than the upstream release). Any changes are
part of this repository's public source, so the modified library's source is
always available under the LGPL.

ArtifactX's copy is upstream **libMBIN 7.07.0** (MBINCompiler tag
`v7.07.0-pre1`, game build 25800958), updated 2026-10-15.

Changes made by ArtifactX to its copy of libMBIN:
- `Common/Async.cs`: one null-forgiving operator (`null!`) to silence a
  nullable-reference warning; no change in behaviour.
- The DataCataloger tool's `dumpfile` command prints the full error chain when a
  file can't be decoded (ArtifactX code, not libMBIN).

## NMS-Tools

The half-precision float helpers in libMBIN's `BinaryStreamExtensions.cs`
come from NMS-Tools.

- Project: https://github.com/HugoPeters/NMS-Tools
- Author: Hugo Peters
- Licence: MIT

## NMS systems ground truth

The 1,000 hand-recorded systems used to check the generator come from
had.sh's corpus (https://glyphs.had.sh), recorded by players on the No Man's
Sky community wiki.

## Fonts

- **Exo 2**: Copyright 2013 The Exo 2 Project Authors
  (https://github.com/googlefonts/Exo-2.0). SIL Open Font License 1.1, full
  text in `src/ArtifactX.WinUI3/Assets/Fonts/Exo2-OFL.txt`.
- **Titillium Web**: Copyright (c) 2009-2011 by Accademia di Belle Arti di
  Urbino and students of MA course of Visual design. SIL Open Font License
  1.1, full text in `src/ArtifactX.WinUI3/Assets/Fonts/TitilliumWeb-OFL.txt`.

## Packages

App:

| Package | Licence | Project |
|---|---|---|
| Microsoft.WindowsAppSDK (WinUI 3) | MIT | https://github.com/microsoft/WindowsAppSDK |
| .NET runtime, System.Text.Json | MIT | https://github.com/dotnet/runtime |
| CommunityToolkit.Mvvm, CommunityToolkit.WinUI.Controls.Primitives | MIT | https://github.com/CommunityToolkit |
| Newtonsoft.Json | MIT | https://github.com/JamesNK/Newtonsoft.Json |
| K4os.Compression.LZ4 (+ Streams) | MIT | https://github.com/MiloszKrajewski/K4os.Compression.LZ4 |
| Microsoft.Data.Sqlite | MIT | https://github.com/dotnet/efcore |
| SQLitePCLRaw | Apache-2.0 | https://github.com/ericsink/SQLitePCL.raw |
| SQLite | Public domain | https://sqlite.org |
| Velopack | MIT | https://github.com/velopack/velopack |

Game-data tool (DataCataloger, not shipped in the app):

| Package | Licence | Project |
|---|---|---|
| Pfim | MIT | https://github.com/nickbabcock/Pfim |
| SkiaSharp | MIT | https://github.com/mono/SkiaSharp |
| ZstdNet | BSD-3-Clause | https://github.com/skbkontur/ZstdNet |
| Entity Framework Core | MIT | https://github.com/dotnet/efcore |

No Man's Sky is a trademark of Hello Games. ArtifactX is a fan-made tool and
is not made by, or associated with, Hello Games.
