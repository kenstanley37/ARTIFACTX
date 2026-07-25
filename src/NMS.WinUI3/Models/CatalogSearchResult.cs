namespace NMS.WinUI3.Models;

/// <summary>One row from CatalogService.SearchAsync. CategoryLabel is the save-file
/// "elv" value (Technology/Product/Substance) derived from the matched MBIN table,
/// not stored on CatalogItem itself - see CatalogService.ElvLabelFor.</summary>
public sealed class CatalogSearchResult
{
    public required string GameId { get; init; }
    public required string CategoryLabel { get; init; }

    /// <summary>Real stack cap for Product/Substance results (e.g. 20 for Metal
    /// Plating), null for Technology (which has no stack concept - see
    /// CatalogClassifier/CatalogItem.MaxStackSize for where this comes from).</summary>
    public int? MaxStackSize { get; init; }
}