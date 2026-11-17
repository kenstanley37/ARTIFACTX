using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArtifactX.WinUI3.ViewModels.ShipDesignerViewModels;

/// <summary>Display helpers shared by the Ship Designer's choices.</summary>
internal static class ShipDesignerText
{
    public static string Title(string text) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());

    public static string PartName(string id) => Title(CatalogService.TryGet(id)?.DisplayName ?? id);

    public static ImageSource? PartIcon(string? id) => id is null ? null : CatalogService.TryGet(id)?.Icon;
}

/// <summary>One ship type in the builder (Fighter, Hauler...).</summary>
public sealed partial class ShipTypeChoiceViewModel(BuilderType type, Action<BuilderType> select) : ObservableObject
{
    public BuilderType Type { get; } = type;
    public string Title { get; } = ShipDesignerText.Title(type.Title);
    public string AccessibleName { get; } = type.Title;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => select(Type);
}

/// <summary>One slot of the ship (cockpit, wings...) with the part picked for it.</summary>
public sealed partial class ShipSlotChoiceViewModel(BuilderSlot slot, Action<BuilderSlot> select, Action<string> addToExosuit) : ObservableObject
{
    public BuilderSlot Slot { get; } = slot;
    public string Label { get; } = slot.Label.ToUpperInvariant();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PartName), nameof(Icon), nameof(AccessibleName))]
    public partial string? Item { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string PartName => Item is null ? "-" : ShipDesignerText.PartName(Item);
    public ImageSource? Icon => ShipDesignerText.PartIcon(Item);
    public string AccessibleName => $"{Slot.Label}: {(Item is null ? "none" : ShipDesignerText.PartName(Item))}";

    [RelayCommand]
    private void Select() => select(Slot);

    /// <summary>The right-click menu: the slot's picked part as an item in the Exosuit.</summary>
    [RelayCommand]
    private void AddToExosuit()
    {
        if (Item is { } item) addToExosuit(item);
    }
}

/// <summary>One part the current slot offers.</summary>
public sealed partial class ShipPartChoiceViewModel(string item, Action<string> select, Action<string> addToExosuit) : ObservableObject
{
    public string Item { get; } = item;
    public string Name { get; } = ShipDesignerText.PartName(item);
    public ImageSource? Icon { get; } = ShipDesignerText.PartIcon(item);
    public Visibility IconVisibility => Icon is null ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => select(Item);

    /// <summary>The right-click menu: this part as an item in the Exosuit.</summary>
    [RelayCommand]
    private void AddToExosuit() => addToExosuit(Item);
}

/// <summary>One paint style the ship type offers.</summary>
public sealed partial class PaintStyleChoiceViewModel(PaintStyle style, Action<PaintStyle> select) : ObservableObject
{
    public PaintStyle Style { get; } = style;
    public string Name { get; } = style.Name;

    /// <summary>The account hasn't unlocked this style (an expedition reward). It can still be picked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName), nameof(LockToolTip))]
    public partial bool IsLocked { get; set; }

    public string AccessibleName => IsLocked ? $"{Name}, locked" : Name;
    public string? LockToolTip => IsLocked ? "An expedition reward you haven't unlocked. You can still try it on." : null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => select(Style);
}

/// <summary>One colour in a colour group's swatch grid.</summary>
public sealed partial class SwatchChoiceViewModel(int index, Swatch swatch, Action<int> pick) : ObservableObject
{
    public int Index { get; } = index;
    public string Name { get; } = swatch.Name;
    public Brush Fill { get; } = new SolidColorBrush(Windows.UI.Color.FromArgb(255, Byte(swatch.Rgb[0]), Byte(swatch.Rgb[1]), Byte(swatch.Rgb[2])));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Outline), nameof(AccessibleName))]
    public partial bool IsPicked { get; set; }

    public Brush Outline => new SolidColorBrush(IsPicked ? Colors.White : Colors.Transparent);

    /// <summary>What a screen reader says: the colour, and whether it is the one picked.</summary>
    public string AccessibleName => IsPicked ? $"{Name}, picked" : Name;

    [RelayCommand]
    private void Pick() => pick(Index);

    private static byte Byte(float v) => (byte)Math.Clamp(v * 255f, 0f, 255f);
}

/// <summary>One colour group of the paint style (primary, secondary...): the game's own grid, two rows with half the
/// swatches in each. One list in a two-row grid, not one list per row: each list is its own Tab stop, so with
/// two rows Tab skipped the second row (2026-11-11, the owner's keyboard test).</summary>
public sealed partial class ColourGroupViewModel : ObservableObject
{
    private readonly Action<string, int> _picked;

    public ColourGroupViewModel(ColourGroup group, IReadOnlyList<Swatch> swatches, int current, Action<string, int> picked)
    {
        GroupTitle = group.Title;
        Title = group.Title.ToUpperInvariant();
        _picked = picked;
        for (int i = 0; i < swatches.Count; i++)
            Swatches.Add(new SwatchChoiceViewModel(i, swatches[i], Pick));
        Mark(current);
    }

    public string GroupTitle { get; }
    public string Title { get; }

    /// <summary>Every colour, in the game's order; the page lays them out in two rows.</summary>
    public List<SwatchChoiceViewModel> Swatches { get; } = new();

    [ObservableProperty]
    public partial string PickedName { get; set; } = "";

    private void Pick(int index)
    {
        Mark(index);
        _picked(GroupTitle, index);
    }

    private void Mark(int index)
    {
        foreach (var s in Swatches) s.IsPicked = s.Index == index;
        PickedName = index >= 0 && index < Swatches.Count ? Swatches[index].Name : "";
    }
}

/// <summary>A card in My designs: the picture, name and type/style. Click to load; the corner button deletes.</summary>
public sealed partial class SavedDesignCardViewModel(SavedDesign saved, string subtitle, Action<SavedDesignCardViewModel> load,
    Action<SavedDesignCardViewModel> delete) : ObservableObject
{
    public SavedDesign Saved { get; } = saved;
    public ShipDesign Design => Saved.Design;
    public string Name => Design.Name;
    public string Subtitle { get; } = subtitle;
    public string LoadToolTip => $"Load \"{Design.Name}\"";
    public string LoadName => $"Load {Design.Name}";
    public string DeleteName => $"Delete {Design.Name}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaceholderVisibility))]
    public partial ImageSource? Thumbnail { get; set; }

    public Visibility PlaceholderVisibility => Thumbnail is null ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Load() => load(this);

    [RelayCommand]
    private void Delete() => delete(this);
}
