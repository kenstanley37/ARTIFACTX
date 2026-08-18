using Microsoft.UI.Xaml.Media.Imaging;

namespace ArtifactX.WinUI3.Models;

public sealed class CatalogEntry
{
    public required string DisplayName { get; init; }
    public BitmapImage? Icon { get; init; }
}