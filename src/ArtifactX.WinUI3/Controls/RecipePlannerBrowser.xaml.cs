using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class RecipePlannerBrowser : UserControl
{
    public RecipePlannerViewModel ViewModel
    {
        get => (RecipePlannerViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RecipePlannerViewModel), typeof(RecipePlannerBrowser), new PropertyMetadata(null));
    public RecipePlannerBrowser() => InitializeComponent();
}
