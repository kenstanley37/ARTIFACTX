using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class StationDetailsPanel : UserControl
{
    public static readonly DependencyProperty DetailsProperty =
        DependencyProperty.Register(nameof(Details), typeof(StationDetailsViewModel), typeof(StationDetailsPanel), new PropertyMetadata(null));

    public StationDetailsViewModel? Details
    {
        get => (StationDetailsViewModel?)GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    public StationDetailsPanel() => InitializeComponent();
}
