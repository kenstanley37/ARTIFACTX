using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Helpers.Freighter;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views.SaveEditorPages;

/// <summary>
/// The Freighter page: wiring only. The details band binds to <see cref="ViewModel"/> (FreighterPageViewModel); save reads
/// and edits are FreighterService's. This page sets up the two grids and the tech list, shows dialogs and drives the
/// two 3D previews. Unlike Exosuit (fixed capacity), a freighter's Technology/Cargo slot counts depend on its class, so
/// the grids are rebuilt in LoadGridsAsync after reading it. There's no selector strip: a save has one freighter.
/// </summary>
public sealed partial class FreighterPage : Page, ILoadoutTemplateHost
{
    public FreighterPageViewModel ViewModel { get; } = new();

    private InventoryGridViewModel? _techViewModel;
    private InventoryGridViewModel? _cargoViewModel;

    // Class -> max Technology/Cargo slots, loaded once per page lifetime from the catalog - shares the "ShipCapacity"
    // category with Ships (GcInventoryTable.ShipInventoryMaxUpgradeSize indexes "Freighter" as its own ship type).
    private Dictionary<string, int>? _shipCapacity;

    public FreighterPage()
    {
        InitializeComponent();
        TechCard.GroupOf = TechListPanel.TypeOf;
        ViewModel.HullLookChanged += (_, _) => _ = ShowFreighterPreviewAsync();
        ViewModel.CaptainChanged += (_, _) => _ = ShowCrewPreviewAsync();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSaved;
        Unloaded += Page_Unloaded;

        LoadGrids();
        TemplatesPanel.Host = this;
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (StatsBoxes.IsStaging) return; // own stat edit: nothing else changes
        DispatcherQueue.TryEnqueue(LoadGrids);
    }

    /// <summary>Releases the session subscriptions: every visit makes a new page, and a dead one left subscribed re-ran
    /// its full reload on every edit anywhere (a reported multi-second slowdown).</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.GameSavedLoadedSlot -= OnGameSaved;
    }

    /// <summary>What follows the game (LiveFieldsService), with the field that shows it. Single fields come before the
    /// inventories holding them (class and stats are in the cargo inventory), so a class picked in game flashes the
    /// class, not the cargo. The customisation list holds the paint the preview uses.</summary>
    private (string[] Path, Func<UIElement> Field)[] LiveFields() => new (string[], Func<UIElement>)[]
    {
        (NmsInventoryContainer.FreighterNamePath, () => NameEditBox),
        (NmsInventoryContainer.FreighterTypePath, () => TypeComboBox),
        (NmsInventoryContainer.FreighterModelSeedPath, () => ModelSeedBox),
        (NmsInventoryContainer.FreighterHomeSeedPath, () => HomeSeedBox),
        (NmsInventoryContainer.FreighterCrewRacePath, () => CrewRaceComboBox),
        (NmsInventoryContainer.FreighterCrewSeedPath, () => CrewSeedBox),
        (NmsInventoryContainer.FreighterEngineEffectPath, () => EngineFlareBox),
        (NmsInventoryContainer.FreighterClassPath, () => ClassPicker),
        (NmsInventoryContainer.FreighterStatBonusesPath, () => StatsBoxes),
        (NmsInventoryContainer.FreighterTechnologyPath, () => TechGrid),
        (NmsInventoryContainer.FreighterCargoPath, () => CargoGrid),
        (new[] { "vLc", "6f=", "l:j" }, () => FreighterPreview),
    };

    /// <summary>The game saved: fields not edited here take its values, and each one that changed pulses once - no
    /// reload (user choice 2026-10-22).</summary>
    private async void OnGameSaved(JObject disk)
    {
        if (!FreighterService.HasFreighter()) return;
        var fields = LiveFields();
        var changed = LiveFieldsService.Adopt(disk, fields.Select(f => f.Path),
            fields.Where(f => f.Field() == TechGrid || f.Field() == CargoGrid).Select(f => f.Path).ToList());
        if (changed.Count == 0) return;
        await LoadGridsAsync();
        foreach (var (path, field) in fields)
            if (changed.Contains(path)) FieldHighlight.Flash(field());
    }

    private async void LoadGrids() => await LoadGridsAsync();

    private async Task LoadGridsAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        await ViewModel.LoadAsync();
        if (!ViewModel.HasFreighter)
        {
            _ = RefreshAddFreighterListAsync();
            return;
        }

        string? classLetter = ViewModel.ClassLetter;
        _shipCapacity ??= await CatalogService.GetShipCapacityAsync();
        int techRows = StarshipCapacity.SlotsToRows(
            _shipCapacity.TryGetValue(StarshipCapacity.TechCapacityKey(StarshipCapacity.ShipType.Freighter, classLetter), out int techSlots)
                ? techSlots : StarshipCapacity.FallbackTechSlots);
        int cargoRows = StarshipCapacity.SlotsToRows(
            _shipCapacity.TryGetValue(StarshipCapacity.CargoCapacityKey(StarshipCapacity.ShipType.Freighter, classLetter), out int cargoSlots)
                ? cargoSlots : StarshipCapacity.FallbackCargoSlots);

        _techViewModel = new InventoryGridViewModel(NmsInventoryContainer.FreighterTechnologyPath, StarshipCapacity.Columns, techRows);
        _cargoViewModel = new InventoryGridViewModel(NmsInventoryContainer.FreighterCargoPath, StarshipCapacity.Columns, cargoRows);
        _techViewModel.Load();
        _cargoViewModel.Load();
        await CatalogService.WarmCacheAsync(_techViewModel.Cells.Concat(_cargoViewModel.Cells).Where(c => c.IsOccupied).Select(c => c.ItemId));

        TechGrid.ViewModel = _techViewModel;
        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        // Every tech on the 24 freighters in the real saves is "Freighter" (2026-10-13).
        TechGrid.AllowedUsageCategories = new[] { "Freighter" };

        CargoGrid.ViewModel = _cargoViewModel;
        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable", "ProceduralProducts" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        // Reloads run many times per page lifetime: unsubscribe first so one edit fires the handler once.
        TechGrid.CellChanged -= GridCellChanged;
        TechGrid.CellChanged += GridCellChanged;
        CargoGrid.CellChanged -= GridCellChanged;
        CargoGrid.CellChanged += GridCellChanged;
        TechGrid.Refresh();
        CargoGrid.Refresh();

        // The tech a freighter can fit, to click in - the same list limits the right-click search.
        TechListPanel.IsBestClass = () => FreighterService.ClassLetter() == "S";
        TechListPanel.SwitchToBestClassAsync = async () =>
        {
            ViewModel.ChooseClass("S");
            await LoadGridsAsync();
        };
        TechListPanel.MoreRoomHint = () => FreighterService.ClassLetter() == "S" ? null
            : "S class has more slots - change the class in Freighter Details to fit more.";
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            var tech = await CatalogService.GetTechForAsync(new[] { "Freighter" });
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modules = await CatalogService.GetUpgradeModulesAsync(FreighterService.ModuleFamilies);
            return Controls.ItemListPanel.SortByGroups(tech.Concat(modules), FreighterService.TechGroup, FreighterService.TechGroupOrder);
        }, "technology", "technologies", "Technology", FreighterService.TechGroup);

        HomeSeedBox.Show(NmsInventoryContainer.FreighterHomeSeedPath);
        ModelSeedBox.Show(NmsInventoryContainer.FreighterModelSeedPath);
        CrewSeedBox.Show(NmsInventoryContainer.FreighterCrewSeedPath);
        UpdateStatDisplay();
        _ = ShowFreighterPreviewAsync();
        _ = ShowCrewPreviewAsync();
    }

    // ------------------------------------------------------------ 3D previews

    private string? _previewKey, _crewPreviewKey;

    /// <summary>The hull from its Type and Model Seed, in the colours picked in game (customisation entry 15, or a
    /// Dreadnought's own entry tagged with its model - FreighterService.Customisation), else the Home Seed's paint.
    /// A Dreadnought is ~2 million triangles, so it's only rebuilt when something it shows changed.</summary>
    private Task ShowFreighterPreviewAsync()
    {
        string? scene = FreighterService.TypeScene(), seed = FreighterService.ModelSeed(), homeSeed = FreighterService.HomeSeed();
        string flare = FreighterService.EngineFlare();
        var paint = FreighterService.Customisation(scene).Entry?["wnR"]?["Aak"];
        string key = $"{scene}|{seed}|{homeSeed}|{flare}|{paint?.ToString(Newtonsoft.Json.Formatting.None)}";
        if (key == _previewKey) return Task.CompletedTask;
        _previewKey = key;
        return FreighterPreview.ShowAsync(scene, seed, ModelPreviewService.StoredColours(paint), EngineFlareColours.For(flare), colourSeedText: homeSeed);
    }

    /// <summary>The captain (#25): the crew race is the NPC scene and the crew seed's first child seed picks its parts
    /// (catalog format 104; Slot 8's Gek captain matched, 2026-11-15). Colours aren't known, so it's a shape scan.</summary>
    private Task ShowCrewPreviewAsync()
    {
        string? scene = FreighterService.CrewRaceScene(), seed = FreighterService.CrewSeed();
        string key = $"{scene}|{seed}";
        if (key == _crewPreviewKey) return Task.CompletedTask;
        _crewPreviewKey = key;
        return CrewPreview.ShowAsync(scene, seed, childPartSeed: true, shapeOnly: true);
    }

    // ------------------------------------------------------------ details band

    private void GridCellChanged(object? sender, EventArgs e) => ViewModel.MarkEdited();

    private void Inventory_Edited(object? sender, EventArgs e) => ViewModel.MarkEdited();

    /// <summary>Stages the rename when the box loses focus, not on every keystroke.</summary>
    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e) => ViewModel.CommitName(NameEditBox.Text);

    /// <summary>A class changes the slot counts (StarshipCapacity), so both grids are rebuilt.</summary>
    private void ClassPicker_ClassChosen(object? sender, string letter)
    {
        ViewModel.ChooseClass(letter);
        LoadGrids();
    }

    private void Seed_Changed(object? sender, SeedChange change) => ViewModel.SeedChanged(
        homeSeed: ReferenceEquals(sender, HomeSeedBox), modelSeed: ReferenceEquals(sender, ModelSeedBox),
        crewSeed: ReferenceEquals(sender, CrewSeedBox));

    private void UpdateStatDisplay()
    {
        StatsBoxes.Show(NmsInventoryContainer.FreighterStatBonusesPath, StatBonusService.FreighterStats);
        ShowInGameTotals();
    }

    /// <summary>The game's Hyperdrive Range and Fleet Coordination under the base stats (#17).</summary>
    private async void ShowInGameTotals() => StatsBoxes.ShowInGameTotals(await OwnedFreighterDisplayStatsService.EvaluateAsync());

    private void StatsBoxes_Edited(object? sender, EventArgs e)
    {
        ViewModel.MarkEdited();
        ShowInGameTotals();
    }

    /// <summary>Undoes every change made on this page - details and seeds as well as the grids.</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetAll();
        LoadGrids();
    }

    // ------------------------------------------------------------ delete / add a freighter

    private async void DeleteFreighterBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_techViewModel is null) return;
        string name = ViewModel.Name.Trim() is { Length: > 0 } n ? n : "your freighter";
        var choice = await DeleteConfirmation.ShowAsync(XamlRoot, name,
            FreighterService.DeleteSummary(_cargoViewModel?.Cells.Count(c => c.IsOccupied) ?? 0));
        if (!choice.Confirmed) return;
        if (choice.SaveTemplateFirst)
        {
            await LoadoutTemplateService.SaveAsync(FreighterService.CaptureFullBuild(_techViewModel,
                $"{(name == "your freighter" ? "Freighter" : name)} (saved before deleting)"));
            await TemplatesPanel.RefreshAsync();
        }
        FreighterBuildService.RemoveFreighter();
        LoadGrids();
    }

    /// <summary>The cards shown when the save has no freighter: the starter hulls ("Start fresh") and every Freighter
    /// Full Build template ("Add a freighter").</summary>
    private async Task RefreshAddFreighterListAsync()
    {
        StarterList.ItemsSource ??= FreighterBuildService.StarterHulls
            .Select(h => AddFreighterChoiceViewModel.Starter(h.Name, h.Description, h.ScenePath)).ToList();
        var templates = (await LoadoutTemplateService.LoadAllAsync())
            .Where(t => t.SourceKind == "Freighter" && t.Scope == "FullBuild")
            .Select(AddFreighterChoiceViewModel.FromTemplate).ToList();
        AddFreighterList.ItemsSource = templates;
        NoFreighterTemplatesTxt.Visibility = templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static AddFreighterChoiceViewModel? ChoiceOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as AddFreighterChoiceViewModel;

    private async void AddFreighterChoice_Click(object sender, RoutedEventArgs e)
    {
        if (ChoiceOf(sender) is { } choice) await AddFreighterAsync(choice.Template);
    }

    /// <summary>See what a template holds before adding it (#24).</summary>
    private async void AddFreighterPreview_Click(object sender, RoutedEventArgs e)
    {
        if (ChoiceOf(sender) is { } choice) await TemplatePreview.ShowAsync(XamlRoot, choice.Template);
    }

    private async void AddFreighterImport_Click(object sender, RoutedEventArgs e)
    {
        if (await TemplateSharingService.ImportAsync("Freighter") is not { } r) return;
        ShowAddStatus(r.Ok && r.Message.Contains("Use Apply", StringComparison.Ordinal)
            ? r.Message.Replace("Use Apply", "Use Add as my freighter", StringComparison.Ordinal) : r.Message, r.Ok);
        if (r.Ok) await RefreshAddFreighterListAsync();
    }

    private void ShowAddStatus(string message, bool ok)
    {
        AddFreighterStatusTxt.Text = message;
        AddFreighterStatusTxt.Foreground = ThemeResources.Brush(ok ? "ThemeDoneBrush" : "ThemeWarningBrush");
        AddFreighterStatusTxt.Visibility = Visibility.Visible;
    }

    /// <summary>Builds a whole freighter from the template (FreighterBuildService) in a save without one. Nothing is
    /// written until Save.</summary>
    private async Task AddFreighterAsync(NmsLoadoutTemplate template)
    {
        var (title, body) = FreighterService.AddFreighterText(template);
        var confirm = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = body },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        if (FreighterBuildService.AddFreighter(template) is string problem)
        {
            ShowAddStatus($"Couldn't add the freighter: {problem}", ok: false);
            return;
        }
        LoadGrids();
    }

    // ------------------------------------------------------------ templates (LoadoutTemplatesPanel)

    /// <summary>Confirms applying a template. A save has one freighter, so there's no target picker. The class compared
    /// is the real one (Cargo's, FreighterClassPath), not the grid's.</summary>
    private async Task<(bool Confirmed, bool AlsoMatchClass)> ConfirmApplyAsync(NmsLoadoutTemplate template, bool fullBuild)
    {
        string label = $"the \"{template.Name}\" template";
        var choice = await TechStackCopyConfirmation.ShowAsync(XamlRoot, label, template.SourceClass,
            template.UnlockedPositions.Select(p => (p.X, p.Y)), "your freighter", _techViewModel!, FreighterService.ClassLetter(),
            FreighterService.ApplySummary(label, fullBuild, fullBuild && FreighterBuildService.HasInterior(template)),
            fullBuild ? "Confirm full build apply" : "Confirm tech stack apply", "Apply");
        return (choice.Confirmed, choice.AlsoMatchClass);
    }

    /// <summary>A class change (alsoMatchClass) can change the slot counts, so the grids are rebuilt.</summary>
    private async Task ApplyTemplateAsync(NmsLoadoutTemplate template, bool fullBuild)
    {
        if (_techViewModel is null) return;
        var (confirmed, alsoMatchClass) = await ConfirmApplyAsync(template, fullBuild);
        if (!confirmed) return;
        if (fullBuild) FreighterService.ApplyFullBuild(_techViewModel, template, alsoMatchClass);
        else FreighterService.ApplyTechStack(_techViewModel, template, alsoMatchClass);
        ViewModel.MarkEdited();
        LoadGrids();
    }

    string? ILoadoutTemplateHost.CurrentName => _techViewModel is null ? null : "Your freighter";

    bool ILoadoutTemplateHost.HasTech => _techViewModel?.Cells.Any(c => c.IsOccupied) == true;

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureTechStack(string name) => FreighterService.CaptureTechStack(_techViewModel!, name);

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureFullBuild(string name) => FreighterService.CaptureFullBuild(_techViewModel!, name);

    Task ILoadoutTemplateHost.ApplyTechStackAsync(NmsLoadoutTemplate template) => ApplyTemplateAsync(template, fullBuild: false);

    Task ILoadoutTemplateHost.UseFullBuildAsync(NmsLoadoutTemplate template) => ApplyTemplateAsync(template, fullBuild: true);
}
