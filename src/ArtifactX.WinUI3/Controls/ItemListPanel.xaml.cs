using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

/// <summary>One card in an ItemListPanel.</summary>
public sealed class ItemCard : INotifyPropertyChanged
{
    public required CatalogPart Part { get; init; }
    public required string Name { get; init; }
    public required string Group { get; init; }

    private ImageSource? _icon;
    public ImageSource? Icon { get => _icon; set { _icon = value; Changed(nameof(Icon)); } }

    private bool _owned;
    /// <summary>Already in the inventory - dimmed, but still addable (another stack).</summary>
    public bool Owned { get => _owned; set { _owned = value; Changed(nameof(CardOpacity)); Changed(nameof(Tooltip)); } }
    public double CardOpacity => Owned ? 0.55 : 1.0;
    public string Tooltip => Owned ? $"{Name} - already in here. Click to add another stack." : $"{Name} - click to add a stack.";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A browsable list of what an inventory can hold, beside its grid, so nobody
/// has to remember item names for the right-click search (user request
/// 2026-10-12; Corvette Cache first). Click a card to put a full stack in the
/// next free slot, or add a stack of every item shown at once. The page hands
/// it the items (only ones that inventory accepts) and the grid to fill.
/// </summary>
public sealed partial class ItemListPanel : UserControl
{
    private const string AllGroups = "All";
    private List<ItemCard> _cards = new();
    private InventorySlotGrid? _grid;
    private string _noun = "item";
    private string _categoryLabel = "Product";

    public ItemListPanel()
    {
        InitializeComponent();
        Unloaded += (_, _) => { if (_grid is not null) _grid.CellChanged -= OnGridChanged; };
    }

    /// <summary>Fills the list. `noun` is what the items are called ("part"),
    /// `categoryLabel` how the save files them ("Product").</summary>
    public async Task LoadAsync(InventorySlotGrid grid, Func<Task<List<CatalogPart>>> source, string noun, string categoryLabel, Func<string, string> groupLabel)
    {
        if (_grid is not null) _grid.CellChanged -= OnGridChanged;
        _grid = grid;
        _grid.CellChanged += OnGridChanged;
        _noun = noun;
        _categoryLabel = categoryLabel;

        var parts = await source();
        _cards = parts.Select(p => new ItemCard
        {
            Part = p,
            Name = CatalogService.TryGet(p.GameId)?.DisplayName is { Length: > 0 } name ? name : p.Name,
            Group = groupLabel(p.Group),
        }).ToList();

        GroupBox.Items.Clear();
        GroupBox.Items.Add(AllGroups);
        foreach (string group in _cards.Select(c => c.Part.Group).Where(g => g is "Structural" or "Decorative").Distinct().OrderBy(g => g))
            GroupBox.Items.Add(group);
        GroupBox.SelectedIndex = 0;

        MarkOwned();
        Apply();
        await LoadIconsAsync();
    }

    /// <summary>Icons in batches, so the list is usable straight away.</summary>
    private async Task LoadIconsAsync()
    {
        foreach (var batch in _cards.Chunk(120))
        {
            await CatalogService.WarmCacheAsync(batch.Select(c => c.Part.GameId));
            foreach (var card in batch)
            {
                var entry = CatalogService.TryGet(card.Part.GameId);
                if (entry?.Icon is not null) card.Icon = entry.Icon;
            }
        }
    }

    private void OnGridChanged(object? sender, EventArgs e)
    {
        MarkOwned();
        if (HideOwnedBox.IsChecked == true) Apply();
        else UpdateAddAll();
    }

    private void MarkOwned()
    {
        var present = _grid?.ItemIdsPresent() ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in _cards) card.Owned = present.Contains(card.Part.GameId);
    }

    private void Filter_Changed(object sender, object e) => Apply();

    private List<ItemCard> Shown()
    {
        string text = SearchBox.Text.Trim();
        string group = GroupBox.SelectedItem as string ?? AllGroups;
        return _cards
            .Where(c => text.Length == 0 || c.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Where(c => group == AllGroups || c.Part.Group == group || c.Part.Group == "Both")
            .Where(c => HideOwnedBox.IsChecked != true || !c.Owned)
            .ToList();
    }

    private void Apply()
    {
        var shown = Shown();
        CardsView.ItemsSource = shown;
        SummaryTxt.Text = $"{_cards.Count:N0} {_noun}s can go in here{(shown.Count != _cards.Count ? $" - showing {shown.Count:N0}" : "")}. Click one to add a full stack to the next free slot.";
        UpdateAddAll();
    }

    private void UpdateAddAll()
    {
        int missing = Shown().Count(c => !c.Owned);
        AddAllBtn.Content = missing == 0 ? $"You have every {_noun} shown" : $"Add a stack of each {_noun} I don't have ({missing:N0})";
        AddAllBtn.IsEnabled = missing > 0;
    }

    private void Card_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ItemCard card || _grid is null) return;
        int added = _grid.AddStacksToFreeSlots(new[] { (card.Part.GameId, _categoryLabel, card.Part.MaxStackSize) });
        Status(added > 0 ? $"Added {card.Name}." : "There's no free slot left - unlock more or make room first.", ok: added > 0);
    }

    private async void AddAll_Click(object sender, RoutedEventArgs e)
    {
        if (_grid is null) return;
        var missing = Shown().Where(c => !c.Owned).ToList();
        int free = _grid.FreeSlotCount;
        if (missing.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = $"Add {missing.Count:N0} {_noun}s?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"Adds one full stack of each {_noun} shown that isn't in here yet, into free slots."
                    + (free >= missing.Count
                        ? $" There's room for all of them ({free:N0} free slots)."
                        : $" Only {free:N0} free slots - the first {free:N0} (A to Z) fit, the rest won't be added."),
            },
            PrimaryButtonText = "Add them",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        int added = _grid.AddStacksToFreeSlots(missing.Select(c => (c.Part.GameId, _categoryLabel, c.Part.MaxStackSize)));
        Status(added == missing.Count ? $"Added {added:N0} {_noun}s." : $"Added {added:N0} of {missing.Count:N0} - no free slots left for the rest.", ok: added > 0);
    }

    private void Status(string text, bool ok)
    {
        StatusTxt.Text = text;
        StatusTxt.Foreground = ThemeResources.Brush(ok ? "ThemeDoneBrush" : "ThemeWarningBrush");
        StatusTxt.Visibility = Visibility.Visible;
    }
}
