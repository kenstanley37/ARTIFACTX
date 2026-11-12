using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ArtifactX.WinUI3.Models;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class BaseStorageControl : UserControl
{
    public BaseStorageViewModel ViewModel { get; } = new();

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
        ViewModel.RefreshContainers();
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

    private void ContainerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: StorageContainerEntry container } button) return;
        // Selecting the active container must not toggle the selection off.
        button.IsChecked = true;
        if (ViewModel.SelectedContainer?.Key == container.Key) return;
        ViewModel.SelectedContainer = container;
        RefreshSelected();
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
        StorageGrid.MaxAllQuantities();
        StorageGrid.Refresh();
        UpdateReset();
    }

    private void SortBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Editor?.Inventory.SortItems() == true) StorageGrid.Refresh();
        UpdateReset();
    }

    private void MergeDuplicatesBtn_Click(object sender, RoutedEventArgs e)
    {
        bool changed = ViewModel.Editor is { } editor && editor.Inventory.TryMergeDuplicates(out _);
        MergeResultTxt.Text = changed ? "Merged compatible stacks." : "No stacks to merge.";
        if (!changed) return;
        StorageGrid.Refresh();
        UpdateReset();
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Revert();
        Refresh();
    }
}
