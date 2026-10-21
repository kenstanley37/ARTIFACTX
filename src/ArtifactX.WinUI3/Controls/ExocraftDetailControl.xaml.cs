using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// Shared UI/logic for one of the 7 fixed exocraft slots (Roamer/Nomad/
/// Colossus/Pilgrim/Dragonfly/Nautilon/Minotaur) - same "thin Page hosts one
/// shared control" split as GekLanguagePage/LanguageWordsControl, since all 7
/// pages are identical except which ExocraftType they target. No Templates
/// system here yet (unlike Ships/Multi-Tool/Freighter) - deliberately kept
/// out of this first pass to keep it shippable; LoadoutTemplateService/
/// NmsLoadoutTemplate are already fully generic (SourceKind is just a
/// string) so adding "Exocraft" support later needs no model changes.
/// </summary>
public sealed partial class ExocraftDetailControl : UserControl
{
    private ExocraftType _type;
    private InventoryGridViewModel? _techViewModel;
    private InventoryGridViewModel? _cargoViewModel;

    public ExocraftDetailControl()
    {
        InitializeComponent();
        TechCard.GroupOf = TechListPanel.TypeOf;
    }

    public void Initialize(ExocraftType type)
    {
        _type = type;
        TitleTxt.Text = ExocraftCapacity.DisplayName(type);

        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        TechGrid.AllowedUsageCategories = ExocraftCapacity.UsageCategories(type);
        // An exocraft's slots are fixed by the game (see SupportsUnlock).
        TechGrid.SupportsUnlock = false;
        CargoGrid.SupportsUnlock = false;

        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable", "ProceduralProducts" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        TechGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;
        CargoGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Control_Unloaded;

        LoadGrids();

        // The tech this vehicle can fit, to click in - the same list limits
        // the right-click search.
        TechLayout.ListTitle = $"{ExocraftCapacity.DisplayName(type).ToUpperInvariant()} TECHNOLOGY";
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            var tech = await CatalogService.GetTechForAsync(ExocraftCapacity.UsageCategories(type));
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // The game names UP_EXSUB "Nautilon Cannon" too, but it's the
            // Humboldt Drive's: template T_SUB, the engine stat (2026-10-13).
            var modules = (await CatalogService.GetUpgradeModulesAsync(ModuleFamiliesFor(type)))
                .Select(m => m.CountKey == "UP_EXSUB" ? m with { Name = "Humboldt Drive upgrade (S)" } : m);
            return ItemListPanel.SortByGroups(tech.Concat(modules), TechGroup, TechGroupOrder);
        }, "technology", "technologies", "Technology", TechGroup);
    }

    /// <summary>Upgrade module families per vehicle, from every exocraft in
    /// the real saves (2026-10-13), each topping out at 3: the wheeled ones
    /// and Dragonfly share the exocraft modules, the Nautilon has two cannon
    /// families (both seen at 3 together), the Minotaur its own four.</summary>
    private static string[] ModuleFamiliesFor(ExocraftType type) => type switch
    {
        ExocraftType.Nautilon => new[] { "UP_SUGUN", "UP_EXSUB" },
        ExocraftType.Minotaur => new[] { "UP_MCENG", "UP_MFIRE", "UP_MCGUN", "UP_MCLAS" },
        _ => new[] { "UP_EXENG", "UP_BOOST", "UP_EXLAS", "UP_EXGUN" },
    };

    private static readonly string[] TechGroupOrder =
        { "Engine & handling", "Mining", "Weapons", "Scanning", "Hazard protection", "Minotaur parts", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats), mapped to what the tech does;
    /// the Minotaur's swappable body parts get their own group. Stat names
    /// read correctly since libMBIN 7.07 (2026-10-15) - before, the COSMOS
    /// stat list was shifted by one (engines read as Vehicle_Scan, scanners as
    /// Vehicle_EngineFuelUse), so both spellings are kept for catalogs built
    /// before the update.</summary>
    private static string TechGroup(CatalogPart tech)
    {
        if (tech.Name.StartsWith("HARDFRAME", StringComparison.OrdinalIgnoreCase)
            || tech.Name.StartsWith("LIQUIDATOR", StringComparison.OrdinalIgnoreCase)) return "Minotaur parts";
        bool oldCatalog = GameDataService.Info?.Format < 4;
        return (tech.Stat ?? "") switch
        {
            "Vehicle_Scan" when oldCatalog => "Engine & handling",
            "Vehicle_EngineFuelUse" when oldCatalog => "Scanning",
            "Vehicle_Engine" or "Vehicle_Boost" or "Vehicle_AutoPilot" => "Engine & handling",
            "Vehicle_Laser" or "Vehicle_LaserDamage" or "Vehicle_TerrainEdit" or "Vehicle_FuelRegen" or "Vehicle_Plough" or "Vehicle_Refiner" => "Mining",
            "Vehicle_Gun" or "Vehicle_GunDamage" or "Vehicle_Flame" or "Vehicle_FlameDamage" or "Vehicle_StunGun" => "Weapons",
            "Vehicle_Scan" or "Vehicle_EngineFuelUse" or "Weapon_Scan_Binoculars" => "Scanning",
            var s when s.StartsWith("Suit_Protection") => "Hazard protection",
            _ => "Utilities",
        };
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadGrids);

    /// <summary>Same leak fix as every other device page in this app (see
    /// project_static_event_leak_fix) - Frame.Navigate creates a fresh Page
    /// (and therefore a fresh instance of this control) on every visit, so
    /// the subscriptions above must be released here or every past visit
    /// keeps reloading on every future edit anywhere in the app.</summary>
    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private async void LoadGrids()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        _techViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.ExocraftTechnologyPath(_type), ExocraftCapacity.Columns, ExocraftCapacity.TechRows(_type));
        _cargoViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.ExocraftCargoPath(_type), ExocraftCapacity.Columns, ExocraftCapacity.CargoRows(_type));

        _techViewModel.Load();
        _cargoViewModel.Load();

        var itemIds = _techViewModel.Cells.Concat(_cargoViewModel.Cells)
            .Where(c => c.IsOccupied)
            .Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        TechGrid.ViewModel = _techViewModel;
        CargoGrid.ViewModel = _cargoViewModel;
        TechGrid.Refresh();
        CargoGrid.Refresh();
        TechListPanel.RefreshOwned();

        PageResetBtn.Visibility = _techViewModel.HasLocalChanges || _cargoViewModel.HasLocalChanges
            || SaveSessionManager.HasStagedEditsUnder(NmsInventoryContainer.ExocraftModelSeedPath(_type))
            ? Visibility.Visible : Visibility.Collapsed;

        ModelSeedBox.Show(NmsInventoryContainer.ExocraftModelSeedPath(_type));
    }

    /// <summary>Undoes this vehicle's tech, cargo and model seed changes.</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.Revert();
        TechGrid.Refresh();
        _cargoViewModel?.Revert();
        CargoGrid.Refresh();
        SaveSessionManager.RevertEditsUnder(NmsInventoryContainer.ExocraftModelSeedPath(_type));
        ModelSeedBox.Show(NmsInventoryContainer.ExocraftModelSeedPath(_type));
        PageResetBtn.Visibility = Visibility.Collapsed;
    }

    private void Inventory_Edited(object? sender, EventArgs e) => PageResetBtn.Visibility = Visibility.Visible;

    private void Seed_Changed(object? sender, SeedChange change)
    {
        PageResetBtn.Visibility = Visibility.Visible;
    }
}
