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
    }
}
