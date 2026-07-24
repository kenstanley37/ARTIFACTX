using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NMS.Core.NmsModels;
using NMS.WinUI3.Services;
using NMS.WinUI3.ViewModels;
using System;
using System.Linq;

namespace NMS.WinUI3.Views;

public sealed partial class ExosuitPage : Page
{
    private readonly InventoryGridViewModel _techViewModel =
        new(NmsInventoryContainer.ExosuitTechnologyPath, ExosuitCapacity.TechnologyColumns, ExosuitCapacity.TechnologyRows);

    private readonly InventoryGridViewModel _cargoViewModel =
        new(NmsInventoryContainer.ExosuitCargoPath, ExosuitCapacity.CargoColumns, ExosuitCapacity.CargoRows);

    public ExosuitPage()
    {
        InitializeComponent();
        TechGrid.ViewModel = _techViewModel;
        CargoGrid.ViewModel = _cargoViewModel;

        TechGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;
        CargoGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        LoadGrids();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadGrids);

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

        PageResetBtn.Visibility = (_techViewModel.HasLocalChanges || _cargoViewModel.HasLocalChanges)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UnlockAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel.UnlockAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void UnlockAllCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel.UnlockAll();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel.Revert();
        TechGrid.Refresh();
        _cargoViewModel.Revert();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}