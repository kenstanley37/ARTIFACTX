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

    public static readonly DependencyProperty ShowListsProperty =
        DependencyProperty.Register(nameof(ShowLists), typeof(bool), typeof(PlanetFishCard), new PropertyMetadata(true));

    public static readonly DependencyProperty ContentPaddingProperty =
        DependencyProperty.Register(nameof(ContentPadding), typeof(Thickness), typeof(PlanetFishCard), new PropertyMetadata(new Thickness(18, 14, 18, 16)));

    /// <summary>Whether to list the fish still to catch and caught here (the planet's page),
    /// or only the counts (a planet's card on Current System, to keep it short).</summary>
    public bool ShowLists
    {
        get => (bool)GetValue(ShowListsProperty);
        set => SetValue(ShowListsProperty, value);
    }

    public Thickness ContentPadding
    {
        get => (Thickness)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public PlanetFishCard() => InitializeComponent();
}
