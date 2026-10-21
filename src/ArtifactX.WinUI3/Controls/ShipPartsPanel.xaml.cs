using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class ShipPartsPanel : UserControl
{
    public ShipPartsPanel() => InitializeComponent();

    public ShipPartsViewModel ViewModel { get; } = new();

    private void ToggleSearch_Click(object sender, RoutedEventArgs e) => ViewModel.SearchOpen = !ViewModel.SearchOpen;

    private async void Search_Click(object sender, RoutedEventArgs e) => await ViewModel.SearchAsync();

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string seed) ViewModel.Use(seed);
    }
}
