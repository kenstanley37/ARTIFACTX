using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

/// <summary>Planet Finder (and the glyph decoder) as its own nav page: the Planets page in
/// finder mode (PlanetsPage.FinderMode), split out 2026-10-17.</summary>
public sealed class PlanetFinderPage : Page
{
    public PlanetFinderPage() => Content = new PlanetsPage(finderMode: true);
}
