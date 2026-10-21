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
using ArtifactX.WinUI3.Views;
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

public sealed partial class MainWindow : Window
{
    private AppWindow _appWindow;

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

        SaveSessionManager.ActiveSessionChanged += (_, _) =>
        {
            UpdateNavAvailability();
            SlotSwitchBar.IsOpen = false; // a save was opened or closed - nothing to offer
            ContextSwitchBar.IsOpen = false;
        };
        UpdateNavAvailability();
        SaveFolderWatchService.GameSavedOtherSlot += OfferSlotSwitch;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSavedLoadedSlot;
        LiveItemsService.Start();
        LiveItemsService.Changed += _ => UpdateLiveBadges();
        InitGameData();

        GameProcessMonitorService.RunningStateChanged += (_, isRunning) =>
            DispatcherQueue.TryEnqueue(() =>
                GameRunningBanner.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed);

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
        GameRunningBanner.Visibility = GameProcessMonitorService.IsGameRunning ? Visibility.Visible : Visibility.Collapsed;

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
        BaseStoragePathIcon.Data = G(NavIconGeometries.BaseStorage);
        CorvetteCachePathIcon.Data = G(NavIconGeometries.CorvetteCache);
        CompanionsPathIcon.Data = G(NavIconGeometries.Companions);
        SettlementsPathIcon.Data = G(NavIconGeometries.Settlements);
        SpaceStationsPathIcon.Data = G(NavIconGeometries.SpaceStations);
        PlanetsPathIcon.Data = G(NavIconGeometries.Planets);
        MilestonesPathIcon.Data = G(NavIconGeometries.Milestones);
        GekLanguagePathIcon.Data = G(NavIconGeometries.Language);
        VyKeenLanguagePathIcon.Data = G(NavIconGeometries.Language);
        KorvaxLanguagePathIcon.Data = G(NavIconGeometries.Language);
        AutophageLanguagePathIcon.Data = G(NavIconGeometries.Language);
        AtlasLanguagePathIcon.Data = G(NavIconGeometries.Language);
        CataloguePathIcon.Data = G(NavIconGeometries.Catalogue);
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
    /// theme - already-built elements keep the old theme's brushes. Removes
    /// the extra back-stack entry so Back still goes where the user expects.</summary>
    private void ReloadCurrentPage()
    {
        var pageType = ContentFrame.CurrentSourcePageType;
        if (pageType is null) return;

        ContentFrame.Navigate(pageType);
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
        if (args.SelectedItemContainer is NavigationViewItem selectedItem && selectedItem.Tag != null)
        {
            string tag = selectedItem.Tag.ToString();
            Type targetPageType = tag switch
            {
                "SaveFolderSelect" => typeof(SaveFolderSelectPage),
                "General" => typeof(GeneralPage),
                "Exosuit" => typeof(ExosuitPage),
                "MultiTool" => typeof(MultiToolPage),
                "Ships" => typeof(ShipsPage),
                "ShipDesigner" => typeof(ShipDesignerPage),
                "Freighter" => typeof(FreighterPage),
                "FreighterUpgrades" => typeof(FreighterUpgradesPage),
                "Frigate" => typeof(FrigatePage),
                "Squadron" => typeof(SquadronPage),
                "BaseStorage" => typeof(BaseStoragePage),
                "CorvetteCache" => typeof(CorvetteCachePage),
                "ColdStorage" => typeof(ColdStoragePage),
                "IngredientStorage" => typeof(IngredientStoragePage),
                "BaseItemCache" => typeof(BaseItemCachePage),
                "Companions" => typeof(CompanionsPage),
                "Settlement" => typeof(SettlementPage),
                "SpaceStations" => typeof(SpaceStationsPage),
                "Planets" => typeof(CurrentSystemPage),
                "PlanetDetails" => typeof(PlanetDetailsPage),
                "PlanetFinder" => typeof(PlanetFinderPage),
                "Bases" => typeof(BasesPage),
                "BaseDetails" => typeof(BaseDetailsPage),
                "Finds" => typeof(FindsPage),
                "Fishing" => typeof(FishingPage),
                "Recipes" => typeof(RecipesPage),
                "Crafting" => typeof(CraftingPage),
                "Guides" => typeof(GuidesPage),
                "Credits" => typeof(CreditsPage),
                "Settings" => typeof(SettingsPage),
                "ItemValues" => typeof(ItemValuesPage),
                "NearCore" => typeof(NearCorePage),
                "Milestones" => typeof(MilestonesPage),
                "AccountData" => typeof(AccountDataPage),
                "Guide" => typeof(GuidePage),
                "ExpeditionRewards" => typeof(ExpeditionRewardsPage),
                "TwitchRewards" => typeof(TwitchRewardsPage),
                "Catalogue" => typeof(CataloguePage),
                "FishingRecords" => typeof(FishingRecordsPage),
                "Exocraft" => typeof(ExocraftPage),
                "LanguageGek" => typeof(GekLanguagePage),
                "LanguageVyKeen" => typeof(VyKeenLanguagePage),
                "LanguageKorvax" => typeof(KorvaxLanguagePage),
                "LanguageAutophage" => typeof(AutophageLanguagePage),
                "LanguageAtlas" => typeof(AtlasLanguagePage),
                "AncestrySearch" => typeof(AncestrySearchView),
                "HGDecryption" => typeof(HGDecryptionPage),
                _ => typeof(SaveFolderSelectPage)
            };

            if (ContentFrame.CurrentSourcePageType != targetPageType)
            {
                ContentFrame.Navigate(targetPageType);
            }
        }
    }

    /// <summary>
    /// Toggles visibility of save-scoped nav items (Exosuit, Starship, etc., once
    /// they exist) based on whether a save is actively loaded in SaveSessionManager.
    /// Account Data is the one exception - it edits accountdata.hg, which lives
    /// once per platform-account folder rather than per save slot, so it only
    /// needs a platform chosen (SaveSessionManager.HasActivePlatform), not a
    /// specific slot loaded; a loaded slot still counts too, since that implies
    /// a platform was chosen along the way.
    /// </summary>
    private void UpdateNavAvailability()
    {
        GeneralNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        ExosuitNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        MultiToolNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        ShipsNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        ShipDesignerNavItem.Visibility = ShipsNavItem.Visibility;
        FreighterNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        FreighterUpgradesNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        FrigateNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        SquadronNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        BaseStorageNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        CorvetteCacheNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        ColdStorageNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        IngredientStorageNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        BaseItemCacheNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        // A reminder: how many different items are waiting in the cache (easy to
        // forget, user 2026-10-15) - kinds of item, not quantities.
        int cacheItems = SaveSessionManager.GetValue(ArtifactX.Core.NmsModels.NmsInventoryContainer.BaseItemCachePath)?[":No"] is Newtonsoft.Json.Linq.JArray cached ? cached.Count : 0;
        BaseItemCacheNavItem.InfoBadge = cacheItems > 0 ? new InfoBadge { Value = cacheItems } : null;
        ToolTipService.SetToolTip(BaseItemCacheNavItem, cacheItems > 0
            ? $"{cacheItems} item{(cacheItems == 1 ? "" : "s")} waiting from a dismantled base" : null);
        CompanionsNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        SettlementNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        SpaceStationsNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        // Explore and Reference work from ArtifactX's own game data and the
        // player's notes, so they're always there (looking up a recipe shouldn't
        // need a save open); only the pages that read the loaded save hide without one.
        ExploreNavItem.Visibility = Visibility.Visible;
        ReferenceNavItem.Visibility = Visibility.Visible;
        PlanetsNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        PlanetFinderNavItem.Visibility = PlanetsNavItem.Visibility;
        NearCoreNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        BasesNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        SaveEditorNavItem.Visibility = SaveSessionManager.IsSaveLoaded || SaveSessionManager.HasActivePlatform
            ? Visibility.Visible : Visibility.Collapsed;
        ExploreSeparator.Visibility = ExploreNavItem.Visibility;
        ReferenceSeparator.Visibility = ReferenceNavItem.Visibility;
        MilestonesNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        CatalogueNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        FishingRecordsNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        ExocraftNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;

        // Guide nests under AccountDataNavItem as a child (see that item's own
        // XAML comment) - hiding the parent already hides it, same as
        // Milestones' language children never getting their own individual
        // Visibility toggle here.
        AccountDataNavItem.Visibility = SaveSessionManager.IsSaveLoaded || SaveSessionManager.HasActivePlatform
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // The slot the game moved to, and one the player said no to (not offered
    // again on every autosave - only once the game moves somewhere else).
    private SaveSlotGroup? _offeredSlot;
    private (string? Folder, int SlotId)? _declinedSlot;

    /// <summary>The game saved a different slot of this account than the one
    /// loaded: offer to follow it, rather than switching on our own - the
    /// loaded save may have edits waiting (user request 2026-10-14).</summary>
    private void OfferSlotSwitch(SaveSlotGroup slot)
    {
        string? folder = Path.GetDirectoryName(slot.PrimaryFile.FullPath);
        if (_declinedSlot is { } no && no.SlotId == slot.SlotId && string.Equals(no.Folder, folder, StringComparison.OrdinalIgnoreCase)) return;
        _declinedSlot = null;
        _offeredSlot = slot;
        string loaded = SaveSessionManager.ActiveSlot is { } a ? $"{a.SlotLabel} ({a.PrimaryFile.SaveName})" : "another save";
        SlotSwitchBar.Message = $"The game is now saving to {slot.SlotLabel} ({slot.PrimaryFile.SaveName}). ArtifactX is showing {loaded}.";
        SlotSwitchButton.Content = $"Switch to {slot.SlotLabel}";
        SlotSwitchBar.IsOpen = true;
    }

    private void SlotSwitchBar_Closed(InfoBar sender, object args)
    {
        if (_offeredSlot is { } slot)
            _declinedSlot = (Path.GetDirectoryName(slot.PrimaryFile.FullPath), slot.SlotId);
        _offeredSlot = null;
    }

    private async void SlotSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_offeredSlot is not { } slot) return;
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

        SlotSwitchBar.IsOpen = false;
        _offeredSlot = null;
        await SaveSessionManager.LoadAsync(slot);
        // Rebuild the page on screen from the new save (pages read it as they open).
        if (ContentFrame.CurrentSourcePageType is Type page && page != typeof(SaveFolderSelectPage))
            ContentFrame.Navigate(page);
    }

    /// <summary>"New" counts on Multi-Tool and Starships for what the game added
    /// while it played (LiveItemsService), until each one is clicked.</summary>
    private void UpdateLiveBadges()
    {
        SetNewBadge(MultiToolNavItem, LiveItemsService.NewCount(LiveCollection.MultiTools), "multi-tool");
        SetNewBadge(ShipsNavItem, LiveItemsService.NewCount(LiveCollection.Ships), "starship");
    }

    private static void SetNewBadge(NavigationViewItem item, int count, string noun)
    {
        item.InfoBadge = count > 0 ? new InfoBadge { Value = count } : null;
        ToolTipService.SetToolTip(item, count > 0 ? $"{count} new {noun}{(count == 1 ? "" : "s")} in your game" : null);
    }

    // The context the player chose to keep editing despite the game moving
    // away from it - not offered again on every autosave.
    private bool? _declinedContextIsExpedition;
    private DispatcherQueueTimer? _contextBarFade;

    /// <summary>The game saved the loaded slot. Ordinary autosaves change
    /// nothing on screen (user rule 2026-10-16); only a move between the
    /// expedition and the main game does, since every page would otherwise
    /// show a character the game is no longer playing.</summary>
    private async void OnGameSavedLoadedSlot(JObject disk)
    {
        bool nowExpedition = SaveSessionManager.IsMidExpedition(disk);
        if (nowExpedition == SaveSessionManager.IsExpeditionActive)
        {
            _declinedContextIsExpedition = null;
            return;
        }
        if (_declinedContextIsExpedition == SaveSessionManager.IsExpeditionActive) return;

        string now = nowExpedition ? "You started an expedition" : "You left the expedition";
        if (!SaveSessionManager.HasUnsavedChanges)
        {
            await ReloadSaveInPlaceAsync();
            ShowContextBar(now, nowExpedition
                ? "ArtifactX is now showing your expedition character."
                : "ArtifactX is now showing your main game.", offerReload: false);
            return;
        }

        int count = SaveSessionManager.PendingEditCount;
        string was = SaveSessionManager.IsExpeditionActive ? "the expedition" : "your main game";
        ShowContextBar($"{now} in No Man's Sky",
            $"Your {count} unsaved {(count == 1 ? "change was" : "changes were")} made to {was}, which the game is no longer playing. " +
            "Reload to see what the game is playing now.", offerReload: true);
    }

    private void ShowContextBar(string title, string message, bool offerReload)
    {
        _contextBarFade?.Stop();
        ContextSwitchBar.Title = title;
        ContextSwitchBar.Message = message;
        ContextSwitchButton.Visibility = offerReload ? Visibility.Visible : Visibility.Collapsed;
        ContextSwitchBar.Opacity = 1;
        ContextSwitchBar.IsOpen = true;
        if (offerReload) return;

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
        if (ContextSwitchBar.Opacity == 0) ContextSwitchBar.IsOpen = false;
        ContextSwitchBar.Opacity = 1;
    }

    private void ContextSwitchBar_Closed(InfoBar sender, object args)
    {
        _contextBarFade?.Stop();
        if (ContextSwitchButton.Visibility == Visibility.Visible)
            _declinedContextIsExpedition = SaveSessionManager.IsExpeditionActive;
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
        ContextSwitchBar.IsOpen = false;
        await ReloadSaveInPlaceAsync();
    }

    /// <summary>Reads the loaded slot fresh from disk and rebuilds the page on
    /// screen from it, keeping the player where they are.</summary>
    private async Task ReloadSaveInPlaceAsync()
    {
        _declinedContextIsExpedition = null;
        await SaveSessionManager.ReloadFromDiskAsync();
        if (ContentFrame.CurrentSourcePageType is Type page && page != typeof(SaveFolderSelectPage))
            ReloadCurrentPage();
    }

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        string targetTag = e.SourcePageType switch
        {
            Type t when t == typeof(SaveFolderSelectPage) => "SaveFolderSelect",
            Type t when t == typeof(GeneralPage) => "General",
            Type t when t == typeof(ExosuitPage) => "Exosuit",
            Type t when t == typeof(MultiToolPage) => "MultiTool",
            Type t when t == typeof(ShipsPage) => "Ships",
            Type t when t == typeof(ShipDesignerPage) => "ShipDesigner",
            Type t when t == typeof(FreighterPage) => "Freighter",
            Type t when t == typeof(FreighterUpgradesPage) => "FreighterUpgrades",
            Type t when t == typeof(FrigatePage) => "Frigate",
            Type t when t == typeof(SquadronPage) => "Squadron",
            Type t when t == typeof(BaseStoragePage) => "BaseStorage",
            Type t when t == typeof(CorvetteCachePage) => "CorvetteCache",
            Type t when t == typeof(ColdStoragePage) => "ColdStorage",
            Type t when t == typeof(IngredientStoragePage) => "IngredientStorage",
            Type t when t == typeof(BaseItemCachePage) => "BaseItemCache",
            Type t when t == typeof(CompanionsPage) => "Companions",
            Type t when t == typeof(SettlementPage) => "Settlement",
            Type t when t == typeof(SpaceStationsPage) => "SpaceStations",
            Type t when t == typeof(CurrentSystemPage) => "Planets",
            Type t when t == typeof(PlanetDetailsPage) => "Planets",
            Type t when t == typeof(PlanetFinderPage) => "PlanetFinder",
            Type t when t == typeof(BasesPage) => "Bases",
            Type t when t == typeof(BaseDetailsPage) => "Bases",
            Type t when t == typeof(FindsPage) => "Finds",
            Type t when t == typeof(FishingPage) => "Fishing",
            Type t when t == typeof(RecipesPage) => "Recipes",
            Type t when t == typeof(CraftingPage) => "Crafting",
            Type t when t == typeof(GuidesPage) => "Guides",
            Type t when t == typeof(CreditsPage) => "Credits",
            Type t when t == typeof(SettingsPage) => "Settings",
            Type t when t == typeof(ItemValuesPage) => "ItemValues",
            Type t when t == typeof(NearCorePage) => "NearCore",
            Type t when t == typeof(MilestonesPage) => "Milestones",
            Type t when t == typeof(AccountDataPage) => "AccountData",
            Type t when t == typeof(GuidePage) => "Guide",
            Type t when t == typeof(ExpeditionRewardsPage) => "ExpeditionRewards",
            Type t when t == typeof(TwitchRewardsPage) => "TwitchRewards",
            Type t when t == typeof(CataloguePage) => "Catalogue",
            Type t when t == typeof(FishingRecordsPage) => "FishingRecords",
            Type t when t == typeof(ExocraftPage) => "Exocraft",
            Type t when t == typeof(GekLanguagePage) => "LanguageGek",
            Type t when t == typeof(VyKeenLanguagePage) => "LanguageVyKeen",
            Type t when t == typeof(KorvaxLanguagePage) => "LanguageKorvax",
            Type t when t == typeof(AutophageLanguagePage) => "LanguageAutophage",
            Type t when t == typeof(AtlasLanguagePage) => "LanguageAtlas",
            Type t when t == typeof(AncestrySearchView) => "AncestrySearch",
            Type t when t == typeof(HGDecryptionPage) => "HGDecryption",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(targetTag))
        {
            var targetItem = FindNavItemByTag(RootNav.MenuItems, targetTag)
                ?? FindNavItemByTag(RootNav.FooterMenuItems, targetTag);
            if (targetItem != null)
            {
                RootNav.SelectedItem = targetItem;
            }
        }
    }

    private NavigationViewItem? FindNavItemByTag(IList<object> itemsList, string targetTag)
    {
        foreach (var menuNode in itemsList)
        {
            if (menuNode is NavigationViewItem item)
            {
                if (item.Tag?.ToString() == targetTag)
                    return item;

                if (item.MenuItems.Count > 0)
                {
                    var foundSubChild = FindNavItemByTag(item.MenuItems, targetTag);
                    if (foundSubChild != null) return foundSubChild;
                }
            }
        }
        return null;
    }
}
