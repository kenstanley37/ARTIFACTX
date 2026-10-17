using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The Ship Designer (2026-10-16, GitHub #5): build a starship the way a space
/// station's ship fabricator does - a type, one part per slot, a paint style and
/// the style's colours - around a large live preview. Every choice comes from the
/// player's own game files (CatalogSteps.AddShipBuilder, category "ShipBuilder"),
/// so it offers exactly what the game's builder offers.
///
/// A built ship keeps its design in the save, not in its seed (in-game build
/// diff): the parts by item id, the style as a texture option and every
/// palette's colours. The seed only names it. Writing a design into the save
/// is in ShipDesignerPage.Save.cs.
/// </summary>
public sealed partial class ShipDesignerPage : Page
{
    private sealed record BuilderItem(string Item, List<string> Groups, string? Cls);
    private sealed record BuilderSlot(string Id, string Label, List<BuilderItem> Items);
    private sealed record ColourGroup(string Title, int DefaultIndex, List<string[]> Slots);
    private sealed record ColourData(string Option, string Swatches, List<ColourGroup> Groups);
    private sealed record BuilderType(string Type, string Title, string Scene, bool Enabled, string StyleGroup,
        List<BuilderSlot> Slots, List<ColourData> Colours);
    private sealed record PaintStyle(string Option, string Name, string Unlock, List<string[]> Layers);
    private sealed record Swatch(float[] Rgb, string Name);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private List<BuilderType> _types = new();
    private Dictionary<string, List<PaintStyle>> _styles = new();
    private Dictionary<string, List<Swatch>> _swatches = new();
    private Dictionary<string, List<string>> _partGroups = new(StringComparer.OrdinalIgnoreCase);

    // The design being made.
    private BuilderType? _type;
    private readonly Dictionary<string, string> _parts = new();   // slot id -> item id
    private string _slot = "";                                     // the slot whose parts are listed
    private PaintStyle? _style;
    private readonly Dictionary<string, int> _colourPicks = new(); // colour group title -> swatch index
    private ulong _seed;
    private int _previewVersion;

    public ShipDesignerPage()
    {
        InitializeComponent();
        GameProcessMonitorService.RunningStateChanged += OnGameRunningStateChanged;
        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnActiveSessionChanged; // slots used change with Add
        Unloaded += Page_Unloaded;
        UpdateWriteButtons();
    }

    private void OnGameRunningStateChanged(object? sender, bool isRunning) =>
        DispatcherQueue.TryEnqueue(UpdateWriteButtons);

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateWriteButtons);

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        GameProcessMonitorService.RunningStateChanged -= OnGameRunningStateChanged;
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged -= OnActiveSessionChanged;
    }

    /// <summary>Add and Apply only stage edits, but edits staged while the game runs
    /// are lost: the game autosaves, and reloading its newer save discards them.
    /// So they wait until No Man's Sky is closed, like Save in the title bar.</summary>
    private void UpdateWriteButtons()
    {
        bool gameRunning = GameProcessMonitorService.IsGameRunning;
        bool saveLoaded = SaveSessionManager.IsSaveLoaded;
        AddNewBtn.IsEnabled = ApplyBtn.IsEnabled = RepaintBtn.IsEnabled = RemovePaintBtn.IsEnabled = saveLoaded && !gameRunning;
        // Free starship slots, said before Add rather than after (2026-10-17).
        int total = NewFromTemplateService.ShipSlotCount, free = NewFromTemplateService.FreeShipSlots;
        // "5 empty starship slots", not "5 of 12 free" - that read as 5 used (2026-10-17).
        ShipSlotsTxt.Text = free == 0 ? "No empty starship slots"
            : $"{free} empty starship slot{(free == 1 ? "" : "s")}";
        ShipSlotsBadge.Visibility = saveLoaded && total > 0 && _paintShip is null ? Visibility.Visible : Visibility.Collapsed;
        var badgeBrush = ThemeResources.Brush(free == 0 ? "ThemeWarningBrush" : "ThemeAccentBrush");
        ShipSlotsBadge.BorderBrush = ShipSlotsIcon.Foreground = ShipSlotsTxt.Foreground = badgeBrush;
        ToolTipService.SetToolTip(ShipSlotsBadge, $"{total - free} of {total} starship slots used. " + (free == 0
            ? "Sell or scrap a ship in game, or delete one on the Starships page, to add another."
            : "Add as new ship puts the design in the first empty slot."));
        if (saveLoaded && free == 0) AddNewBtn.IsEnabled = false;
        string what = _paintShip is null ? "add or apply a design" : "repaint the ship";
        WriteBlockedTxt.Text = gameRunning ? $"Close No Man's Sky to {what}. You can keep choosing meanwhile."
            : !saveLoaded ? $"Load a save to {what}."
            : "";
        WriteBlockedTxt.Visibility = WriteBlockedTxt.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // From Starships' "Paint this ship": the ship's index (Paint.cs).
        _paintShip = e.Parameter is int ship ? ship : null;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var rows = await CatalogService.GetJsonCategoryAsync("ShipBuilder");
        bool IsList(string json, string field) =>
            json.TrimStart().StartsWith("[", StringComparison.Ordinal) && json.Contains($"\"{field}\"", StringComparison.Ordinal);
        _types = rows.Where(r => r.Value.TrimStart().StartsWith("{", StringComparison.Ordinal))
            .Select(r => JsonSerializer.Deserialize<BuilderType>(r.Value, Json)).OfType<BuilderType>()
            .Where(t => t.Enabled && t.Slots.Count > 0)
            .OrderBy(t => t.Type switch { "Fighter" => 0, "Dropship" => 1, "Scientific" => 2, "Sail" => 3, _ => 4 })
            .ToList();
        // Style rows list options, swatch rows colours ("SHIP_METALLIC" is a swatch set).
        _styles = rows.Where(r => IsList(r.Value, "option"))
            .ToDictionary(r => r.Key, r => JsonSerializer.Deserialize<List<PaintStyle>>(r.Value, Json) ?? new());
        _swatches = rows.Where(r => IsList(r.Value, "rgb"))
            .ToDictionary(r => r.Key, r => JsonSerializer.Deserialize<List<Swatch>>(r.Value, Json) ?? new());
        if (_types.Count == 0)
        {
            NoDataPanel.Visibility = Visibility.Visible;
            return;
        }
        _partGroups = (await CatalogService.GetPartGroupsAsync())
            .GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Descriptors, StringComparer.OrdinalIgnoreCase);
        // Part names and icons, and the names of the items that unlock paint styles.
        await CatalogService.WarmCacheAsync(_types.SelectMany(t => t.Slots).SelectMany(s => s.Items).Select(i => i.Item)
            .Concat(_styles.Values.SelectMany(l => l).Select(s => s.Unlock).Where(u => u.Length > 0)));
        await MultiToolKinds.EnsureTreesAsync();

        BodyGrid.Visibility = Visibility.Visible;
        _seed = NewSeed();
        SelectType(_types[0]);
        if (_paintShip is not null) await EnterPaintModeAsync();
        await RefreshDesignsAsync();
    }

    private static ulong NewSeed() => (ulong)Random.Shared.NextInt64(long.MinValue, long.MaxValue);

    // ------------------------------------------------------------------ type and parts

    private void SelectType(BuilderType type)
    {
        // Another ship type is another ship: a new design, not the saved one changed.
        if (_designId is not null && type != _type) SetDesign(null);
        _type = type;
        _parts.Clear();
        foreach (var slot in type.Slots)
            if (slot.Items.Count > 0) _parts[slot.Id] = slot.Items[0].Item;
        _slot = type.Slots.FirstOrDefault()?.Id ?? "";
        PartSearchBox.Text = "";
        _style = (_styles.GetValueOrDefault(type.StyleGroup) ?? new()).FirstOrDefault();
        ResetColours();
        BuildTypePanel();
        BuildSlotTabs();
        ShowParts();
        ShowPaint();
        RefreshName();
        RefreshPreview();
    }

    private void BuildTypePanel()
    {
        TypePanel.Children.Clear();
        foreach (var type in _types)
        {
            var button = new Button { Content = Title(type.Title), Padding = new Thickness(12, 5, 12, 5) };
            ThemeResources.StyleChoiceButton(button, type == _type);
            button.Click += (_, _) => { if (type != _type) SelectType(type); };
            TypePanel.Children.Add(button);
        }
    }

    /// <summary>One row per slot, showing the part picked for it - the game builder's
    /// slots, so the whole design reads at a glance. Click one to list its parts.</summary>
    private void BuildSlotTabs()
    {
        SlotTabs.Children.Clear();
        if (_type is null) return;
        foreach (var slot in _type.Slots)
        {
            string? item = _parts.GetValueOrDefault(slot.Id);
            var content = new Grid { ColumnSpacing = 10 };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (item is not null && CatalogService.TryGet(item)?.Icon is { } icon)
                content.Children.Add(new Image { Source = icon, Width = 34, Height = 34 });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = slot.Label.ToUpperInvariant(), Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] });
            text.Children.Add(new TextBlock { Text = item is null ? "-" : PartName(item), FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(text, 1);
            content.Children.Add(text);
            var button = new Button
            {
                Content = content, Padding = new Thickness(8, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            ThemeResources.StyleChoiceButton(button, slot.Id == _slot);
            button.Click += (_, _) =>
            {
                if (slot.Id == _slot) return;
                _slot = slot.Id;
                PartSearchBox.Text = "";
                BuildSlotTabs();
                ShowParts();
            };
            SlotTabs.Children.Add(button);
        }
    }

    private void ShowParts()
    {
        PartsPanel.Children.Clear();
        var slot = _type?.Slots.FirstOrDefault(s => s.Id == _slot);
        if (slot is null) return;
        string find = PartSearchBox.Text.Trim();
        var shown = slot.Items
            .Where(i => find.Length == 0 || PartName(i.Item).Contains(find, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(i => PartName(i.Item), StringComparer.CurrentCulture).ToList();
        if (shown.Count == 0)
            PartsPanel.Children.Add(new TextBlock
            {
                Text = $"No {slot.Label.ToLowerInvariant()} part matches \"{find}\".",
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 12, TextWrapping = TextWrapping.Wrap,
            });
        foreach (var item in shown)
        {
            var button = new Button { Content = PartCard(item.Item), Padding = new Thickness(4), VerticalAlignment = VerticalAlignment.Stretch };
            ThemeResources.StyleChoiceButton(button, _parts.GetValueOrDefault(slot.Id) == item.Item);
            ToolTipService.SetToolTip(button, PartName(item.Item));
            button.Click += (_, _) =>
            {
                _parts[slot.Id] = item.Item;
                foreach (var b in PartsPanel.Children.OfType<Button>()) ThemeResources.StyleChoiceButton(b, b == button);
                BuildSlotTabs();
                RefreshPreview();
            };
            PartsPanel.Children.Add(button);
        }
    }

    private void PartSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ShowParts();

    private static UIElement PartCard(string id)
    {
        var card = new StackPanel { Width = 76, Spacing = 2 };
        if (CatalogService.TryGet(id)?.Icon is { } icon)
            card.Children.Add(new Image { Source = icon, Width = 46, Height = 46, HorizontalAlignment = HorizontalAlignment.Center });
        card.Children.Add(new TextBlock { Text = PartName(id), FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxLines = 3 });
        return card;
    }

    private static string PartName(string id) => Title(CatalogService.TryGet(id)?.DisplayName ?? id);

    private static string Title(string text) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());

    // ------------------------------------------------------------------ paint

    /// <summary>The paint column: the style, then each colour the style offers.</summary>
    private void ShowPaint()
    {
        PaintPanel.Children.Clear();
        if (_type is null) return;
        var stylePanel = new StackPanel { Spacing = 6 };
        stylePanel.Children.Add(new TextBlock { Text = "PAINT STYLE", Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] });
        var styleRow = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6 };
        foreach (var style in _styles.GetValueOrDefault(_type.StyleGroup) ?? new())
        {
            var button = new Button { Content = style.Name, Padding = new Thickness(12, 5, 12, 5) };
            ThemeResources.StyleChoiceButton(button, style == _style);
            button.Click += (_, _) =>
            {
                if (style == _style) return;
                _style = style;
                _paintRemoved = false;
                ResetColours();
                ShowPaint();
                RefreshPreview();
            };
            styleRow.Children.Add(button);
        }
        stylePanel.Children.Add(styleRow);
        // Some styles are unlocked in game by expedition rewards; say so, as the game would.
        if (_style is { Unlock.Length: > 0 })
            stylePanel.Children.Add(new TextBlock
            {
                Text = $"In game, {_style.Name} has to be unlocked first ({PartName(_style.Unlock)}).",
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 12, TextWrapping = TextWrapping.Wrap,
            });
        PaintPanel.Children.Add(stylePanel);

        var data = CurrentColours();
        if (data is null) return;
        var swatches = _swatches.GetValueOrDefault(data.Swatches) ?? new();
        foreach (var group in data.Groups)
        {
            var panel = new StackPanel { Spacing = 6 };
            var picked = new TextBlock { Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            header.Children.Add(new TextBlock { Text = group.Title.ToUpperInvariant(), Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"], VerticalAlignment = VerticalAlignment.Center });
            header.Children.Add(picked);
            panel.Children.Add(header);
            // The game's own grid: two rows, half the swatches in each.
            var grid = new Grid { ColumnSpacing = 3, RowSpacing = 3, HorizontalAlignment = HorizontalAlignment.Left };
            int columns = Math.Max(1, (swatches.Count + 1) / 2);
            for (int c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var cells = new List<Border>();
            for (int i = 0; i < swatches.Count; i++)
            {
                int index = i;
                var cell = new Border { Width = 31, Height = 28, CornerRadius = new CornerRadius(3), Background = SwatchBrush(swatches[i]), BorderThickness = new Thickness(2) };
                ToolTipService.SetToolTip(cell, swatches[i].Name);
                Grid.SetColumn(cell, i % columns);
                Grid.SetRow(cell, i / columns);
                cell.Tapped += (_, _) =>
                {
                    _colourPicks[group.Title] = index;
                    MarkPicked(cells, index);
                    picked.Text = swatches[index].Name;
                    _paintRemoved = false;
                    RefreshPreview();
                };
                cells.Add(cell);
                grid.Children.Add(cell);
            }
            int current = _colourPicks.GetValueOrDefault(group.Title, group.DefaultIndex);
            MarkPicked(cells, current);
            if (current >= 0 && current < swatches.Count) picked.Text = swatches[current].Name;
            panel.Children.Add(grid);
            PaintPanel.Children.Add(panel);
        }
    }

    /// <summary>The colour settings for the chosen style: the entry for its option, or the type's only one.</summary>
    private ColourData? CurrentColours() =>
        _type is null ? null
            : _type.Colours.FirstOrDefault(c => c.Option == _style?.Option) ?? _type.Colours.FirstOrDefault(c => c.Option.Length == 0)
              ?? _type.Colours.FirstOrDefault();

    private void ResetColours()
    {
        _colourPicks.Clear();
        foreach (var group in CurrentColours()?.Groups ?? new()) _colourPicks[group.Title] = group.DefaultIndex;
    }

    private static void MarkPicked(List<Border> cells, int picked)
    {
        for (int i = 0; i < cells.Count; i++)
            cells[i].BorderBrush = new SolidColorBrush(i == picked ? Colors.White : Colors.Transparent);
    }

    private static SolidColorBrush SwatchBrush(Swatch swatch) =>
        new(Windows.UI.Color.FromArgb(255, Byte(swatch.Rgb[0]), Byte(swatch.Rgb[1]), Byte(swatch.Rgb[2])));

    private static byte Byte(float v) => (byte)Math.Clamp(v * 255f, 0f, 255f);

    // ------------------------------------------------------------------ name and preview

    private async void RefreshName()
    {
        if (_type is null) return;
        if (_paintShip is not null) { ShipNameTxt.Text = _paintShipName; return; }
        string? name = await MultiToolKinds.ShipNameAsync(_seed, _type.Scene);
        ShipNameTxt.Text = name ?? "";
    }

    private void NewNameBtn_Click(object sender, RoutedEventArgs e)
    {
        _seed = NewSeed();
        RefreshName();
        UpdateDesignState();
    }

    private async void RefreshPreview()
    {
        int version = ++_previewVersion;
        _previewReady = false;
        UpdateDesignState();
        if (_type is null || GameDataService.Game is not { } game) return;
        PreviewNote.Text = "Building the model from your game files...";
        var models = await ModelPreviewService.ModelsAsync(game);
        if (models is null || version != _previewVersion) return;

        string scene = _type.Scene;
        var root = MultiToolKinds.Load(ShipParts.DescriptorFor(scene));
        // Each chosen item turns on its model parts (PartGroups: FIGHT_COCKAA -> _COCKPIT_A, _ANOSE_A),
        // looked up in its own slot: an Explorer's left and right wings share item ids
        // (SCIEN_WINGT_A) but not parts (SCIEN_L_WINGT_A / SCIEN_R_WINGT_A).
        // Paint mode: the ship's own parts - from its seed, or the ones it was built with.
        var stored = (_paintShip is not null ? _paintShipParts : _type.Slots
            .SelectMany(slot => slot.Items.FirstOrDefault(i => i.Item == _parts.GetValueOrDefault(slot.Id))?.Groups ?? new()))
            .SelectMany(g => _partGroups.GetValueOrDefault(g) ?? new()).ToList();
        var options = root is null ? new HashSet<string>(StringComparer.Ordinal) : ModelPreviewService.AllOptionIds(root, MultiToolKinds.Load, StringComparer.Ordinal);
        var chosen = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ShipParts.Generate(scene, _seed, MultiToolKinds.Load, stored).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var mesh = await Task.Run(() => models.Build(scene, options, chosen));
        if (version != _previewVersion) return;
        if (mesh is null)
        {
            PreviewView.Show(null, _ => 0x9EA2AAu);
            PreviewNote.Text = "This ship's model couldn't be read from your game files.";
            return;
        }

        var colour = DesignColours();
        var styles = (_style?.Layers ?? new()).Select(l => l.Length > 1 ? l[1] : "").Where(o => o.Length > 0).ToHashSet(StringComparer.Ordinal);
        if (_paintRemoved)
        {
            // Paint removed: the colours it had when found can't be shown yet, so the
            // hologram scan, as on Starships - not the swatches still picked (2026-10-17).
            PreviewView.Show(mesh, _ => 0x8C8E94u, hologram: true);
            PreviewNote.Text = "Paint removed - it goes back to the colours it had when you found or bought it, which can't be shown yet. Pick a colour to paint it again.";
            _previewReady = true;
            return;
        }
        var textures = await ModelPreviewService.PaintAsync(models, mesh, colour, _seed, styles);
        if (version != _previewVersion) return;
        PreviewView.Show(mesh, _ => 0x8C8E94u, m => textures.GetValueOrDefault(m));
        PreviewNote.Text = _paintShip is not null
            ? "Drag to turn. Your ship with the paint you pick - its parts stay as they are."
            : "Drag to turn. Built from your game files with the parts and colours you pick.";
        _previewReady = true;
        OnPreviewReady();
    }

    /// <summary>The design's colours: each colour group sets its palette slots to the picked
    /// swatch; every other slot is the dark metal grey the game itself filled in on a
    /// built ship (0.351 - most palettes of "The Eagle of Beauty").</summary>
    private Func<string, string, float[]?> DesignColours()
    {
        var set = new Dictionary<(string, string), float[]>();
        var data = CurrentColours();
        var swatches = data is null ? new() : _swatches.GetValueOrDefault(data.Swatches) ?? new();
        foreach (var group in data?.Groups ?? new())
        {
            int index = _colourPicks.GetValueOrDefault(group.Title, group.DefaultIndex);
            if (index < 0 || index >= swatches.Count) continue;
            foreach (var slot in group.Slots.Where(s => s.Length > 1)) set[(slot[0], slot[1])] = swatches[index].Rgb;
        }
        var grey = new[] { 0.351f, 0.351f, 0.351f };
        return (palette, slot) => set.GetValueOrDefault((palette, slot == "Unique" ? "Alternative3" : slot)) ?? grey;
    }

    private void UpdateDataLink_Click(object sender, RoutedEventArgs e) => App.MainWindowInstance?.StartGameDataUpdate();
}
