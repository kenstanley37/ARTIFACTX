using Microsoft.UI.Xaml.Media.Imaging;

namespace ArtifactX.WinUI3.Models;

public sealed class CatalogEntry
{
    public required string DisplayName { get; init; }
    public BitmapImage? Icon { get; init; }

    /// <summary>The icon's PNG bytes, for derived pictures such as the Catalog page's
    /// silhouettes (Helpers.Catalog.SilhouetteImages).</summary>
    public byte[]? IconPng { get; init; }
    public string? CatalogGroup { get; init; }
    public string? TemplateType { get; init; }
}
