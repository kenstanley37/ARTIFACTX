using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// The Regions page's map (<see cref="RegionStarMap"/>) beside its colour key and the picked star's name,
/// distance and glyphs, all bound to <see cref="RegionsViewModel"/>. The page shows it in the REGION MAP card
/// and again filling the page when expanded (without the Expand button).
/// </summary>
public sealed partial class RegionMapView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(RegionsViewModel), typeof(RegionMapView), new PropertyMetadata(null));

    public RegionsViewModel? ViewModel
    {
        get => (RegionsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>False in the expanded view, which has its own Close button.</summary>
    public Visibility ShowExpandButton { get; set; } = Visibility.Visible;

    public RegionMapView() => InitializeComponent();

    private void OnStarSelected(object? sender, RegionMapStar star) => ViewModel?.SelectMapStar(star);
}
