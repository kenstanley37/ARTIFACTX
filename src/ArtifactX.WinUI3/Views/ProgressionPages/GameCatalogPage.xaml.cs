using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views.ProgressionPages;

/// <summary>
/// Catalog: the game's Catalog screen for the loaded save (GameCatalogViewModel).
/// Reloads on save switches and on edits made elsewhere (title bar Reset/Save); its
/// own edits update the tiles in place.
/// </summary>
public sealed partial class GameCatalogPage : Page
{
    public GameCatalogViewModel ViewModel { get; } = new();

    public GameCatalogPage()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;
        _ = ViewModel.LoadAsync();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (ViewModel.IsStaging) return;
        DispatcherQueue.TryEnqueue(() => _ = ViewModel.LoadAsync());
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private void Section_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: GameCatalogSectionViewModel section }) ViewModel.SelectSection(section);
    }

    private void Category_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is GameCatalogCategoryViewModel category) ViewModel.SelectedCategory = category;
    }

    private void Item_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GameCatalogItemViewModel item) ViewModel.SetItem(item, !item.IsUnlocked);
    }

    private void All_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleAll();

    private async void Reset_Click(object sender, RoutedEventArgs e) => await ViewModel.ResetAsync();
}
