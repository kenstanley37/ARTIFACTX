using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
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
    private const double TechCollapsedHeight = 232;
    private const double CargoCollapsedHeight = 368;

    private static readonly string ChevronDownGlyph = char.ConvertFromUtf32(0xE70D);
    private static readonly string ChevronUpGlyph = char.ConvertFromUtf32(0xE70E);

    private ExocraftType _type;
    private InventoryGridViewModel? _techViewModel;
    private InventoryGridViewModel? _cargoViewModel;

    public ExocraftDetailControl()
    {
        InitializeComponent();
    }

    public void Initialize(ExocraftType type)
    {
        _type = type;
        TitleTxt.Text = ExocraftCapacity.DisplayName(type);

        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        TechGrid.AllowedUsageCategories = ExocraftCapacity.UsageCategories(type);

        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        TechGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;
        CargoGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Control_Unloaded;

        LoadGrids();
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

        PageResetBtn.Visibility = (_techViewModel.HasLocalChanges || _cargoViewModel.HasLocalChanges)
            ? Visibility.Visible : Visibility.Collapsed;

        ModelSeedEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.ExocraftModelSeedPath(_type))?.Value<string>() ?? "";
    }

    private void ModelSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = ModelSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsInventoryContainer.ExocraftModelSeedPath(_type))?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.ExocraftModelSeedPath(_type));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenerateModelSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        ModelSeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.ExocraftModelSeedPath(_type));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void UnlockAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.UnlockAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void UnlockAllCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel?.UnlockAll();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MaxAllQtyCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel?.MaxAllQuantities();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void SuperchargeAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.SuperchargeAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void RepairAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.RepairAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void TechExpandBtn_Click(object sender, RoutedEventArgs e)
    {
        bool isCollapsed = TechScrollViewer.MaxHeight < double.PositiveInfinity;
        TechScrollViewer.MaxHeight = isCollapsed ? double.PositiveInfinity : TechCollapsedHeight;
        TechExpandIcon.Glyph = isCollapsed ? ChevronUpGlyph : ChevronDownGlyph;
        ToolTipService.SetToolTip(TechExpandBtn, isCollapsed ? "Collapse" : "Expand");
    }

    private void CargoExpandBtn_Click(object sender, RoutedEventArgs e)
    {
        bool isCollapsed = CargoScrollViewer.MaxHeight < double.PositiveInfinity;
        CargoScrollViewer.MaxHeight = isCollapsed ? double.PositiveInfinity : CargoCollapsedHeight;
        CargoExpandIcon.Glyph = isCollapsed ? ChevronUpGlyph : ChevronDownGlyph;
        ToolTipService.SetToolTip(CargoExpandBtn, isCollapsed ? "Collapse" : "Expand");
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.Revert();
        TechGrid.Refresh();
        _cargoViewModel?.Revert();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
