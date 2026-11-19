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

    // Another galaxy picked while a system is open (the galaxy bar): the same glyphs are a different system there, so
    // this one closes rather than stay on screen under the wrong galaxy (owner, 2026-11-18). The address page keeps the
    // glyphs, so Find system opens them in the new galaxy. Listening only while on screen.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Services.GalaxyService.Changed += OnGalaxyChanged;
    }

    protected override void OnDisappearing()
    {
        Services.GalaxyService.Changed -= OnGalaxyChanged;
        base.OnDisappearing();
    }

    private async void OnGalaxyChanged()
    {
        if (ViewModel.GalaxyNumber == 0 || ViewModel.GalaxyNumber == Services.GalaxyService.Current) return;
        Services.GalaxyService.Changed -= OnGalaxyChanged;
        if (Shell.Current is { } shell && shell.Navigation.NavigationStack.Count > 1) await shell.GoToAsync("..");
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        try
        {
            if (_code is null) throw new ArgumentException();
            await ViewModel.LoadSystemAsync(_code, _galaxy);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemDetails] {_code} galaxy {_galaxy}: {ex}");
            ViewModel.CopyBannerText = "The system could not be loaded. Go back and check the address and galaxy.";
            ViewModel.HasCopyBanner = true;
        }
    }
}
