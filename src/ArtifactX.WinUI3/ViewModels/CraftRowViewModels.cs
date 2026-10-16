using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One line of CraftingPage's ingredient tree, indented by depth.</summary>
public sealed class CraftTreeRowViewModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ImageSource? Icon { get; init; }
    public required string Amount { get; init; }
    public required Thickness Indent { get; init; }
    public required string Tag { get; init; }
    public required Brush TagBrush { get; init; }
    public required bool IsCrafted { get; init; }
    public required string ToolTip { get; init; }
}

/// <summary>One raw material in CraftingPage's shopping list.</summary>
public sealed class CraftRawRowViewModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ImageSource? Icon { get; init; }
    public required string Total { get; init; }
    public required string TotalLabel { get; init; }
    public required string Have { get; init; }
    public required Brush HaveBrush { get; init; }
    public required Visibility HaveVisibility { get; init; }
    /// <summary>Raw counts for the CSV export.</summary>
    public long Needed { get; init; }
    public long Carried { get; init; }
    public required string RefineHint { get; init; }
    public required Visibility RefineVisibility { get; init; }
    public required string RefineToolTip { get; init; }
}
