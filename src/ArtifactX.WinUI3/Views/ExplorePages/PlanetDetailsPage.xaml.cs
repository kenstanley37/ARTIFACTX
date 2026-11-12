using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Helpers.Layout;
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
        else Frame.Navigate(typeof(CurrentSystemPage));
    }

    private async void Previous_Click(object sender, RoutedEventArgs e) => await ViewModel.GoPreviousAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await ViewModel.GoNextAsync();
    // The pinned bar's summary shows once the header card has scrolled away.
    private void Scroller_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e) => UpdateSummary();
    private void HeaderCard_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSummary();
    private void UpdateSummary() => Summary.IsShown = PinnedHeader.IsScrolledAway(Scroller, HeaderCard);

    private void ToggleEditor_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleEditor();
    private void CollapseFish_Click(object sender, RoutedEventArgs e) => ViewModel.SetFishCollapsed(true);
    private void ExpandFish_Click(object sender, RoutedEventArgs e) => ViewModel.SetFishCollapsed(false);

    private async void Creature_DeleteRequested(object? sender, CreatureCardRequest request)
    {
        var dialog = new ContentDialog
        {
            Title = $"Remove {request.Creature.Name}?",
            Content = "This removes the creature from what ArtifactX shows for this planet. The game isn't changed.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Keep it",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.DeleteCreature(request.Creature);
    }

    private async void Creature_Preview3DRequested(object? sender, CreatureCardRequest request)
    {
        var card = request.Creature;
        await CreaturePreviewControl.ShowAsync(XamlRoot, card.Creature, card.RigId, card.IsSilhouette,
            await CreatureModelService.PartsForSeedAsync(card.ModelScene, card.ModelSeed));
    }

    private void PlanetEquipment_CrashedShipsRequested(object? sender, EventArgs e)
    {
        Frame.Navigate(typeof(CrashedShipsPage), ViewModel.Planet?.Address);
    }

    private void PlanetEquipment_CabinetMultiToolsRequested(object? sender, EventArgs e)
    {
        Frame.Navigate(typeof(CabinetMultiToolsPage), ViewModel.Planet?.Address);
    }
}
