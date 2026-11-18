using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views.SaveEditorPages;

/// <summary>
/// The Starships page: wiring only. The strip, details band and tech card bind to <see cref="ViewModel"/>
/// (ShipsPageViewModel); save reads and edits are StarshipService's. This page sets up the tech and cargo grids and the
/// tech list, follows the game (live fields and ships), runs the name box's typing, shows dialogs and draws the parts
/// list and 3D preview.
/// </summary>
public sealed partial class ShipsPage : Page, ILoadoutTemplateHost
{
    public ShipsPageViewModel ViewModel { get; } = new();

    public ShipsPage()
    {
        InitializeComponent();
        TechCard.GroupOf = TechListPanel.TypeOf;
        PartsPanel.ViewModel.SeedChosen += PartsPanel_SeedChosen;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        LiveItemsService.Changed += OnLiveItemsChanged;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSaved;
        Unloaded += Page_Unloaded;

        LoadShipList();
        TemplatesPanel.Host = this;
    }

    /// <summary>Skips the reload for this page's own stat edits (StatsBoxes) and name typing - rebuilding both grids on
    /// each made switching boxes lag (2026-08-01). Every other edit gets the full reload.</summary>
    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (ViewModel.IsStagingName || StatsBoxes.IsStaging) return;
        DispatcherQueue.TryEnqueue(() => LoadShipList());
    }

    /// <summary>The game added or dropped a ship while it played: only the strip changes, the ship on screen stays.</summary>
    private void OnLiveItemsChanged(LiveCollection list)
    {
        if (list == LiveCollection.Ships) DispatcherQueue.TryEnqueue(() => LoadShipList(live: true));
    }

    /// <summary>Releases the session subscriptions: every visit makes a new page, and a dead one left subscribed re-ran
    /// its full reload on every edit anywhere (a reported multi-second slowdown, 2026-08-01).</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
        LiveItemsService.Changed -= OnLiveItemsChanged;
        SaveSessionManager.GameSavedLoadedSlot -= OnGameSaved;
    }

    /// <summary>What follows the game for the ship on screen (LiveFieldsService), with the field that shows it. The class
    /// is kept on both inventories, so both class fields come before the inventories - a class change flashes the class,
    /// not the grids. The ship's customisation entry holds its paint and parts.</summary>
    private (string[] Path, Func<UIElement> Field)[] LiveFields(int ship)
    {
        string i = ship.ToString(CultureInfo.InvariantCulture);
        return new (string[], Func<UIElement>)[]
        {
            (new[] { "vLc", "6f=", "@Cs", i, "NKm" }, () => NameEditBox),
            (new[] { "vLc", "6f=", "@Cs", i, "NTx", "93M" }, () => TypeComboBox),
            (NmsInventoryContainer.ShipModelSeedPath(ship), () => SeedBox),
            (NmsInventoryContainer.ShipTechnologyPath(ship).Append("B@N").Append("1o6").ToArray(), () => ClassPicker),
            (NmsInventoryContainer.ShipCargoPath(ship).Append("B@N").Append("1o6").ToArray(), () => ClassPicker),
            (NmsInventoryContainer.ShipStatBonusesPath(ship), () => StatsBoxes),
            (NmsInventoryContainer.ShipTechnologyPath(ship), () => TechGrid),
            (NmsInventoryContainer.ShipCargoPath(ship), () => CargoGrid),
            (new[] { "vLc", "6f=", "l:j", NewFromTemplateService.ShipCustomisationIndex(ship).ToString(CultureInfo.InvariantCulture) }, () => PartsPanel),
        };
    }

    /// <summary>The game saved: the ship on screen takes its values where nothing is edited here, and each changed field
    /// pulses once - no reload (2026-10-22).</summary>
    private void OnGameSaved(JObject disk)
    {
        if (ViewModel.SelectedIndex < 0) return;
        var fields = LiveFields(ViewModel.SelectedIndex);
        var changed = LiveFieldsService.Adopt(disk, fields.Select(f => f.Path),
            fields.Where(f => f.Field() == TechGrid || f.Field() == CargoGrid).Select(f => f.Path).ToList());
        if (changed.Count == 0) return;
        LoadShipList();
        foreach (var field in fields.Where(f => changed.Contains(f.Path)).Select(f => f.Field()).Distinct())
            FieldHighlight.Flash(field);
    }

    /// <summary>Re-reads the owned ships (ShipsPageViewModel.Reload) and, unless a live update left it alone, the ship on
    /// screen; then works out the unnamed ones' game names.</summary>
    private void LoadShipList(bool live = false)
    {
        if (ViewModel.Reload(live)) LoadSelectedShip();
        else if (!SaveSessionManager.IsSaveLoaded) ClearGrids();
        NameShips();
    }

    /// <summary>The game's names for unnamed ships arrive in the background: relabels the strip (and the name box, when
    /// it shows one of them and the player hasn't typed into it).</summary>
    private async void NameShips()
    {
        var before = ViewModel.Selected;
        if (!await ViewModel.WorkOutGameNamesAsync()) return;
        LoadShipList(live: true);
        ViewModel.ShowGameName(before);
    }

    private void ClearGrids()
    {
        TechGrid.ViewModel = null;
        TechGrid.Refresh();
        CargoGrid.ViewModel = null;
        CargoGrid.Refresh();
    }

    private void ShipStrip_ChipClicked(object? sender, EquipmentChipViewModel chip)
    {
        if (chip.IsEmptySlot)
        {
            Frame.Navigate(typeof(ShipDesignerPage));
            return;
        }
        if (ViewModel.Select(chip.Index)) LoadSelectedShip();
    }

    private async void LoadSelectedShip() => await LoadSelectedShipAsync();

    private async Task LoadSelectedShipAsync()
    {
        if (!await ViewModel.LoadSelectedAsync()) return;
        if (ViewModel.Tech is not { } tech || ViewModel.Cargo is not { } cargo)
        {
            ClearGrids();
            TechLayout.ShowList = false;
            SeedBox.Show(null);
            StatsBoxes.Clear();
            return;
        }
        int index = ViewModel.SelectedIndex;
        var techRule = ViewModel.TechRule;

        TechGrid.IconStandIns = ViewModel.ModulePartIcons;
        TechGrid.ViewModel = tech;
        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        TechGrid.AllowedUsageCategories = techRule.UsageCategories;

        CargoGrid.ViewModel = cargo;
        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable", "ProceduralProducts" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        // Reloads run on every strip click: unsubscribe first so one edit fires the handler once.
        TechGrid.CellChanged -= GridCellChanged;
        TechGrid.CellChanged += GridCellChanged;
        CargoGrid.CellChanged -= GridCellChanged;
        CargoGrid.CellChanged += GridCellChanged;
        TechGrid.Refresh();
        CargoGrid.Refresh();

        // The tech this kind of ship can fit, to click in - the same list limits the right-click search.
        TechLayout.ShowList = true;
        TechLayout.ListTitle = techRule.Header;
        // Figurines and trails don't boost each other in game.
        TechListPanel.NoAdjacency = p => StarshipService.TechGroup(p) is "Figurines" or "Starship trails";
        TechListPanel.IsBestClass = () => StarshipService.ReadClass(index) == "S";
        TechListPanel.SwitchToBestClassAsync = async () =>
        {
            ViewModel.ChooseClass("S");
            await LoadSelectedShipAsync();
        };
        TechListPanel.MoreRoomHint = () => StarshipService.ReadClass(index) == "S" ? null
            : "S class has more slots - change the class in Ship Details to fit more.";
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            // Advanced Launch System is a special ship's built-in launcher: it blocks take-off next to a normal Launch
            // Thruster and the game won't let you remove it (user, 2026-10-13); no real ship had it.
            var all = (await CatalogService.GetTechForAsync(techRule.UsageCategories))
                .Where(t => !string.Equals(t.GameId, "LAUNCHER_SPEC", StringComparison.OrdinalIgnoreCase)).ToList();
            TechGrid.AllowedItemIds = all.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modules = await CatalogService.GetUpgradeModulesAsync(StarshipService.ModuleFamiliesFor(techRule.Header));
            return StarshipService.SortTech(all.Concat(modules));
        }, "technology", "technologies", "Technology", StarshipService.TechGroup);

        SeedBox.Show(NmsInventoryContainer.ShipModelSeedPath(index));
        RefreshShipParts();
        UpdateStatDisplay();
    }

    // ------------------------------------------------------------ details band

    private void GridCellChanged(object? sender, EventArgs e) => ViewModel.MarkEdited();

    private void Inventory_Edited(object? sender, EventArgs e) => ViewModel.MarkEdited();

    private void NameUpdateLink_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.HideNameUpdateLink();
        App.MainWindowInstance?.StartGameDataUpdate();
    }

    // The rename is staged shortly after typing stops, so Save shows up without leaving the box (2026-10-17: the player
    // typed a name and found no Save), and straight away on Enter or leaving the box.
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _nameTimer;

    /// <summary>Clicking in selects the whole name, ready to type over. A lambda, not the method group
    /// NameEditBox.SelectAll: handing the TextBox's own projected method to the queue closed the app on click
    /// (2026-10-17).</summary>
    private void NameEditBox_GotFocus(object sender, RoutedEventArgs e) =>
        DispatcherQueue.TryEnqueue(() => NameEditBox.SelectAll());

    private void NameEditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Only the player's typing - the view model filling the box isn't an edit.
        if (NameEditBox.FocusState == FocusState.Unfocused) return;
        _nameTimer ??= CreateNameTimer();
        _nameTimer.Stop();
        _nameTimer.Start();
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer CreateNameTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => CommitName(typing: true);
        return timer;
    }

    private void NameEditBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        _nameTimer?.Stop();
        CommitName(typing: true);
        e.Handled = true;
    }

    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _nameTimer?.Stop();
        CommitName(typing: false);
    }

    /// <summary>Stages the rename. While the box has focus the ship isn't reloaded - that would put the text back and
    /// move the cursor - only the strip (and, in the view model, the tech heading) is relabelled.</summary>
    private void CommitName(bool typing)
    {
        if (!ViewModel.CommitName(typing)) return;
        LoadShipList(live: typing);
    }

    /// <summary>A class can change the ship's Tech/Cargo slot totals (StarshipCapacity), so both grids are rebuilt.</summary>
    private void ClassPicker_ClassChosen(object? sender, string letter)
    {
        ViewModel.ChooseClass(letter);
        LoadSelectedShip();
    }

    /// <summary>The Base Stats boxes: the ship's raw stat rolls (PMT.@bB - SHIP_DAMAGE/SHIELD/HYPERDRIVE/AGILE), which
    /// the Class letter doesn't touch (SetClass only writes B@N.1o6), so they need their own fields.</summary>
    private void UpdateStatDisplay()
    {
        StatsBoxes.Show(NmsInventoryContainer.ShipStatBonusesPath(ViewModel.SelectedIndex), StatBonusService.ShipStats,
            NmsInventoryContainer.ShipTechnologyStatBonusesPath(ViewModel.SelectedIndex));
        ShowInGameTotals();
    }

    /// <summary>The game's totals under the base stats (#17); dropped if another ship was picked meanwhile. The ship's
    /// own values: what the game shows while it is the active ship. For any other ship the visor rates it against the
    /// active one, so it reads "When flown" rather than "In game".</summary>
    private async void ShowInGameTotals()
    {
        int index = ViewModel.SelectedIndex;
        var totals = await OwnedShipDisplayStatsService.EvaluateAsync(index);
        if (index != ViewModel.SelectedIndex) return;
        StatsBoxes.ShowInGameTotals(totals, ViewModel.Selected?.IsGameActive == true ? "In game" : "When flown");
    }

    private void StatsBoxes_Edited(object? sender, EventArgs e)
    {
        ViewModel.MarkEdited();
        ShowInGameTotals();
    }

    /// <summary>Undoes every staged change to the selected ship - its inventories and also its name, seed and stats,
    /// which the grids' own revert didn't cover.</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetSelected();
        LoadShipList();
    }

    private void Seed_Changed(object? sender, SeedChange change)
    {
        RefreshShipParts();
        ViewModel.MarkEdited();
    }

    // ------------------------------------------------------------ copy / delete / add

    /// <summary>Copies the selected ship's whole tech loadout onto another owned ship, after showing what else that needs
    /// (slots to unlock, a class difference with an opt-in to match it). Cargo is deliberately left out.</summary>
    private async void CopyTechStackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is not { } source || ViewModel.Tech is not { } sourceTech) return;
        var others = ViewModel.Ships.Where(s => s.Index != source.Index).ToList();
        if (others.Count == 0)
        {
            await new ContentDialog
            {
                Title = "No other ships",
                Content = "You only own one starship, so there's nothing to copy this stack onto.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }

        var pickerList = new ListView { SelectionMode = ListViewSelectionMode.Single, ItemsSource = others };
        var pickerDialog = new ContentDialog
        {
            Title = $"Copy {source.Name}'s tech stack to...",
            Content = pickerList,
            PrimaryButtonText = "Next",
            IsPrimaryButtonEnabled = false,
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        pickerList.SelectionChanged += (_, _) => pickerDialog.IsPrimaryButtonEnabled = pickerList.SelectedIndex >= 0;
        if (await pickerDialog.ShowAsync() != ContentDialogResult.Primary || pickerList.SelectedItem is not OwnedShip target) return;

        var (targetType, targetClass) = StarshipService.TypeAndClass(target.Index);
        var capacity = await CatalogService.GetShipCapacityAsync();
        var targetTech = new InventoryGridViewModel(NmsInventoryContainer.ShipTechnologyPath(target.Index), StarshipCapacity.Columns,
            StarshipCapacity.SlotsToRows(StarshipService.TechSlots(capacity, targetType, targetClass)));
        targetTech.Load();
        var (confirmed, alsoMatchClass) = await TechStackCopyConfirmation.ShowAsync(XamlRoot, $"a copy of {source.Name}'s",
            StarshipService.ReadClass(source.Index), sourceTech.Cells.Where(c => c.IsOccupied).Select(c => (c.X, c.Y)),
            target.Name, targetTech, targetTech.CurrentClass);
        if (!confirmed) return;
        targetTech.CopyTechStackFrom(sourceTech, alsoMatchClass);
        StarshipService.SyncCargoClass(target.Index);
        ViewModel.MarkEdited();
        LoadShipList();
    }

    /// <summary>Deletes the selected ship from the save (with its corvette build), after a strict confirmation that can
    /// keep it as a template first. The ship being flown can't go - the game needs one.</summary>
    private async void DeleteShipBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is not { } ship || ViewModel.Tech is not { } tech) return;
        if (ship.IsGameActive)
        {
            await DeleteConfirmation.ShowBlockedAsync(XamlRoot, "That's the ship you're flying",
                $"{ship.Name} is your active ship, and the game always needs one. Switch to another ship in game, save, then delete this one here.");
            return;
        }
        var choice = await DeleteConfirmation.ShowAsync(XamlRoot, ship.Name, StarshipService.DeleteContents(ship));
        if (!choice.Confirmed) return;
        if (choice.SaveTemplateFirst)
        {
            await LoadoutTemplateService.SaveAsync(StarshipService.CaptureFullBuild(tech, $"{ship.Name} (saved before deleting)", ship));
            await TemplatesPanel.RefreshAsync();
        }
        NewFromTemplateService.RemoveShip(ship.Index);
        ViewModel.SelectAfterReload();
        LoadShipList();
    }

    /// <summary>Full Build templates add a whole new ship in the first empty slot (NewFromTemplateService) - the ships you
    /// have are never changed. Tech Stack templates Apply onto the selected one.</summary>
    private async Task AddAsNewAsync(NmsLoadoutTemplate template)
    {
        int skeleton = ViewModel.Selected?.Index ?? ViewModel.Ships.FirstOrDefault()?.Index ?? -1;
        if (skeleton < 0 || NewFromTemplateService.FirstEmptyShipSlot() is not int slot)
        {
            await new ContentDialog
            {
                Title = "No room for another Starship",
                Content = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = StarshipService.NoRoomText(hasShip: skeleton >= 0) },
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }
        var (title, body) = StarshipService.AddAsNewText(template, slot);
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
        if (NewFromTemplateService.AddShip(template, skeleton) is not int added) return;
        ViewModel.SelectAfterReload(added);
        LoadShipList();
    }

    /// <summary>Applies a template to the ship on screen. A class change (alsoMatchClass) can change the slot totals
    /// (StarshipCapacity), so the grids are rebuilt.</summary>
    private async Task ApplyTemplateAsync(NmsLoadoutTemplate template)
    {
        if (ViewModel.Selected is not { } target || ViewModel.Tech is not { } tech) return;
        var (confirmed, alsoMatchClass) = await TechStackCopyConfirmation.ShowAsync(XamlRoot, $"the \"{template.Name}\" template",
            template.SourceClass, template.UnlockedPositions.Select(p => (p.X, p.Y)), target.Name, tech, tech.CurrentClass);
        if (!confirmed) return;
        tech.ApplyTemplate(template, alsoMatchClass);
        StarshipService.SyncCargoClass(target.Index);
        ViewModel.MarkEdited();
        LoadSelectedShip();
    }

    string? ILoadoutTemplateHost.CurrentName => ViewModel.Tech is null ? null : ViewModel.Selected?.Name;

    bool ILoadoutTemplateHost.HasTech => ViewModel.Tech?.Cells.Any(c => c.IsOccupied) == true;

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureTechStack(string name) =>
        StarshipService.CaptureTechStack(ViewModel.Tech!, name, ViewModel.Selected!);

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureFullBuild(string name) =>
        StarshipService.CaptureFullBuild(ViewModel.Tech!, name, ViewModel.Selected!);

    Task ILoadoutTemplateHost.ApplyTechStackAsync(NmsLoadoutTemplate template) => ApplyTemplateAsync(template);

    Task ILoadoutTemplateHost.UseFullBuildAsync(NmsLoadoutTemplate template) => AddAsNewAsync(template);

    // ------------------------------------------------------------ parts & 3D preview

    private int _previewVersion;

    /// <summary>Rebuilds the parts list and preview for the selected ship's type and the seed in the box.</summary>
    private async void RefreshShipParts()
    {
        PartsPanel.ViewModel.Clear();
        // Hide the last ship's model (and drop a build still running for it), or a ship that shows no parts kept the
        // previous ship's model (2026-10-15).
        int refresh = ++_previewVersion;
        int index = ViewModel.SelectedIndex;
        ShipPreviewGroup.Visibility = Visibility.Collapsed;
        string? scene = StarshipService.Scene(index);
        PaintShipBtn.Visibility = scene is not null && ShipPartsService.IsPaintable(scene) ? Visibility.Visible : Visibility.Collapsed;
        if (scene is null) return;

        bool hasData = await ShipPartsService.EnsureLoadedAsync();
        // A newer refresh started while this one waited (switching ships fires two): let that one fill the panel, or
        // the parts list showed twice (2026-10-17).
        if (refresh != _previewVersion) return;
        if (!hasData)
        {
            PartsPanel.ViewModel.ShowMessage("Update your game data (Settings › Game data) to see which parts a seed gives.");
            return;
        }

        var root = ShipPartsService.LoadTree(ShipParts.DescriptorFor(scene));
        bool hasSeed = ShipPartsService.TryParseSeed(SeedBox.Text, out ulong seed);
        var built = ShipPartsService.BuiltLookOf(index);
        // Corvettes are built from parts placed in game, stored apart from the seed (#73).
        if (ViewModel.Selected?.IsCorvette == true) ShowCorvettePreview();
        else if (root is null || hasSeed)
        {
            await ShipPartsService.EnsureLookAsync();
            if (refresh != _previewVersion) return;
            ShowPreview(scene, hasSeed ? seed : 0, root, built);
        }
        PartsPanel.ViewModel.Show(scene, root, hasSeed ? seed : null, built);
    }

    /// <summary>A found seed's Use: stages it; Seed_Changed refreshes the parts.</summary>
    private void PartsPanel_SeedChosen(object? sender, string seed)
    {
        if (ViewModel.SelectedIndex >= 0) SeedBox.Set(seed);
    }

    /// <summary>The 3D preview: the seed's parts built from the player's own game files (ShipModels). A ship built at a
    /// station (or repainted) shows its saved paint; found and bought ships show as a hologram scan.</summary>
    private async void ShowPreview(string scene, ulong seed, IReadOnlyList<PartGroup>? root, BuiltLook? built = null)
    {
        int version = ++_previewVersion;
        if (GameDataService.Game is null) { ShipPreviewGroup.Visibility = Visibility.Collapsed; return; }
        // Cleared first: the previous ship stayed on screen while this one built (2026-10-17).
        ShipPreviewView.ShowMessage("Building this ship's model from your game files...");
        ShipPreviewNote.Text = "";
        ShipPreviewGroup.Visibility = Visibility.Visible;

        var preview = await ShipPartsService.BuildPreviewAsync(scene, seed, root, built, () => version != _previewVersion);
        if (preview is null || version != _previewVersion) return;
        ShowBuiltPreview(preview, "This ship's model couldn't be read from your game files.");
    }

    /// <summary>The 3D preview of a corvette as built: its parts put together from the player's game files.</summary>
    private async void ShowCorvettePreview()
    {
        int version = ++_previewVersion;
        if (GameDataService.Game is null) { ShipPreviewGroup.Visibility = Visibility.Collapsed; return; }
        ShipPreviewView.ShowMessage("Putting your corvette together from your game files...");
        ShipPreviewNote.Text = "";
        ShipPreviewGroup.Visibility = Visibility.Visible;

        var preview = await ShipPartsService.BuildCorvettePreviewAsync(ViewModel.SelectedIndex, () => version != _previewVersion);
        if (preview is null || version != _previewVersion) return;
        ShowBuiltPreview(preview, "This corvette's build couldn't be read from your game files.");
    }

    private void ShowBuiltPreview(ShipPartsService.ShipPreviewBuild preview, string unreadable)
    {
        if (preview.Mesh is null) ShipPreviewView.ShowMessage(unreadable);
        else if (preview.Hologram) ShipPreviewView.Show(preview.Mesh, preview.MaterialColour, hologram: true);
        else ShipPreviewView.Show(preview.Mesh, preview.MaterialColour, preview.Painted);
        ShipPreviewNote.Text = preview.Note;
    }

    private void PaintShipBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIndex >= 0) Frame.Navigate(typeof(ShipDesignerPage), ViewModel.SelectedIndex);
    }
}
