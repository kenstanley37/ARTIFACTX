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
        Loaded += async (_, _) => await ViewModel.InitializeCommand.ExecuteAsync(null);
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