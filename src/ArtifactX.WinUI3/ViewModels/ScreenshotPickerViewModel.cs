using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One screenshot in the picker: Steam's small copy to show, the full picture to use.</summary>
public sealed class ScreenshotChoiceViewModel
{
    public ScreenshotChoiceViewModel(SteamAccountsService.Screenshot shot)
    {
        Path = shot.Path;
        Label = shot.Taken.ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
        // Decoded at the size it's shown; the picture loads when its tile scrolls into view.
        Thumbnail = new BitmapImage(new Uri(shot.ThumbnailPath)) { DecodePixelWidth = 240 };
    }

    public string Path { get; }
    public string Label { get; }
    public string AccessibleName => $"Screenshot from {Label}";
    public BitmapImage Thumbnail { get; }
}

/// <summary>The player's No Man's Sky screenshots from Steam, newest first, to pick a page background from.</summary>
public sealed partial class ScreenshotPickerViewModel : ObservableObject
{
    public ObservableCollection<ScreenshotChoiceViewModel> Shots { get; } = new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial ScreenshotChoiceViewModel? Selected { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    public bool HasSelection => Selected is not null;
    public bool IsEmpty => !IsLoading && Shots.Count == 0;

    /// <summary>Call on the UI thread: the folders are read off it.</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        var shots = await Task.Run(SteamAccountsService.NmsScreenshots);
        Shots.Clear();
        foreach (var shot in shots) Shots.Add(new ScreenshotChoiceViewModel(shot));
        IsLoading = false;
    }
}
