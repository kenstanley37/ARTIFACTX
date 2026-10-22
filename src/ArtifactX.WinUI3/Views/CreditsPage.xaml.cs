using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using ArtifactX.WinUI3.ViewModels;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Credits: the people and projects ArtifactX builds on. Keep this list in
/// step with THIRD-PARTY-NOTICES.md (repo root) when a dependency is added,
/// and credit work by what it does for ArtifactX, in plain words.
/// </summary>
public sealed partial class CreditsPage : Page
{
    public IReadOnlyList<CreditSection> CreditSections { get; } = Sections.Select(s => new CreditSection(s.Section, s.Blurb, s.Items)).ToList();

    private static readonly (string Section, string Blurb, Credit[] Items)[] Sections =
    {
        ("THE COMMUNITY", "Research and tools from fellow No Man's Sky players.", new Credit[]
        {
            new("nms_namegen", "Stuart Coyle, had.sh and GoodGuysFree",
                "The reverse-engineered star system generator ArtifactX's system details are ported from: names, star colour, race, economy, wealth and conflict for any address.",
                "MIT", "https://github.com/hadsh/nms_namegen"),
            new("NMS systems ground truth", "had.sh, and the players who recorded systems on the community wiki",
                "1,000 hand-recorded star systems used to check that ArtifactX's generator gives the same answers as the game.",
                "Data", "https://glyphs.had.sh", "Open website"),
            new("MBINCompiler / libMBIN", "monkeyman192, emoose and contributors",
                "Reads the game's own data files: items, recipes, fish, icons and everything else in ArtifactX's game data.",
                "LGPL-3.0", "https://github.com/monkeyman192/MBINCompiler"),
            new("NMS-Tools", "Hugo Peters",
                "Half-precision number helpers used when reading the game's data files.",
                "MIT", "https://github.com/HugoPeters/NMS-Tools"),
        }),
        ("BUILT WITH", "Libraries inside the ArtifactX app.", new Credit[]
        {
            new("Windows App SDK / WinUI 3", "Microsoft", "The app's windows, controls and layout.", "MIT", "https://github.com/microsoft/WindowsAppSDK"),
            new(".NET", "Microsoft and the .NET community", "The runtime ArtifactX runs on, including System.Text.Json.", "MIT", "https://github.com/dotnet/runtime"),
            new("Windows Community Toolkit", "The .NET Foundation and contributors", "MVVM helpers and layout controls such as the wrapping card grids.", "MIT", "https://github.com/CommunityToolkit/Windows"),
            new("Json.NET (Newtonsoft.Json)", "James Newton-King", "Reads and writes your save files' JSON.", "MIT", "https://github.com/JamesNK/Newtonsoft.Json"),
            new("K4os.Compression.LZ4", "Milosz Krajewski", "Unpacks and repacks the game's compressed save files.", "MIT", "https://github.com/MiloszKrajewski/K4os.Compression.LZ4"),
            new("Microsoft.Data.Sqlite", "Microsoft", "Reads ArtifactX's game data and your own user.db (notes, finds, guides).", "MIT", "https://github.com/dotnet/efcore"),
            new("SQLitePCLRaw", "Eric Sink", "Connects .NET to SQLite.", "Apache-2.0", "https://github.com/ericsink/SQLitePCL.raw"),
            new("SQLite", "The SQLite developers", "The database engine behind the game data and user.db.", "Public domain", "https://sqlite.org", "Open website"),
            new("Velopack", "The Velopack team", "Installs and updates ArtifactX.", "MIT", "https://github.com/velopack/velopack"),
        }),
        ("GAME-DATA TOOLS", "Used by ArtifactX's own tool that builds its game data from your game files.", new Credit[]
        {
            new("Pfim", "Nick Babcock", "Decodes the game's DDS textures into item, fish and glyph icons.", "MIT", "https://github.com/nickbabcock/Pfim"),
            new("SkiaSharp", "Microsoft and the Mono team", "Resizes and saves those icons.", "MIT", "https://github.com/mono/SkiaSharp"),
            new("ZstdNet", "SKB Kontur", "Unpacks the game's compressed data archives.", "BSD-3-Clause", "https://github.com/skbkontur/ZstdNet"),
            new("Entity Framework Core", "Microsoft", "Writes the game data database.", "MIT", "https://github.com/dotnet/efcore"),
        }),
        ("FONTS", "", new Credit[]
        {
            new("Exo 2", "The Exo 2 Project Authors (Natanael Gama)", "Headings and numbers.", "SIL OFL 1.1", "https://github.com/googlefonts/Exo-2.0"),
            new("Titillium Web", "Accademia di Belle Arti di Urbino and students", "Body text.", "SIL OFL 1.1", "https://fonts.google.com/specimen/Titillium+Web", "Open website"),
        }),
        ("THE GAME", "", new Credit[]
        {
            new("No Man's Sky", "Hello Games",
                "The universe all of this is about. ArtifactX is a fan-made tool and is not made by, or associated with, Hello Games. No Man's Sky is a trademark of Hello Games.",
                "", "https://www.nomanssky.com", "Open website"),
        }),
    };

    public CreditsPage() => InitializeComponent();
}
