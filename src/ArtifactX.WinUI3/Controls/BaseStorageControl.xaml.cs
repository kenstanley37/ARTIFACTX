using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class BaseStorageControl : UserControl
{
    public BaseStorageViewModel ViewModel { get; } = new();
    private bool _refreshingContainers;

    public BaseStorageControl()
    {
        InitializeComponent();
        StorageGrid.AllowedCategories = ["Substance", "Product"];
        StorageGrid.AllowedTemplateTypes = ["GcProductTable", "GcSubstanceTable", "ProceduralProducts"];
        StorageGrid.SupportsSupercharge = false;
        StorageGrid.SupportsRepair = false;
        StorageGrid.ProductStorageMultiplier = 10;
        StorageGrid.CellChanged += (_, _) => UpdateReset();
        _ = new CargoStorageFeed(this,
            () => ViewModel.Containers.Select(c => ArtifactX.Core.NmsModels.NmsInventoryContainer.BaseStorageContainerPath(c.Key)), Refresh);
    }

    private void Refresh()
    {
        _refreshingContainers = true;
        ViewModel.RefreshContainers();
        _refreshingContainers = false;
        RefreshSelected();
    }

    private async void RefreshSelected()
    {
        bool changed = await ViewModel.LoadSelectedAsync();
        StorageGrid.ViewModel = ViewModel.Editor?.Inventory;
        if (changed) StorageGrid.Refresh();
        GridHeaderTxt.Text = ViewModel.SelectedContainer is { } selected ? $"CONTENTS - {selected.Name.ToUpperInvariant()}" : "CONTENTS";
        UpdateReset();
    }

    private void UpdateReset() => PageResetBtn.Visibility = ViewModel.HasChanges ? Visibility.Visible : Visibility.Collapsed;

    private void ContainerSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_refreshingContainers) RefreshSelected();
    }

    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e) => ViewModel.Rename();

    private void UnlockAllBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Editor?.Inventory.UnlockAll();
        StorageGrid.Refresh();
        UpdateReset();
    }

    private void MaxAllQtyBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Editor?.Inventory.MaxAllQuantities();
        StorageGrid.Refresh();
        UpdateReset();
    }

    private void SortBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Editor?.Inventory.SortItems() == true) StorageGrid.Refresh();
        UpdateReset();
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Revert();
        Refresh();
    }
}
