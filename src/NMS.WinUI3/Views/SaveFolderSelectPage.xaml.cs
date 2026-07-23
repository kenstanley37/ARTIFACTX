using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NMS.WinUI3.Services;
using System;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace NMS.WinUI3.Views;

public sealed partial class SaveFolderSelectPage : Page
{
    public SaveFolderSelectPage()
    {
        InitializeComponent();

        // Auto-load if folder already saved
        var settings = ApplicationData.Current.LocalSettings;
        if (settings.Values.TryGetValue("SaveFolderPath", out object pathObj))
        {
            string folderPath = pathObj.ToString();
            if (Directory.Exists(folderPath))
            {
                SelectedFolderTxt.Text = folderPath; // ✅ Display saved path immediately
                InitializeFolderScan(folderPath);
            }
        }

    }

    private async void PickFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var window = App.MainWindowInstance;
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder folder = await picker.PickSingleFolderAsync();
        if (folder == null)
            return;

        SelectedFolderTxt.Text = folder.Path;

        // Validate folder contains real save files (save*.hg)
        var hgFiles = Directory.GetFiles(folder.Path, "save*.hg", SearchOption.TopDirectoryOnly);

        if (hgFiles.Length == 0)
        {
            await new ContentDialog
            {
                Title = "Invalid Folder",
                Content = "This folder does not contain any .hg save files. Please select a valid No Man's Sky save folder.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();

            return;
        }

        // Save folder path
        var settings = ApplicationData.Current.LocalSettings;
        settings.Values["SaveFolderPath"] = folder.Path;

        // DO NOT collapse the picker — keep UI persistent
        // FolderPickerSection.Visibility = Visibility.Collapsed;

        // Show overview section
        SaveOverviewSection.Visibility = Visibility.Visible;

        // Initialize folder scan
        InitializeFolderScan(folder.Path);
    }

    private async void InitializeFolderScan(string folderPath)
    {
        LoadingRing.Visibility = Visibility.Visible;
        LoadingRing.IsActive = true;

        var results = await SaveWorkspaceService.InitializeFolderScanAsync(folderPath);

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;

        // Show the overview section
        SaveOverviewSection.Visibility = Visibility.Visible;

        // Clear previous content
        SaveOverviewSection.Children.Clear();

        // Build a simple list of slot summaries
        var stack = new StackPanel { Spacing = 8 };

        foreach (var slot in results)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Slot {slot.SlotId} — {slot.PlayerName} — {slot.Units:N0} Units",
                FontSize = 14,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
            });
        }

        SaveOverviewSection.Children.Add(stack);
    }
}
