using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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

    public event EventHandler? CrashedShipsRequested;
    public event EventHandler? CabinetMultiToolsRequested;

    public PlanetEquipmentPanel() => InitializeComponent();

    private void OpenCrashedShips_Click(object sender, RoutedEventArgs e) => CrashedShipsRequested?.Invoke(this, EventArgs.Empty);
    private void OpenCabinetMultiTools_Click(object sender, RoutedEventArgs e) => CabinetMultiToolsRequested?.Invoke(this, EventArgs.Empty);
}
