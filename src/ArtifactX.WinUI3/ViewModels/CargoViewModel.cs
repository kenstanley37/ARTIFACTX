using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>Selection only; storage controls are created by the page on first use.</summary>
public sealed partial class CargoViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string SelectedTab { get; set; } = "BaseStorage";

    public Visibility BaseStorageVisibility => Visible("BaseStorage");
    public Visibility IngredientStorageVisibility => Visible("IngredientStorage");
    public Visibility ColdStorageVisibility => Visible("ColdStorage");
    public Visibility CorvetteCacheVisibility => Visible("CorvetteCache");
    public Visibility BaseItemCacheVisibility => Visible("BaseItemCache");

    private Visibility Visible(string tab) => SelectedTab == tab ? Visibility.Visible : Visibility.Collapsed;

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(BaseStorageVisibility));
        OnPropertyChanged(nameof(IngredientStorageVisibility));
        OnPropertyChanged(nameof(ColdStorageVisibility));
        OnPropertyChanged(nameof(CorvetteCacheVisibility));
        OnPropertyChanged(nameof(BaseItemCacheVisibility));
    }
}
