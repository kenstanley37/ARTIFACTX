using ArtifactX.GameData;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The game-data bar over every page: ArtifactX builds its item catalog from the player's own No Man's Sky files
/// (GameDataService) without blocking save editing. Says when to set up or update, shows a build's progress and elapsed
/// time, and how it ended (ready, cancelled, or the error). The window draws it, picks the game folder and pulses the
/// button when a new offer appears (OfferShown).
/// </summary>
public sealed partial class GameDataBarViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    // An offer the player closed stays closed for this session.
    private bool _offerClosed;
    private string? _error;
    private bool _cancelled;
    private bool _justBuilt;
    // Kept in a field: a timer held only by a local can be collected before it fires, which left "Game data ready" up for
    // good (2026-10-14).
    private DispatcherQueueTimer? _readyTimer;
    // The offer last pulsed: each new offer draws the eye once, not on every refresh.
    private string? _pulsedOffer;

    /// <summary>Whether the setup page is on screen - it shows all of this itself.</summary>
    public Func<bool> IsOnSetupPage { get; set; } = () => false;

    /// <summary>A new offer appeared: the window pulses the button (2026-10-16: a player missed "Update your game data"
    /// and wondered why ship names didn't show).</summary>
    public event Action? OfferShown;

    [ObservableProperty] public partial bool IsOpen { get; set; }
    [ObservableProperty] public partial bool IsClosable { get; private set; } = true;
    [ObservableProperty] public partial InfoBarSeverity Severity { get; private set; }
    [ObservableProperty] public partial string Title { get; private set; } = "";
    [ObservableProperty] public partial string Message { get; private set; } = "";
    [ObservableProperty] public partial string ButtonText { get; private set; } = "";
    [ObservableProperty] public partial bool ShowButton { get; private set; }

    /// <summary>Offers get the warm attention colour; Cancel and Try again keep the accent.</summary>
    [ObservableProperty] public partial Style? ButtonStyle { get; private set; }

    [ObservableProperty] public partial bool ShowProgress { get; private set; }
    [ObservableProperty] public partial double ProgressValue { get; private set; }
    [ObservableProperty] public partial bool ProgressIndeterminate { get; private set; }

    private void Show(InfoBarSeverity severity, string title, string message, string? button, bool offer = false)
    {
        Severity = severity;
        Title = title;
        Message = message;
        ShowButton = button is not null;
        ButtonText = button ?? "";
        ButtonStyle = (Style)Application.Current.Resources[offer ? "ThemeAttentionButtonStyle" : "ThemeAccentButtonStyle"];
        IsOpen = true;
        if (offer && _pulsedOffer != title)
        {
            _pulsedOffer = title;
            OfferShown?.Invoke();
        }
    }

    public void Refresh()
    {
        var game = GameDataService.Game;
        ShowProgress = false;
        IsClosable = true;

        if (IsOnSetupPage() || GameDataService.NeedsSetup && !GameDataService.IsBuilding)
        {
            IsOpen = false;
            return;
        }
        if (GameDataService.IsBuilding)
        {
            var p = GameDataService.Progress;
            ShowProgress = true;
            ProgressValue = (p?.Fraction ?? 0) * 100;
            ProgressIndeterminate = p is null;
            IsClosable = false;
            Show(InfoBarSeverity.Informational, "Reading your game files",
                (p is null ? "Starting..." : $"Step {p.Step} of {p.Steps}: {p.Label}.")
                    + (GameDataService.BuildElapsed is { } running ? $" {GameDataService.FormatElapsed(running)} so far." : "")
                    + " You can keep using ArtifactX - item names and icons update when it's done.",
                "Cancel");
            return;
        }
        if (_cancelled)
        {
            Show(InfoBarSeverity.Informational, "Cancelled", "Your game data wasn't changed.", "Start again");
            return;
        }
        if (_error is { } error)
        {
            Show(InfoBarSeverity.Error, "Game data wasn't updated", error, "Try again");
            return;
        }
        if (_justBuilt)
        {
            Show(InfoBarSeverity.Success, "Game data ready",
                $"Read from your No Man's Sky{(game is null ? "" : $" ({game.BuildLabel})")}"
                + (GameDataService.LastBuildDuration is { } took ? $" in {GameDataService.FormatElapsed(took)}." : "."), null);
            return;
        }

        if (_offerClosed) { IsOpen = false; return; }
        switch (GameDataService.State)
        {
            case GameDataState.NotBuilt when game is null:
            case GameDataState.NoGameFound when !File.Exists(GameDataService.CatalogPath):
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
                IsOpen = false;
                break;
        }
    }

    /// <summary>Starts a build (the game folder already known).</summary>
    public void StartBuild()
    {
        _error = null;
        _cancelled = false;
        _justBuilt = false;
        GameDataService.StartBuild();
        Refresh();
    }

    /// <summary>The picked folder isn't No Man's Sky: the bar says so.</summary>
    public void NotAGameFolder(string path)
    {
        _error = $"\"{path}\" isn't a No Man's Sky folder. Choose the folder the game is installed in (the one with GAMEDATA and Binaries in it).";
        Refresh();
    }

    /// <summary>The player closed the bar.</summary>
    public void Closed()
    {
        _offerClosed = true;
        _error = null;
        _cancelled = false;
        _justBuilt = false;
    }

    /// <summary>A build ended; null error means it worked, and the "ready" note goes away on its own.</summary>
    public void Built(string? error)
    {
        _cancelled = error is not null && error.StartsWith("Cancelled", StringComparison.Ordinal);
        _error = _cancelled ? null : error;
        _justBuilt = error is null;
        if (error is null)
        {
            _readyTimer?.Stop();
            _readyTimer = _dispatcher.CreateTimer();
            _readyTimer.Interval = TimeSpan.FromSeconds(8);
            _readyTimer.IsRepeating = false;
            _readyTimer.Tick += (_, _) => { _justBuilt = false; Refresh(); };
            _readyTimer.Start();
        }
        Refresh();
    }
}
