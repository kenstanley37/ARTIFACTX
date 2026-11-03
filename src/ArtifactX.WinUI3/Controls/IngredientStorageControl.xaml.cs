using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.WinUI3.Services;

namespace ArtifactX.WinUI3.Controls;

/// <summary>The cooking Ingredient Storage (Portable Grill / Nutrient Processor), 50 slots (2026-10-15) - see FoodStoreControl.</summary>
public sealed partial class IngredientStorageControl : UserControl
{
    public IngredientStorageControl()
    {
        InitializeComponent();
        Store.Init(coldStorage: false);
        Store.Changed += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        _ = new CargoStorageFeed(this, () => Store.InventoryPaths, Refresh);
    }

    private void Refresh()
    {
        Store.Reload();
        PageResetBtn.Visibility = Store.HasLocalChanges ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        Store.Revert();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
