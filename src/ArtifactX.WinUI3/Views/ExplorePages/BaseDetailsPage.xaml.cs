using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>One base's own page, opened from the Bases page with its BaseRowViewModel.</summary>
public sealed partial class BaseDetailsPage : Page
{
    private double _backdropAspect;

    public BaseDetailsViewModel ViewModel { get; private set; } = null!;

    public BaseDetailsPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is not BaseRowViewModel row) return;
        ViewModel = new BaseDetailsViewModel(row);
        Bindings.Update();
        await ViewModel.LoadAsync();
    }

    /// <summary>The base's picture behind the header, faded to transparent at the edges of
    /// the area it fills, so it melts into the page with no seam (2026-10-18). Redone when
    /// the header's shape changes much (window resized).</summary>
    private async Task ShowBackdropAsync()
    {
        var entry = ViewModel?.Row.Entry;
        if (entry is null || !ViewModel!.Row.HasPicture || entry.Address == 0) return;
        double w = HeaderGrid.ActualWidth + 24, h = HeaderGrid.ActualHeight + 14;
        if (w < 50 || h < 50) return;
        double aspect = w / h;
        if (_backdropAspect > 0 && Math.Abs(aspect / _backdropAspect - 1) < 0.08) return;
        _backdropAspect = aspect;
        if (await GameScreenshotCache.LoadAsync(SaveSessionManager.ActivePlatformFolder, entry.Address, entry.ScreenshotName, faded: true, aspect: aspect) is { } faded)
        {
            BackdropBrush.ImageSource = faded;
            Backdrop.Visibility = Visibility.Visible;
        }
    }

    private void HeaderGrid_SizeChanged(object sender, SizeChangedEventArgs e) => _ = ShowBackdropAsync();

    private void Back_Click(object sender, RoutedEventArgs e) => GoBack();

    private void GoBack()
    {
        if (Frame.CanGoBack) Frame.GoBack();
        else Frame.Navigate(typeof(BasesPage));
    }

    private void OpenSettlement_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(SettlementPage));

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ViewModel.SelectAll();
    private void DeselectAll_Click(object sender, RoutedEventArgs e) => ViewModel.DeselectAll();

    private async void DeleteParts_Click(object sender, RoutedEventArgs e)
    {
        var kinds = ViewModel.SelectedKinds;
        if (kinds.Count == 0) return;
        bool confirmed = await DeleteConfirmation.ShowListAsync(XamlRoot,
            $"Delete {ViewModel.SelectedParts:N0} parts?",
            $"This deletes every part of {(kinds.Count == 1 ? "this kind" : $"these {kinds.Count:N0} kinds")} from {ViewModel.Row.Name}. The base itself stays.",
            kinds.Select(k => $"{k.Name} · {k.CountLabel}").ToList());
        if (confirmed) ViewModel.DeleteSelectedParts();
    }

    private async void DeleteBase_Click(object sender, RoutedEventArgs e)
    {
        bool confirmed = await DeleteConfirmation.ShowListAsync(XamlRoot, $"Delete {ViewModel.Row.Name}?",
            "This deletes the base from this save, with everything built in it.",
            [$"{ViewModel.Row.Name} - {ViewModel.Row.Detail}"],
            "A deleted base is gone completely, base computer and all - to empty it but keep it, delete its parts instead.");
        if (confirmed && ViewModel.DeleteBase()) GoBack();
    }

    private async void DeleteObject_Click(object sender, RoutedEventArgs e)
    {
        bool confirmed = await DeleteConfirmation.ShowListAsync(XamlRoot, $"Delete {ViewModel.Row.Name}?",
            "This deletes this placed object from this save.",
            [$"{ViewModel.Row.Name} - {ViewModel.Row.Detail}"]);
        if (confirmed && ViewModel.DeleteObject()) GoBack();
    }
}
