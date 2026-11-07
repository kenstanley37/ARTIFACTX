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
}

