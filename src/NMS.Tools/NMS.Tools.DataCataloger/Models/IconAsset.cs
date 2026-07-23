namespace NMS.Tools.DataCataloger.Models;

public class IconAsset
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public CatalogItem? Item { get; set; }

    /// <summary>Which field this came from on the source row, e.g. "Icon" or "HeroIcon".</summary>
    public string SourceField { get; set; } = "";

    public int IconBlobId { get; set; }
    public IconBlob? IconBlob { get; set; }
}