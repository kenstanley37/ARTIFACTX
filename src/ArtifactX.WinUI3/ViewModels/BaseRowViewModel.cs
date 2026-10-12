using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row on BasesPage - a planet base with its portal address. Read-only.</summary>
public sealed class BaseRowViewModel
{
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string Facts { get; init; }
    public required Brush Swatch { get; init; }
    public required string PortalCode { get; init; }
    public required List<ImageSource> Glyphs { get; init; }
    public required string DistanceLabel { get; init; }

    // Sort keys
    public required double SortDistance { get; init; }
    public required int Galaxy { get; init; }
}
