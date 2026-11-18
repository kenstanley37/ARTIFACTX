using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class PortalGlyphsPage : ContentPage
{
    public PortalGlyphsPage(PortalScannerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // The galaxy bar on this page changes the same galaxy as the Galaxy box; listen only while on screen.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Services.GalaxyService.Changed += FollowGalaxy;
        FollowGalaxy();
    }

    protected override void OnDisappearing()
    {
        Services.GalaxyService.Changed -= FollowGalaxy;
        base.OnDisappearing();
    }

    private void FollowGalaxy() => ((PortalScannerViewModel)BindingContext).FollowGalaxy();
}
