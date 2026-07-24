using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NMS.WinUI3.Controls;
using NMS.WinUI3.Services;
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

    public AppTitleBar TitleBar => CustomTitleBar;

    public MainWindow()
    {
        InitializeComponent();
        InitializeCustomTitleBar();

        // Ready for when Exosuit/Starship/etc. nav items exist - subscribe now so
        // the wiring is already correct the moment they're added.
        SaveSessionManager.ActiveSessionChanged += (_, _) => UpdateNavAvailability();
        UpdateNavAvailability();

        ContentFrame.Navigate(typeof(SaveFolderSelectPage));

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
                "SaveFolderSelect" => typeof(SaveFolderSelectPage),
                "Exosuit" => typeof(ExosuitPage),
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
        ExosuitNavItem.Visibility = SaveSessionManager.IsSaveLoaded ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        string targetTag = e.SourcePageType switch
        {
            Type t when t == typeof(SaveFolderSelectPage) => "SaveFolderSelect",
            Type t when t == typeof(ExosuitPage) => "Exosuit",
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