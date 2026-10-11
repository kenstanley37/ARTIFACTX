using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row on ItemValuesPage. Read-only.</summary>
public sealed class ItemValueRowViewModel
{
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string Value { get; init; }
    public ImageSource? Icon { get; init; }
}
