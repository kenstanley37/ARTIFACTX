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

}
