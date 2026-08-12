using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Resources;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.Views;
using ArtifactX.WinUI3.Views.InspectionPages;
using System;
using System.Collections.Generic;
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

    public MainWindow()
    {
        InitializeComponent();

        SetNavIconGeometries();

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

        SaveSessionManager.ActiveSessionChanged += (_, _) => UpdateNavAvailability();
        UpdateNavAvailability();

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
        GameRunningBanner.Visibility = GameProcessMonitorService.IsGameRunning ? Visibility.Visible : Visibility.Collapsed;

        _ = RunStartupUpdateCheckAsync();

        ContentFrame.Navigate(typeof(SaveFolderSelectPage));

        var defaultItem = FindNavItemByTag(RootNav.MenuItems, "SaveFolderSelect");
        if (defaultItem != null) RootNav.SelectedItem = defaultItem;
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
        MilestonesPathIcon.Data = G(NavIconGeometries.Milestones);
        GekLanguagePathIcon.Data = G(NavIconGeometries.Language);
        VyKeenLanguagePathIcon.Data = G(NavIconGeometries.Language);
        KorvaxLanguagePathIcon.Data = G(NavIconGeometries.Language);
        AutophageLanguagePathIcon.Data = G(NavIconGeometries.Language);
        AtlasLanguagePathIcon.Data = G(NavIconGeometries.Language);
        CataloguePathIcon.Data = G(NavIconGeometries.Catalogue);
        FishingRecordsPathIcon.Data = G(NavIconGeometries.FishingRecords);
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
                "Freighter" => typeof(FreighterPage),
                "FreighterUpgrades" => typeof(FreighterUpgradesPage),
                "Frigate" => typeof(FrigatePage),
                "Squadron" => typeof(SquadronPage),
                "BaseStorage" => typeof(BaseStoragePage),
                "CorvetteCache" => typeof(CorvetteCachePage),
                "Companions" => typeof(CompanionsPage),
                "Settlement" => typeof(SettlementPage),
                "Milestones" => typeof(MilestonesPage),
                "AccountData" => typeof(AccountDataPage),
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
        FreighterNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        FreighterUpgradesNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        FrigateNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        SquadronNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        BaseStorageNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        CorvetteCacheNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        CompanionsNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        SettlementNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        MilestonesNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        CatalogueNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        FishingRecordsNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        ExocraftNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;

        AccountDataNavItem.Visibility = SaveSessionManager.IsSaveLoaded || SaveSessionManager.HasActivePlatform
            ? Visibility.Visible
            : Visibility.Collapsed;
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
            Type t when t == typeof(FreighterPage) => "Freighter",
            Type t when t == typeof(FreighterUpgradesPage) => "FreighterUpgrades",
            Type t when t == typeof(FrigatePage) => "Frigate",
            Type t when t == typeof(SquadronPage) => "Squadron",
            Type t when t == typeof(BaseStoragePage) => "BaseStorage",
            Type t when t == typeof(CorvetteCachePage) => "CorvetteCache",
            Type t when t == typeof(CompanionsPage) => "Companions",
            Type t when t == typeof(SettlementPage) => "Settlement",
            Type t when t == typeof(MilestonesPage) => "Milestones",
            Type t when t == typeof(AccountDataPage) => "AccountData",
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
            var targetItem = FindNavItemByTag(RootNav.MenuItems, targetTag);
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