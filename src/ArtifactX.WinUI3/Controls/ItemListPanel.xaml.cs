using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    /// <summary>Technology is installed, not stacked: once, or up to
    /// Part.Limit copies for upgrade modules.</summary>
    public bool IsTech { get; init; }

    private int _count;
    /// <summary>How many are in the inventory (modules: of the whole family,
    /// any class).</summary>
    public int Count { get => _count; set { _count = value; Changed(nameof(CardOpacity)); Changed(nameof(Tooltip)); } }
    /// <summary>Technology: as many as allowed. Stacks: at least one.</summary>
    public bool Owned => IsTech ? Count >= Part.Limit : Count > 0;
    /// <summary>More copies that may still be installed.</summary>
    public int Room => IsTech ? Math.Max(0, Part.Limit - Count) : 0;
    public double CardOpacity => Owned ? 0.55 : 1.0;
    public string Tooltip => (IsTech, Owned, Part.Limit > 1) switch
    {
        (true, _, true) => $"{Name} - {Count} of {Part.Limit} installed (the game allows {Part.Limit} upgrades per technology).{(Owned ? "" : " Click to install one; each gets its own random stats.")}",
        (true, true, _) => $"{Name} - already installed.",
        (true, false, _) => $"{Name} - click to install it.",
        (false, true, _) => $"{Name} - already in here. Click to add another stack.",
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
    // Bound once and edited in place: replacing ItemsSource on every install
    // (Hide installed re-filters) scrolled the list back to the top (2026-10-13).
    private readonly ObservableCollection<ItemCard> _shown = new();
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

    /// <summary>Orders a tech list for display: by group (in `groupOrder`),
    /// then each stat together, the main item before its add-ons, upgrade
    /// modules after the tech, A to Z.</summary>
    public static List<CatalogPart> SortByGroups(IEnumerable<CatalogPart> tech, Func<CatalogPart, string> group, string[] groupOrder) => tech
        .OrderBy(t => Array.IndexOf(groupOrder, group(t)) is var i && i < 0 ? int.MaxValue : i)
        .ThenBy(t => t.Stat ?? "", StringComparer.Ordinal)
        .ThenBy(t => t.Primary ? 0 : t.Seeded ? 2 : 1)
        .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    /// <summary>Re-marks what's installed after the grid was reloaded from
    /// outside (Reset, a save switch) rather than through a cell edit.</summary>
    public void RefreshOwned()
    {
        MarkOwned();
        Apply();
    }

    /// <summary>Optional hint shown when everything won't fit even after
    /// unlocking, e.g. "S class has more slots" (null for none). The page
    /// knows the device's class; changing it stays the player's choice.</summary>
    public Func<string?>? MoreRoomHint { get; set; }

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
            // Modules keep their own "... upgrade (S)" name, not the plain tech name.
            Name = !p.Seeded && CatalogService.TryGet(p.GameId)?.DisplayName is { Length: > 0 } name ? name : p.Name,
            Group = groupLabel(p),
            IsTech = _isTech,
        }).ToList();

        string? keepGroup = GroupBox.SelectedItem as string;
        GroupBox.Items.Clear();
        GroupBox.Items.Add(AllGroups);
        // In list order, so the filter reads the same way the cards do.
        foreach (string group in _cards.Select(c => c.Group).Where(g => g.Length > 0).Distinct())
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
        if (_isTech)
        {
            _grid.AdjacencyOf = AdjacencyOf;
            _grid.Refresh();
        }
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

    /// <summary>Tech that can't be installed together - one of each set at a
    /// time (e.g. the exosuit's two personal refiners; the game data has no
    /// field for this, so the page lists them).</summary>
    public IReadOnlyList<string[]> OneOf { get; set; } = Array.Empty<string[]>();

    private void MarkOwned()
    {
        // A module family's template placeholder ("UPGRADE MODULE", e.g.
        // T_FR_HYP) counts towards that family's 3 in game, so count it too.
        var templateFamily = _cards.Where(c => c.Part.TemplateId is not null && c.Part.CountKey is not null)
            .GroupBy(c => c.Part.TemplateId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Part.CountKey!, StringComparer.OrdinalIgnoreCase);
        var counts = (_grid?.ItemIdsInSlots() ?? new List<string>())
            .Select(id => templateFamily.TryGetValue(id, out var family) ? family : CountKeyOf(id))
            .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        foreach (var card in _cards)
        {
            string key = card.Part.CountKey ?? card.Part.GameId;
            var set = OneOf.FirstOrDefault(s => s.Contains(key, StringComparer.OrdinalIgnoreCase));
            card.Count = set is null ? counts.GetValueOrDefault(key) : set.Sum(id => counts.GetValueOrDefault(id));
        }
    }

    /// <summary>What an installed id counts towards: itself, or for a seeded
    /// upgrade module ("UP_PULSE4#12345") its family ("UP_PULSE"), so every
    /// class of a module counts against the same limit.</summary>
    private static string CountKeyOf(string id)
    {
        int hash = id.IndexOf('#');
        if (hash < 0) return id;
        // Only the one class character: TrimEnd took "UP_TOX3" to "UP_TO",
        // so toxic modules never counted towards their limit (2026-10-13).
        string module = id[..hash];
        return module.Length > 1 && module[^1] is >= '0' and <= '4' or 'X' ? module[..^1] : module;
    }

    /// <summary>Technology's type for placement: its main stat (a module counts
    /// as its tech's), so new tech lands beside its own kind and gets the
    /// game's adjacency boost. Stacks have no type - they just fill in order.</summary>
    /// <summary>Items the game gives no adjacency boost (e.g. figurines and
    /// trails) - no border, and Arrange leaves them to fill in last.</summary>
    public Func<CatalogPart, bool>? NoAdjacency { get; set; }

    public string? TypeOf(string installedId) =>
        _isTech && PartFor(installedId) is { } part && NoAdjacency?.Invoke(part) != true ? part.Stat : null;

    /// <summary>Type + border colour for the grid's adjacency borders.</summary>
    private (string Type, string Colour)? AdjacencyOf(string installedId) =>
        _isTech && PartFor(installedId) is { Stat: { } stat, Colour: { } colour } part && NoAdjacency?.Invoke(part) != true
            ? (stat, colour) : null;

    private CatalogPart? PartFor(string installedId)
    {
        string key = CountKeyOf(installedId);
        return _cards.FirstOrDefault(c => string.Equals(c.Part.CountKey ?? c.Part.GameId, key, StringComparison.OrdinalIgnoreCase))?.Part;
    }

    /// <summary>The id to write: seeded modules get a fresh seed each, in the
    /// range the game's own modules use (1,016-99,671 in the real saves).</summary>
    private static string NewIdFor(CatalogPart part) =>
        part.Seeded ? $"{part.GameId}#{Random.Shared.Next(10000, 100000)}" : part.GameId;

    /// <summary>Amount/max for a new item: modules hold no charge (-1/100, as
    /// the game writes them), everything else the grid's usual full stack.</summary>
    private (string, string, int, int) EntryFor(CatalogPart part)
    {
        if (part.Seeded) return (NewIdFor(part), _categoryLabel, -1, 100);
        int cap = _grid!.StackCapFor(_categoryLabel, part.MaxStackSize);
        return (part.GameId, _categoryLabel, cap, cap);
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

    /// <summary>Makes the bound collection match `target` with the fewest
    /// removes/inserts, so the GridView keeps its scroll position.</summary>
    private void Sync(List<ItemCard> target)
    {
        if (CardsView.ItemsSource != _shown) CardsView.ItemsSource = _shown;
        var keep = target.ToHashSet();
        for (int i = _shown.Count - 1; i >= 0; i--)
            if (!keep.Contains(_shown[i])) _shown.RemoveAt(i);
        for (int i = 0; i < target.Count; i++)
        {
            if (i < _shown.Count && ReferenceEquals(_shown[i], target[i])) continue;
            int at = _shown.IndexOf(target[i]);
            if (at >= 0) _shown.Move(at, i);
            else _shown.Insert(i, target[i]);
        }
    }

    private void Apply()
    {
        var shown = Shown();
        Sync(shown);
        string showing = shown.Count != _cards.Count ? $" - showing {shown.Count:N0}" : "";
        SummaryTxt.Text = _isTech
            ? $"{_cards.Count:N0} {_nouns} fit here{showing}. Click one to install it in the next free slot."
            : $"{_cards.Count:N0} {_nouns} can go in here{showing}. Click one to add a full stack to the next free slot.";
        UpdateAddAll();
    }

    private void UpdateAddAll()
    {
        int missing = ToAdd(Shown()).Count;
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
        if (_isTech && card.Owned && card.Part.Limit == 1
            && OneOf.FirstOrDefault(s => s.Contains(card.Part.GameId, StringComparer.OrdinalIgnoreCase)) is { } exclusive
            && !_grid.ItemIdsPresent().Contains(card.Part.GameId))
        {
            Status($"{card.Name} can't go in alongside {string.Join(" / ", _cards.Where(c => exclusive.Contains(c.Part.GameId, StringComparer.OrdinalIgnoreCase) && c != card).Select(c => c.Name))} - only one at a time.", ok: false);
            return;
        }
        if (_isTech && card.Owned)
        {
            Status(card.Part.Limit > 1
                ? $"Already {card.Part.Limit} {card.Name.Replace(" (S)", "")}s installed - the game allows {card.Part.Limit} per technology."
                : $"{card.Name} is already installed.", ok: false);
            return;
        }
        int added = _grid.AddToFreeSlots(new[] { EntryFor(card.Part) }, TypeOf);
        Status(added > 0 ? $"{(_isTech ? "Installed" : "Added")} {card.Name}." : "There's no free slot left - unlock more or make room first.", ok: added > 0);
    }

    private async void AddAll_Click(object sender, RoutedEventArgs e)
    {
        if (_grid is null) return;
        var missing = ToAdd(Shown());
        int free = _grid.FreeSlotCount;
        if (missing.Count == 0) return;

        // Short of room: offer to unlock what's needed (on by default - it's
        // what the player is after), never silently (user, 2026-10-13).
        int shortBy = Math.Max(0, missing.Count - free);
        int canUnlock = Math.Min(shortBy, _grid.LockedSlotCount);
        int stillShort = shortBy - canUnlock;
        var unlockBox = new CheckBox
        {
            Content = $"Unlock {canUnlock:N0} more slot{(canUnlock == 1 ? "" : "s")} so {(stillShort == 0 ? "everything fits" : "more fits")}",
            IsChecked = true,
            Visibility = canUnlock > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        string? hint = stillShort > 0 ? MoreRoomHint?.Invoke() : null;
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = (_isTech
                    ? $"Installs each {_noun} shown that isn't installed yet, and upgrade modules up to the game's 3 per technology, into free slots."
                    : $"Adds one full stack of each {_noun} shown that isn't in here yet, into free slots.")
                + (shortBy == 0
                    ? $" There's room for all of them ({free:N0} free slots)."
                    : $" Only {free:N0} free slots for {missing.Count:N0}."),
        });
        body.Children.Add(unlockBox);
        if (stillShort > 0)
            body.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"Even with every slot unlocked, {stillShort:N0} won't fit and will be left out." + (hint is null ? "" : " " + hint),
            });

        var dialog = new ContentDialog
        {
            Title = _isTech ? $"Install {missing.Count:N0} {_nouns}?" : $"Add {missing.Count:N0} {_nouns}?",
            Content = body,
            PrimaryButtonText = _isTech ? "Install them" : "Add them",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        if (canUnlock > 0 && unlockBox.IsChecked == true) _grid.UnlockSlots(canUnlock);
        int added = _grid.AddToFreeSlots(missing.Select(EntryFor), TypeOf);
        string verb = _isTech ? "Installed" : "Added";
        Status(added == missing.Count ? $"{verb} {added:N0} {_nouns}." : $"{verb} {added:N0} of {missing.Count:N0} - no free slots left for the rest.", ok: added > 0);
    }

    /// <summary>What "add all" adds: one stack of each missing item, or for
    /// technology every copy still allowed (modules up to their limit).</summary>
    private List<CatalogPart> ToAdd(List<ItemCard> shown)
    {
        if (!_isTech) return shown.Where(c => !c.Owned).Select(c => c.Part).ToList();
        // Only the first missing one of each one-of set (both refiners went
        // in together before - each card checked on its own, 2026-10-13).
        var setsTaken = new HashSet<string[]>();
        var parts = new List<CatalogPart>();
        foreach (var c in shown)
        {
            var set = OneOf.FirstOrDefault(s => s.Contains(c.Part.GameId, StringComparer.OrdinalIgnoreCase));
            if (set is not null && c.Room > 0 && !setsTaken.Add(set)) continue;
            parts.AddRange(Enumerable.Repeat(c.Part, c.Room));
        }
        return parts;
    }

    private void Status(string text, bool ok)
    {
        StatusTxt.Text = text;
        StatusTxt.Foreground = ThemeResources.Brush(ok ? "ThemeDoneBrush" : "ThemeWarningBrush");
        StatusTxt.Visibility = Visibility.Visible;
    }
}
