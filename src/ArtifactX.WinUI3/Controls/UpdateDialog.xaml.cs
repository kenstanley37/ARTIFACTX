using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace ArtifactX.WinUI3.Controls;

/// <summary>The update window, opened from the title bar's "Update available" and Settings. While it downloads the
/// window can't be closed (the update would restart the app behind a closed window) and Later is hidden.</summary>
public sealed partial class UpdateDialog : ContentDialog
{
    private static bool _open;

    public UpdateDialogViewModel ViewModel { get; }

    private UpdateDialog(UpdateDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        PrimaryButtonText = viewModel.PrimaryText;
        PrimaryButtonClick += OnPrimaryClick;
        SecondaryButtonClick += OnSecondaryClick;
        Closing += (_, args) => { if (ViewModel.IsBusy) args.Cancel = true; };
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpdateDialogViewModel.Failed))
                StatusTxt.Foreground = (Brush)Application.Current.Resources[ViewModel.Failed ? "ThemeWarningBrush" : "ThemeTextBrush"];
        };
    }

    /// <summary>Shows the window for <paramref name="result"/>; one at a time (WinUI allows a single open ContentDialog).</summary>
    public static async Task ShowAsync(XamlRoot root, UpdateCheckResult result, bool isPreview = false)
    {
        if (_open) return;
        var dialog = new UpdateDialog(new UpdateDialogViewModel(result, isPreview)) { XamlRoot = root };
        _open = true;
        try { await dialog.ShowAsync(); }
        catch (Exception) { /* another dialog was already open */ }
        finally { _open = false; }
    }

    private async void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!ViewModel.UpdatesInPlace)
        {
            if (ViewModel.ReleaseUrl is string url) await Launcher.LaunchUriAsync(new Uri(url));
            return;
        }

        args.Cancel = true; // stay open: progress shows here
        IsPrimaryButtonEnabled = false;
        IsSecondaryButtonEnabled = false;
        string later = CloseButtonText;
        CloseButtonText = "";
        var ui = DispatcherQueue;
        await ViewModel.RunAsync(action => ui.TryEnqueue(() => action()));

        // Still here: the preview finished or the update failed. Let the player try again or close.
        IsPrimaryButtonEnabled = true;
        IsSecondaryButtonEnabled = true;
        CloseButtonText = ViewModel.IsPreview ? "Close" : later;
        if (ViewModel.Failed) PrimaryButtonText = "Try again";
    }

    private async void OnSecondaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; // opening the page shouldn't close the window
        if (ViewModel.ReleaseUrl is string url) await Launcher.LaunchUriAsync(new Uri(url));
    }
}
