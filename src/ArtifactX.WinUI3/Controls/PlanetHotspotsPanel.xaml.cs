using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// UserControl hosting the Planetary Hotspots locator panel.
/// </summary>
public sealed partial class PlanetHotspotsPanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(PlanetHotspotsViewModel),
            typeof(PlanetHotspotsPanel),
            new PropertyMetadata(null));

    public PlanetHotspotsViewModel? ViewModel
    {
        get => (PlanetHotspotsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public PlanetHotspotsPanel()
    {
        InitializeComponent();
    }
}

