namespace NMS.Tools.DataCataloger.Models;

/// <summary>
/// One discovered "table" MBIN, e.g. GcProductTable, GcTechnologyTable, GcSubstanceTable.
/// Discovered automatically by reflection (see CatalogClassifier) - not hardcoded - so
/// tables added/renamed by future NMS updates show up here without code changes.
/// </summary>
public class CatalogCategory
{
    public int Id { get; set; }

    /// <summary>The libMBIN template type name, e.g. "GcProductTable".</summary>
    public string TemplateType { get; set; } = "";

    /// <summary>The row type contained in the table, e.g. "GcProductData".</summary>
    public string RowType { get; set; } = "";

    /// <summary>Relative path of the source MBIN, e.g. "metadata/reality/tables/nms_reality_gcproducttable.mbin".</summary>
    public string SourceMbinPath { get; set; } = "";

    public List<CatalogItem> Items { get; set; } = new();
}