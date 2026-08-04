using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ArtifactX.WinUI3.Views;

public sealed partial class SaveFolderSelectPage : Page
{
    public SaveFolderSelectViewModel ViewModel { get; } = new();

    public SaveFolderSelectPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await ViewModel.InitializeCommand.ExecuteAsync(null);
            RefreshActivePlatformHighlight();
        };

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        Unloaded += (_, _) => SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshActivePlatformHighlight);

    /// <summary>Marks whichever candidate matches SaveSessionManager's current
    /// platform (or slot, which implies a platform) so the XAML can highlight
    /// it - fired on load and whenever the active session changes (a slot
    /// loads, or a different platform card is expanded elsewhere/earlier).</summary>
    private void RefreshActivePlatformHighlight()
    {
        foreach (var candidate in ViewModel.Candidates)
            candidate.IsActivePlatform = string.Equals(
                candidate.FolderPath, SaveSessionManager.ActivePlatformFolder, StringComparison.OrdinalIgnoreCase);
    }

    private async void BrowseFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        await ViewModel.TryAddCustomFolderAsync(folder.Path);
    }

    private async void SaveSlot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SaveSlotGroup slot }) return;
        await SaveSessionManager.LoadAsync(slot);
    }

    /// <summary>Opening a platform card (whether by a user click or restored
    /// expanded state from a prior session) is treated as "working within this
    /// platform account" even before a specific save slot is picked - lets
    /// account-wide pages (Account Data) become available off just this,
    /// since accountdata.hg lives once per platform folder, not per slot.</summary>
    private void PlatformExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.Tag is SaveFolderCandidate candidate)
            SaveSessionManager.SetActivePlatform(candidate.FolderPath);
    }

    private void RemoveFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SaveFolderCandidate candidate })
            ViewModel.RemoveCandidateCommand.Execute(candidate);
    }

    /// <summary>Pure display metadata - never touches the actual folder on
    /// disk. Pre-fills with whatever name (custom or default) is currently
    /// showing, so editing an existing name doesn't mean retyping the whole
    /// thing.</summary>
    private async void RenameFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SaveFolderCandidate candidate }) return;

        var nameBox = new TextBox { Text = candidate.DisplayName, PlaceholderText = "e.g. \"Backup Drive\"" };
        var dialog = new ContentDialog
        {
            Title = "Name this folder",
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { new TextBlock { Text = "Shown in place of \"Custom Folder\" - doesn't rename anything on disk." }, nameBox }
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ViewModel.RenameCandidate(candidate, nameBox.Text?.Trim());
    }
}