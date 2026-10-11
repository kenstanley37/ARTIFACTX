using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One fish card on FishingPage. Read-only.</summary>
public sealed class FishCardViewModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ImageSource? Icon { get; init; }
    public required string Rarity { get; init; }
    public required Brush RarityBrush { get; init; }
    public required string Traits { get; init; }
    public required string Habitat { get; init; }
    public required string Caught { get; init; }
    public required Brush CaughtBrush { get; init; }
    public required double CardOpacity { get; init; }
    public required string Spots { get; init; }
    public required Visibility SpotsVisibility { get; init; }
}
