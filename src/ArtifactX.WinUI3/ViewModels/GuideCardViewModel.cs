using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One saved video on GuidesPage. Read-only; edits go through the form.</summary>
public sealed class GuideCardViewModel
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string Title { get; init; }
    public required string Creator { get; init; }
    public required string Platform { get; init; }
    public required Brush PlatformBrush { get; init; }
    public ImageSource? Thumbnail { get; init; }
    public required Visibility ThumbnailVisibility { get; init; }
    public required string Topic { get; init; }
    public required Visibility TopicVisibility { get; init; }
    public required List<FindItemChip> Items { get; init; }
    public required Visibility ItemsVisibility { get; init; }
    public required string Notes { get; init; }
    public required Visibility NotesVisibility { get; init; }
}
