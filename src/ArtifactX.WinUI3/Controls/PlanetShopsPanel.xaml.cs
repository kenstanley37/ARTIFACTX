using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

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

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if ((sender as FrameworkElement)?.DataContext is ShopSiteRowViewModel shop)
        {
            Shops?.ToggleSelection(shop);
        }
    }

    private static bool IsInsideButton(DependencyObject? obj)
    {
        while (obj is not null)
        {
            if (obj is Button) return true;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return false;
    }
}
