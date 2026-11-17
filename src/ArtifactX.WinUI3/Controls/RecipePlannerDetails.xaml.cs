using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>The recipe page's Selected step tab: the selected tile's details, and for a raw ingredient where to find it
/// (<see cref="IngredientSourcePanel"/>).</summary>
public sealed partial class RecipePlannerDetails : UserControl
{
    public RecipePlannerItemViewModel? Item
    {
        get => (RecipePlannerItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(RecipePlannerItemViewModel), typeof(RecipePlannerDetails), new PropertyMetadata(null));

    public IngredientSourceViewModel? Source
    {
        get => (IngredientSourceViewModel?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(IngredientSourceViewModel), typeof(RecipePlannerDetails), new PropertyMetadata(null));

    public RecipePlannerDetails() => InitializeComponent();
}
