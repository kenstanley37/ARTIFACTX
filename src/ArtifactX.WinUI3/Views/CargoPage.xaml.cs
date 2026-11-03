using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

/// <summary>One navigation entry for storage. Each tab is instantiated only on
/// first selection and retained until navigation leaves Cargo.</summary>
public sealed partial class CargoPage : Page
{
    public CargoViewModel ViewModel { get; } = new();

    public CargoPage()
    {
        InitializeComponent();
        StorageTabs.SelectedItem = StorageTabs.MenuItems[0];
    }

    private void StorageTabs_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tab }) return;
        ViewModel.SelectedTab = tab;
        switch (tab)
        {
            case "BaseStorage": BaseStorageHost.Content ??= new BaseStorageControl(); break;
            case "IngredientStorage": IngredientStorageHost.Content ??= new IngredientStorageControl(); break;
            case "ColdStorage": ColdStorageHost.Content ??= new ColdStorageControl(); break;
            case "CorvetteCache": CorvetteCacheHost.Content ??= new CorvetteCacheControl(); break;
            case "BaseItemCache": BaseItemCacheHost.Content ??= new BaseItemCacheControl(); break;
        }
    }
}
