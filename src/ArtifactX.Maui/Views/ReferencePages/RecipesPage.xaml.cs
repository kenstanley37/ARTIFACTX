using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ReferencePages;

public partial class RecipesPage : ContentPage
{
    public RecipesViewModel ViewModel { get; } = new();

    public RecipesPage()
    {
        InitializeComponent();
        BindingContext = ViewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }
}
