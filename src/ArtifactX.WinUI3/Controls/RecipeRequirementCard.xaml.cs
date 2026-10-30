using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
namespace ArtifactX.WinUI3.Controls;

public sealed partial class RecipeRequirementCard : UserControl
{
    private string? _selectedRecipe;

    public RecipePlanCardViewModel ViewModel
    {
        get => (RecipePlanCardViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RecipePlanCardViewModel), typeof(RecipeRequirementCard), new PropertyMetadata(null, (sender, args) =>
        {
            var control = (RecipeRequirementCard)sender;
            control._selectedRecipe = (args.NewValue as RecipePlanCardViewModel)?.SelectedChoice?.Id;
        }));
    public event Action<string, string>? RecipeChanged;
    public RecipeRequirementCard() => InitializeComponent();
    private void Choice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: RecipePlanChoice choice } && choice.Id != _selectedRecipe)
        {
            _selectedRecipe = choice.Id;
            RecipeChanged?.Invoke(ViewModel.ItemId, choice.Id);
        }
    }
}
