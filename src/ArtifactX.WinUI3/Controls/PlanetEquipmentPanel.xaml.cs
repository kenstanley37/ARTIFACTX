using ArtifactX.WinUI3.ViewModels;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class PlanetEquipmentPanel : UserControl
{
    public static readonly DependencyProperty EquipmentProperty =
        DependencyProperty.Register(nameof(Equipment), typeof(PlanetEquipmentViewModel), typeof(PlanetEquipmentPanel), new PropertyMetadata(null));

    public PlanetEquipmentViewModel? Equipment
    {
        get => (PlanetEquipmentViewModel?)GetValue(EquipmentProperty);
        set => SetValue(EquipmentProperty, value);
    }

    public PlanetEquipmentPanel() => InitializeComponent();

    private void ToggleShips_Click(object sender, RoutedEventArgs e) => Equipment?.ToggleShips();

    private void ShipFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            Equipment?.SelectShipFilter(key);
        }
    }

    private void CopyTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SystemMultiToolItemViewModel item })
        {
            item.CopySeed();
        }
    }

    private void CopyShip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SystemStarshipItemViewModel item })
        {
            item.CopySeed();
        }
    }
}

