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

    /// <summary>Technology is installed once, not stacked.</summary>
    public bool IsTech { get; init; }

    private bool _owned;
    /// <summary>Already in the inventory - dimmed; stacks can still be added
    /// again, technology can't.</summary>
    public bool Owned { get => _owned; set { _owned = value; Changed(nameof(CardOpacity)); Changed(nameof(Tooltip)); } }
    public double CardOpacity => Owned ? 0.55 : 1.0;
    public string Tooltip => (IsTech, Owned) switch
    {
        (true, true) => $"{Name} - already installed.",
        (true, false) => $"{Name} - click to install it.",
        (false, true) => $"{Name} - already in here. Click to add another stack.",
        _ => $"{Name} - click to add a stack.",
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A browsable list of what an inventory can hold, beside its grid, so nobody
/// has to remember item names for the right-click search (user request
/// 2026-10-12; Corvette Cache first). Click a card to put a full stack in the
/// next free slot, or add a stack of every item shown at once. The page hands
/// it the items (only ones that inventory accepts) and the grid to fill.
/// With the "Technology" category it installs instead: one of each, never a
/// duplicate, and installed tech is hidden at first (2026-10-13, Starships).
/// </summary>
public sealed partial class ItemListPanel : UserControl
{
    private const string AllGroups = "All";
    private List<ItemCard> _cards = new();
    private InventorySlotGrid? _grid;
    private string _noun = "item";
    private string _nouns = "items";
    private string _categoryLabel = "Product";
    private bool _isTech;
    private bool _loadedOnce;
    private int _loadVersion;

    public ItemListPanel()
    {
        InitializeComponent();
        Unloaded += (_, _) => { if (_grid is not null) _grid.CellChanged -= OnGridChanged; };
    }

    /// <summary>How tall the scrolling card list is.</summary>
    public double ListHeight { get => CardsView.Height; set => CardsView.Height = value; }

    /// <summary>Fills the list (again, when the page switches device). `noun` is
    /// what the items are called ("part"), `nouns` its plural, `categoryLabel`
    /// how the save files them ("Product", or "Technology" to install tech).
    /// `groupLabel` names each item's group for the filter; "" for none.</summary>
    public async Task LoadAsync(InventorySlotGrid grid, Func<Task<List<CatalogPart>>> source, string noun, string nouns, string categoryLabel, Func<CatalogPart, string> groupLabel)
    {
        int version = ++_loadVersion;
        if (_grid is not null) _grid.CellChanged -= OnGridChanged;
        _grid = grid;
        _grid.CellChanged += OnGridChanged;
        _noun = noun;
        _nouns = nouns;
        _categoryLabel = categoryLabel;
        _isTech = categoryLabel == "Technology";

        var parts = await source();
        if (version != _loadVersion) return; // a newer load (another ship) won
        _cards = parts.Select(p => new ItemCard
        {
            Part = p,
            Name = CatalogService.TryGet(p.GameId)?.DisplayName is { Length: > 0 } name ? name : p.Name,
            Group = groupLabel(p),
            IsTech = _isTech,
        }).ToList();

        string? keepGroup = GroupBox.SelectedItem as string;
        GroupBox.Items.Clear();
        GroupBox.Items.Add(AllGroups);
        foreach (string group in _cards.Select(c => c.Group).Where(g => g.Length > 0).Distinct().OrderBy(g => g))
            GroupBox.Items.Add(group);
        int keep = keepGroup is null ? -1 : GroupBox.Items.IndexOf(keepGroup);
        GroupBox.SelectedIndex = keep > 0 ? keep : 0;
        // A filter with one choice is just clutter.
        GroupBox.Visibility = GroupBox.Items.Count > 2 ? Visibility.Visible : Visibility.Collapsed;

        if (!_loadedOnce)
        {
            _loadedOnce = true;
            HideOwnedBox.Content = _isTech ? "Hide installed" : "Hide ones I have";
            // What's already fitted is noise when picking what to add.
            if (_isTech) HideOwnedBox.IsChecked = true;
        }
        StatusTxt.Visibility = Visibility.Collapsed;

        MarkOwned();
        Apply();
        await LoadIconsAsync(version);
    }

    /// <summary>Icons in batches, so the list is usable straight away.</summary>
    private async Task LoadIconsAsync(int version)
    {
        foreach (var batch in _cards.Chunk(120))
        {
            await CatalogService.WarmCacheAsync(batch.Select(c => c.Part.GameId));
            if (version != _loadVersion) return;
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
            .Where(c => group == AllGroups || c.Group == group)
            .Where(c => HideOwnedBox.IsChecked != true || !c.Owned)
            .ToList();
    }

    private void Apply()
    {
        var shown = Shown();
        CardsView.ItemsSource = shown;
        string showing = shown.Count != _cards.Count ? $" - showing {shown.Count:N0}" : "";
        SummaryTxt.Text = _isTech
            ? $"{_cards.Count:N0} {_nouns} fit here{showing}. Click one to install it in the next free slot."
            : $"{_cards.Count:N0} {_nouns} can go in here{showing}. Click one to add a full stack to the next free slot.";
        UpdateAddAll();
    }

    private void UpdateAddAll()
    {
        int missing = Shown().Count(c => !c.Owned);
        AddAllBtn.Content = (missing, _isTech) switch
        {
            (0, true) => $"Every {_noun} shown is installed",
            (0, false) => $"You have every {_noun} shown",
            (_, true) => $"Install each one I don't have ({missing:N0})",
            _ => $"Add a stack of each {_noun} I don't have ({missing:N0})",
        };
        AddAllBtn.IsEnabled = missing > 0;
    }

    private void Card_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ItemCard card || _grid is null) return;
        if (_isTech && card.Owned)
        {
            Status($"{card.Name} is already installed.", ok: false);
            return;
        }
        int added = _grid.AddStacksToFreeSlots(new[] { (card.Part.GameId, _categoryLabel, card.Part.MaxStackSize) });
        Status(added > 0 ? $"{(_isTech ? "Installed" : "Added")} {card.Name}." : "There's no free slot left - unlock more or make room first.", ok: added > 0);
    }

    private async void AddAll_Click(object sender, RoutedEventArgs e)
    {
        if (_grid is null) return;
        var missing = Shown().Where(c => !c.Owned).ToList();
        int free = _grid.FreeSlotCount;
        if (missing.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = _isTech ? $"Install {missing.Count:N0} {_nouns}?" : $"Add {missing.Count:N0} {_nouns}?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = (_isTech
                        ? $"Installs one of each {_noun} shown that isn't installed yet, into free slots."
                        : $"Adds one full stack of each {_noun} shown that isn't in here yet, into free slots.")
                    + (free >= missing.Count
                        ? $" There's room for all of them ({free:N0} free slots)."
                        : $" Only {free:N0} free slots - the first {free:N0} (A to Z) fit, the rest won't be added."),
            },
            PrimaryButtonText = _isTech ? "Install them" : "Add them",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        int added = _grid.AddStacksToFreeSlots(missing.Select(c => (c.Part.GameId, _categoryLabel, c.Part.MaxStackSize)));
        string verb = _isTech ? "Installed" : "Added";
        Status(added == missing.Count ? $"{verb} {added:N0} {_nouns}." : $"{verb} {added:N0} of {missing.Count:N0} - no free slots left for the rest.", ok: added > 0);
    }

    private void Status(string text, bool ok)
    {
        StatusTxt.Text = text;
        StatusTxt.Foreground = ThemeResources.Brush(ok ? "ThemeDoneBrush" : "ThemeWarningBrush");
        StatusTxt.Visibility = Visibility.Visible;
    }
}
