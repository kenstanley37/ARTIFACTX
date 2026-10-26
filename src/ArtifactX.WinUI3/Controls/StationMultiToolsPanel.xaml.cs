using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class StationMultiToolsPanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(StationMultiToolsViewModel), typeof(StationMultiToolsPanel), new PropertyMetadata(null));

    public StationMultiToolsViewModel? ViewModel
    {
        get => (StationMultiToolsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public StationMultiToolsPanel() => InitializeComponent();

}
