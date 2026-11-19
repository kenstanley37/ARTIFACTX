using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class LargeGlyphsPage : ContentPage
{
    public LargeGlyphsPage(LargeGlyphsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void Close_Clicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    protected override bool OnBackButtonPressed()
    {
        _ = Navigation.PopModalAsync();
        return true;
    }
}
