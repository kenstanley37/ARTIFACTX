using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views.SaveEditorPages;

/// <summary>
/// The Ship Designer (2026-10-16, GitHub #5): build a starship the way a space
/// station's ship fabricator does - a type, one part per slot, a paint style and
/// the style's colours - around a large live preview. Every choice comes from the
/// player's own game files (CatalogSteps.AddShipBuilder, category "ShipBuilder"),
/// so it offers exactly what the game's builder offers.
///
/// A built ship keeps its design in the save, not in its seed (in-game build
/// diff): the parts by item id, the style as a texture option and every
/// palette's colours. The seed only names it. Save-staging and catalog loading
/// live in <see cref="ShipBuilderService"/>.
/// </summary>
public sealed partial class ShipDesignerPage : Page
{
    private const int ThumbWidth = 288, ThumbHeight = 180; // drawn at 2x the card's 144 x 90

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

    // The saved design being edited, if one was saved or loaded: as saved, to tell
    // whether it has unsaved changes.
    private ShipDesign? _editing;
    private string? _designId;
    private string _designName = "";
    private string _designCreated = "";
    private List<SavedDesign> _savedDesigns = new();
    private readonly Dictionary<string, BitmapImage> _thumbs = new();
    private ShipDesign? _needsThumbnail;
    private bool _previewReady;

    // Paint mode (2026-10-17): Starships' "Paint this ship" opens the designer on one
    // of the player's own ships.
    private int? _paintShip;
    private string _paintShipName = "";
    private List<string> _paintShipParts = new();
    private JToken? _paintShipSmp;
    private bool _paintShipHasPaint;
    private bool _paintRemoved;

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
        // Used and free both said, the user's wording (2026-10-17): "5 of 12 free" read as 5 used.
        ShipSlotsTxt.Text = $"{total - free}/{total} ship slots used · " + (free == 0 ? "none free" : $"{free} free");
        ShipSlotsBadge.Visibility = saveLoaded && total > 0 && _paintShip is null ? Visibility.Visible : Visibility.Collapsed;
        var badgeBrush = ThemeResources.Brush(free == 0 ? "ThemeWarningBrush" : "ThemeAccentBrush");
        ShipSlotsBadge.BorderBrush = ShipSlotsIcon.Foreground = ShipSlotsTxt.Foreground = badgeBrush;
        ToolTipService.SetToolTip(ShipSlotsBadge, (free == 0
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
        _paintShip = e.Parameter is int ship ? ship : null;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var data = await ShipBuilderService.LoadCatalogDataAsync();
        _types = data.Types;
        _styles = data.Styles;
        _swatches = data.Swatches;
        _partGroups = data.PartGroups;
        if (_types.Count == 0)
        {
            NoDataPanel.Visibility = Visibility.Visible;
            return;
        }

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
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, type.Title);
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
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{slot.Label}: {(item is null ? "none" : PartName(item))}");
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
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, PartName(item.Item));
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

        var data = ShipBuilderService.CurrentColours(_type, _style);
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

    private void ResetColours()
    {
        _colourPicks.Clear();
        foreach (var group in ShipBuilderService.CurrentColours(_type, _style)?.Groups ?? new())
            _colourPicks[group.Title] = group.DefaultIndex;
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
        string? name = await StarshipService.ShipNameAsync(_seed, _type.Scene);
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
        var root = ShipPartsService.LoadTree(ShipParts.DescriptorFor(scene));
        // Each chosen item turns on its model parts (PartGroups: FIGHT_COCKAA -> _COCKPIT_A, _ANOSE_A),
        // looked up in its own slot: an Explorer's left and right wings share item ids
        // (SCIEN_WINGT_A) but not parts (SCIEN_L_WINGT_A / SCIEN_R_WINGT_A).
        // Paint mode: the ship's own parts - from its seed, or the ones it was built with.
        var stored = (_paintShip is not null ? _paintShipParts : _type.Slots
            .SelectMany(slot => slot.Items.FirstOrDefault(i => i.Item == _parts.GetValueOrDefault(slot.Id))?.Groups ?? new()))
            .SelectMany(g => _partGroups.GetValueOrDefault(g) ?? new()).ToList();
        var options = root is null ? new HashSet<string>(StringComparer.Ordinal) : ModelPreviewService.AllOptionIds(root, ShipPartsService.LoadTree, StringComparer.Ordinal);
        var chosen = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ShipParts.Generate(scene, _seed, ShipPartsService.LoadTree, stored).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var mesh = await Task.Run(() => models.Build(scene, options, chosen));
        if (version != _previewVersion) return;
        if (mesh is null)
        {
            PreviewView.Show(null, _ => 0x9EA2AAu);
            PreviewNote.Text = "This ship's model couldn't be read from your game files.";
            return;
        }

        var colour = ShipBuilderService.DesignColours(_type, _style, _swatches, _colourPicks);
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

    private void UpdateDataLink_Click(object sender, RoutedEventArgs e) => App.MainWindowInstance?.StartGameDataUpdate();

    // ------------------------------------------------------------------ saved designs

    /// <summary>Editing a saved design: saves the changes under its name, no questions
    /// (2026-10-17, the user: "Save changes shouldn't ask for a name"). A new design:
    /// asks for a name first.</summary>
    private async void SaveDesignBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null) return;
        if (_editing is { } saved) await SaveAsync(CurrentDesign(saved.Id, saved.Name, saved.CreatedAt));
        else await SaveAsNewAsync();
    }

    private async void SaveAsNewBtn_Click(object sender, RoutedEventArgs e) => await SaveAsNewAsync();

    private async Task SaveAsNewAsync()
    {
        if (_type is null) return;
        var nameBox = new TextBox
        {
            Header = "Design name",
            Text = _editing is null ? ShipNameTxt.Text : $"{_designName} (copy)",
            MaxLength = 80,
        };
        nameBox.SelectAll();
        var dialog = new ContentDialog
        {
            Title = "Save as a new design",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        nameBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = nameBox.Text.Trim().Length > 0;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await SaveAsync(CurrentDesign(Guid.NewGuid().ToString("N"), nameBox.Text.Trim(), UserDatabase.Now()));
    }

    private async Task SaveAsync(ShipDesign design)
    {
        byte[]? thumbnail = _previewReady ? await TakeThumbnailAsync() : null;
        try
        {
            await ShipDesignsService.SaveAsync(design, thumbnail);
        }
        catch (Exception ex)
        {
            await Message("Couldn't save the design", ex.Message);
            return;
        }
        _thumbs.Remove(design.Id);
        _needsThumbnail = thumbnail is null ? design : null;
        SetDesign(design);
        await RefreshDesignsAsync();
    }

    private ShipDesign CurrentDesign(string id, string name, string createdAt) => new(
        id, name, _type!.Type,
        new Dictionary<string, string>(_parts),
        _style?.Option,
        new Dictionary<string, int>(_colourPicks),
        "0x" + _seed.ToString("X16", CultureInfo.InvariantCulture),
        createdAt);

    private async Task RefreshDesignsAsync()
    {
        _savedDesigns = await ShipDesignsService.LoadAllAsync();
        await ShowDesignsAsync();
    }

    private async void DesignSearchBox_TextChanged(object sender, TextChangedEventArgs e) => await ShowDesignsAsync();

    private async Task ShowDesignsAsync()
    {
        string find = DesignSearchBox.Text.Trim();
        var shown = _savedDesigns.Where(d => find.Length == 0
            || d.Design.Name.Contains(find, StringComparison.CurrentCultureIgnoreCase)
            || DesignSubtitle(d.Design).Contains(find, StringComparison.CurrentCultureIgnoreCase)).ToList();
        DesignsEmptyTxt.Text = _savedDesigns.Count == 0
            ? "No saved designs yet. Make a ship you like and click Save design to keep it here."
            : shown.Count == 0 ? $"No design matches \"{find}\"." : "";
        DesignsEmptyTxt.Visibility = DesignsEmptyTxt.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        DesignsPanel.Children.Clear();
        foreach (var saved in shown)
            DesignsPanel.Children.Add(await DesignCardAsync(saved));
    }

    /// <summary>A card: the picture, name and type/style. Click to load; the small
    /// button in the corner deletes (beside the card, not inside its button).</summary>
    private async Task<UIElement> DesignCardAsync(SavedDesign saved)
    {
        var design = saved.Design;
        var picture = new Grid { Width = 144, Height = 90, CornerRadius = new CornerRadius(4), Background = ThemeResources.Brush("ThemePanelAltBrush") };
        if (await ThumbnailAsync(saved) is { } image)
            picture.Children.Add(new Image { Source = image, Stretch = Stretch.Uniform });
        else
            picture.Children.Add(new TextBlock
            {
                Text = "Load it to take a picture", FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8),
            });
        var content = new StackPanel { Width = 144, Spacing = 2 };
        content.Children.Add(picture);
        content.Children.Add(new TextBlock { Text = design.Name, FontWeight = FontWeights.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
        content.Children.Add(new TextBlock
        {
            Text = DesignSubtitle(design), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis,
            Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
        });
        var card = new Button { Content = content, Tag = design.Id, Padding = new Thickness(6), VerticalAlignment = VerticalAlignment.Stretch };
        ThemeResources.StyleChoiceButton(card, design.Id == _designId);
        ToolTipService.SetToolTip(card, $"Load \"{design.Name}\"");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(card, $"Load {design.Name}");
        // Painting a ship: a design lends its paint only (its parts would be a different ship).
        card.Click += (_, _) => { if (_paintShip is not null) UseDesignPaint(design); else LoadDesign(design, saved.Thumbnail is null); };

        var delete = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 12 }, Padding = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 10, 0),
        };
        ToolTipService.SetToolTip(delete, "Delete this design");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(delete, $"Delete {design.Name}");
        delete.Click += async (_, _) =>
        {
            if (!await Confirm($"Delete \"{design.Name}\"?", "The design is removed from My designs. Ships you already made from it stay as they are.", "Delete")) return;
            await ShipDesignsService.DeleteAsync(design.Id);
            _thumbs.Remove(design.Id);
            if (design.Id == _designId) SetDesign(null);
            await RefreshDesignsAsync();
        };

        var cell = new Grid();
        cell.Children.Add(card);
        cell.Children.Add(delete);
        return cell;
    }

    private string DesignSubtitle(ShipDesign design)
    {
        var type = _types.FirstOrDefault(t => t.Type == design.Type);
        string style = type is null ? "" : _styles.GetValueOrDefault(type.StyleGroup)?.FirstOrDefault(s => s.Option == design.Style)?.Name ?? "";
        return string.Join(" · ", new[] { type is null ? design.Type : Title(type.Title), style }.Where(t => t.Length > 0));
    }

    private async Task<BitmapImage?> ThumbnailAsync(SavedDesign saved)
    {
        if (saved.Thumbnail is not { Length: > 0 } png) return null;
        if (_thumbs.TryGetValue(saved.Design.Id, out var cached)) return cached;
        var image = await ShipBuilderService.DecodeBitmapAsync(png);
        if (image is not null) _thumbs[saved.Design.Id] = image;
        return image;
    }

    /// <summary>The preview, as a PNG at its opening angle.</summary>
    private async Task<byte[]?> TakeThumbnailAsync()
    {
        if (await PreviewView.SnapshotAsync(ThumbWidth, ThumbHeight) is not { } pixels) return null;
        return await ShipBuilderService.EncodeThumbnailPngAsync(pixels, ThumbWidth, ThumbHeight);
    }

    /// <summary>Called when the preview has drawn the current design: a loaded design
    /// with no picture gets one - if the preview still shows exactly that design.</summary>
    private async void OnPreviewReady()
    {
        if (_needsThumbnail is not { } design || design.Id != _designId) return;
        if (_type?.Type != design.Type || _style?.Option != design.Style
            || !design.Parts.OrderBy(p => p.Key).SequenceEqual(_parts.OrderBy(p => p.Key))) return;
        _needsThumbnail = null;
        if (await TakeThumbnailAsync() is not { } png) return;
        try
        {
            await ShipDesignsService.SetThumbnailAsync(design.Id, png);
        }
        catch (Exception)
        {
            return; // no picture this time; the card keeps its placeholder
        }
        await RefreshDesignsAsync();
    }

    /// <summary>Puts a saved design back into the designer. Anything the game data no
    /// longer has (after a game update) falls back to the builder's default.</summary>
    private async void LoadDesign(ShipDesign design, bool needsThumbnail)
    {
        var type = _types.FirstOrDefault(t => t.Type == design.Type);
        if (type is null)
        {
            await Message("Can't load this design", $"Your game data has no \"{design.Type}\" ship type. Update your game data, or delete the design.");
            return;
        }
        bool missing = false;
        _type = type;
        _parts.Clear();
        foreach (var slot in type.Slots)
        {
            if (design.Parts.TryGetValue(slot.Id, out string? item) && slot.Items.Any(i => i.Item == item)) _parts[slot.Id] = item;
            else if (slot.Items.Count > 0) { _parts[slot.Id] = slot.Items[0].Item; missing = true; }
        }
        _slot = type.Slots.FirstOrDefault()?.Id ?? "";
        PartSearchBox.Text = "";
        var styles = _styles.GetValueOrDefault(type.StyleGroup) ?? new();
        _style = styles.FirstOrDefault(s => s.Option == design.Style);
        if (_style is null) { _style = styles.FirstOrDefault(); missing |= design.Style is not null; }
        ResetColours();
        int swatchCount = _swatches.GetValueOrDefault(ShipBuilderService.CurrentColours(_type, _style)?.Swatches ?? "")?.Count ?? 0;
        foreach (var (group, index) in design.Colours)
            if (_colourPicks.ContainsKey(group) && index >= 0 && index < swatchCount) _colourPicks[group] = index;
        string seed = design.Seed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? design.Seed[2..] : design.Seed;
        if (ulong.TryParse(seed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsed)) _seed = parsed;

        _needsThumbnail = needsThumbnail && !missing ? design : null;
        SetDesign(design);
        BuildTypePanel();
        BuildSlotTabs();
        ShowParts();
        ShowPaint();
        RefreshName();
        RefreshPreview();
        if (missing)
            await Message("Some parts have changed", "Your game data no longer has everything this design used, so those choices are back to the default. Save the design again to keep it this way.");
    }

    private void NewDesignBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null) return;
        SetDesign(null);
        _seed = NewSeed();
        SelectType(_type);
    }

    /// <summary>Says whether this is a new design or a saved one being edited, and
    /// whether that one has changed since it was saved.</summary>
    private void UpdateDesignState()
    {
        if (_paintShip is not null) return;
        if (_editing is not { } saved || _type is null)
        {
            DesignNameTxt.Text = "New design · not saved yet";
            NewDesignBtn.Visibility = Visibility.Collapsed;
            SaveDesignBtn.Content = "Save design...";
            SaveDesignBtn.IsEnabled = true;
            SaveAsNewBtn.Visibility = Visibility.Collapsed;
            return;
        }
        var now = CurrentDesign(saved.Id, saved.Name, saved.CreatedAt);
        bool changed = now.Type != saved.Type || now.Style != saved.Style || now.Seed != saved.Seed
            || !SameEntries(now.Parts, saved.Parts) || !SameEntries(now.Colours, saved.Colours);
        DesignNameTxt.Text = $"Editing: {saved.Name}" + (changed ? " · unsaved changes" : "");
        NewDesignBtn.Visibility = Visibility.Visible;
        SaveDesignBtn.Content = "Save changes";
        SaveDesignBtn.IsEnabled = changed;
        SaveAsNewBtn.Visibility = Visibility.Visible;
    }

    private static bool SameEntries<T>(Dictionary<string, T> a, Dictionary<string, T> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value));

    private void SetDesign(ShipDesign? design)
    {
        _editing = design;
        _designId = design?.Id;
        _designName = design?.Name ?? "";
        _designCreated = design?.CreatedAt ?? "";
        UpdateDesignState();
        foreach (var card in DesignsPanel.Children.OfType<Grid>().Select(g => g.Children.OfType<Button>().FirstOrDefault()).OfType<Button>())
            ThemeResources.StyleChoiceButton(card, card.Tag is string id && id == design?.Id);
    }

    // ------------------------------------------------------------------ paint mode

    private async Task EnterPaintModeAsync()
    {
        int index = _paintShip!.Value;
        var target = await ShipBuilderService.ReadPaintTargetAsync(index, _types);
        if (target is null)
        {
            _paintShip = null;
            await Message("This ship can't be painted here", "Only Fighters, Haulers, Explorers and Solars can be painted - the ship types a space station's fabricator builds.");
            return;
        }

        _paintShipName = target.ShipName;
        _paintShipSmp = target.SmpClone;
        _paintShipParts = target.StoredParts;
        _paintShipHasPaint = target.HasPaint;

        SetDesign(null);
        SelectType(target.Type);
        _seed = target.Seed;
        if (target.CustomisationData is not null && _paintShipHasPaint)
            _style = ShipBuilderService.PickCurrentPaint(target.CustomisationData, target.Type, _styles, _swatches, _style, _colourPicks);

        TypeSection.Visibility = PartsSection.Visibility = PartSearchBox.Visibility = PartsScroll.Visibility = Visibility.Collapsed;
        PaintPartsNote.Visibility = Visibility.Visible;
        PaintPartsTxt.Text = (_paintShipParts.Count > 0
                ? "This ship keeps the parts it was built with."
                : "This ship keeps its own parts - the ones its seed gives.")
            + " Only its paint changes. The game itself doesn't let you repaint a ship you found or bought; this changes its colours in your save.";
        DesignNameTxt.Text = "Repainting your ship · its parts and name stay as they are";
        NewDesignBtn.Visibility = NewNameBtn.Visibility = Visibility.Collapsed;
        DesignActions.Visibility = SaveActions.Visibility = Visibility.Collapsed;
        PaintActions.Visibility = Visibility.Visible;
        RemovePaintBtn.Visibility = _paintShipHasPaint && _paintShipParts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateWriteButtons();
        ShowPaint();
        RefreshName();
        RefreshPreview();
    }

    private async void RepaintBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_paintShip is not int index || _type is null || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        ShipBuilderService.StageRepaint(index, _paintShipSmp, _type, _style, _parts, _swatches, _colourPicks);
        _paintShipHasPaint = true;
        _paintRemoved = false;
        RefreshPreview();
        RemovePaintBtn.Visibility = _paintShipParts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        await Message("Repainted", $"\"{_paintShipName}\" has its new paint. Save from the title bar to write it to your save, then load the save in game.");
    }

    private async void RemovePaintBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_paintShip is not int index || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        if (!await Confirm($"Remove the paint from \"{_paintShipName}\"?",
                "It goes back to the colours it had when you found or bought it. Nothing is written to your save until you Save.", "Remove paint")) return;
        ShipBuilderService.StageRemovePaint(index);
        _paintShipHasPaint = false;
        _paintRemoved = true;
        RemovePaintBtn.Visibility = Visibility.Collapsed;
        RefreshPreview();
        await Message("Paint removed", "Save from the title bar to write it to your save.");
    }

    private void ExitPaintBtn_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(ShipDesignerPage));

    /// <summary>Paint mode: a saved design lends its paint style and colours.</summary>
    private async void UseDesignPaint(ShipDesign design)
    {
        if (_type is null) return;
        var style = (_styles.GetValueOrDefault(_type.StyleGroup) ?? new()).FirstOrDefault(s => s.Option == design.Style);
        if (design.Style is not null && style is null)
        {
            await Message("That paint style isn't offered here", $"\"{design.Name}\" uses a paint style this ship type doesn't have.");
            return;
        }
        _style = style ?? _style;
        _paintRemoved = false;
        ResetColours();
        foreach (var (group, swatch) in design.Colours)
            if (_colourPicks.ContainsKey(group)) _colourPicks[group] = swatch;
        ShowPaint();
        RefreshPreview();
    }

    // ------------------------------------------------------------------ add & apply to save

    private async void AddNewBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        var owned = ShipBuilderService.RegularShips();
        int active = SaveSessionManager.GetValue(ArtifactX.Core.NmsModels.NmsInventoryContainer.ActiveShipIndexPath)?.Value<int>() ?? -1;
        // The new ship starts as a copy of one you own (its tech, so it flies straight away).
        int skeleton = owned.Any(s => s.Index == active) ? active : owned.FirstOrDefault().Index;
        if (owned.Count == 0 || NewFromTemplateService.FirstEmptyShipSlot() is not int slot)
        {
            await Message("No room for another Starship", owned.Count == 0
                ? "This save doesn't have a starship to start from yet. Get one in game first."
                : $"All {NewFromTemplateService.ShipSlotCount} Starship slots are in use. Sell or scrap one in game, or delete one on the Starships page, then try again.");
            return;
        }
        if (!await Confirm($"Add \"{ShipNameTxt.Text}\" as a new Starship?",
                $"It goes in empty slot {slot + 1} with these parts and paint. It starts with the technology of one of your ships so it can fly, and an empty cargo hold. Nothing is written to your save until you Save.",
                "Add")) return;

        ShipBuilderService.StageNewShip(skeleton, slot, _type, _seed, _style, _parts, _swatches, _colourPicks);
        await Message("Added", $"\"{ShipNameTxt.Text}\" is in slot {slot + 1}. Save from the title bar to write it to your save, then load the save in game.");
    }

    private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        var owned = ShipBuilderService.RegularShips();
        if (owned.Count == 0) { await Message("No ship to apply it to", "This save has no starship the design can go on."); return; }

        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 360 };
        foreach (var (index, scene, name) in owned)
        {
            string label = name.Length > 0 ? name
                : ShipBuilderService.TryParseSeed(index) is ulong seed && await StarshipService.ShipNameAsync(seed, scene) is { } gameName ? gameName
                : $"Starship {index + 1}";
            list.Items.Add(new ListViewItem { Content = label, Tag = index });
        }
        // A ship with a name of its own keeps it unless asked; one without takes the
        // design's name anyway (the game names it from the new seed). Always shown, so
        // the choice can be found: greyed out and ticked when there is nothing to choose.
        var rename = new CheckBox { Content = $"Rename it to \"{ShipNameTxt.Text}\"", IsChecked = true, IsEnabled = false };
        var renameNote = new TextBlock
        {
            Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Text = "Pick a ship. One you named yourself can keep its name.",
        };
        bool renameChoice = true;
        rename.Click += (_, _) => { if (rename.IsEnabled) renameChoice = rename.IsChecked == true; };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = $"The ship gets this design's type, parts, paint and name (\"{ShipNameTxt.Text}\"). Its technology, cargo, class and stats stay as they are.",
        });
        panel.Children.Add(list);
        panel.Children.Add(rename);
        panel.Children.Add(renameNote);
        var dialog = new ContentDialog
        {
            Title = "Apply the design to which ship?",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            XamlRoot = XamlRoot,
        };
        list.SelectionChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = list.SelectedItem is not null;
            bool named = list.SelectedItem is ListViewItem { Tag: int picked } && owned.Any(s => s.Index == picked && s.Name.Length > 0);
            rename.IsEnabled = named;
            rename.IsChecked = !named || renameChoice;
            renameNote.Text = named ? "You named this ship. Untick to keep its name."
                : "The game named this ship, so it takes the design's name.";
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || list.SelectedItem is not ListViewItem { Tag: int target }) return;

        ShipBuilderService.StageApplyDesign(target, rename.IsChecked == true, _type, _seed, _style, _parts, _swatches, _colourPicks);
        await Message("Applied", "Save from the title bar to write it to your save, then load the save in game.");
    }

    private async Task Message(string title, string text) =>
        await new ContentDialog { Title = title, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, CloseButtonText = "OK", XamlRoot = XamlRoot }.ShowAsync();

    private async Task<bool> Confirm(string title, string text, string action) =>
        await new ContentDialog
        {
            Title = title, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = action, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
        }.ShowAsync() == ContentDialogResult.Primary;
}
