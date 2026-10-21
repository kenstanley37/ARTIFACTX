using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Views;

public sealed partial class ExosuitPage : Page
{
    // Matches the ScrollViewers' own XAML-declared MaxHeight (~3 rows for
    // Technology, ~5 for Cargo) - restored when a section is collapsed again.


    private readonly InventoryGridViewModel _techViewModel =
        new(NmsInventoryContainer.ExosuitTechnologyPath, ExosuitCapacity.TechnologyColumns, ExosuitCapacity.TechnologyRows);

    private readonly InventoryGridViewModel _cargoViewModel =
        new(NmsInventoryContainer.ExosuitCargoPath, ExosuitCapacity.CargoColumns, ExosuitCapacity.CargoRows);

    public ExosuitPage()
    {
        InitializeComponent();
        TechCard.GroupOf = TechListPanel.TypeOf;
        TechGrid.ViewModel = _techViewModel;
        CargoGrid.ViewModel = _cargoViewModel;

        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        // Every tech on the 24 exosuits in the real saves is "Suit" (2026-10-13).
        TechGrid.AllowedUsageCategories = new[] { "Suit" };

        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable", "ProceduralProducts" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        TechGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;
        CargoGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;
        LoadGrids();
        LoadTechList();
    }

    /// <summary>Exosuit module families, from every suit in the real saves
    /// (2026-10-13): each tops out at 3 per suit. UP_HAZ only exists as
    /// class 0/X (no shop module) and UP_SNSUIT/UP_RBSUIT are one-off
    /// salvaged/anomalous finds, so they aren't offered.</summary>
    private static readonly string[] ModuleFamilies =
        { "UP_ENGY", "UP_JET", "UP_SHLD", "UP_HOT", "UP_COLD", "UP_TOX", "UP_RAD", "UP_UNW" };

    private static readonly string[] TechGroupOrder =
        { "Life support", "Hazard protection", "Movement", "Shields", "Translators", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats), same as Starships.</summary>
    private static string TechGroup(CatalogPart tech) => (tech.Stat ?? "") switch
    {
        "Suit_Energy" => "Life support",
        "Suit_Protection" => "Hazard protection",
        "Suit_Jetpack" or "Suit_Underwater" => "Movement",
        "Suit_Armour_Shield" => "Shields",
        "Suit_AutoTranslator" => "Translators",
        _ => "Utilities",
    };

    private void LoadTechList()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        // In game only one personal refiner fits at a time (user, 2026-10-13).
        TechListPanel.OneOf = new[] { new[] { "SUIT_REFINER", "SUIT_REFINER2" } };
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            var tech = await CatalogService.GetTechForAsync(new[] { "Suit" });
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modules = await CatalogService.GetUpgradeModulesAsync(ModuleFamilies);
            return ItemListPanel.SortByGroups(tech.Concat(modules), TechGroup, TechGroupOrder);
        }, "technology", "technologies", "Technology", TechGroup);
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadGrids);

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
    }

    private async void LoadGrids()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        _techViewModel.Load();
        _cargoViewModel.Load();

        var itemIds = _techViewModel.Cells.Concat(_cargoViewModel.Cells)
            .Where(c => c.IsOccupied)
            .Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        TechGrid.Refresh();
        CargoGrid.Refresh();
        TechListPanel.RefreshOwned();

        PageResetBtn.Visibility = (_techViewModel.HasLocalChanges || _cargoViewModel.HasLocalChanges)
            ? Visibility.Visible : Visibility.Collapsed;
    }




    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel.Revert();
        TechGrid.Refresh();
        _cargoViewModel.Revert();
        CargoGrid.Refresh();
        TechListPanel.RefreshOwned();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }

    private void Inventory_Edited(object? sender, EventArgs e) => PageResetBtn.Visibility = Visibility.Visible;
}
