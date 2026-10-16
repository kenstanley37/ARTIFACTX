using ArtifactX.GameData;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// First-run setup (2026-10-14): reads the player's own game files into
/// ArtifactX's catalog before any save can be opened (GameDataService), with
/// one overall progress bar, the current step, elapsed and remaining time, and
/// a plain-words activity feed. When it finishes, MainWindow brings the menu
/// back and opens Save Selection.
/// </summary>
public sealed partial class GameDataSetupPage : Page
{
    private string? _message;
    private bool _messageIsError;

    public GameDataSetupPage()
    {
        InitializeComponent();
        GameDataService.ProgressChanged += OnProgress;
        GameDataService.ActivityLogged += OnActivity;
        GameDataService.BuildFinished += OnFinished;
        GameDataService.StateChanged += OnStateChanged;
        Unloaded += (_, _) =>
        {
            GameDataService.ProgressChanged -= OnProgress;
            GameDataService.ActivityLogged -= OnActivity;
            GameDataService.BuildFinished -= OnFinished;
            GameDataService.StateChanged -= OnStateChanged;
        };
        ActivityTxt.Text = string.Join(Environment.NewLine, GameDataService.Activity);
        Refresh();
    }

    private void OnProgress(GameDataProgress p) => DispatcherQueue.TryEnqueue(Refresh);
    private void OnStateChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Refresh);

    private void OnActivity(string line) => DispatcherQueue.TryEnqueue(() =>
    {
        ActivityTxt.Text = ActivityTxt.Text.Length == 0 ? line : ActivityTxt.Text + Environment.NewLine + line;
        ActivityScroll.UpdateLayout();
        ActivityScroll.ChangeView(null, ActivityScroll.ScrollableHeight, null, disableAnimation: true);
    });

    private void OnFinished(string? error) => DispatcherQueue.TryEnqueue(() =>
    {
        _message = error;
        _messageIsError = error is not null && !error.StartsWith("Cancelled", StringComparison.Ordinal);
        Refresh();
    });

    private void Refresh()
    {
        var game = GameDataService.Game;
        bool building = GameDataService.IsBuilding;

        GameTxt.Text = game is null
            ? "No Man's Sky wasn't found on this PC. Choose the folder it's installed in - the one with GAMEDATA and Binaries in it."
            : $"No Man's Sky ({game.BuildLabel}) at {game.RootFolder}";
        StartBtn.Visibility = building ? Visibility.Collapsed : Visibility.Visible;
        StartBtn.IsEnabled = game is not null;
        StartBtn.Content = _message is null ? "Read my game files" : "Try again";
        ChangeBtn.IsEnabled = !building;

        MessageTxt.Visibility = _message is null || building ? Visibility.Collapsed : Visibility.Visible;
        MessageTxt.Text = _message ?? "";
        MessageTxt.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[_messageIsError ? "ThemeWarningBrush" : "ThemeTextDimBrush"];

        ProgressCard.Visibility = building || ActivityTxt.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        CancelBtn.Visibility = building ? Visibility.Visible : Visibility.Collapsed;
        LogBtn.IsEnabled = File.Exists(GameDataService.LogPath);

        if (GameDataService.Progress is { } p && building)
        {
            StepTxt.Text = p.Step == 0 ? "Starting..." : $"Step {p.Step} of {p.Steps}: {p.Label}";
            Bar.Value = p.Fraction * 100;
            PercentTxt.Text = $"{p.Fraction * 100:0}%";
            TimeTxt.Text = $"{Elapsed(p.Elapsed)} so far" + Remaining(p);
        }
        else if (!building)
        {
            StepTxt.Text = _message is null ? "" : _messageIsError ? "Stopped" : "Cancelled";
            PercentTxt.Text = "";
            TimeTxt.Text = "";
        }
    }

    /// <summary>"about 6 minutes left", once there's enough done to guess from.</summary>
    private static string Remaining(GameDataProgress p)
    {
        if (p.Fraction < 0.05 || p.Elapsed.TotalSeconds < 20) return "";
        double left = p.Elapsed.TotalSeconds * (1 - p.Fraction) / p.Fraction;
        return left < 60 ? " · less than a minute left" : $" · about {Math.Ceiling(left / 60):0} minute{(left >= 120 ? "s" : "")} left";
    }

    private static string Elapsed(TimeSpan t) => t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}m {t.Seconds:00}s" : $"{t.Seconds}s";

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        _message = null;
        ActivityTxt.Text = "";
        GameDataService.StartBuild();
        Refresh();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => GameDataService.CancelBuild();

    private async void Change_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        if (GameDataService.ChooseGameFolder(folder.Path))
        {
            _message = null;
        }
        else
        {
            _message = $"\"{folder.Path}\" isn't a No Man's Sky folder. Choose the one the game is installed in (with GAMEDATA and Binaries in it).";
            _messageIsError = true;
        }
        Refresh();
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(GameDataService.LogPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GameDataService.LogPath) { UseShellExecute = true });
    }
}
