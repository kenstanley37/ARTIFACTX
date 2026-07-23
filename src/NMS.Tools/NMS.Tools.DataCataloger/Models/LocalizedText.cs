namespace NMS.Tools.DataCataloger.Models;

/// <summary>
/// Raw loc-key -> text pairs pulled straight from TkLocalisationTable.
/// Kept as its own table (rather than only caching resolved text on CatalogItem)
/// so a future run can add more languages without redoing the whole extraction.
/// English-only for now per current scope.
/// </summary>
public class LocalizedText
{
    public int Id { get; set; }
    public string LocKey { get; set; } = "";
    public string Language { get; set; } = "en";
    public string Text { get; set; } = "";
}