using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class RecipePlannerCard : UserControl
{
    public RecipePlannerItemViewModel ViewModel
    {
        get => (RecipePlannerItemViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RecipePlannerItemViewModel), typeof(RecipePlannerCard), new PropertyMetadata(null));
    public RecipePlannerCard() => InitializeComponent();
}
