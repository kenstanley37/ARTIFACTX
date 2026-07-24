using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NMS.Core.NmsModels;
using NMS.WinUI3.Services;
using NMS.WinUI3.ViewModels;
using System;

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

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        LoadGrids();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) => LoadGrids();

    private void LoadGrids()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        _techViewModel.Load();
        TechGrid.Refresh();

        _cargoViewModel.Load();
        CargoGrid.Refresh();
    }

    private void UnlockAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel.UnlockAll();
        TechGrid.Refresh();
    }

    private void UnlockAllCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel.UnlockAll();
        CargoGrid.Refresh();
    }
}