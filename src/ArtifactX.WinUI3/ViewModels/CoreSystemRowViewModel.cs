using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row on NearCorePage - a known star system and how close it is
/// to its galaxy's core. Read-only.</summary>
public sealed class CoreSystemRowViewModel
{
    public required string Rank { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string CoreDistance { get; init; }
    public required string FromYou { get; init; }
    public required string PortalCode { get; init; }
    public required List<ImageSource> Glyphs { get; init; }
    public required string NotesLabel { get; init; }
    public required Visibility NotesVisibility { get; init; }

    /// <summary>The system you're in: accent border + "YOU ARE HERE" badge.</summary>
    public required Brush RowBorder { get; init; }
    public required Thickness RowBorderThickness { get; init; }
    public required Visibility HereVisibility { get; init; }
}
