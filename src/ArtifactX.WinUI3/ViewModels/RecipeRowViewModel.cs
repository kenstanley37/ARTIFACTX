using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item in a recipe (ingredient or result): icon, amount, name.</summary>
public sealed class RecipeItemViewModel
{
    public required string ItemId { get; init; }
    public required string Label { get; init; }
    public ImageSource? Icon { get; init; }
}

/// <summary>One recipe card on RecipesPage. Pinning updates the card in place
/// (no list rebuild, so the grid keeps its scroll position).</summary>
public sealed class RecipeRowViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public required string RecipeId { get; init; }
    public required List<RecipeItemViewModel> Ingredients { get; init; }
    public required RecipeItemViewModel Result { get; init; }
    public required string Name { get; init; }
    public required string Station { get; init; }

    /// <summary>"Horrifying Mush + Sievert Beans" - for the compact pinned tiles.</summary>
    public string IngredientsText => string.Join(" + ", Ingredients.Select(i => i.Label));

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value) return;
            _isPinned = value;
            PropertyChanged?.Invoke(this, new(nameof(IsPinned)));
            PropertyChanged?.Invoke(this, new(nameof(CardBorder)));
            PropertyChanged?.Invoke(this, new(nameof(CardBorderThickness)));
            PropertyChanged?.Invoke(this, new(nameof(PinGlyph)));
            PropertyChanged?.Invoke(this, new(nameof(PinTip)));
        }
    }

    private bool _isHighlighted;
    /// <summary>The recipe picked from the Pinned strip - outlined on both its
    /// pinned tile and its card until another pin is picked.</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value) return;
            _isHighlighted = value;
            PropertyChanged?.Invoke(this, new(nameof(IsHighlighted)));
            PropertyChanged?.Invoke(this, new(nameof(CardBorder)));
            PropertyChanged?.Invoke(this, new(nameof(CardBorderThickness)));
            PropertyChanged?.Invoke(this, new(nameof(TileBorder)));
            PropertyChanged?.Invoke(this, new(nameof(TileBorderThickness)));
        }
    }

    public Microsoft.UI.Xaml.Media.Brush CardBorder =>
        ArtifactX.WinUI3.Services.ThemeResources.Brush(IsHighlighted ? "ThemeDoneBrush" : IsPinned ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Microsoft.UI.Xaml.Thickness CardBorderThickness => new(IsHighlighted ? 3 : IsPinned ? 2 : 1);
    public Microsoft.UI.Xaml.Media.Brush TileBorder =>
        ArtifactX.WinUI3.Services.ThemeResources.Brush(IsHighlighted ? "ThemeDoneBrush" : "ThemeAccentBrush");
    public Microsoft.UI.Xaml.Thickness TileBorderThickness => new(IsHighlighted ? 3 : 1);
    public string PinGlyph => IsPinned ? "\uE841" : "\uE718"; // PinnedFill / Pin
    public string PinTip => IsPinned ? "Unpin this recipe" : "Pin this recipe to the top of the page";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
