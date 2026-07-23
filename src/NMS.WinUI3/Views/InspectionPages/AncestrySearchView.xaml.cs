using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NMS.Core;
using NMS.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace NMS.WinUI3.Views.InspectionPages;

public sealed partial class AncestrySearchView : Page
{
    private string? baseDirectoryPath;

    private byte[]? cachedBytesLeft;
    private byte[]? cachedBytesRight;
    private string? pathLeftFile;
    private string? pathRightFile;

    // 📍 NEW: Keep the full unfiltered background results cached in memory
    private List<JsonNodeMatch> _rawMatchesLeft = new();
    private List<JsonNodeMatch> _rawMatchesRight = new();

    public AncestrySearchView()
    {
        this.InitializeComponent();
        GlobalSearchBox.TextChanged += GlobalSearchBox_TextChanged;
    }

    private async void SelectFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");
            folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

            var window = NMS.WinUI3.App.MainWindowInstance;
            if (window != null)
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            }

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                baseDirectoryPath = folder.Path;
                FolderPathLabel.Text = $"Directory Path: {baseDirectoryPath}";
                ResetWorkspaceContexts();

                var files = Directory.GetFiles(baseDirectoryPath, "*.*")
                    .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".hg", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName)
                    .ToList();

                if (files.Count > 0)
                {
                    FileDropLeft.ItemsSource = files;
                    FileDropRight.ItemsSource = files;

                    GlobalSearchBox.IsEnabled = true;
                    GlobalValueBox.IsEnabled = true; // 📍 Unlock value box as well
                    GlobalStatusLabel.Text = "Discovered files. Select files to compare and type a search target key.";
                }
                else
                {
                    GlobalStatusLabel.Text = "No valid save target files (.json/.hg) discovered here.";
                }
            }
        }
        catch (Exception ex)
        {
            GlobalStatusLabel.Text = $"WinUI Picker execution failure: {ex.Message}";
        }
    }

    private void FileDropLeft_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileDropLeft.SelectedItem is string fileName && !string.IsNullOrEmpty(baseDirectoryPath))
        {
            pathLeftFile = Path.Combine(baseDirectoryPath, fileName);
            cachedBytesLeft = null;
            EvaluateButtonStates();
        }
    }

    private void FileDropRight_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileDropRight.SelectedItem is string fileName && !string.IsNullOrEmpty(baseDirectoryPath))
        {
            pathRightFile = Path.Combine(baseDirectoryPath, fileName);
            cachedBytesRight = null;
            EvaluateButtonStates();
        }
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        EvaluateButtonStates();
    }

    // 📍 NEW: Filter the displayed rows dynamically as you type a value
    private void GlobalValueBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyDisplayedFilters();
    }

    private void EvaluateButtonStates()
    {
        string currentSearch = GlobalSearchBox.Text.Trim();
        bool hasSearchToken = !string.IsNullOrEmpty(currentSearch);

        ScanBtnLeft.IsEnabled = hasSearchToken && !string.IsNullOrEmpty(pathLeftFile);
        ScanBtnRight.IsEnabled = hasSearchToken && !string.IsNullOrEmpty(pathRightFile);
    }

    // 📍 NEW: Process filtering based on both Key and Value boxes simultaneously
    private void ApplyDisplayedFilters()
    {
        string valFilter = GlobalValueBox.Text.Trim();

        if (string.IsNullOrEmpty(valFilter))
        {
            MatchesListLeft.ItemsSource = _rawMatchesLeft;
            MatchesListRight.ItemsSource = _rawMatchesRight;
            return;
        }

        // Filter out rows where the ExtractedValue does not match your query string
        MatchesListLeft.ItemsSource = _rawMatchesLeft
            .Where(m => m.ExtractedValue != null && m.ExtractedValue.Contains(valFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        MatchesListRight.ItemsSource = _rawMatchesRight
            .Where(m => m.ExtractedValue != null && m.ExtractedValue.Contains(valFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async void ScanBtnLeft_Click(object sender, RoutedEventArgs e)
    {
        string targetToken = GlobalSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(targetToken) || string.IsNullOrEmpty(pathLeftFile)) return;

        LoadingRingLeft.IsActive = true;
        ScanBtnLeft.IsEnabled = false;
        MatchesListLeft.ItemsSource = null;
        TreeTextLeft.Text = string.Empty;

        try
        {
            await Task.Run(async () =>
            {
                if (cachedBytesLeft == null)
                {
                    cachedBytesLeft = await DecompressFileToBytesAsync(pathLeftFile);
                }

                if (cachedBytesLeft != null && cachedBytesLeft.Length > 0)
                {
                    _rawMatchesLeft = JsonPathEngine.SearchAncestry(cachedBytesLeft, targetToken);
                }
            });

            ApplyDisplayedFilters(); // 📍 Apply current filters to the list
            GlobalStatusLabel.Text = $"File A scan complete. Found {_rawMatchesLeft.Count} raw key matches.";
        }
        catch (Exception ex)
        {
            GlobalStatusLabel.Text = $"File A stream error: {ex.Message}";
        }
        finally
        {
            LoadingRingLeft.IsActive = false;
            EvaluateButtonStates();
        }
    }

    private async void ScanBtnRight_Click(object sender, RoutedEventArgs e)
    {
        string targetToken = GlobalSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(targetToken) || string.IsNullOrEmpty(pathRightFile)) return;

        LoadingRingRight.IsActive = true;
        ScanBtnRight.IsEnabled = false;
        MatchesListRight.ItemsSource = null;
        TreeTextRight.Text = string.Empty;

        try
        {
            await Task.Run(async () =>
            {
                if (cachedBytesRight == null)
                {
                    cachedBytesRight = await DecompressFileToBytesAsync(pathRightFile);
                }

                if (cachedBytesRight != null && cachedBytesRight.Length > 0)
                {
                    _rawMatchesRight = JsonPathEngine.SearchAncestry(cachedBytesRight, targetToken);
                }
            });

            ApplyDisplayedFilters(); // 📍 Apply current filters to the list
            GlobalStatusLabel.Text = $"File B scan complete. Found {_rawMatchesRight.Count} raw key matches.";
        }
        catch (Exception ex)
        {
            GlobalStatusLabel.Text = $"File B stream error: {ex.Message}";
        }
        finally
        {
            LoadingRingRight.IsActive = false;
            EvaluateButtonStates();
        }
    }

    private async Task<byte[]> DecompressFileToBytesAsync(string filePath)
    {
        using Stream uncompressedStream = await SaveStreamProcessor.DecompressSaveToStreamAsync(filePath);
        using var ms = new MemoryStream();
        await uncompressedStream.CopyToAsync(ms);
        byte[] rawJsonBytes = ms.ToArray();

        if (rawJsonBytes.Length == 0) return Array.Empty<byte>();

        int validLength = rawJsonBytes.Length;
        while (validLength > 0 && (rawJsonBytes[validLength - 1] == 0x00 || rawJsonBytes[validLength - 1] <= 0x20))
        {
            validLength--;
        }

        int jsonStartOffset = 0;
        if (validLength >= 3 && rawJsonBytes[0] == 0xEF && rawJsonBytes[1] == 0xBB && rawJsonBytes[2] == 0xBF)
        {
            jsonStartOffset = 3;
        }

        if (jsonStartOffset == 0 && validLength == rawJsonBytes.Length)
        {
            return rawJsonBytes;
        }

        byte[] cleanedBytes = new byte[validLength - jsonStartOffset];
        Buffer.BlockCopy(rawJsonBytes, jsonStartOffset, cleanedBytes, 0, cleanedBytes.Length);
        return cleanedBytes;
    }

    private void MatchesListLeft_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MatchesListLeft.SelectedItem is JsonNodeMatch match && match.TreeLineage != null)
        {
            TreeTextLeft.Text = string.Join(Environment.NewLine, match.TreeLineage);
        }
    }

    private void MatchesListRight_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MatchesListRight.SelectedItem is JsonNodeMatch match && match.TreeLineage != null)
        {
            TreeTextRight.Text = string.Join(Environment.NewLine, match.TreeLineage);
        }
    }

    private void ResetWorkspaceContexts()
    {
        _rawMatchesLeft.Clear();
        _rawMatchesRight.Clear();
        MatchesListLeft.ItemsSource = null;
        MatchesListRight.ItemsSource = null;
        TreeTextLeft.Text = string.Empty;
        TreeTextRight.Text = string.Empty;
        FileDropLeft.ItemsSource = null;
        FileDropRight.ItemsSource = null;
        pathLeftFile = null;
        pathRightFile = null;
        cachedBytesLeft = null;
        cachedBytesRight = null;
        ScanBtnLeft.IsEnabled = false;
        ScanBtnRight.IsEnabled = false;
    }
}