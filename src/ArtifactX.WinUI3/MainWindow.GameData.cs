using ArtifactX.GameData;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3;

/// <summary>
/// The game-data bar above every page (2026-10-14): ArtifactX builds its item
/// catalog from the player's own No Man's Sky files (GameDataService), so on
/// first run it offers to read them, after a game update it offers to update,
/// and while it works it shows progress - never blocking the app, which stays
/// usable throughout (save editing doesn't need the catalog).
/// </summary>
public sealed partial class MainWindow
{
    // An offer the player closed stays closed for this session.
    private bool _gameDataOfferClosed;
    private string? _gameDataError;
    private bool _gameDataCancelled;
    private bool _gameDataJustBuilt;
    // Kept in a field: a timer held only by a local can be collected before it
    // fires, which left "Game data ready" up for good (2026-10-14).
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _gameDataReadyTimer;

    private void InitGameData()
    {
        GameDataService.ProgressChanged += _ => DispatcherQueue.TryEnqueue(ShowGameData);
        GameDataService.BuildFinished += error => DispatcherQueue.TryEnqueue(() => OnGameDataBuilt(error));
        GameDataService.StateChanged += (_, _) => DispatcherQueue.TryEnqueue(ShowGameData);
        ShowGameData();
    }

    /// <summary>The setup page, with the menu hidden so nothing else can be
    /// opened until the game data exists (GameDataService.NeedsSetup).</summary>
    private void ShowSetup()
    {
        RootNav.IsPaneVisible = false;
        ContentFrame.Navigate(typeof(GameDataSetupPage));
    }

    private void ShowSaveSelection()
    {
        RootNav.IsPaneVisible = true;
        ContentFrame.Navigate(typeof(SaveFolderSelectPage));
        var item = FindNavItemByTag(RootNav.MenuItems, "SaveFolderSelect");
        if (item != null) RootNav.SelectedItem = item;
    }

    private void ShowGameData()
    {
        var bar = GameDataBar;
        var game = GameDataService.Game;
        GameDataProgress.Visibility = Visibility.Collapsed;
        GameDataButton.Visibility = Visibility.Visible;
        bar.IsClosable = true;

        void Show(InfoBarSeverity severity, string title, string message, string? button, bool offer = false)
        {
            bar.Severity = severity;
            bar.Title = title;
            bar.Message = message;
            if (button is null) GameDataButton.Visibility = Visibility.Collapsed;
            else GameDataButton.Content = button;
            // Offers get the warm attention colour; Cancel and Try again keep the accent.
            GameDataButton.Style = (Style)Application.Current.Resources[offer ? "ThemeAttentionButtonStyle" : "ThemeAccentButtonStyle"];
            bar.IsOpen = true;
            if (offer && _pulsedOffer != title) { _pulsedOffer = title; PulseGameDataButton(); }
        }

        // The setup page shows all of this itself.
        if (ContentFrame.CurrentSourcePageType == typeof(GameDataSetupPage) || GameDataService.NeedsSetup && !GameDataService.IsBuilding)
        {
            bar.IsOpen = false;
            return;
        }

        if (GameDataService.IsBuilding)
        {
            var p = GameDataService.Progress;
            GameDataProgress.Visibility = Visibility.Visible;
            GameDataProgress.Maximum = 100;
            GameDataProgress.Value = (p?.Fraction ?? 0) * 100;
            GameDataProgress.IsIndeterminate = p is null;
            bar.IsClosable = false;
            Show(InfoBarSeverity.Informational, "Reading your game files",
                p is null ? "Starting..." : $"Step {p.Step} of {p.Steps}: {p.Label}. You can keep using ArtifactX - item names and icons update when it's done.",
                "Cancel");
            return;
        }
        if (_gameDataCancelled)
        {
            Show(InfoBarSeverity.Informational, "Cancelled", "Your game data wasn't changed.", "Start again");
            return;
        }
        if (_gameDataError is { } error)
        {
            Show(InfoBarSeverity.Error, "Game data wasn't updated", error, "Try again");
            return;
        }
        if (_gameDataJustBuilt)
        {
            Show(InfoBarSeverity.Success, "Game data ready",
                $"Read from your No Man's Sky{(game is null ? "" : $" ({game.BuildLabel})")}.", null);
            return;
        }

        if (_gameDataOfferClosed) { bar.IsOpen = false; return; }
        switch (GameDataService.State)
        {
            case GameDataState.NotBuilt when game is null:
            case GameDataState.NoGameFound when !System.IO.File.Exists(GameDataService.CatalogPath):
                Show(InfoBarSeverity.Warning, "No Man's Sky wasn't found",
                    "ArtifactX reads item names, icons and recipes from your own copy of the game. Choose the folder No Man's Sky is installed in.",
                    "Choose folder", offer: true);
                break;
            case GameDataState.NotBuilt:
                Show(InfoBarSeverity.Informational, "Set up ArtifactX for your game",
                    "ArtifactX reads item names, icons, recipes and more from your own copy of No Man's Sky - nothing from the game comes with ArtifactX. "
                    + "It takes a minute or two, once.",
                    "Read my game files", offer: true);
                break;
            case GameDataState.GameUpdated:
                Show(InfoBarSeverity.Informational, "No Man's Sky has updated",
                    $"Your game is now {game?.BuildLabel}. Update ArtifactX's game data so new items and changes show up - it takes a minute or two and ArtifactX stays usable.",
                    "Update game data", offer: true);
                break;
            case GameDataState.BuilderUpdated:
                Show(InfoBarSeverity.Informational, "Update your game data",
                    "This version of ArtifactX reads more from your game files. Update the game data to get it - it takes a minute or two and ArtifactX stays usable.",
                    "Update game data", offer: true);
                break;
            default:
                bar.IsOpen = false;
                break;
        }
    }

    // The offer last pulsed: each new offer draws the eye once, not on every refresh.
    private string? _pulsedOffer;

    /// <summary>Three gentle pulses of the bar's button when a new offer appears
    /// (2026-10-16: a player missed "Update your game data" and wondered why ship
    /// names didn't show). A few pulses, not an endless one - it gets noticed
    /// without pulling the eye from whatever the player is doing. Skipped when
    /// Windows animations are off.</summary>
    private void PulseGameDataButton()
    {
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;
        var scale = new Microsoft.UI.Xaml.Media.ScaleTransform();
        GameDataButton.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        GameDataButton.RenderTransform = scale;
        var story = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            var pulse = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = new Microsoft.UI.Xaml.Media.Animation.RepeatBehavior(3),
                BeginTime = TimeSpan.FromMilliseconds(400),
            };
            var ease = new Microsoft.UI.Xaml.Media.Animation.SineEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut };
            pulse.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 1 });
            pulse.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(350), Value = 1.12, EasingFunction = ease });
            pulse.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(700), Value = 1, EasingFunction = ease });
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(pulse, scale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(pulse, property);
            story.Children.Add(pulse);
        }
        story.Begin();
    }

    /// <summary>Starts a game-data update from a page's own "Update now" link
    /// (where the missing data shows), as the bar's button does.</summary>
    public void StartGameDataUpdate() => GameDataButton_Click(GameDataButton, new RoutedEventArgs());

    private async void GameDataButton_Click(object sender, RoutedEventArgs e)
    {
        if (GameDataService.IsBuilding)
        {
            GameDataService.CancelBuild();
            return;
        }
        if (GameDataService.Game is null && !await PickGameFolderAsync()) return;

        _gameDataError = null;
        _gameDataCancelled = false;
        _gameDataJustBuilt = false;
        GameDataService.StartBuild();
        ShowGameData();
    }

    /// <summary>Asks for the game's folder. False when the player cancelled or
    /// picked a folder that isn't No Man's Sky (the bar then says so).</summary>
    private async Task<bool> PickGameFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return false;
        if (GameDataService.ChooseGameFolder(folder.Path)) return true;
        _gameDataError = $"\"{folder.Path}\" isn't a No Man's Sky folder. Choose the folder the game is installed in (the one with GAMEDATA and Binaries in it).";
        ShowGameData();
        return false;
    }

    private void GameDataBar_Closed(InfoBar sender, object args)
    {
        _gameDataOfferClosed = true;
        _gameDataError = null;
        _gameDataCancelled = false;
        _gameDataJustBuilt = false;
    }

    private void OnGameDataBuilt(string? error)
    {
        _gameDataCancelled = error is not null && error.StartsWith("Cancelled", StringComparison.Ordinal);
        _gameDataError = _gameDataCancelled ? null : error;
        _gameDataJustBuilt = error is null;
        if (error is null)
        {
            CatalogService.Reload();
            // First run done: on to the saves. Otherwise pages read the catalog
            // as they open, so re-open the current one.
            if (ContentFrame.CurrentSourcePageType == typeof(GameDataSetupPage))
                ShowSaveSelection();
            else if (ContentFrame.CurrentSourcePageType is Type page && page != typeof(SaveFolderSelectPage))
                ContentFrame.Navigate(page);
            // The "ready" note goes away on its own.
            _gameDataReadyTimer?.Stop();
            _gameDataReadyTimer = DispatcherQueue.CreateTimer();
            _gameDataReadyTimer.Interval = TimeSpan.FromSeconds(8);
            _gameDataReadyTimer.IsRepeating = false;
            _gameDataReadyTimer.Tick += (_, _) => { _gameDataJustBuilt = false; ShowGameData(); };
            _gameDataReadyTimer.Start();
        }
        ShowGameData();
    }
}
