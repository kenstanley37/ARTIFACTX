using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ReferencePages;

public partial class ItemValuesPage : ContentPage
{
    public ItemValuesViewModel ViewModel { get; } = new();

    public ItemValuesPage()
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
