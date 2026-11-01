using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

public sealed partial class CookingRecipesPage : Page
{
    public RecipePlannerViewModel ViewModel { get; } = new(RecipePlannerMode.Cooking);
    public CookingRecipesPage() => InitializeComponent();
}
