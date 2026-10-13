namespace ArtifactX.WinUI3.Models;

/// <summary>One row from CatalogService.SearchAsync. CategoryLabel is the save-file
/// "elv" value (Technology/Product/Substance) derived from the matched MBIN table,
/// not stored on CatalogItem itself - see CatalogService.ElvLabelFor.</summary>
/// <summary>A part that can go in an inventory, for item lists (the Corvette
/// Cache's parts list): Group is e.g. Structural / Decorative / Both.</summary>
/// <summary>An item offered in an ItemListPanel. For upgrade modules, CountKey
/// is the module family ("UP_PULSE") every installed copy counts towards,
/// Limit how many the game allows, and Seeded means each copy gets its own
/// "#seed" (the game rolls the module's stats from it). Stat is the main stat
/// the tech works on (from the catalog's TechStats, e.g. "Ship_Hyperdrive"),
/// Primary marks a device's main item (Hyperdrive vs its Cadmium Drive).</summary>
public sealed record CatalogPart(string GameId, string Name, string Group, int? MaxStackSize,
    string? CountKey = null, int Limit = 1, bool Seeded = false, string? Stat = null, bool Primary = false,
    string? Colour = null);

public sealed class CatalogSearchResult
{
    public required string GameId { get; init; }
    public required string CategoryLabel { get; init; }

    /// <summary>Real stack cap for Product/Substance results (e.g. 20 for Metal
    /// Plating), null for Technology (which has no stack concept - see
    /// CatalogClassifier/CatalogItem.MaxStackSize for where this comes from).</summary>
    public int? MaxStackSize { get; init; }
}