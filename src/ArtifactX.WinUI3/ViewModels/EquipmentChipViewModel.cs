using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

public enum EquipmentChipKind { Item, Gone, EmptySlot }

/// <summary>
/// One button in the Multi-Tool or Starships selector strip (EquipmentStrip): an item the
/// save holds, one the game no longer has (GONE, faded until the save is reloaded), or an
/// empty slot. What LiveItemsService found while the game played gets a NEW pill and
/// slides in once.
/// </summary>
public sealed class EquipmentChipViewModel
{
    private EquipmentChipViewModel(EquipmentChipKind kind, int index, string name)
    {
        Kind = kind;
        Index = index;
        Name = name;
    }

    public static EquipmentChipViewModel Item(int index, string name, bool isSelected, bool isActive, bool isNew, bool animateIn, bool isCorvette = false) =>
        new(EquipmentChipKind.Item, index, name)
        {
            IsSelected = isSelected,
            IsActive = isActive,
            IsNew = isNew,
            AnimateIn = animateIn,
            IsCorvette = isCorvette,
        };

    public static EquipmentChipViewModel Gone(GoneItem item) => new(EquipmentChipKind.Gone, -1, item.Name);

    public static EquipmentChipViewModel EmptySlot(string tip) => new(EquipmentChipKind.EmptySlot, -1, "Empty slot") { EmptyTip = tip };

    public EquipmentChipKind Kind { get; }
    public int Index { get; }
    public string Name { get; }

    public bool IsSelected { get; private init; }
    public bool IsActive { get; private init; }
    public bool IsCorvette { get; private init; }
    public bool IsNew { get; private init; }

    /// <summary>Plays the slide-in once, the first time a new item is shown.</summary>
    public bool AnimateIn { get; private init; }

    private string? EmptyTip { get; init; }

    public bool IsItem => Kind == EquipmentChipKind.Item;
    public bool IsGone => Kind == EquipmentChipKind.Gone;
    public bool IsEmptySlot => Kind == EquipmentChipKind.EmptySlot;

    public Windows.UI.Text.FontWeight NameWeight => IsSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

    public Brush Border => ThemeResources.Brush(IsSelected || IsNew ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Thickness BorderThickness => new(IsSelected || IsNew ? 2 : 1);
    public Brush Background => IsEmptySlot ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        : ThemeResources.Brush(IsSelected ? "ThemeSelectedFillBrush" : "ThemePanelAltBrush");
    public double Opacity => Kind switch { EquipmentChipKind.Gone => 0.45, EquipmentChipKind.EmptySlot => 0.55, _ => 1 };

    public string? Tip => Kind switch
    {
        EquipmentChipKind.Gone => "No longer in your game - sold, scrapped or traded in No Man's Sky. It goes the next time this save is opened.",
        EquipmentChipKind.EmptySlot => EmptyTip,
        _ => IsNew ? "New in your game" : null,
    };

    public string AutomationName => Kind switch
    {
        EquipmentChipKind.Gone => $"{Name}, gone",
        EquipmentChipKind.EmptySlot => Name,
        _ => string.Join(", ", new[] { Name, IsCorvette ? "corvette" : null, IsActive ? "active" : null, IsNew ? "new" : null }
            .Where(s => s is not null)),
    };
}
