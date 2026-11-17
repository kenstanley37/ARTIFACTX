using ArtifactX.Core.Theming;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.Maui.Models;

public sealed partial class PortalReviewSlot : ObservableObject
{
    public PortalReviewSlot(int position) { Position = position; IsSelected = position == 1; }
    public int Position { get; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label), nameof(PathData), nameof(AccessibleName))]
    public partial char? Digit { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label), nameof(AccessibleName))]
    public partial bool NeedsReview { get; set; }
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
    public string Label => $"{Position}{(NeedsReview ? " ?" : "")}";
    public string PathData => Digit is char c ? ArtifactXPortalGlyphs.ForDigit(c).PathData : "M0,0";
    public string AccessibleName => $"Position {Position}, {(Digit is char c ? ArtifactXPortalGlyphs.ForDigit(c).Name : "empty")}{(NeedsReview ? ", check this glyph" : "")}";
}

public sealed record PortalGalaxyOption(int Number, string Name)
{
    public string DisplayName => $"{Name} ({Number})";
    public override string ToString() => DisplayName;
}
