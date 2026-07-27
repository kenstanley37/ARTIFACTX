using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.Views;
using ArtifactX.WinUI3.Views.InspectionPages;
using System;
using System.Collections.Generic;
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

    public AppTitleBar TitleBar => CustomTitleBar;

    public MainWindow()
    {
        InitializeComponent();
        InitializeCustomTitleBar();
        WindowPlacementService.Restore(WindowNative.GetWindowHandle(this));

        Closed += MainWindow_Closed;

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

        ContentFrame.Navigate(typeof(SaveFolderSelectPage));

        var defaultItem = FindNavItemByTag(RootNav.MenuItems, "SaveFolderSelect");
        if (defaultItem != null) RootNav.SelectedItem = defaultItem;
    }

    /// <summary>Blocks closing while there's a staged edit nobody's committed
    /// yet - without this, alt-F4 or the titlebar X silently discards
    /// whatever's pending with zero warning, the same edits the in-app Reset
    /// button treats as important enough to ask about. Save/Discard mirrors
    /// AppTitleBar's own Save button, including staying disabled while No
    /// Man's Sky is running (SaveSessionManager.CommitAsync isn't safe to
    /// call then either).</summary>
    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // Saved on every entry (including the re-entrant call after a
        // confirmed close below) - the window's bounds haven't changed
        // between then and now, so it's harmless to save more than once,
        // and this way every real closing path is covered without needing
        // its own explicit save call.
        WindowPlacementService.Save(WindowNative.GetWindowHandle(this));

        if (_closeConfirmed || !SaveSessionManager.HasUnsavedChanges)
            return;

        args.Handled = true;

        bool canSave = !GameProcessMonitorService.IsGameRunning;

        var dialog = new ContentDialog
        {
            Title = "Unsaved changes",
            Content = canSave
                ? $"You have unsaved changes to {SaveSessionManager.ActiveLabel}. Save before closing?"
                : $"You have unsaved changes to {SaveSessionManager.ActiveLabel}. No Man's Sky is currently " +
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
                await SaveSessionManager.CommitAsync();
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
                "BaseStorage" => typeof(BaseStoragePage),
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
    /// Currently a no-op - there's nothing to gate until those pages are added.
    /// </summary>
    private void UpdateNavAvailability()
    {
        GeneralNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        ExosuitNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        MultiToolNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        ShipsNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        FreighterNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
        BaseStorageNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
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
            Type t when t == typeof(BaseStoragePage) => "BaseStorage",
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