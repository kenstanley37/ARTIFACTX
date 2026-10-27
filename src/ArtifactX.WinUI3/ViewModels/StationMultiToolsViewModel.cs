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

public sealed partial class StationReloadToolCard(string name, string rating, string stats, string position,
    string slotSummary, InventoryGridSnapshot inventory, string modelPath, ulong modelSeed, ulong siteSeed) : ObservableObject
{
    public string Name { get; } = name;
    public string Rating { get; } = rating;
    [ObservableProperty] public partial string Stats { get; set; } = stats;
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
                tool.HasSupportedDamagePotential
                    ? string.Create(CultureInfo.InvariantCulture, $"Damage potential {tool.DamagePotential:0.0} · Scanner range {tool.ScannerRange:0.0}")
                    : string.Create(CultureInfo.InvariantCulture, $"Scanner range {tool.ScannerRange:0.0}"),
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
                (current, next) => current.Stats = next.Stats);
            HasStationReloadTools = cards.Length > 0;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationMultiTools] Saved reload station unavailable: {error.Message}");
        }
    }

}
