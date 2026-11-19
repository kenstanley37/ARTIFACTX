using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class FinderPage : ContentPage
{
    public FinderViewModel ViewModel { get; } = new();

    public FinderPage()
    {
        InitializeComponent();
        BindingContext = ViewModel;
    }

    // Recent systems change on the Systems tab; the start list follows each time the page shows.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ViewModel.Refresh();
    }
}
