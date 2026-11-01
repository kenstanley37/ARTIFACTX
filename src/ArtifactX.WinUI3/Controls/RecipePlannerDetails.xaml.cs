using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class RecipePlannerDetails : UserControl
{
    public RecipePlannerItemViewModel? Item
    {
        get => (RecipePlannerItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(RecipePlannerItemViewModel), typeof(RecipePlannerDetails), new PropertyMetadata(null));
    public event Action<string, string>? RecipeChanged;
    public RecipePlannerDetails() => InitializeComponent();
    private void Choice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Item is { } item && sender is ComboBox { SelectedItem: RecipePlanChoice choice }
            && choice.Id != item.RecipeId && item.Choices.Any(c => c.Id == choice.Id))
            RecipeChanged?.Invoke(item.Id, choice.Id);
    }
}
