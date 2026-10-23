using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class SystemStarshipsPanel : UserControl
{
    public static readonly DependencyProperty StarshipsProperty =
        DependencyProperty.Register(nameof(Starships), typeof(SystemStarshipsViewModel), typeof(SystemStarshipsPanel), new PropertyMetadata(null));

    public SystemStarshipsViewModel? Starships
    {
        get => (SystemStarshipsViewModel?)GetValue(StarshipsProperty);
        set => SetValue(StarshipsProperty, value);
    }

    public SystemStarshipsPanel() => InitializeComponent();

    private void Toggle_Click(object sender, RoutedEventArgs e) => Starships?.Toggle();

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            Starships?.SelectFilter(key);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SystemStarshipItemViewModel item })
        {
            item.CopySeed();
        }
    }
}
