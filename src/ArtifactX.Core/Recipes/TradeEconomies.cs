namespace ArtifactX.Core.Recipes;

/// <summary>
/// Which economy deals in a trade good: the game's TradeCategory on the 35 trade goods (GcProductTable), mapped to the
/// economy names the Galaxy Map uses (and the system notes record). Moved from the desktop's Item Values page
/// (2026-11-18) so the phone's says the same.
/// </summary>
public static class TradeEconomies
{
    /// <summary>Trade category to economy, in the order the pickers list them.</summary>
    public static readonly IReadOnlyList<(string Category, string Economy)> All =
    [
        ("Commodity", "Trading"),
        ("Mineral", "Mining"),
        ("Component", "Manufacturing"),
        ("Tech", "Technology"),
        ("Exotic", "Scientific"),
        ("Energy", "Power Generation"),
        ("Alloy", "Advanced Materials"),
    ];

    /// <summary>The economy that trades an item of <paramref name="category"/>, or null for anything that isn't a
    /// trade good.</summary>
    public static string? EconomyFor(string category) => All.FirstOrDefault(e => e.Category == category).Economy;

    /// <summary>"Raw substance · Trade good · Mining economies · Contraband - sold at outlaw stations".</summary>
    public static string Detail(bool isSubstance, string category, bool illegal)
    {
        var parts = new List<string> { isSubstance ? "Raw substance" : "Product" };
        if (EconomyFor(category) is string economy) parts.Add($"Trade good · {economy} economies");
        if (illegal) parts.Add("Contraband - sold at outlaw stations");
        return string.Join(" · ", parts);
    }
}
