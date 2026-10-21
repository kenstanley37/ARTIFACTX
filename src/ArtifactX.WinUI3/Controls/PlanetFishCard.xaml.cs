using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class PlanetFishCard : UserControl
{
    public static readonly DependencyProperty FishProperty =
        DependencyProperty.Register(nameof(Fish), typeof(PlanetFishViewModel), typeof(PlanetFishCard), new PropertyMetadata(null));

    public PlanetFishViewModel? Fish
    {
        get => (PlanetFishViewModel?)GetValue(FishProperty);
        set => SetValue(FishProperty, value);
    }

    public PlanetFishCard() => InitializeComponent();
}
