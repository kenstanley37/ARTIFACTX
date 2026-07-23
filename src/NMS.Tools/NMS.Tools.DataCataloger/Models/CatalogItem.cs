namespace NMS.Tools.DataCataloger.Models;

public class CatalogItem
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    public CatalogCategory? Category { get; set; }

    /// <summary>The row's own game ID, e.g. "CASING". This is what save-game data references.</summary>
    public string GameId { get; set; } = "";

    /// <summary>Loc key for the primary display name, e.g. "CASING_NAME" (null if none found).</summary>
    public string? NameLocKey { get; set; }

    /// <summary>Resolved English text for NameLocKey, cached here for zero-join lookups.</summary>
    public string? NameEnglish { get; set; }

    /// <summary>Loc key for the short/lowercase variant, e.g. "CASING_NAME_L".</summary>
    public string? NameLowerLocKey { get; set; }
    public string? NameLowerEnglish { get; set; }

    /// <summary>Loc key for description text, if present.</summary>
    public string? DescriptionLocKey { get; set; }
    public string? DescriptionEnglish { get; set; }

    public List<IconAsset> Icons { get; set; } = new();
}