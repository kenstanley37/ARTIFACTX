using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class SystemDetailsPage : ContentPage, IQueryAttributable
{
    private string? _code;
    private int _galaxy;

    public SystemDetailsPage(SystemFromGlyphsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
    }

    public SystemFromGlyphsViewModel ViewModel { get; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _code = query.TryGetValue("code", out var code) ? code as string : null;
        _galaxy = query.TryGetValue("galaxy", out var galaxy) && int.TryParse(galaxy?.ToString(), out int number) ? number : 0;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        try
        {
            if (_code is null) throw new ArgumentException();
            await ViewModel.LoadSystemAsync(_code, _galaxy);
        }
        catch (Exception)
        {
            ViewModel.CopyBannerText = "The system could not be loaded. Go back and check the address and galaxy.";
            ViewModel.HasCopyBanner = true;
        }
    }
}
