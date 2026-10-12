namespace ArtifactX.WinUI3.Models;

/// <summary>One row from CatalogService.SearchAsync. CategoryLabel is the save-file
/// "elv" value (Technology/Product/Substance) derived from the matched MBIN table,
/// not stored on CatalogItem itself - see CatalogService.ElvLabelFor.</summary>
/// <summary>A part that can go in an inventory, for item lists (the Corvette
/// Cache's parts list): Group is e.g. Structural / Decorative / Both.</summary>
public sealed record CatalogPart(string GameId, string Name, string Group, int? MaxStackSize);

public sealed class CatalogSearchResult
{
    public required string GameId { get; init; }
    public required string CategoryLabel { get; init; }

    /// <summary>Real stack cap for Product/Substance results (e.g. 20 for Metal
    /// Plating), null for Technology (which has no stack concept - see
    /// CatalogClassifier/CatalogItem.MaxStackSize for where this comes from).</summary>
    public int? MaxStackSize { get; init; }
}