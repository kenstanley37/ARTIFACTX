using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Unlike every other inventory page, Base Storage Containers ("Chests") have
/// no fixed set of known keys/array indices - a player can place any number of
/// them, each getting an arbitrary short key when placed. This page discovers
/// them at runtime by structural shape (WA4.rri == "Chest") rather than
/// reading a known path, confirmed via real save data: renaming 10 containers
/// in-game to "BS0".."BS9" and searching found all 10 as top-level siblings
/// under vLc/6f= sharing that exact marker.
/// </summary>
public sealed partial class BaseStoragePage : Page
{
    private sealed record ContainerEntry(string Key, string Name);

    private InventoryGridViewModel? _gridViewModel;
    private string? _selectedKey;
    private List<ContainerEntry> _containers = new();

    public BaseStoragePage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        LoadContainerList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadContainerList);

    /// <summary>Without this, the constructor's subscriptions above never
    /// get released across page navigation (Frame.Navigate makes a fresh
    /// Page instance every visit, no NavigationCacheMode set anywhere in
    /// this app) - every past visit leaves a dead instance permanently
    /// subscribed, re-running its full reload on every future edit
    /// anywhere in the app. Root cause of a reported slowdown where
    /// editing any page became multi-second after enough navigation.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    /// <summary>Scans every top-level vLc/6f= sibling for the "Chest" marker -
    /// see BaseStorageContainerPath's doc comment for why this can't be a
    /// fixed path list the way Ships/Multi-Tool are.
    ///
    /// A "Chest" marker alone isn't ownership - confirmed 2026-08-11 via a
    /// real before/after save diff (see [[project_fresh_save_bug_sweep]])
    /// that a genuinely zero-progress save already had 13 such containers
    /// pre-allocated from the very start, none placed, all empty. Occupied-
    /// items and Name were both tried and rejected as filters (a real,
    /// placed-but-still-empty container is indistinguishable from an
    /// unplaced one on either signal) - but a controlled test nailed it
    /// instead: the user built exactly 10 real containers in-game (renamed
    /// BASE0-BASE9 purely so this could be verified against the raw save,
    /// never used as the actual signal) and confirmed via extensive in-game
    /// testing that no 11th slot is ever reachable - matching the in-game
    /// UI itself, which numbers physical containers 0-9. Re-decrypting
    /// showed the 10 real ones are EXACTLY the first 10 of the 13
    /// Chest-shaped entries in the save's own property order, with the
    /// same 3 dead entries always last - and since all 13 already existed
    /// before any container was placed, that order reflects a fixed layout
    /// baked into the save format itself, not play/build order. Takes only
    /// the first 10 in that natural order; the trailing slots appear to be
    /// permanently unreachable padding on every save, not just this one.</summary>
    private static List<ContainerEntry> DiscoverContainers()
    {
        const int MaxRealContainers = 10;

        var results = new List<ContainerEntry>();

        if (SaveSessionManager.GetValue("vLc", "6f=") is not JObject playerState)
            return results;

        int unnamedCount = 0;
        foreach (var prop in playerState.Properties())
        {
            if (results.Count >= MaxRealContainers) break;
            if (prop.Value is not JObject obj) continue;
            if (obj["WA4"]?["rri"]?.Value<string>() != "Chest") continue;

            // "BLD_STORAGE_NAME" is the game's own literal unlocalized
            // default, not a real user-given name - treated as unnamed, same
            // as an empty NKm. Falls back to a plain sequential label, never
            // the raw save key (prop.Name) - that's internal plumbing a user
            // has no way to make sense of.
            string name = obj["NKm"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(name) || name == "BLD_STORAGE_NAME")
                name = $"Storage Container {++unnamedCount}";

            results.Add(new ContainerEntry(prop.Name, name));
        }

        return results.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void LoadContainerList()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _containers = new();
            _selectedKey = null;
            ContainerSelectorPanel.Children.Clear();
            StorageGrid.ViewModel = null;
            StorageGrid.Refresh();
            return;
        }

        _containers = DiscoverContainers();

        if (_selectedKey is null || _containers.All(c => c.Key != _selectedKey))
            _selectedKey = _containers.FirstOrDefault()?.Key;

        BuildSelectorStrip();
        LoadSelectedContainer();
    }

    private void BuildSelectorStrip()
    {
        ContainerSelectorPanel.Children.Clear();

        foreach (var container in _containers)
        {
            bool isSelected = container.Key == _selectedKey;

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = container.Name,
                    FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal
                },
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                BorderBrush = new SolidColorBrush(isSelected
                    ? Color.FromArgb(255, 255, 157, 0)
                    : Color.FromArgb(255, 90, 98, 112)),
                Background = new SolidColorBrush(isSelected
                    ? Color.FromArgb(60, 255, 157, 0)
                    : Color.FromArgb(20, 255, 255, 255))
            };

            button.Click += (_, _) =>
            {
                _selectedKey = container.Key;
                BuildSelectorStrip();
                LoadSelectedContainer();
            };

            ContainerSelectorPanel.Children.Add(button);
        }
    }

    private async void LoadSelectedContainer()
    {
        if (_selectedKey is null)
        {
            _gridViewModel = null;
            StorageGrid.ViewModel = null;
            StorageGrid.Refresh();
            GridHeaderTxt.Text = "Contents";
            NameEditBox.Text = "";
            return;
        }

        var selectedContainer = _containers.First(c => c.Key == _selectedKey);

        _gridViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.BaseStorageContainerPath(_selectedKey),
            BaseStorageCapacity.Columns,
            BaseStorageCapacity.Rows);

        _gridViewModel.Load();

        var itemIds = _gridViewModel.Cells.Where(c => c.IsOccupied).Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        StorageGrid.ViewModel = _gridViewModel;
        StorageGrid.AllowedCategories = new[] { "Substance", "Product" };
        StorageGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable" };
        StorageGrid.SupportsSupercharge = false;
        StorageGrid.SupportsRepair = false;
        StorageGrid.ProductStorageMultiplier = 10;

        // Reload can run many times per page lifetime (every selector click) -
        // unsubscribe first so CellChanged doesn't fire the reset-button
        // handler multiple times per single actual edit.
        StorageGrid.CellChanged -= StorageGrid_CellChanged;
        StorageGrid.CellChanged += StorageGrid_CellChanged;

        StorageGrid.Refresh();

        GridHeaderTxt.Text = $"Contents - {selectedContainer.Name}";
        PageResetBtn.Visibility = _gridViewModel.HasLocalChanges ? Visibility.Visible : Visibility.Collapsed;

        NameEditBox.Text = selectedContainer.Name;
    }

    private void StorageGrid_CellChanged(object? sender, EventArgs e) =>
        PageResetBtn.Visibility = Visibility.Visible;

    /// <summary>Stages the rename when the field loses focus - not on every
    /// keystroke, matching Ships/MultiTool/Freighter's own name-editing
    /// behavior.</summary>
    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedKey is null) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        var currentContainer = _containers.FirstOrDefault(c => c.Key == _selectedKey);
        if (currentContainer != null && newName == currentContainer.Name) return;

        var namePath = NmsInventoryContainer.BaseStorageContainerPath(_selectedKey).Append("NKm").ToArray();
        SaveSessionManager.StageEdit(newName, namePath);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadContainerList();
    }

    private void UnlockAllBtn_Click(object sender, RoutedEventArgs e)
    {
        _gridViewModel?.UnlockAll();
        StorageGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MaxAllQtyBtn_Click(object sender, RoutedEventArgs e)
    {
        _gridViewModel?.MaxAllQuantities();
        StorageGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _gridViewModel?.Revert();
        StorageGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
