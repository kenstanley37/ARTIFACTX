using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

public sealed partial class PlanetFinderPage : Page
{
    public PlanetFinderPage()
    {
        InitializeComponent();
        Host.Content = new PlanetsPage(finderMode: true);
    }
}
