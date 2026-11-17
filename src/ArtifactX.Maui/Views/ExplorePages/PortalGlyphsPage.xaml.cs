using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class PortalGlyphsPage : ContentPage
{
    public PortalGlyphsPage(PortalScannerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
