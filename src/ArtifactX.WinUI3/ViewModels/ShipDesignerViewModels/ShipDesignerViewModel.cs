using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ShipDesignerViewModels;

/// <summary>
/// The Ship Designer (2026-10-16, GitHub #5): build a starship the way a space station's ship fabricator does - a
/// type, one part per slot, a paint style and the style's colours - around a large live preview. Every choice comes
/// from the player's own game files (CatalogSteps.AddShipBuilder, category "ShipBuilder"), so it offers exactly what
/// the game's builder offers.
///
/// A built ship keeps its design in the save, not in its seed (in-game build diff): the parts by item id, the style
/// as a texture option and every palette's colours. The seed only names it. Save-staging and catalog loading live in
/// <see cref="ShipBuilderService"/>; saved designs in <see cref="ShipDesignsService"/>.
///
/// Paint mode (2026-10-17): Starships' "Paint this ship" opens the designer on one of the player's own ships; only
/// its paint changes. Moved here from the page's code-behind 2026-11-10 (no behaviour change).
/// </summary>
public sealed partial class ShipDesignerViewModel : ObservableObject
{
    private const int ThumbWidth = 288, ThumbHeight = 180; // drawn at 2x the card's 144 x 90

    private readonly IShipDesignerHost _host;
    private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();

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

    // The saved design being edited, if one was saved or loaded: as saved, to tell whether it has unsaved changes.
    private ShipDesign? _editing;
    private string? _designId;
    private string _designName = "";
    private List<SavedDesign> _savedDesigns = new();
    private readonly Dictionary<string, BitmapImage> _thumbs = new();
    private ShipDesign? _needsThumbnail;
    private bool _previewReady;

    // Paint mode.
    private int? _paintShip;
    private string _paintShipName = "";
    private List<string> _paintShipParts = new();
    private JToken? _paintShipSmp;
    private bool _paintShipHasPaint;
    private bool _paintRemoved;
    private bool _suspendSearch;

    public ShipDesignerViewModel(IShipDesignerHost host) => _host = host;

    public ObservableCollection<ShipTypeChoiceViewModel> Types { get; } = new();
    public ObservableCollection<ShipSlotChoiceViewModel> Slots { get; } = new();
    public ObservableCollection<ShipPartChoiceViewModel> Parts { get; } = new();
    public ObservableCollection<PaintStyleChoiceViewModel> PaintStyles { get; } = new();
    public ObservableCollection<ColourGroupViewModel> ColourGroups { get; } = new();
    public ObservableCollection<SavedDesignCardViewModel> Designs { get; } = new();

    [ObservableProperty] public partial bool HasNoData { get; set; }
    [ObservableProperty] public partial bool HasData { get; set; }
    [ObservableProperty] public partial string PartSearch { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PartsEmptyVisible))] public partial string PartsEmptyText { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StyleUnlockVisible))] public partial string StyleUnlockNote { get; set; } = "";
    [ObservableProperty] public partial string ShipName { get; set; } = "";
    [ObservableProperty] public partial string DesignStateText { get; set; } = "";
    [ObservableProperty] public partial bool NewDesignVisible { get; set; }
    [ObservableProperty] public partial bool NewNameVisible { get; set; } = true;
    [ObservableProperty] public partial string SaveDesignText { get; set; } = "Save design...";
    [ObservableProperty] public partial bool SaveDesignEnabled { get; set; } = true;
    [ObservableProperty] public partial bool SaveAsNewVisible { get; set; }
    [ObservableProperty] public partial string PreviewNote { get; set; } = "Drag to turn. Built from your game files with the parts and colours you pick.";
    [ObservableProperty] public partial string DesignSearch { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DesignsEmptyVisible))] public partial string DesignsEmptyText { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDesignMode))] public partial bool IsPaintMode { get; set; }
    [ObservableProperty] public partial string PaintPartsText { get; set; } = "";
    [ObservableProperty] public partial bool RemovePaintVisible { get; set; }
    [ObservableProperty] public partial bool WriteEnabled { get; set; }
    [ObservableProperty] public partial bool AddNewEnabled { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(WriteBlockedVisible))] public partial string WriteBlockedText { get; set; } = "";
    [ObservableProperty] public partial string SlotsText { get; set; } = "";
    [ObservableProperty] public partial bool SlotsBadgeVisible { get; set; }
    [ObservableProperty] public partial Brush? SlotsBrush { get; set; }
    [ObservableProperty] public partial string SlotsToolTip { get; set; } = "";

    public bool PartsEmptyVisible => PartsEmptyText.Length > 0;
    public bool StyleUnlockVisible => StyleUnlockNote.Length > 0;
    public bool DesignsEmptyVisible => DesignsEmptyText.Length > 0;
    public bool WriteBlockedVisible => WriteBlockedText.Length > 0;
    public bool IsDesignMode => !IsPaintMode;

    // ------------------------------------------------------------------ lifetime

    public void Attach()
    {
        GameProcessMonitorService.RunningStateChanged += OnGameRunningStateChanged;
        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnActiveSessionChanged; // slots used change with Add
        UpdateWriteState();
    }

    public void Detach()
    {
        GameProcessMonitorService.RunningStateChanged -= OnGameRunningStateChanged;
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged -= OnActiveSessionChanged;
    }

    private void OnGameRunningStateChanged(object? sender, bool isRunning) => _ui.TryEnqueue(UpdateWriteState);

    private void OnActiveSessionChanged(object? sender, EventArgs e) => _ui.TryEnqueue(UpdateWriteState);

    /// <summary>Add and Apply only stage edits, but edits staged while the game runs are lost: the game autosaves,
    /// and reloading its newer save discards them. So they wait until No Man's Sky is closed, like Save in the title bar.</summary>
    private void UpdateWriteState()
    {
        bool gameRunning = GameProcessMonitorService.IsGameRunning;
        bool saveLoaded = SaveSessionManager.IsSaveLoaded;
        WriteEnabled = saveLoaded && !gameRunning;
        // Free starship slots, said before Add rather than after (2026-10-17); used and free both said, the user's
        // wording ("5 of 12 free" read as 5 used).
        int total = NewFromTemplateService.ShipSlotCount, free = NewFromTemplateService.FreeShipSlots;
        SlotsText = $"{total - free}/{total} ship slots used · " + (free == 0 ? "none free" : $"{free} free");
        SlotsBadgeVisible = saveLoaded && total > 0 && _paintShip is null;
        SlotsBrush = ThemeResources.Brush(free == 0 ? "ThemeWarningBrush" : "ThemeAccentBrush");
        SlotsToolTip = free == 0
            ? "Sell or scrap a ship in game, or delete one on the Starships page, to add another."
            : "Add as new ship puts the design in the first empty slot.";
        AddNewEnabled = WriteEnabled && !(saveLoaded && free == 0);
        string what = _paintShip is null ? "add or apply a design" : "repaint the ship";
        WriteBlockedText = gameRunning ? $"Close No Man's Sky to {what}. You can keep choosing meanwhile."
            : !saveLoaded ? $"Load a save to {what}."
            : "";
    }

    public async Task LoadAsync(int? paintShip)
    {
        _paintShip = paintShip;
        var data = await ShipBuilderService.LoadCatalogDataAsync();
        _types = data.Types;
        _styles = data.Styles;
        _swatches = data.Swatches;
        _partGroups = data.PartGroups;
        if (_types.Count == 0)
        {
            HasNoData = true;
            return;
        }

        HasData = true;
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
        ClearPartSearch();
        _style = (_styles.GetValueOrDefault(type.StyleGroup) ?? new()).FirstOrDefault();
        ResetColours();
        BuildTypes();
        BuildSlots();
        ShowParts();
        ShowPaint();
        _ = RefreshNameAsync();
        _ = RefreshPreviewAsync();
    }

    private void BuildTypes()
    {
        Types.Clear();
        foreach (var type in _types)
            Types.Add(new ShipTypeChoiceViewModel(type, t => { if (t != _type) SelectType(t); }) { IsSelected = type == _type });
    }

    /// <summary>One row per slot, showing the part picked for it - the game builder's slots, so the whole design
    /// reads at a glance. Click one to list its parts.</summary>
    private void BuildSlots()
    {
        Slots.Clear();
        if (_type is null) return;
        foreach (var slot in _type.Slots)
            Slots.Add(new ShipSlotChoiceViewModel(slot, SelectSlot) { Item = _parts.GetValueOrDefault(slot.Id), IsSelected = slot.Id == _slot });
    }

    private void SelectSlot(BuilderSlot slot)
    {
        if (slot.Id == _slot) return;
        _slot = slot.Id;
        foreach (var s in Slots) s.IsSelected = s.Slot.Id == _slot;
        ClearPartSearch();
        ShowParts();
    }

    private void ClearPartSearch()
    {
        _suspendSearch = true;
        PartSearch = "";
        _suspendSearch = false;
    }

    partial void OnPartSearchChanged(string value)
    {
        if (!_suspendSearch) ShowParts();
    }

    private void ShowParts()
    {
        Parts.Clear();
        PartsEmptyText = "";
        var slot = _type?.Slots.FirstOrDefault(s => s.Id == _slot);
        if (slot is null) return;
        string find = PartSearch.Trim();
        var shown = slot.Items
            .Where(i => find.Length == 0 || ShipDesignerText.PartName(i.Item).Contains(find, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(i => ShipDesignerText.PartName(i.Item), StringComparer.CurrentCulture).ToList();
        if (shown.Count == 0) PartsEmptyText = $"No {slot.Label.ToLowerInvariant()} part matches \"{find}\".";
        string? picked = _parts.GetValueOrDefault(slot.Id);
        foreach (var item in shown)
            Parts.Add(new ShipPartChoiceViewModel(item.Item, SelectPart) { IsSelected = picked == item.Item });
    }

    private void SelectPart(string item)
    {
        _parts[_slot] = item;
        foreach (var p in Parts) p.IsSelected = p.Item == item;
        foreach (var s in Slots.Where(s => s.Slot.Id == _slot)) s.Item = item;
        _ = RefreshPreviewAsync();
    }

    // ------------------------------------------------------------------ paint

    /// <summary>The paint column: the style, then each colour the style offers.</summary>
    private void ShowPaint()
    {
        PaintStyles.Clear();
        ColourGroups.Clear();
        StyleUnlockNote = "";
        if (_type is null) return;
        foreach (var style in _styles.GetValueOrDefault(_type.StyleGroup) ?? new())
            PaintStyles.Add(new PaintStyleChoiceViewModel(style, SelectStyle) { IsSelected = style == _style });
        // Some styles are unlocked in game by expedition rewards; say so, as the game would.
        if (_style is { Unlock.Length: > 0 })
            StyleUnlockNote = $"In game, {_style.Name} has to be unlocked first ({ShipDesignerText.PartName(_style.Unlock)}).";

        var data = ShipBuilderService.CurrentColours(_type, _style);
        if (data is null) return;
        var swatches = _swatches.GetValueOrDefault(data.Swatches) ?? new();
        foreach (var group in data.Groups)
            ColourGroups.Add(new ColourGroupViewModel(group, swatches, _colourPicks.GetValueOrDefault(group.Title, group.DefaultIndex), PickColour));
    }

    private void SelectStyle(PaintStyle style)
    {
        if (style == _style) return;
        _style = style;
        _paintRemoved = false;
        ResetColours();
        ShowPaint();
        _ = RefreshPreviewAsync();
    }

    private void PickColour(string group, int index)
    {
        _colourPicks[group] = index;
        _paintRemoved = false;
        _ = RefreshPreviewAsync();
    }

    private void ResetColours()
    {
        _colourPicks.Clear();
        foreach (var group in ShipBuilderService.CurrentColours(_type, _style)?.Groups ?? new())
            _colourPicks[group.Title] = group.DefaultIndex;
    }

    // ------------------------------------------------------------------ name and preview

    private async Task RefreshNameAsync()
    {
        if (_type is null) return;
        if (_paintShip is not null) { ShipName = _paintShipName; return; }
        ShipName = await StarshipService.ShipNameAsync(_seed, _type.Scene) ?? "";
    }

    [RelayCommand]
    private async Task NewNameAsync()
    {
        _seed = NewSeed();
        await RefreshNameAsync();
        UpdateDesignState();
    }

    private async Task RefreshPreviewAsync()
    {
        int version = ++_previewVersion;
        _previewReady = false;
        UpdateDesignState();
        if (_type is null || GameDataService.Game is not { } game) return;
        PreviewNote = "Building the model from your game files...";
        var models = await ModelPreviewService.ModelsAsync(game);
        if (models is null || version != _previewVersion) return;

        string scene = _type.Scene;
        var root = ShipPartsService.LoadTree(ShipParts.DescriptorFor(scene));
        // Each chosen item turns on its model parts (PartGroups: FIGHT_COCKAA -> _COCKPIT_A, _ANOSE_A), looked up in
        // its own slot: an Explorer's left and right wings share item ids (SCIEN_WINGT_A) but not parts
        // (SCIEN_L_WINGT_A / SCIEN_R_WINGT_A). Paint mode: the ship's own parts - from its seed, or the ones it was
        // built with.
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
            _host.ShowPreview(null, null, hologram: false);
            PreviewNote = "This ship's model couldn't be read from your game files.";
            return;
        }

        var colour = ShipBuilderService.DesignColours(_type, _style, _swatches, _colourPicks);
        var styles = (_style?.Layers ?? new()).Select(l => l.Length > 1 ? l[1] : "").Where(o => o.Length > 0).ToHashSet(StringComparer.Ordinal);
        if (_paintRemoved)
        {
            // Paint removed: the colours it had when found can't be shown yet, so the hologram scan, as on Starships -
            // not the swatches still picked (2026-10-17).
            _host.ShowPreview(mesh, null, hologram: true);
            PreviewNote = "Paint removed - it goes back to the colours it had when you found or bought it, which can't be shown yet. Pick a colour to paint it again.";
            _previewReady = true;
            return;
        }
        var textures = await ModelPreviewService.PaintAsync(models, mesh, colour, _seed, styles);
        if (version != _previewVersion) return;
        _host.ShowPreview(mesh, m => textures.GetValueOrDefault(m), hologram: false);
        PreviewNote = _paintShip is not null
            ? "Drag to turn. Your ship with the paint you pick - its parts stay as they are."
            : "Drag to turn. Built from your game files with the parts and colours you pick.";
        _previewReady = true;
        await OnPreviewReadyAsync();
    }

    [RelayCommand]
    private static void UpdateGameData() => App.MainWindowInstance?.StartGameDataUpdate();

    // ------------------------------------------------------------------ saved designs

    /// <summary>Editing a saved design: saves the changes under its name, no questions (2026-10-17, the user: "Save
    /// changes shouldn't ask for a name"). A new design: asks for a name first.</summary>
    [RelayCommand]
    private async Task SaveDesignAsync()
    {
        if (_type is null) return;
        if (_editing is { } saved) await SaveAsync(CurrentDesign(saved.Id, saved.Name, saved.CreatedAt));
        else await SaveAsNewAsync();
    }

    [RelayCommand]
    private async Task SaveAsNewAsync()
    {
        if (_type is null) return;
        string? name = await _host.AskDesignNameAsync(_editing is null ? ShipName : $"{_designName} (copy)");
        if (name is null) return;
        await SaveAsync(CurrentDesign(Guid.NewGuid().ToString("N"), name, UserDatabase.Now()));
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
            await _host.ShowMessageAsync("Couldn't save the design", ex.Message);
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

    partial void OnDesignSearchChanged(string value) => _ = ShowDesignsAsync();

    private async Task ShowDesignsAsync()
    {
        string find = DesignSearch.Trim();
        var shown = _savedDesigns.Where(d => find.Length == 0
            || d.Design.Name.Contains(find, StringComparison.CurrentCultureIgnoreCase)
            || DesignSubtitle(d.Design).Contains(find, StringComparison.CurrentCultureIgnoreCase)).ToList();
        DesignsEmptyText = _savedDesigns.Count == 0
            ? "No saved designs yet. Make a ship you like and click Save design to keep it here."
            : shown.Count == 0 ? $"No design matches \"{find}\"." : "";

        // Every card first, pictures after: a second call while pictures decode (typing in the search box) then
        // replaces the list whole instead of interleaving its cards with this one's.
        Designs.Clear();
        var cards = shown.Select(saved => new SavedDesignCardViewModel(saved, DesignSubtitle(saved.Design), LoadCard, DeleteCard)
            { IsSelected = saved.Design.Id == _designId }).ToList();
        foreach (var card in cards) Designs.Add(card);
        foreach (var card in cards) card.Thumbnail = await ThumbnailAsync(card.Saved);
    }

    private void LoadCard(SavedDesignCardViewModel card)
    {
        // Painting a ship: a design lends its paint only (its parts would be a different ship).
        if (_paintShip is not null) _ = UseDesignPaintAsync(card.Design);
        else _ = LoadDesignAsync(card.Design, card.Saved.Thumbnail is null);
    }

    private async void DeleteCard(SavedDesignCardViewModel card)
    {
        var design = card.Design;
        if (!await _host.ConfirmAsync($"Delete \"{design.Name}\"?", "The design is removed from My designs. Ships you already made from it stay as they are.", "Delete")) return;
        await ShipDesignsService.DeleteAsync(design.Id);
        _thumbs.Remove(design.Id);
        if (design.Id == _designId) SetDesign(null);
        await RefreshDesignsAsync();
    }

    private string DesignSubtitle(ShipDesign design)
    {
        var type = _types.FirstOrDefault(t => t.Type == design.Type);
        string style = type is null ? "" : _styles.GetValueOrDefault(type.StyleGroup)?.FirstOrDefault(s => s.Option == design.Style)?.Name ?? "";
        return string.Join(" · ", new[] { type is null ? design.Type : ShipDesignerText.Title(type.Title), style }.Where(t => t.Length > 0));
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
        if (await _host.SnapshotPreviewAsync(ThumbWidth, ThumbHeight) is not { } pixels) return null;
        return await ShipBuilderService.EncodeThumbnailPngAsync(pixels, ThumbWidth, ThumbHeight);
    }

    /// <summary>Called when the preview has drawn the current design: a loaded design with no picture gets one - if
    /// the preview still shows exactly that design.</summary>
    private async Task OnPreviewReadyAsync()
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

    /// <summary>Puts a saved design back into the designer. Anything the game data no longer has (after a game
    /// update) falls back to the builder's default.</summary>
    private async Task LoadDesignAsync(ShipDesign design, bool needsThumbnail)
    {
        var type = _types.FirstOrDefault(t => t.Type == design.Type);
        if (type is null)
        {
            await _host.ShowMessageAsync("Can't load this design", $"Your game data has no \"{design.Type}\" ship type. Update your game data, or delete the design.");
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
        ClearPartSearch();
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
        BuildTypes();
        BuildSlots();
        ShowParts();
        ShowPaint();
        _ = RefreshNameAsync();
        _ = RefreshPreviewAsync();
        if (missing)
            await _host.ShowMessageAsync("Some parts have changed", "Your game data no longer has everything this design used, so those choices are back to the default. Save the design again to keep it this way.");
    }

    [RelayCommand]
    private void NewDesign()
    {
        if (_type is null) return;
        SetDesign(null);
        _seed = NewSeed();
        SelectType(_type);
    }

    /// <summary>Says whether this is a new design or a saved one being edited, and whether that one has changed since
    /// it was saved.</summary>
    private void UpdateDesignState()
    {
        if (_paintShip is not null) return;
        if (_editing is not { } saved || _type is null)
        {
            DesignStateText = "New design · not saved yet";
            NewDesignVisible = false;
            SaveDesignText = "Save design...";
            SaveDesignEnabled = true;
            SaveAsNewVisible = false;
            return;
        }
        var now = CurrentDesign(saved.Id, saved.Name, saved.CreatedAt);
        bool changed = now.Type != saved.Type || now.Style != saved.Style || now.Seed != saved.Seed
            || !SameEntries(now.Parts, saved.Parts) || !SameEntries(now.Colours, saved.Colours);
        DesignStateText = $"Editing: {saved.Name}" + (changed ? " · unsaved changes" : "");
        NewDesignVisible = true;
        SaveDesignText = "Save changes";
        SaveDesignEnabled = changed;
        SaveAsNewVisible = true;
    }

    private static bool SameEntries<T>(Dictionary<string, T> a, Dictionary<string, T> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value));

    private void SetDesign(ShipDesign? design)
    {
        _editing = design;
        _designId = design?.Id;
        _designName = design?.Name ?? "";
        UpdateDesignState();
        foreach (var card in Designs) card.IsSelected = card.Design.Id == design?.Id;
    }

    // ------------------------------------------------------------------ paint mode

    private async Task EnterPaintModeAsync()
    {
        int index = _paintShip!.Value;
        var target = await ShipBuilderService.ReadPaintTargetAsync(index, _types);
        if (target is null)
        {
            _paintShip = null;
            // Back to an ordinary new design: the state line and slots badge were skipped while this was paint mode.
            UpdateDesignState();
            UpdateWriteState();
            await _host.ShowMessageAsync("This ship can't be painted here", "Only Fighters, Haulers, Explorers and Solars can be painted - the ship types a space station's fabricator builds.");
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

        IsPaintMode = true;
        PaintPartsText = (_paintShipParts.Count > 0
                ? "This ship keeps the parts it was built with."
                : "This ship keeps its own parts - the ones its seed gives.")
            + " Only its paint changes. The game itself doesn't let you repaint a ship you found or bought; this changes its colours in your save.";
        DesignStateText = "Repainting your ship · its parts and name stay as they are";
        NewDesignVisible = NewNameVisible = false;
        RemovePaintVisible = _paintShipHasPaint && _paintShipParts.Count == 0;
        UpdateWriteState();
        ShowPaint();
        _ = RefreshNameAsync();
        _ = RefreshPreviewAsync();
    }

    [RelayCommand]
    private async Task RepaintAsync()
    {
        if (_paintShip is not int index || _type is null || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        ShipBuilderService.StageRepaint(index, _paintShipSmp, _type, _style, _parts, _swatches, _colourPicks);
        _paintShipHasPaint = true;
        _paintRemoved = false;
        _ = RefreshPreviewAsync();
        RemovePaintVisible = _paintShipParts.Count == 0;
        await _host.ShowMessageAsync("Repainted", $"\"{_paintShipName}\" has its new paint. Save from the title bar to write it to your save, then load the save in game.");
    }

    [RelayCommand]
    private async Task RemovePaintAsync()
    {
        if (_paintShip is not int index || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        if (!await _host.ConfirmAsync($"Remove the paint from \"{_paintShipName}\"?",
                "It goes back to the colours it had when you found or bought it. Nothing is written to your save until you Save.", "Remove paint")) return;
        ShipBuilderService.StageRemovePaint(index);
        _paintShipHasPaint = false;
        _paintRemoved = true;
        RemovePaintVisible = false;
        _ = RefreshPreviewAsync();
        await _host.ShowMessageAsync("Paint removed", "Save from the title bar to write it to your save.");
    }

    [RelayCommand]
    private void ExitPaint() => _host.OpenNewDesigner();

    /// <summary>Paint mode: a saved design lends its paint style and colours.</summary>
    private async Task UseDesignPaintAsync(ShipDesign design)
    {
        if (_type is null) return;
        var style = (_styles.GetValueOrDefault(_type.StyleGroup) ?? new()).FirstOrDefault(s => s.Option == design.Style);
        if (design.Style is not null && style is null)
        {
            await _host.ShowMessageAsync("That paint style isn't offered here", $"\"{design.Name}\" uses a paint style this ship type doesn't have.");
            return;
        }
        _style = style ?? _style;
        _paintRemoved = false;
        ResetColours();
        foreach (var (group, swatch) in design.Colours)
            if (_colourPicks.ContainsKey(group)) _colourPicks[group] = swatch;
        ShowPaint();
        _ = RefreshPreviewAsync();
    }

    // ------------------------------------------------------------------ add & apply to save

    [RelayCommand]
    private async Task AddNewAsync()
    {
        if (_type is null || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        var owned = ShipBuilderService.RegularShips();
        int active = SaveSessionManager.GetValue(NmsInventoryContainer.ActiveShipIndexPath)?.Value<int>() ?? -1;
        // The new ship starts as a copy of one you own (its tech, so it flies straight away).
        int skeleton = owned.Any(s => s.Index == active) ? active : owned.FirstOrDefault().Index;
        if (owned.Count == 0 || NewFromTemplateService.FirstEmptyShipSlot() is not int slot)
        {
            await _host.ShowMessageAsync("No room for another Starship", owned.Count == 0
                ? "This save doesn't have a starship to start from yet. Get one in game first."
                : $"All {NewFromTemplateService.ShipSlotCount} Starship slots are in use. Sell or scrap one in game, or delete one on the Starships page, then try again.");
            return;
        }
        if (!await _host.ConfirmAsync($"Add \"{ShipName}\" as a new Starship?",
                $"It goes in empty slot {slot + 1} with these parts and paint. It starts with the technology of one of your ships so it can fly, and an empty cargo hold. Nothing is written to your save until you Save.",
                "Add")) return;

        ShipBuilderService.StageNewShip(skeleton, slot, _type, _seed, _style, _parts, _swatches, _colourPicks);
        await _host.ShowMessageAsync("Added", $"\"{ShipName}\" is in slot {slot + 1}. Save from the title bar to write it to your save, then load the save in game.");
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (_type is null || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        var owned = ShipBuilderService.RegularShips();
        if (owned.Count == 0) { await _host.ShowMessageAsync("No ship to apply it to", "This save has no starship the design can go on."); return; }

        var ships = new List<ApplyShipChoice>();
        foreach (var (index, scene, name) in owned)
        {
            string label = name.Length > 0 ? name
                : ShipBuilderService.TryParseSeed(index) is ulong seed && await StarshipService.ShipNameAsync(seed, scene) is { } gameName ? gameName
                : $"Starship {index + 1}";
            ships.Add(new ApplyShipChoice(index, label, name.Length > 0));
        }
        var choice = new ApplyDesignViewModel(ShipName, ships);
        if (!await _host.ShowApplyDesignAsync(choice) || choice.SelectedShip is not { } target) return;

        ShipBuilderService.StageApplyDesign(target.Index, choice.Rename == true, _type, _seed, _style, _parts, _swatches, _colourPicks);
        await _host.ShowMessageAsync("Applied", "Save from the title bar to write it to your save, then load the save in game.");
    }
}
