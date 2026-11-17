using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class HomePage : ContentPage
{
    public HomeViewModel ViewModel { get; } = new();

    public HomePage()
    {
        InitializeComponent();
        BindingContext = ViewModel;
    }
}
