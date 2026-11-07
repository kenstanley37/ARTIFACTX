using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class CrashedShipRow : UserControl
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(CrashedShipRowViewModel), typeof(CrashedShipRow), new PropertyMetadata(null));

    public CrashedShipRowViewModel? Row
    {
        get => (CrashedShipRowViewModel?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    public CrashedShipRow() => InitializeComponent();
}
