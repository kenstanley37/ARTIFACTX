using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NMS.WinUI3.Controls;
using NMS.WinUI3.Views;
using NMS.WinUI3.Views.InspectionPages;
using System;
using System.Collections.Generic;
using Windows.UI;
using WinRT.Interop;

namespace NMS.WinUI3;

public sealed partial class MainWindow : Window
{
    private AppWindow _appWindow;

    // FIXED: Expose title bar control context cleanly
    public AppTitleBar TitleBar => CustomTitleBar;

    public MainWindow()
    {
        InitializeComponent();

        // 1. Initialize custom TitleBar configuration lifecycle
        InitializeCustomTitleBar();

        // Boot directly to our main dashboard view on launch
        ContentFrame.Navigate(typeof(SaveFolderSelectPage));

        // Dynamic search lookup updates initial index flag state
        var defaultItem = FindNavItemByTag(RootNav.MenuItems, "SaveFolderSelect");
        if (defaultItem != null) RootNav.SelectedItem = defaultItem;
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
                "SaveHub" => typeof(SaveHubPage),
                "SaveFolderSelect" => typeof(SaveFolderSelectPage),
                "Inventory" => typeof(InventoryPage),
                "AncestrySearch" => typeof(AncestrySearchView),
                "HGDecryption" => typeof(HGDecryptionPage),
                //"DataDiagnostic" => typeof(DataDiagnosticPage),
                _ => typeof(SaveHubPage)
            };

            if (ContentFrame.CurrentSourcePageType != targetPageType)
            {
                // 📍 FIXED: Erased hardcoded constraint lock pass
                ContentFrame.Navigate(targetPageType);
            }
        }
    }

    /// <summary>
    /// Safely unlocks the sidebar navigation options from child pages after save validation passes.
    /// </summary>
    public void SetNavigationState(bool isEnabled)
    {
        if (RootNav != null)
        {
            RootNav.IsEnabled = isEnabled;
        }
    }

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        string targetTag = e.SourcePageType switch
        {
            Type t when t == typeof(SaveHubPage) => "SaveHub",
            Type t when t == typeof(SaveFolderSelectPage) => "SaveFolderSelect",
            Type t when t == typeof(InventoryPage) => "Inventory",
            Type t when t == typeof(AncestrySearchView) => "AncestrySearch",
            Type t when t == typeof(HGDecryptionPage) => "HGDecryption",
            //Type t when t == typeof(DataDiagnosticPage) => "DataDiagnostic",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(targetTag))
        {
            // 📍 FIXED: Implemented deep hierarchy recursive element selection updater
            var targetItem = FindNavItemByTag(RootNav.MenuItems, targetTag);
            if (targetItem != null)
            {
                RootNav.SelectedItem = targetItem;
            }
        }
    }

    /// <summary>
    /// Monitored recursive lookahead algorithm locates a NavigationViewItem matching the tag context,
    /// traversing into sub-menus seamlessly.
    /// </summary>
    private NavigationViewItem? FindNavItemByTag(IList<object> itemsList, string targetTag)
    {
        foreach (var menuNode in itemsList)
        {
            if (menuNode is NavigationViewItem item)
            {
                if (item.Tag?.ToString() == targetTag)
                {
                    return item;
                }

                // Check deep nested submenu children blocks if they exist
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