using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NMS.WinUI3.Models;
using NMS.WinUI3.Services;
using NMS.WinUI3.ViewModels;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace NMS.WinUI3.Views;

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
}