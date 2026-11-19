using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class EggSequencerPage : ContentPage
{
    public EggSequencerViewModel ViewModel { get; } = new();

    public EggSequencerPage()
    {
        InitializeComponent();
        BindingContext = ViewModel;
    }

    // Listening only while on screen (Android can recreate the page in the same process).
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ViewModel.ItemPlaced += OnItemPlaced;
        await ViewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        ViewModel.ItemPlaced -= OnItemPlaced;
        base.OnDisappearing();
    }

    // After an item goes in, back up to the slots so the dose and what happens are in sight.
    private async void OnItemPlaced() => await Scroller.ScrollToAsync(0, 0, true);

    // The label beside the box toggles it too: a bigger target than the box alone.
    private void OnlyFullEffect_Tapped(object? sender, TappedEventArgs e) => ViewModel.OnlyFullEffect = !ViewModel.OnlyFullEffect;
}
