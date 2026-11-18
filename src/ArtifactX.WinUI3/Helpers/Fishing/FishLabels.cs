using System;
using System.Globalization;

namespace ArtifactX.WinUI3.Helpers.Fishing;

/// <summary>Plain words and colours for the fish cards on Fishing.</summary>
public static class FishLabels
{
    public static int RarityRank(string quality) => quality switch
    {
        "Common" => 0, "Rare" => 1, "Epic" => 2, "Legendary" => 3, _ => 4,
    };

    /// <summary>Rarity colours - fixed like the class badges on Finds, not themed.</summary>
    public static Windows.UI.Color RarityColour(string quality) => quality switch
    {
        "Rare" => Windows.UI.Color.FromArgb(255, 60, 110, 200),
        "Epic" => Windows.UI.Color.FromArgb(255, 130, 80, 200),
        "Legendary" => Windows.UI.Color.FromArgb(255, 201, 151, 34),
        _ => Windows.UI.Color.FromArgb(255, 95, 110, 120),
    };

    public static string Size(string size) => size switch
    {
        "ExtraLarge" => "Extra large",
        "" => "",
        _ => size,
    };

    public static string Time(string time) => time switch
    {
        "Day" => "Day only",
        "Night" => "Night only",
        _ => "Day or night",
    };

    /// <summary>"#RRGGBB" as a colour (NmsPlanets.TypeColour).</summary>
    public static Windows.UI.Color ParseColour(string hex) => Windows.UI.Color.FromArgb(255,
        byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber), byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber),
        byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber));
}
