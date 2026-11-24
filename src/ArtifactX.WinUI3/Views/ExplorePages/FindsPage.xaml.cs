using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>
/// Tools > Finds: wiring only. The You are here band, filters, cards and the Add / Edit form bind to
/// <see cref="ViewModel"/> (FindsPageViewModel and its FindFormViewModel). This page passes on the game's saves
/// (FishCatchRecorder.SnapshotSeen) and the finds / planet / system-note changes, confirms deletes and handles the
/// buttons and the item picker. Never writes to the save.
/// </summary>
public sealed partial class FindsPage : Page
{
    public FindsPageViewModel ViewModel { get; } = new();

    public FindsPage()
    {
        InitializeComponent();
        // The app-wide feed of the game's saves (FishCatchRecorder): one reader for every page.
        FishCatchRecorder.SnapshotSeen += ViewModel.OnSnapshot;
        JumpPlanner.RangeChanged += ViewModel.Apply;
        FindsService.Changed += OnFindsChanged;
        PlanetDetailsService.Changed += OnPlanetChanged;
        SystemNotesService.Changed += OnSystemNotesChanged;
        Unloaded += (_, _) =>
        {
            JumpPlanner.RangeChanged -= ViewModel.Apply;
            FindsService.Changed -= OnFindsChanged;
            PlanetDetailsService.Changed -= OnPlanetChanged;
            SystemNotesService.Changed -= OnSystemNotesChanged;
            FishCatchRecorder.SnapshotSeen -= ViewModel.OnSnapshot;
        };
        _ = ViewModel.LoadAsync();
    }

    private void OnFindsChanged() => DispatcherQueue.TryEnqueue(() => ViewModel.Refresh());

    private void OnPlanetChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() => ViewModel.Apply());

    private void OnSystemNotesChanged(ulong system) => DispatcherQueue.TryEnqueue(() => ViewModel.Refresh());

    // ---------------------------------------------------------------- list

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var (headers, rows) = ViewModel.CsvRows();
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX finds", headers, rows);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var confirm = new ContentDialog
        {
            Title = "Delete this find?",
            Content = ViewModel.FindById(id)?.Title is { Length: > 0 } t ? $"\"{t}\" will be removed." : "This find will be removed.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary) ViewModel.Delete(id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string id) ViewModel.Edit(id);
    }

    // ---------------------------------------------------------------- form

    private void FormToggle_Click(object sender, RoutedEventArgs e) => ViewModel.Form.Toggle();

    private void UseHere_Click(object sender, RoutedEventArgs e) => ViewModel.Form.UseHere();

    private void FormItemSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) sender.ItemsSource = ViewModel.Form.Suggestions(sender.Text);
    }

    private async void FormItemSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        string name = args.SelectedItem as string ?? "";
        sender.Text = "";
        await ViewModel.Form.AddItemAsync(name, ViewModel.NameOf);
    }

    private void FormItemRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string id) ViewModel.Form.RemoveItem(id);
    }

    private void FormSave_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Form.Save()) ViewModel.Refresh();
    }

    private void FormCancel_Click(object sender, RoutedEventArgs e) => ViewModel.Form.Cancel();
}
