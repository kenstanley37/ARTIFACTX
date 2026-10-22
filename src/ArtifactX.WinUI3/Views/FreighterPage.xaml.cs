using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Unlike Exosuit (fixed capacity), a Freighter's Technology/Cargo max slots
/// depend on its Class, so the grids can't be built once in the constructor -
/// they're rebuilt in LoadGrids after reading the current class, same pattern
/// as ShipsPage/MultiToolPage. Unlike those pages there's no selector strip:
/// a player only ever actively manages one freighter's inventory (owning a
/// wider Frigate Fleet is a separate system, not exposed here).
/// </summary>
public sealed partial class FreighterPage : Page, ILoadoutTemplateHost
{


    private InventoryGridViewModel? _techViewModel;
    private InventoryGridViewModel? _cargoViewModel;

    // Class -> max Technology/Cargo slots, loaded once per page lifetime from
    // the catalog - shares the same "ShipCapacity" category as Ships, since
    // GcInventoryTable.ShipInventoryMaxUpgradeSize indexes "Freighter" as its
    // own ship type (see StarshipCapacity.ShipType.Freighter).
    private Dictionary<string, int>? _shipCapacity;

    // Model scene-path options for Type/Crew Race, loaded once per page
    // lifetime - options don't change mid-session. Same pattern as
    // MultiToolPage's Type combo.
    private sealed record SceneOption(string DisplayName, string ScenePath);
    private List<SceneOption>? _typeOptions;
    private List<SceneOption>? _crewRaceOptions;
    private bool _suppressTypeSelectionEvent;
    private bool _suppressCrewRaceSelectionEvent;


    public FreighterPage()
    {
        InitializeComponent();
        TechCard.GroupOf = TechListPanel.TypeOf;

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

    /// <summary>Without this, the constructor's subscriptions above never
    /// get released across page navigation (Frame.Navigate makes a fresh
    /// Page instance every visit, no NavigationCacheMode set anywhere in
    /// this app) - every past visit leaves a dead instance permanently
    /// subscribed, re-running its full reload on every future edit
    /// anywhere in the app. Root cause of a reported slowdown where
    /// editing any page became multi-second after enough navigation.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.GameSavedLoadedSlot -= OnGameSaved;
    }

    /// <summary>What follows the game (LiveFieldsService), with the field that shows it.
    /// Single fields come before the inventories holding them (class and stats are in
    /// the cargo inventory), so a class picked in game flashes the class, not the cargo.
    /// The customisation list holds the paint the preview uses.</summary>
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

    /// <summary>The game saved: fields not edited here take its values, and each one
    /// that changed pulses once - no reload (user choice 2026-10-22).</summary>
    private async void OnGameSaved(JObject disk)
    {
        if (!FreighterService.HasFreighter()) return;
        var fields = LiveFields();
        var changed = LiveFieldsService.Adopt(disk, fields.Select(f => f.Path));
        if (changed.Count == 0) return;
        await LoadGridsAsync();
        foreach (var (path, field) in fields)
            if (changed.Contains(path)) FieldHighlight.Flash(field());
    }

    private async Task<Dictionary<string, int>> GetShipCapacityAsync()
    {
        _shipCapacity ??= await CatalogService.GetShipCapacityAsync();
        return _shipCapacity;
    }

    private async void LoadGrids() => await LoadGridsAsync();

    private async Task LoadGridsAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        if (!FreighterService.HasFreighter())
        {
            NoFreighterPanel.Visibility = Visibility.Visible;
            HeaderGrid.Visibility = Visibility.Collapsed;
            _ = RefreshAddFreighterListAsync();
            return;
        }

        NoFreighterPanel.Visibility = Visibility.Collapsed;
        HeaderGrid.Visibility = Visibility.Visible;

        string? classLetter = FreighterService.ClassLetter();
        var capacity = await GetShipCapacityAsync();
        int techRows = StarshipCapacity.SlotsToRows(
            capacity.TryGetValue(StarshipCapacity.TechCapacityKey(StarshipCapacity.ShipType.Freighter, classLetter), out int techSlots)
                ? techSlots : StarshipCapacity.FallbackTechSlots);
        int cargoRows = StarshipCapacity.SlotsToRows(
            capacity.TryGetValue(StarshipCapacity.CargoCapacityKey(StarshipCapacity.ShipType.Freighter, classLetter), out int cargoSlots)
                ? cargoSlots : StarshipCapacity.FallbackCargoSlots);

        _techViewModel = new InventoryGridViewModel(NmsInventoryContainer.FreighterTechnologyPath, StarshipCapacity.Columns, techRows);
        _cargoViewModel = new InventoryGridViewModel(NmsInventoryContainer.FreighterCargoPath, StarshipCapacity.Columns, cargoRows);

        _techViewModel.Load();
        _cargoViewModel.Load();

        var itemIds = _techViewModel.Cells.Concat(_cargoViewModel.Cells)
            .Where(c => c.IsOccupied)
            .Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

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

        // Reload can run many times per page lifetime (class change, session
        // refresh) - unsubscribe first so CellChanged doesn't fire the
        // reset-button handler multiple times per single actual edit.
        TechGrid.CellChanged -= GridCellChanged;
        TechGrid.CellChanged += GridCellChanged;
        CargoGrid.CellChanged -= GridCellChanged;
        CargoGrid.CellChanged += GridCellChanged;

        TechGrid.Refresh();
        CargoGrid.Refresh();

        PageResetBtn.Visibility = FreighterService.HasPageEdits() ? Visibility.Visible : Visibility.Collapsed;

        // The tech a freighter can fit, to click in - the same list limits
        // the right-click search.
        TechListPanel.IsBestClass = () => FreighterService.ClassLetter() == "S";
        TechListPanel.SwitchToBestClassAsync = async () =>
        {
            SaveSessionManager.StageEdit("S", NmsInventoryContainer.FreighterClassPath);
            PageResetBtn.Visibility = Visibility.Visible;
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

        ShowClass();
        NameEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterNamePath)?.Value<string>() ?? "";
        ShowFoldedName();
        HomeSeedBox.Show(NmsInventoryContainer.FreighterHomeSeedPath);
        ModelSeedBox.Show(NmsInventoryContainer.FreighterModelSeedPath);
        CrewSeedBox.Show(NmsInventoryContainer.FreighterCrewSeedPath);
        ShowEngineFlare();

        await SyncTypeComboBoxAsync();
        await SyncCrewRaceComboBoxAsync();
        UpdateStatDisplay();
        _ = ShowFreighterPreviewAsync();
    }

    /// <summary>The 3D preview from the freighter's Type and Model seed, through staged edits.</summary>
    /// Custom colours the player picked in game are character-customisation entry 15 (l:j) -
    /// found by a paint diff on Slot 8, 2026-10-22: Paint/Alternative2 = accent, Freighter/
    /// Primary = secondary, Freighter/Alternative2 = primary.
    /// A Dreadnought's are kept in its own entry instead, tagged with its model ("@6c":
    /// "^PIRATEFREIGHTER" in T>1, entry 23 on Slot 1) with PirateBase/PirateAlt palettes -
    /// exactly what its textures ask for (2026-10-22).
    private Task ShowFreighterPreviewAsync()
    {
        string? scene = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>();
        string? seed = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterModelSeedPath)?.Value<string>();
        var paint = CustomisationFor(scene)?["wnR"]?["Aak"];
        string? flare = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterEngineEffectPath)?.Value<string>();
        // The page reloads on every edit and game save; a Dreadnought is ~2 million
        // triangles, so the model is only rebuilt when something it shows changed.
        string key = $"{scene}|{seed}|{flare}|{paint?.ToString(Newtonsoft.Json.Formatting.None)}";
        if (key == _previewKey) return Task.CompletedTask;
        _previewKey = key;
        return FreighterPreview.ShowAsync(scene, seed, ModelPreviewService.StoredColours(paint), FlareColour(flare));
    }

    private string? _previewKey;

    /// <summary>The customisation entry holding this freighter model's colours: the one tagged
    /// with the model's name, else entry 15.</summary>
    private static JToken? CustomisationFor(string? scene)
    {
        if (SaveSessionManager.GetValue("vLc", "6f=", "l:j") is not JArray entries) return null;
        string model = "^" + System.IO.Path.GetFileName(scene ?? "").Split('.')[0];
        return entries.FirstOrDefault(e => e["wnR"]?["T>1"] is JArray tags
                && tags.Any(t => string.Equals(t["@6c"]?.ToString(), model, StringComparison.OrdinalIgnoreCase)))
            ?? (entries.Count > 15 ? entries[15] : null);
    }

    /// <summary>Each flare's glow, picked by eye from the game's Customisation screen (2026-10-22).</summary>
    private static uint FlareColour(string? value) => value?.ToUpperInvariant() switch
    {
        "^GREEN" => 0x3DFF7A,
        "^BLACKHOLE" => 0xC58BFF,
        "^YELLOW" => 0xFFE44A,
        "^PURPLE" => 0xF04AE0,
        "^TURQUOISE" => 0x3FEFF0,
        "^RED" => 0xFF4A35,
        _ => 0x4A8CFF, // ^DEFAULT: Standard blue
    };

    private void GridCellChanged(object? sender, EventArgs e) =>
        PageResetBtn.Visibility = Visibility.Visible;

    /// <summary>Stages the rename when the field loses focus - not on every
    /// keystroke, matching Ships/MultiTool's own name-editing behavior.</summary>
    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        string? currentName = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterNamePath)?.Value<string>();
        if (newName == currentName) return;

        SaveSessionManager.StageEdit(newName, NmsInventoryContainer.FreighterNamePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Loads the Type option list from the catalog once per page
    /// lifetime, then syncs the combo's selection to the current freighter's
    /// scene path - same pattern as MultiToolPage's SyncTypeComboBoxAsync.</summary>
    private async Task SyncTypeComboBoxAsync()
    {
        _suppressTypeSelectionEvent = true;

        if (_typeOptions is null)
        {
            var raw = await CatalogService.GetFreighterTypesAsync();
            _typeOptions = raw.Select(t => new SceneOption(t.DisplayName, t.ScenePath)).ToList();
            TypeComboBox.ItemsSource = _typeOptions;
        }

        string? currentPath = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>();
        TypeComboBox.SelectedItem = _typeOptions.FirstOrDefault(t =>
            string.Equals(t.ScenePath, currentPath, StringComparison.OrdinalIgnoreCase));

        _suppressTypeSelectionEvent = false;
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTypeSelectionEvent) return;
        if (TypeComboBox.SelectedItem is not SceneOption option) return;

        SaveSessionManager.StageEdit(option.ScenePath, NmsInventoryContainer.FreighterTypePath);
        PageResetBtn.Visibility = Visibility.Visible;
        _ = ShowFreighterPreviewAsync();
    }

    private async Task SyncCrewRaceComboBoxAsync()
    {
        _suppressCrewRaceSelectionEvent = true;

        if (_crewRaceOptions is null)
        {
            var raw = await CatalogService.GetFreighterCrewRacesAsync();
            _crewRaceOptions = raw.Select(t => new SceneOption(t.DisplayName, t.ScenePath)).ToList();
            CrewRaceComboBox.ItemsSource = _crewRaceOptions;
        }

        string? currentPath = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterCrewRacePath)?.Value<string>();
        CrewRaceComboBox.SelectedItem = _crewRaceOptions.FirstOrDefault(t =>
            string.Equals(t.ScenePath, currentPath, StringComparison.OrdinalIgnoreCase));

        _suppressCrewRaceSelectionEvent = false;
    }

    private void CrewRaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressCrewRaceSelectionEvent) return;
        if (CrewRaceComboBox.SelectedItem is not SceneOption option) return;

        SaveSessionManager.StageEdit(option.ScenePath, NmsInventoryContainer.FreighterCrewRacePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Reads/writes NmsInventoryContainer.FreighterClassPath (8ZP,
    /// Cargo) directly rather than going through _techViewModel.CurrentClass/
    /// SetClass (tied to 0wS, Technology) the way Ships/MultiTool's own class
    /// selectors do - see FreighterClassPath's own doc comment for why
    /// Freighter specifically needs its own separate read/write here.</summary>
    private void ShowClass()
    {
        ClassPicker.Visibility = !SaveSessionManager.IsSaveLoaded ? Visibility.Collapsed : Visibility.Visible;
        ClassPicker.SelectedClass = FreighterService.ClassLetter();
    }

    private void ClassPicker_ClassChosen(object? sender, string letter)
    {
        // A class change can change the freighter's real Tech/Cargo
        // slot totals (StarshipCapacity), so both grids get fully
        // rebuilt via LoadGrids rather than just refreshed in place.
        SaveSessionManager.StageEdit(letter, NmsInventoryContainer.FreighterClassPath);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadGrids();
    }

    /// <summary>Loads 0wS.@bB's raw continuous stat rolls (FREI_HYPERDRIVE/
    /// FREI_FLEET) into the editable NumberBoxes - same source/shape as
    /// Multi-Tool's OsQ.@bB, confirmed by real save data and cross-checked
    /// against a reference tool's own "Hyperdrive"/"Fleet Coordination" Base Stats.</summary>
    /// <summary>The Base Stats boxes: the stat-bonus entries this page shows.</summary>
    private static readonly (string Label, string Key)[] BaseStats = StatBonusService.FreighterStats;

    private void UpdateStatDisplay()
    {
        StatsBoxes.Show(NmsInventoryContainer.FreighterStatBonusesPath, BaseStats);
    }

    private void StatsBoxes_Edited(object? sender, EventArgs e) => PageResetBtn.Visibility = Visibility.Visible;

    /// <summary>The whole freighter as a Full Build template: tech, unlocked
    /// slots, stats, look and furnished interior.</summary>
    private NmsLoadoutTemplate BuildFullBuildTemplate(string name)
    {
        string freighterName = NameEditBox.Text?.Trim() is { Length: > 0 } n ? n : "Freighter";
        var template = _techViewModel!.ExtractTemplate(name, freighterName, "Freighter");
        template.Scope = "FullBuild";
        template.Stats = StatBonusService.ReadAll(NmsInventoryContainer.FreighterStatBonusesPath);
        // Model Seed only (the hull's own look) - deliberately never the
        // Crew Seed (the captain's look, a separate concept), matching
        // NmsLoadoutTemplate.Seed's own doc comment.
        template.Seed = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterModelSeedPath)?.Value<string>();
        template.TypeScenePath = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>();
        FreighterBuildService.Capture(template);
        // Every unlocked slot, not just the ones holding tech (a Tech Stack
        // template keeps only those, so it never unlocks extra slots).
        template.UnlockedPositions = _techViewModel.Cells.Where(c => c.State != InventorySlotState.Locked)
            .Select(c => new NmsLoadoutPosition { X = c.X, Y = c.Y }).ToList();
        return template;
    }

    private async void DeleteFreighterBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_techViewModel is null) return;
        string name = NameEditBox.Text?.Trim() is { Length: > 0 } n ? n : "your freighter";
        int cargo = _cargoViewModel?.Cells.Count(c => c.IsOccupied) ?? 0;
        int frigates = (SaveSessionManager.GetValue(NmsFrigatePaths.FrigateArrayPath) as JArray)?.Count ?? 0;
        string whatGoes = "its tech, its furnished interior"
            + (cargo > 0 ? $", the {cargo} stacks in its cargo" : "")
            + " and its look. What you've learned (blueprints, known tech) stays"
            + (frigates > 0 ? $", and your {frigates} frigate{(frigates == 1 ? "" : "s")} stay as they are" : "");
        var choice = await DeleteConfirmation.ShowAsync(XamlRoot, name, whatGoes);
        if (!choice.Confirmed) return;

        if (choice.SaveTemplateFirst)
        {
            await LoadoutTemplateService.SaveAsync(BuildFullBuildTemplate($"{(name == "your freighter" ? "Freighter" : name)} (saved before deleting)"));
            await TemplatesPanel.RefreshAsync();
        }
        FreighterBuildService.RemoveFreighter();
        LoadGrids();
    }

    /// <summary>The cards shown when the save has no freighter: the starter hulls ("Start
    /// fresh") and every Freighter Full Build template ("Add a freighter").</summary>
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
        var result = await TemplateSharingService.ImportAsync("Freighter");
        if (result is not { } r) return;
        AddFreighterStatusTxt.Text = r.Ok && r.Message.Contains("Use Apply", StringComparison.Ordinal)
            ? r.Message.Replace("Use Apply", "Use Add as my freighter", StringComparison.Ordinal) : r.Message;
        AddFreighterStatusTxt.Foreground = ThemeResources.Brush(r.Ok ? "ThemeDoneBrush" : "ThemeWarningBrush");
        AddFreighterStatusTxt.Visibility = Visibility.Visible;
        if (r.Ok) await RefreshAddFreighterListAsync();
    }

    /// <summary>Builds a whole freighter from the template (FreighterBuildService)
    /// in a save that doesn't have one. Nothing is written until Save.</summary>
    private async Task AddFreighterAsync(NmsLoadoutTemplate template)
    {
        bool fresh = template.TechItems.Count <= 2 && !FreighterBuildService.HasInterior(template);
        var confirm = new ContentDialog
        {
            Title = fresh ? $"Add a new {template.Name.ToLowerInvariant()}?" : $"Add \"{template.Name}\" as your freighter?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = fresh
                    ? "A brand-new one, just as the game hands it over: C class, a hyperdrive and scanner, the starter interior and a random look, "
                        + "parked in the system you're in, with a new Gek captain (change the race under Crew Race). Nothing is written to your save until you Save."
                    : "It arrives with this template's model, look, tech, class, stats and "
                    + (FreighterBuildService.HasInterior(template) ? "furnished interior" : "the standard starter interior")
                    + ", parked in the system you're in. Its cargo starts empty and it gets a new Gek captain (change the race under Crew Race). "
                    + "Nothing is written to your save until you Save."
            },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        if (FreighterBuildService.AddFreighter(template) is string problem)
        {
            AddFreighterStatusTxt.Text = $"Couldn't add the freighter: {problem}";
            AddFreighterStatusTxt.Foreground = ThemeResources.Brush("ThemeWarningBrush");
            AddFreighterStatusTxt.Visibility = Visibility.Visible;
            return;
        }
        LoadGrids();
    }

    /// <summary>Applies a saved Full Build template's tech, stats, and Model
    /// Seed to the freighter - Class only changes if the user opts in, same
    /// as ApplyTemplateAsync.</summary>
    private async Task ApplyFullBuildTemplateAsync(NmsLoadoutTemplate template)
    {
        if (_techViewModel is null) return;

        var sourcePositions = template.UnlockedPositions.Select(p => (p.X, p.Y));
        var (confirmed, alsoMatchClass) = await ShowApplyConfirmationAsync(
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions, includesStatsAndSeed: true,
            replacesInterior: FreighterBuildService.HasInterior(template));

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        FreighterBuildService.ApplyInterior(template);
        StatBonusService.WriteAll(NmsInventoryContainer.FreighterStatBonusesPath, template.Stats);
        if (!string.IsNullOrEmpty(template.Seed))
            SaveSessionManager.StageEdit(template.Seed, NmsInventoryContainer.FreighterModelSeedPath);
        if (!string.IsNullOrEmpty(template.TypeScenePath))
            SaveSessionManager.StageEdit(template.TypeScenePath, NmsInventoryContainer.FreighterTypePath);
        // ApplyTemplate's own internal class write lands on the Technology
        // container (0wS, inert for Class - see FreighterClassPath), so the
        // real class write has to happen here too, same as Seed/TypeScenePath.
        if (alsoMatchClass && !string.IsNullOrEmpty(template.SourceClass))
            SaveSessionManager.StageEdit(template.SourceClass, NmsInventoryContainer.FreighterClassPath);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadGrids();
    }

    /// <summary>Applies a saved Tech Stack template to the freighter - same
    /// flow as Ships/MultiTool's ApplyTemplateAsync, minus the target picker
    /// step (only one freighter exists to apply onto). A class change
    /// (alsoMatchClass) can change the freighter's real Tech/Cargo slot
    /// totals (StarshipCapacity), so the grids get fully rebuilt via
    /// LoadGrids rather than just refreshed in place.</summary>
    private async Task ApplyTemplateAsync(NmsLoadoutTemplate template)
    {
        if (_techViewModel is null) return;

        var sourcePositions = template.UnlockedPositions.Select(p => (p.X, p.Y));
        var (confirmed, alsoMatchClass) = await ShowApplyConfirmationAsync(
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions);

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        // See ApplyFullBuildTemplateAsync - ApplyTemplate's own internal class
        // write lands on the wrong (inert) container for Freighter.
        if (alsoMatchClass && !string.IsNullOrEmpty(template.SourceClass))
            SaveSessionManager.StageEdit(template.SourceClass, NmsInventoryContainer.FreighterClassPath);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadGrids();
    }

    /// <summary>Confirms applying a template. A save only ever has one freighter, so
    /// there's no target picker - just "apply this to your freighter". The class
    /// compared is the real one (Cargo's, FreighterClassPath), not the grid's.</summary>
    private async Task<(bool Confirmed, bool AlsoMatchClass)> ShowApplyConfirmationAsync(
        string sourceLabel, string? sourceClass, IEnumerable<(int X, int Y)> sourcePositions, bool includesStatsAndSeed = false,
        bool replacesInterior = false)
    {
        if (_techViewModel is null) return (false, false);

        string summary = includesStatsAndSeed && replacesInterior
            ? $"This replaces your freighter's entire tech loadout, base stats, Type, appearance seed and interior (every room and furnishing) with {sourceLabel}. "
                + "Items in your storage containers aren't affected."
            : includesStatsAndSeed
            ? $"This replaces your freighter's entire tech loadout, base stats, Type, and appearance seed with {sourceLabel}."
            : $"This replaces your freighter's entire tech loadout with {sourceLabel}.";
        var choice = await TechStackCopyConfirmation.ShowAsync(XamlRoot, sourceLabel, sourceClass, sourcePositions,
            "your freighter", _techViewModel, FreighterService.ClassLetter(), summary,
            includesStatsAndSeed ? "Confirm full build apply" : "Confirm tech stack apply", "Apply");
        return (choice.Confirmed, choice.AlsoMatchClass);
    }

    // ------------------------------------------------------------ Templates sidebar (LoadoutTemplatesPanel)

    string? ILoadoutTemplateHost.CurrentName => _techViewModel is null ? null : "Your freighter";

    bool ILoadoutTemplateHost.HasTech => _techViewModel?.Cells.Any(c => c.IsOccupied) == true;

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureTechStack(string name) =>
        _techViewModel!.ExtractTemplate(name, NameEditBox.Text?.Trim() is { Length: > 0 } n ? n : "Freighter", "Freighter");

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureFullBuild(string name) => BuildFullBuildTemplate(name);

    Task ILoadoutTemplateHost.ApplyTechStackAsync(NmsLoadoutTemplate template) => ApplyTemplateAsync(template);

    Task ILoadoutTemplateHost.UseFullBuildAsync(NmsLoadoutTemplate template) => ApplyFullBuildTemplateAsync(template);

    /// <summary>Undoes every change made on this page - the details and seeds as well
    /// as the grids, which were all the old Reset covered.</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        FreighterService.RevertPageEdits();
        PageResetBtn.Visibility = Visibility.Collapsed;
        LoadGrids();
    }

    /// <summary>What the Details band says while folded: your freighter's name.</summary>
    private void ShowFoldedName() => DetailsCard.FoldedName = NameEditBox.Text?.Trim() ?? "";

    private void Inventory_Edited(object? sender, EventArgs e) => PageResetBtn.Visibility = Visibility.Visible;

    /// <summary>One of the game's seven engine flares (NmsInventoryContainer.FreighterEngineEffects).</summary>
    private sealed record EngineFlare(string Name, string Value);

    private bool _loadingFlare;

    private void ShowEngineFlare()
    {
        _loadingFlare = true;
        EngineFlareBox.ItemsSource ??= NmsInventoryContainer.FreighterEngineEffects.Select(f => new EngineFlare(f.Name, f.Value)).ToList();
        string current = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterEngineEffectPath)?.Value<string>() ?? "";
        EngineFlareBox.SelectedItem = ((List<EngineFlare>)EngineFlareBox.ItemsSource).FirstOrDefault(f => string.Equals(f.Value, current, StringComparison.OrdinalIgnoreCase));
        _loadingFlare = false;
    }

    private void EngineFlareBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingFlare || EngineFlareBox.SelectedItem is not EngineFlare flare) return;
        if (flare.Value == SaveSessionManager.GetValue(NmsInventoryContainer.FreighterEngineEffectPath)?.Value<string>()) return;
        SaveSessionManager.StageEdit(flare.Value, NmsInventoryContainer.FreighterEngineEffectPath);
        PageResetBtn.Visibility = Visibility.Visible;
        _ = ShowFreighterPreviewAsync();
    }

    private void Seed_Changed(object? sender, SeedChange change)
    {
        PageResetBtn.Visibility = Visibility.Visible;
        if (sender == ModelSeedBox) _ = ShowFreighterPreviewAsync();
    }
}
