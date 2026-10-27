using ArtifactX.Core.Collections;
using System.Collections.ObjectModel;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One installed technology on a compact cabinet card: its icon and, for upgrade modules, class badge.</summary>
public sealed class StationToolTechChip(string name, Microsoft.UI.Xaml.Media.ImageSource? icon, string? classLetter)
{
    public string Name { get; } = name;
    public Microsoft.UI.Xaml.Media.ImageSource? Icon { get; } = icon;
    public string ClassLetter { get; } = classLetter ?? "";
    public bool HasClass { get; } = classLetter is not null;
    public Microsoft.UI.Xaml.Media.Brush ClassBrush { get; } = ThemeResources.ClassBrush(classLetter);
    public string Label { get; } = classLetter is null ? name : $"{name} - {classLetter}-class upgrade";
}

public sealed partial class StationReloadToolCard(string name, string rating, string damage, string scanner, string position,
    string slotSummary, InventoryGridSnapshot inventory, string modelPath, ulong modelSeed, ulong siteSeed) : ObservableObject
{
    public IReadOnlyList<StationToolTechChip> Technologies { get; } = inventory.Cells
        .Where(c => c.IsOccupied && c.ItemId is not null)
        .Select(c => new StationToolTechChip(c.DisplayName ?? CatalogService.TryGet(c.ItemId)?.DisplayName ?? c.ShortLabel,
            CatalogService.TryGet(c.ItemId)?.Icon, CatalogService.ModuleClassLetter(c.ItemId)))
        .ToArray();
    public string Name { get; } = name;
    public string Rating { get; } = rating;
    /// <summary>Empty when damage is unsupported (pirate cabinets without a live preview state).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasDamage))] public partial string Damage { get; set; } = damage;
    public bool HasDamage => Damage.Length > 0;
    [ObservableProperty] public partial string Scanner { get; set; } = scanner;
    public string Position { get; } = position;
    public string SlotSummary { get; } = slotSummary;
    public InventoryGridSnapshot Inventory { get; } = inventory;
    public string ModelPath { get; } = modelPath;
    public ulong ModelSeed { get; } = modelSeed;
    public ulong SiteSeed { get; } = siteSeed;
    public (ulong Site, ulong Model, string Path) Identity => (SiteSeed, ModelSeed, ModelPath);
    public string DisplayKey => $"{ModelPath}|{ModelSeed}|{Name}|{Rating}|{Position}|{SlotSummary}|"
        + string.Join(";", Inventory.Cells.Select(c => $"{c.X},{c.Y},{c.State},{c.ItemId},{c.IsSupercharged},{c.DisplayName}"));
}

/// <summary>Supported station cabinet records for the current system's saved-reload scenario.</summary>
public sealed partial class StationMultiToolsViewModel : ObservableObject
{
    private int _stationLoadVersion;
    private ulong? _displaySystem;
    private object? _displaySession;
    public ObservableCollection<StationReloadToolCard> StationReloadTools { get; } = new();
    [ObservableProperty] public partial bool HasStationReloadTools { get; private set; }
    [ObservableProperty] public partial string StationReloadHint { get; private set; } = "";

    public async Task UpdateAsync(ulong? system, PlanetSnapshot snapshot)
    {
        int version = ++_stationLoadVersion;
        var session = SaveSessionManager.SessionIdentity;
        if (_displaySystem != system || !ReferenceEquals(session, _displaySession))
        {
            StationReloadTools.Clear();
            HasStationReloadTools = false;
            _displaySystem = system;
            _displaySession = session;
        }
        if (system is null) return;
        try
        {
            var diskSnapshot = snapshot.EquipmentSaveInputs is null ? await LiveSaveWatcher.GetLatestAsync() : snapshot;
            if (version != _stationLoadVersion || !ReferenceEquals(session, SaveSessionManager.SessionIdentity)
                || diskSnapshot.EquipmentSaveInputs is null) return;
            var tools = await LocatedMultiToolService.GenerateSavedReloadStationAsync(system.Value, diskSnapshot.EquipmentSaveInputs);
            if (version != _stationLoadVersion || !ReferenceEquals(session, SaveSessionManager.SessionIdentity)) return;
            await CatalogService.WarmCacheAsync(tools.SelectMany(t => t.Inventory.Technologies).Select(t => t.Id));
            if (version != _stationLoadVersion || !ReferenceEquals(session, SaveSessionManager.SessionIdentity)) return;
            var cards = tools.Select(tool => new StationReloadToolCard(tool.Contents.Tool.DefaultName,
                tool.Contents.RatingIndex switch { 0 => "C-Class", 1 => "B-Class", 2 => "A-Class", 3 => "S-Class", _ => "" },
                tool.HasSupportedDamagePotential ? tool.DamagePotential.ToString("0.0", CultureInfo.InvariantCulture) : "",
                tool.ScannerRange.ToString("0.0", CultureInfo.InvariantCulture),
                string.Create(CultureInfo.InvariantCulture,
                    $"System position: X {tool.Contents.Location.WorldPosition.X:0.00}, Y {tool.Contents.Location.WorldPosition.Y:0.00}, Z {tool.Contents.Location.WorldPosition.Z:0.00}"),
                $"{tool.Inventory.Layout.UnlockedSlots.Length} unlocked slots, {tool.Inventory.Technologies.Count} installed technologies, {tool.Inventory.SuperchargedSlots.Count} supercharged slots",
                StationToolInventory.Create(tool.Inventory), tool.Contents.Tool.ModelPath, tool.Contents.Tool.Seed, tool.Contents.Location.SiteSeed)).ToArray();
            StationReloadHint = "Reload this save at its saved location, then visit this system's space station. Changing where you reload can change these tools. Only supported cabinets are shown.";
            if (tools.Any(t => t.UsesLivePreviewState))
                StationReloadHint += " Damage reflects the game's current preview state.";
            // The shared save feed also reports unchanged cabinets. Keep existing controls
            // and their rotated previews alive until a visible field actually changes.
            ObservableCollectionSync.Apply(StationReloadTools, cards, c => c.Identity,
                (current, next) => current.DisplayKey == next.DisplayKey,
                (current, next) => { current.Damage = next.Damage; current.Scanner = next.Scanner; });
            HasStationReloadTools = cards.Length > 0;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationMultiTools] Saved reload station unavailable: {error.Message}");
        }
    }

}
