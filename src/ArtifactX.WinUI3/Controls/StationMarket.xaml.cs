using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class StationMarket : UserControl
{
    public static readonly DependencyProperty MarketProperty =
        DependencyProperty.Register(nameof(Market), typeof(StationMarketViewModel), typeof(StationMarket), new PropertyMetadata(null));

    public StationMarketViewModel? Market
    {
        get => (StationMarketViewModel?)GetValue(MarketProperty);
        set => SetValue(MarketProperty, value);
    }

    public StationMarket() => InitializeComponent();

    private void Toggle_Click(object sender, RoutedEventArgs e) => Market?.Toggle();
}
