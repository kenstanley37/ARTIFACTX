using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ReferencePages;

public partial class RecipeDetailsPage : ContentPage, IQueryAttributable
{
    private string? _recipeId;

    public RecipeDetailsPage(RecipeDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
    }

    public RecipeDetailsViewModel ViewModel { get; }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _recipeId = query.TryGetValue("id", out var id) ? id as string : null;

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await ViewModel.LoadAsync(_recipeId);
    }
}
