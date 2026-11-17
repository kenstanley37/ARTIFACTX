using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>"Where it grows" and "Closest to you" for a raw ingredient (IngredientSourceViewModel). Its own control so
/// other pages (Planet Details, a future finder page) can show the same thing.</summary>
public sealed partial class IngredientSourcePanel : UserControl
{
    public IngredientSourceViewModel ViewModel
    {
        get => (IngredientSourceViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(IngredientSourceViewModel), typeof(IngredientSourcePanel), new PropertyMetadata(null));

    public IngredientSourcePanel() => InitializeComponent();
}
