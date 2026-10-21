using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Owned settlements (SettlementService): a card per settlement, the selected one's
/// details (SettlementViewModel) and what its buildings need (SettlementMaterialsPanel).
/// Reloads on every staged edit and save switch; Reset undoes every change to the
/// selected settlement.
/// </summary>
public sealed partial class SettlementPage : Page
{
    // Details needs ~620 px (feature dropdowns + stat boxes), materials ~600 px
    // to read its "where to get it" lines; below that they stack (user 2026-10-14).
    private const double DetailsWidth = 640, MaterialsMinWidth = 600;

    private List<SettlementEntry> _settlements = new();
    private int _selectedIndex = -1;
    private int _loadVersion;

    public SettlementViewModel ViewModel { get; } = new();

    public SettlementPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;
        ViewModel.NameChanged += (_, _) => LoadSettlementList();

        LoadSettlementList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadSettlementList);

    /// <summary>Static events hold their subscribers: without this every past visit
    /// would stay subscribed and reload on every edit anywhere in the app.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private void LoadSettlementList()
    {
        _settlements = SaveSessionManager.IsSaveLoaded ? SettlementService.ReadOwned() : new();
        if (_settlements.All(s => s.Index != _selectedIndex))
            _selectedIndex = _settlements.FirstOrDefault()?.Index ?? -1;

        _ = ShowCardsAsync();
        _ = LoadSelectedAsync();
    }

    private async Task ShowCardsAsync()
    {
        await SettlementService.LoadBuildingNamesAsync();
        // Production icons/names on the cards come from the catalog cache.
        await CatalogService.WarmCacheAsync(SettlementCardViewModel.ProductIds(_settlements));
        SettlementCards.ItemsSource = _settlements.Select(s => new SettlementCardViewModel(s, s.Index == _selectedIndex)).ToList();
    }

    private void SettlementCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SettlementCardViewModel card) return;
        _selectedIndex = card.Index;
        _ = ShowCardsAsync();
        _ = LoadSelectedAsync();
    }

    private async Task LoadSelectedAsync()
    {
        int version = ++_loadVersion;
        int index = _selectedIndex;
        if (index < 0)
        {
            ViewModel.Clear();
            PageResetBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            await ViewModel.LoadAsync(index);
            if (version != _loadVersion) return;
            PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsSettlementPaths.SettlementPath(index))
                ? Visibility.Visible : Visibility.Collapsed;
        }
        await Materials.ViewModel.LoadAsync(index);
    }

    private void SettlementCards_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool beside = e.NewSize.Width >= DetailsWidth + SettlementCardsGrid.ColumnSpacing + MaterialsMinWidth;
        var columns = SettlementCardsGrid.ColumnDefinitions;
        columns[0].Width = beside ? new GridLength(DetailsWidth) : new GridLength(1, GridUnitType.Star);
        columns[1].Width = beside ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetRow(MaterialsCard, beside ? 0 : 1);
        Grid.SetColumn(MaterialsCard, beside ? 1 : 0);
    }

    private void AddFeatureSlotBtn_Click(object sender, RoutedEventArgs e) => ViewModel.AddFeatureSlot();

    /// <summary>Undoes every staged change to the selected settlement (and only that one).</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;
        SaveSessionManager.RevertEditsUnder(NmsSettlementPaths.SettlementPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Collapsed;
        LoadSettlementList();
    }
}
