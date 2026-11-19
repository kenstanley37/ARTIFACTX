using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Resources;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using ArtifactX.WinUI3.Views.AppPages;
using ArtifactX.WinUI3.Views.SaveEditorPages;
using ArtifactX.WinUI3.Views.ExplorePages;
using ArtifactX.WinUI3.Views.InspectionPages;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.UI;
using WinRT.Interop;

namespace ArtifactX.WinUI3;

/// <summary>
/// The main window: the title bar, the nav and the page frame. Its state - which nav items show, the badges, the
/// game-data bar and the "the game moved" notices - is <see cref="ViewModel"/> (MainWindowViewModel). This class wires
/// the app-wide services up at start, shows the startup, close and switch dialogs, reloads pages and animates.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; } = new();

    private AppWindow _appWindow = null!;

    // Set right before a programmatic re-Close() following user confirmation,
    // so MainWindow_Closed lets that second attempt through instead of
    // re-prompting - Closed fires again on every Close() call, confirmed or not.
    private bool _closeConfirmed;

    // Guards the disclaimer check below so it only runs once - Content.Loaded
    // can in principle fire more than once (e.g. a theme/resource reload).
    private bool _startupChecksDone;

    public AppTitleBar TitleBar => CustomTitleBar;

    /// <summary>The nav sections that open and close, by the key their state is saved under.</summary>
    private (string Key, NavigationViewItem Item)[] ExpandableNavItems => new[]
    {
        ("SaveEditor", SaveEditorNavItem),
        ("AccountData", AccountDataNavItem),
        ("Milestones", MilestonesNavItem),
        ("Tools", ExploreNavItem), // the old "Tools" key, so its saved open/closed state carries over
        ("Reference", ReferenceNavItem),
        ("StructuralAnalysis", StructuralAnalysisNavItem),
    };

    /// <summary>Puts the nav back the way the player left it - which sections
    /// were open, and whether the pane was open - and keeps it saved as they
    /// change it (user, 2026-10-13: re-collapsing Save Editor and opening
    /// Tools on every launch). Per profile, so it travels with their backups.</summary>
    private void RestoreNavLayout()
    {
        foreach (var (key, item) in ExpandableNavItems)
            if (SettingsStore.Get(SettingsStore.Scope.Profile, $"NavExpanded.{key}") is string saved)
                item.IsExpanded = saved == "1";
        if (SettingsStore.Get(SettingsStore.Scope.Profile, "NavPaneOpen") is string pane)
            RootNav.IsPaneOpen = pane == "1";

        RootNav.Expanding += (_, e) => SaveNavExpanded(e.ExpandingItemContainer, true);
        RootNav.Collapsed += (_, e) => SaveNavExpanded(e.CollapsedItemContainer, false);
        RootNav.PaneOpened += (_, _) => SettingsStore.Set(SettingsStore.Scope.Profile, "NavPaneOpen", "1");
        RootNav.PaneClosed += (_, _) => SettingsStore.Set(SettingsStore.Scope.Profile, "NavPaneOpen", "0");
    }

    private void SaveNavExpanded(NavigationViewItemBase? container, bool expanded)
    {
        foreach (var (key, item) in ExpandableNavItems)
            if (ReferenceEquals(item, container))
                SettingsStore.Set(SettingsStore.Scope.Profile, $"NavExpanded.{key}", expanded ? "1" : "0");
    }

    public MainWindow()
    {
        InitializeComponent();

        SetNavIconGeometries();
        ThemeService.ThemeChanged += (_, _) => ApplyThemeToFrame();
        RestoreNavLayout();

#if !DEBUG
        // Dev-only tools for reverse-engineering raw JSON field mappings - not
        // something a real end user should ever see or need. This whole block
        // is compiled out of Debug builds, so during development the nav stays
        // visible by default (the XAML doesn't set Collapsed itself); a Release
        // build always runs this and hides both the separator and the section.
        StructuralAnalysisSeparator.Visibility = Visibility.Collapsed;
        StructuralAnalysisNavItem.Visibility = Visibility.Collapsed;
#endif

        InitializeCustomTitleBar();
        WindowPlacementService.Restore(WindowNative.GetWindowHandle(this));

        Closed += MainWindow_Closed;

        // Content.Loaded (not Activated) - Activated depends on OS-level
        // window-focus timing and, on a real run, never fired this dialog at
        // all (root-caused to an unobserved exception from the old fire-and-
        // forget "_ = ShowDisclaimerIfNeededAsync()" call swallowing whatever
        // went wrong). Loaded fires once Content is actually in the live
        // visual tree, which is the real precondition for XamlRoot being
        // usable - not dependent on OS focus at all - and this method is now
        // awaited properly with its own try/catch instead of discarded.
        ((FrameworkElement)Content).Loaded += MainWindow_ContentLoaded;

        SaveSessionManager.ActiveSessionChanged += (_, _) => ViewModel.SessionChanged();
        ViewModel.RefreshNav();
        SaveFolderWatchService.GameSavedOtherSlot += ViewModel.OfferSlotSwitch;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSavedLoadedSlot;
        LiveItemsService.Start();
        LiveItemsService.Changed += _ => ViewModel.RefreshLiveBadges();
        InitGameData();

        GameProcessMonitorService.RunningStateChanged += (_, isRunning) =>
            DispatcherQueue.TryEnqueue(() => ViewModel.IsGameRunning = isRunning);

        // Start() calls Poll() synchronously before returning - if ArtifactX is already
        // running, the state flips false->true and the event fires *during* this
        // call. Subscribing above first means we never miss that one-time event,
        // but this explicit sync afterward covers the same case even if that
        // ordering ever gets disturbed again - it doesn't depend on catching a
        // transition, just reads whatever the current state actually is.
        GameProcessMonitorService.Start(DispatcherQueue);
        // Where fish are caught is learned from each new save, on every page (FishCatchRecorder).
        FishCatchRecorder.Start(DispatcherQueue);
        // Off unless turned on in Settings (GitHub #18, step 1).
        ScreenCaptureLogService.Start();
        ViewModel.IsGameRunning = GameProcessMonitorService.IsGameRunning;

        _ = RunStartupUpdateCheckAsync();

        // No game data yet: set up first - a save can't be opened without it.
        if (GameDataService.NeedsSetup) ShowSetup();
        else ShowSaveSelection();
    }

    // Assigns each nav PathIcon's Data from the raw path strings in
    // NavIconGeometries, converted via the same string->Geometry conversion
    // XAML itself uses for a literal Data="M..." attribute. Done here in
    // code-behind (not as XAML Geometry resources) because a keyed
    // <Geometry x:Key="..."> resource crashed natively inside
    // Microsoft.UI.Xaml.dll at runtime the moment PathIcon.Data resolved it
    // via {StaticResource}, despite compiling cleanly - see
    // project_nav_icons.md.
    private void SetNavIconGeometries()
    {
        static Geometry G(string data) => (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data);

        ExosuitPathIcon.Data = G(NavIconGeometries.Exosuit);
        MultiToolPathIcon.Data = G(NavIconGeometries.MultiTool);
        StarshipsPathIcon.Data = G(NavIconGeometries.Starships);
        FreighterPathIcon.Data = G(NavIconGeometries.Freighter);
        FreighterUpgradesPathIcon.Data = G(NavIconGeometries.Freighter);
        FrigatesPathIcon.Data = G(NavIconGeometries.Frigates);
        SquadronPathIcon.Data = G(NavIconGeometries.Squadron);
        CargoPathIcon.Data = G(NavIconGeometries.BaseStorage);
        CompanionsPathIcon.Data = G(NavIconGeometries.Companions);
        CreatureFinderPathIcon.Data = G(NavIconGeometries.Companions);
        SettlementsPathIcon.Data = G(NavIconGeometries.Settlements);
        SpaceStationsPathIcon.Data = G(NavIconGeometries.SpaceStations);
        PlanetsPathIcon.Data = G(NavIconGeometries.Planets);
        MilestonesPathIcon.Data = G(NavIconGeometries.Milestones);
        GekLanguagePathIcon.Data = G(NavIconGeometries.Language);
        VyKeenLanguagePathIcon.Data = G(NavIconGeometries.Language);
        KorvaxLanguagePathIcon.Data = G(NavIconGeometries.Language);
        AutophageLanguagePathIcon.Data = G(NavIconGeometries.Language);
        AtlasLanguagePathIcon.Data = G(NavIconGeometries.Language);
        GameCatalogPathIcon.Data = G(NavIconGeometries.Catalogue);
        FishingRecordsPathIcon.Data = G(NavIconGeometries.FishingRecords);
        FishingPathIcon.Data = G(NavIconGeometries.FishingRecords);
        ExocraftPathIcon.Data = G(NavIconGeometries.Exocraft);
    }

    // Fire-and-observed (not fire-and-forget) - RefreshAsync itself already
    // catches network/parse failures internally and turns them into an
    // Error UpdateCheckResult rather than throwing, but this still logs
    // anything truly unexpected instead of letting it vanish silently, same
    // discipline as ShowDisclaimerIfNeededAsync below after that bug.
    private async Task RunStartupUpdateCheckAsync()
    {
        try
        {
            await UpdateCheckService.RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Startup update check failed: {ex}");
        }
    }

    /// <summary>Fires once Content is actually in the live visual tree - the
    /// real precondition for XamlRoot being usable, unlike Activated (see the
    /// constructor's comment on why that was the original, unreliable choice).</summary>
    private async void MainWindow_ContentLoaded(object sender, RoutedEventArgs e)
    {
        if (_startupChecksDone) return;
        _startupChecksDone = true;

        try
        {
            await ShowDisclaimerIfNeededAsync();
        }
        catch (Exception ex)
        {
            // This ran as a discarded "_ = ShowDisclaimerIfNeededAsync()" the
            // first time this shipped and never showed at all - an exception
            // here (e.g. XamlRoot not ready yet) was silently swallowed with
            // nothing surfaced anywhere. Now properly awaited AND logged, so
            // a repeat of that failure mode is at least visible in the
            // Output window instead of invisible.
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Disclaimer dialog failed: {ex}");
        }
    }

    /// <summary>First-launch-only warning that using this unofficial, third-party
    /// save editor is at the user's own risk. Declining (the Close button, or
    /// ESC - WinUI maps both to the same ContentDialogResult.None) closes the
    /// app outright rather than letting it be dismissed and continuing
    /// unacknowledged. AppSettingsService persists acceptance so it never
    /// shows again once accepted.</summary>
    private async Task ShowDisclaimerIfNeededAsync()
    {
        if (AppSettingsService.HasAcceptedDisclaimer) return;

        var dialog = new ContentDialog
        {
            Title = "Before You Continue",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
                Text = "ArtifactX is an unofficial, third-party save editor for No Man's Sky - it is not made by, " +
                       "affiliated with, or endorsed by Hello Games.\n\n" +
                       "Editing your save files carries an inherent risk of corruption or lost progress. ArtifactX " +
                       "automatically creates a timestamped backup of a save before it overwrites it, which you can " +
                       "restore from the Save Selection page if something goes wrong - but you are using this " +
                       "software entirely at your own risk, and its developers are not responsible for any data " +
                       "loss or other issues that result from its use.\n\n" +
                       "By clicking \"I Understand and Accept\" below, you acknowledge and accept this. If you'd " +
                       "rather not, click \"Decline\" and the app will close."
            },
            PrimaryButtonText = "I Understand and Accept",
            CloseButtonText = "Decline",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            AppSettingsService.SetDisclaimerAccepted();
        }
        else
        {
            Close();
        }
    }

    /// <summary>Blocks closing while there's a staged edit nobody's committed
    /// yet - without this, alt-F4 or the titlebar X silently discards
    /// whatever's pending with zero warning, the same edits the in-app Reset
    /// button treats as important enough to ask about. Save/Discard mirrors
    /// AppTitleBar's own Save button, including staying disabled while No
    /// Man's Sky is running (SaveSessionManager.CommitAsync isn't safe to
    /// call then either). Also covers AccountSessionManager - a completely
    /// separate staged-edit set (see that class's doc comment) that this
    /// same close path would otherwise silently drop with no warning at all.</summary>
    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // Saved on every entry (including the re-entrant call after a
        // confirmed close below) - the window's bounds haven't changed
        // between then and now, so it's harmless to save more than once,
        // and this way every real closing path is covered without needing
        // its own explicit save call.
        WindowPlacementService.Save(WindowNative.GetWindowHandle(this));

        bool slotDirty = SaveSessionManager.HasUnsavedChanges;
        bool accountDirty = AccountSessionManager.HasUnsavedChanges;

        if (_closeConfirmed || (!slotDirty && !accountDirty))
            return;

        args.Handled = true;

        bool canSave = !GameProcessMonitorService.IsGameRunning;

        string what = (slotDirty, accountDirty) switch
        {
            (true, true) => $"{SaveSessionManager.ActiveLabel} and your account-wide data",
            (true, false) => SaveSessionManager.ActiveLabel!,
            _ => "your account-wide data"
        };

        var dialog = new ContentDialog
        {
            Title = "Unsaved changes",
            Content = canSave
                ? $"You have unsaved changes to {what}. Save before closing?"
                : $"You have unsaved changes to {what}. No Man's Sky is currently " +
                  "running, so saving is disabled right now - closing will discard these changes.",
            PrimaryButtonText = canSave ? "Save" : null,
            SecondaryButtonText = "Discard and Close",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && canSave)
        {
            try
            {
                if (slotDirty) await SaveSessionManager.CommitAsync();
                if (accountDirty) await AccountSessionManager.CommitAsync();
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "Save failed",
                    Content = $"{ex.Message}\n\nThe app will stay open so you don't lose these changes.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                }.ShowAsync();
                return;
            }
        }
        else if (result != ContentDialogResult.Secondary)
        {
            return; // Cancel, or dismissed without a choice - stay open
        }

        _closeConfirmed = true;
        Close();
    }

    /// <summary>Frame elements (title bar, nav pane) live for the whole session,
    /// so a swapped theme dictionary alone doesn't reach them. Flipping
    /// RequestedTheme forces every {ThemeResource} in the tree to re-resolve
    /// against the new dictionary; it's flipped straight back to Dark in the
    /// same call, so no light frame is ever rendered.</summary>
    private void ApplyThemeToFrame()
    {
        RootGrid.RequestedTheme = ElementTheme.Light;
        RootGrid.RequestedTheme = ElementTheme.Dark;
        ReloadCurrentPage();
    }

    /// <summary>Re-creates the current page so it picks up a newly applied
    /// theme, rebuilt game data or another save - already-built elements keep
    /// what they read. Passes the page's own navigation parameter again (Planet
    /// Details opened empty without its planet, 2026-11-11) and removes the extra
    /// back-stack entry so Back still goes where the user expects.</summary>
    private void ReloadCurrentPage()
    {
        var pageType = ContentFrame.CurrentSourcePageType;
        if (pageType is null) return;

        ContentFrame.Navigate(pageType, _currentPageParameter);
        if (ContentFrame.BackStackDepth > 0)
            ContentFrame.BackStack.RemoveAt(ContentFrame.BackStackDepth - 1);
    }

    private void InitializeCustomTitleBar()
    {
        IntPtr windowHandle = WindowNative.GetWindowHandle(this);
        WindowId windowId = new WindowId((ulong)windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Color.FromArgb(255, 240, 243, 248);
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 20, 26, 39);
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 255, 157, 0);
        }
    }

    private void RootNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem selectedItem && selectedItem.Tag is not null)
        {
            Type targetPageType = NavigationService.ResolvePageType(selectedItem.Tag.ToString());
            if (ContentFrame.CurrentSourcePageType != targetPageType)
            {
                ContentFrame.Navigate(targetPageType);
            }
        }
    }

    private void SlotSwitchBar_Closed(InfoBar sender, object args) => ViewModel.SlotSwitchDeclined();

    private async void SlotSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.OfferedSlot is not { } slot) return;
        if (SaveSessionManager.HasUnsavedChanges)
        {
            var confirm = new ContentDialog
            {
                Title = "Switch saves?",
                Content = $"You have unsaved changes to {SaveSessionManager.ActiveLabel}. Switching to {slot.SlotLabel} discards them.",
                PrimaryButtonText = "Discard and switch",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }

        ViewModel.TakeSlotSwitch();
        await SaveSessionManager.LoadAsync(slot);
        // Rebuild the page on screen from the new save (pages read it as they open).
        if (ContentFrame.CurrentSourcePageType is Type page && page != typeof(SaveFolderSelectPage))
            ReloadCurrentPage();
    }

    private DispatcherQueueTimer? _contextBarFade;

    /// <summary>The game moved the loaded save between an expedition and the main game (MainWindowViewModel decides):
    /// reload and say so, or offer the reload when there are edits.</summary>
    private async void OnGameSavedLoadedSlot(JObject disk)
    {
        if (ViewModel.OnGameSavedLoadedSlot(disk) is not { } change) return;
        if (change.ReloadFirst) await ReloadSaveInPlaceAsync();
        ShowContextBar(change);
    }

    private void ShowContextBar(ContextChange change)
    {
        _contextBarFade?.Stop();
        ContextSwitchBar.Opacity = 1;
        ViewModel.ShowContext(change);
        if (change.OfferReload) return;

        // A notice, not a question: it fades out on its own.
        if (_contextBarFade is null)
        {
            _contextBarFade = DispatcherQueue.CreateTimer();
            _contextBarFade.IsRepeating = false;
            _contextBarFade.Interval = TimeSpan.FromSeconds(8);
            _contextBarFade.Tick += FadeContextBar;
        }
        _contextBarFade.Start();
    }

    private async void FadeContextBar(DispatcherQueueTimer timer, object args)
    {
        ContextSwitchBar.Opacity = 0; // OpacityTransition animates it
        await Task.Delay(600);
        if (ContextSwitchBar.Opacity == 0) ViewModel.ContextOpen = false;
        ContextSwitchBar.Opacity = 1;
    }

    private void ContextSwitchBar_Closed(InfoBar sender, object args)
    {
        _contextBarFade?.Stop();
        ViewModel.ContextClosed();
    }

    private async void ContextSwitchReload_Click(object sender, RoutedEventArgs e)
    {
        if (SaveSessionManager.HasUnsavedChanges)
        {
            var confirm = new ContentDialog
            {
                Title = "Reload this save?",
                Content = $"Your unsaved changes to {SaveSessionManager.ActiveLabel} will be discarded.",
                PrimaryButtonText = "Discard and reload",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }
        ViewModel.ContextOpen = false;
        await ReloadSaveInPlaceAsync();
    }

    /// <summary>Reads the loaded slot fresh from disk and rebuilds the page on
    /// screen from it, keeping the player where they are.</summary>
    private async Task ReloadSaveInPlaceAsync()
    {
        ViewModel.ContextReloaded();
        await SaveSessionManager.ReloadFromDiskAsync();
        if (ContentFrame.CurrentSourcePageType is Type page && page != typeof(SaveFolderSelectPage))
            ReloadCurrentPage();
    }

    /// <summary>The parameter the page on screen was opened with (a planet for Planet Details), so
    /// ReloadCurrentPage can open it again the same way.</summary>
    private object? _currentPageParameter;

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _currentPageParameter = e.Parameter;
        string targetTag = NavigationService.ResolveNavTag(e.SourcePageType);
        if (!string.IsNullOrEmpty(targetTag))
        {
            var targetItem = NavigationService.FindNavItemByTag(RootNav.MenuItems, targetTag)
                ?? NavigationService.FindNavItemByTag(RootNav.FooterMenuItems, targetTag);
            if (targetItem != null)
            {
                RootNav.SelectedItem = targetItem;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Game-data bar (ArtifactX builds its item catalog from the player's own
    // No Man's Sky files via GameDataService, never blocking save editing).
    // -----------------------------------------------------------------------

    private void InitGameData()
    {
        ViewModel.GameData.IsOnSetupPage = () => ContentFrame.CurrentSourcePageType == typeof(GameDataSetupPage);
        ViewModel.GameData.OfferShown += PulseGameDataButton;
        GameDataService.ProgressChanged += _ => DispatcherQueue.TryEnqueue(() => ViewModel.GameData.Refresh());
        GameDataService.BuildFinished += error => DispatcherQueue.TryEnqueue(() => OnGameDataBuilt(error));
        GameDataService.StateChanged += (_, _) => DispatcherQueue.TryEnqueue(() => ViewModel.GameData.Refresh());
        ViewModel.GameData.Refresh();
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
        var item = NavigationService.FindNavItemByTag(RootNav.MenuItems, "SaveFolderSelect");
        if (item != null) RootNav.SelectedItem = item;
    }

    /// <summary>Three gentle pulses of the bar's button when a new offer appears
    /// (2026-10-16: a player missed "Update your game data" and wondered why ship
    /// names didn't show). A few pulses, not an endless one - it gets noticed
    /// without pulling the eye from whatever the player is doing. Skipped when
    /// Windows animations are off.</summary>
    private void PulseGameDataButton()
    {
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;
        var scale = new ScaleTransform();
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
        ViewModel.GameData.StartBuild();
    }

    /// <summary>Asks for the game's folder. False when the player cancelled or
    /// picked a folder that isn't No Man's Sky (the bar then says so).</summary>
    private async Task<bool> PickGameFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return false;
        if (GameDataService.ChooseGameFolder(folder.Path)) return true;
        ViewModel.GameData.NotAGameFolder(folder.Path);
        return false;
    }

    private void GameDataBar_Closed(InfoBar sender, object args) => ViewModel.GameData.Closed();

    /// <summary>A build ended. Done: the catalog is re-read; first run goes on to the saves, otherwise the page on
    /// screen re-opens, since pages read the catalog as they open.</summary>
    private void OnGameDataBuilt(string? error)
    {
        if (error is null)
        {
            CatalogService.Reload();
            if (ContentFrame.CurrentSourcePageType == typeof(GameDataSetupPage))
                ShowSaveSelection();
            else if (ContentFrame.CurrentSourcePageType is Type page && page != typeof(SaveFolderSelectPage))
                ReloadCurrentPage();
        }
        ViewModel.GameData.Built(error);
    }
}
