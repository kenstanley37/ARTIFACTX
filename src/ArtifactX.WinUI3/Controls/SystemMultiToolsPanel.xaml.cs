using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class SystemMultiToolsPanel : UserControl
{
    public static readonly DependencyProperty MultiToolsProperty =
        DependencyProperty.Register(nameof(MultiTools), typeof(SystemMultiToolsViewModel), typeof(SystemMultiToolsPanel), new PropertyMetadata(null));

    public SystemMultiToolsViewModel? MultiTools
    {
        get => (SystemMultiToolsViewModel?)GetValue(MultiToolsProperty);
        set => SetValue(MultiToolsProperty, value);
    }

    public SystemMultiToolsPanel() => InitializeComponent();

    private void Toggle_Click(object sender, RoutedEventArgs e) => MultiTools?.Toggle();

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            MultiTools?.SelectFilter(key);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SystemMultiToolItemViewModel item })
        {
            item.CopySeed();
        }
    }
}

