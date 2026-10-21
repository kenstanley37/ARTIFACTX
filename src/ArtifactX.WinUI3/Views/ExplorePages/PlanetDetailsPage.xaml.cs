using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>A planet's own page (PlanetDetailsViewModel), opened from Current System with a KnownPlanet.</summary>
public sealed partial class PlanetDetailsPage : Page
{
    public PlanetDetailsViewModel ViewModel { get; } = new();

    public PlanetDetailsPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Attach(DispatcherQueue);
        if (e.Parameter is KnownPlanet planet) await ViewModel.LoadAsync(planet);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Detach();
    }

    public Visibility IsCollapsedWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The details card is as tall as the others (300) - or as its editor while that's open.</summary>
    public double DetailsCardHeight(bool editing) => editing ? double.NaN : 300;

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
        else Frame.Navigate(typeof(PlanetsPage));
    }

    private async void Previous_Click(object sender, RoutedEventArgs e) => await ViewModel.GoPreviousAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await ViewModel.GoNextAsync();
    private void ToggleEditor_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleEditor();
    private void CollapseFish_Click(object sender, RoutedEventArgs e) => ViewModel.SetFishCollapsed(true);
    private void ExpandFish_Click(object sender, RoutedEventArgs e) => ViewModel.SetFishCollapsed(false);

    private async void CaptureCreature_Click(object sender, RoutedEventArgs e) => await CaptureAsync(null, CaptureCreatureBtn);

    private async void Creature_CaptureRequested(object? sender, CreatureCardRequest request) =>
        await CaptureAsync(request.Creature.Name, request.Anchor);

    private async System.Threading.Tasks.Task CaptureAsync(string? creatureName, FrameworkElement anchor)
    {
        if (!ViewModel.CanCapture) return;
        CaptureResult result;
        try
        {
            result = await ViewModel.CaptureAsync(creatureName);
        }
        catch (Exception ex)
        {
            result = new CaptureResult(false, ex.Message);
        }
        CaptureFlyoutIcon.Glyph = result.Success ? "" : "";
        CaptureFlyoutIcon.Foreground = ThemeResources.Brush(result.Success ? "ThemeAccentBrush" : "ThemeWarningBrush");
        CaptureFlyoutTitle.Text = !result.Success ? "Couldn't read the screen" : result.ReadInfo is not null ? "Creatures read" : "Picture taken";
        CaptureFlyoutDetails.Text = result.Message;
        CaptureFlyout.ShowAt(anchor.IsLoaded ? anchor : CaptureCreatureBtn);
    }

    private async void Creature_DeleteRequested(object? sender, CreatureCardRequest request)
    {
        var dialog = new ContentDialog
        {
            Title = $"Remove {request.Creature.Name}?",
            Content = "This removes the creature, and its picture, from what ArtifactX recorded about this planet. The game isn't changed - reading the fauna list again brings it back.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Keep it",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.DeleteCreature(request.Creature);
    }
}
