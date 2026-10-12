using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A small icon + label shown on a "Sells items" find card.</summary>
public sealed class FindItemChip
{
    public required string Label { get; init; }
    public ImageSource? Icon { get; init; }
}

/// <summary>One card on FindsPage. Read-only; edits go through the form.</summary>
public sealed class FindCardViewModel
{
    public required string Id { get; init; }
    public required string TypeLabel { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required Visibility DetailVisibility { get; init; }
    public required string ClassLetter { get; init; }
    public required Visibility ClassVisibility { get; init; }
    public required Brush ClassBrush { get; init; }
    public required List<FindItemChip> Items { get; init; }
    public required Visibility ItemsVisibility { get; init; }
    public required string Place { get; init; }
    public required string Facts { get; init; }
    public required string Distance { get; init; }
    public required string PortalCode { get; init; }
    public required List<ImageSource> Glyphs { get; init; }
    public required string Notes { get; init; }
    public required Visibility NotesVisibility { get; init; }
}
