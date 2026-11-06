using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class PlanetShopsPanel : UserControl
{
    public static readonly DependencyProperty ShopsProperty =
        DependencyProperty.Register(nameof(Shops), typeof(PlanetShopsViewModel), typeof(PlanetShopsPanel), new PropertyMetadata(null));

    public PlanetShopsViewModel? Shops
    {
        get => (PlanetShopsViewModel?)GetValue(ShopsProperty);
        set => SetValue(ShopsProperty, value);
    }

    public PlanetShopsPanel() => InitializeComponent();
}
