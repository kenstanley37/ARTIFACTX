using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

public sealed record StationReloadToolCard(string Name, string Rating, string Stats, string Position, string SlotSummary, InventoryGridSnapshot Inventory, string ModelPath, ulong ModelSeed)
{
    public string DisplayKey => $"{ModelPath}|{ModelSeed}|{Name}|{Rating}|{Stats}|{Position}|{SlotSummary}|"
        + string.Join(";", Inventory.Cells.Select(c => $"{c.X},{c.Y},{c.State},{c.ItemId},{c.IsSupercharged},{c.DisplayName}"));
}

/// <summary>Supported station cabinet records for the current system's saved-reload scenario.</summary>
public sealed partial class StationMultiToolsViewModel : ObservableObject
{
    private int _stationLoadVersion;
    private ulong? _displaySystem;
    private object? _displaySlot;
    [ObservableProperty] public partial IReadOnlyList<StationReloadToolCard> StationReloadTools { get; private set; } = [];
    [ObservableProperty] public partial bool HasStationReloadTools { get; private set; }
    [ObservableProperty] public partial string StationReloadHint { get; private set; } = "";

    public async Task UpdateAsync(ulong? system, PlanetSnapshot snapshot)
    {
        int version = ++_stationLoadVersion;
        var slot = SaveSessionManager.ActiveSlot;
        if (_displaySystem != system || !ReferenceEquals(slot, _displaySlot))
        {
            StationReloadTools = [];
            HasStationReloadTools = false;
            _displaySystem = system;
            _displaySlot = slot;
        }
        if (system is null) return;
        try
        {
            var diskSnapshot = snapshot.EquipmentSaveInputs is null ? await LiveSaveWatcher.GetLatestAsync() : snapshot;
            if (version != _stationLoadVersion || !ReferenceEquals(slot, SaveSessionManager.ActiveSlot)
                || diskSnapshot.EquipmentSaveInputs is null) return;
            var tools = await LocatedMultiToolService.GenerateSavedReloadStationAsync(system.Value, diskSnapshot.EquipmentSaveInputs);
            if (version != _stationLoadVersion || !ReferenceEquals(slot, SaveSessionManager.ActiveSlot)) return;
            await CatalogService.WarmCacheAsync(tools.SelectMany(t => t.Inventory.Technologies).Select(t => t.Id));
            if (version != _stationLoadVersion || !ReferenceEquals(slot, SaveSessionManager.ActiveSlot)) return;
            var cards = tools.Select(tool => new StationReloadToolCard(tool.Contents.Tool.DefaultName,
                tool.Contents.RatingIndex switch { 0 => "C-Class", 1 => "B-Class", 2 => "A-Class", 3 => "S-Class", _ => "" },
                tool.HasSupportedDamagePotential
                    ? string.Create(CultureInfo.InvariantCulture, $"Damage potential {tool.DamagePotential:0.0} · Scanner range {tool.ScannerRange:0.0}")
                    : string.Create(CultureInfo.InvariantCulture, $"Scanner range {tool.ScannerRange:0.0}"),
                string.Create(CultureInfo.InvariantCulture,
                    $"System position: X {tool.Contents.Location.WorldPosition.X:0.00}, Y {tool.Contents.Location.WorldPosition.Y:0.00}, Z {tool.Contents.Location.WorldPosition.Z:0.00}"),
                $"{tool.Inventory.Layout.UnlockedSlots.Length} unlocked slots, {tool.Inventory.Technologies.Count} installed technologies, {tool.Inventory.SuperchargedSlots.Count} supercharged slots",
                StationToolInventory.Create(tool.Inventory), tool.Contents.Tool.ModelPath, tool.Contents.Tool.Seed)).ToArray();
            StationReloadHint = "Reload this save at its saved location, then visit this system's space station. Changing where you reload can change these tools. Only supported cabinets are shown.";
            if (tools.Any(t => t.UsesLivePreviewState))
                StationReloadHint += " Damage reflects the game's current preview state.";
            // The shared save feed also reports unchanged cabinets. Keep existing controls
            // and their rotated previews alive until a visible field actually changes.
            if (!StationReloadTools.Select(c => c.DisplayKey).SequenceEqual(cards.Select(c => c.DisplayKey)))
                StationReloadTools = cards;
            HasStationReloadTools = cards.Length > 0;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationMultiTools] Saved reload station unavailable: {error.Message}");
        }
    }

}
