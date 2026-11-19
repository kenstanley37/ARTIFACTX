using ArtifactX.Maui.Views.ExplorePages;
using Microsoft.Maui.Controls;

namespace ArtifactX.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("live-portal-scan", typeof(LivePortalScannerPage));
        Routing.RegisterRoute("system-details", typeof(SystemDetailsPage));
        Routing.RegisterRoute("recipe-details", typeof(Views.ReferencePages.RecipeDetailsPage));
        Routing.RegisterRoute("item-values", typeof(Views.ReferencePages.ItemValuesPage));
    }
}
