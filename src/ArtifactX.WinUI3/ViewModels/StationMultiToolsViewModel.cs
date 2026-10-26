using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

public sealed record StationReloadToolCard(string Name, string Rating, string Stats, string Position);

/// <summary>Supported station cabinet records for the current system's saved-reload scenario.</summary>
public sealed partial class StationMultiToolsViewModel : ObservableObject
{
    private int _stationLoadVersion;
    [ObservableProperty] public partial IReadOnlyList<StationReloadToolCard> StationReloadTools { get; private set; } = [];
    [ObservableProperty] public partial bool HasStationReloadTools { get; private set; }
    [ObservableProperty] public partial string StationReloadHint { get; private set; } = "";

    public async Task UpdateAsync(ulong? system, PlanetSnapshot snapshot)
    {
        int version = ++_stationLoadVersion;
        var slot = SaveSessionManager.ActiveSlot;
        StationReloadTools = [];
        HasStationReloadTools = false;
        if (system is null) return;
        try
        {
            var diskSnapshot = snapshot.EquipmentSaveInputs is null ? await LiveSaveWatcher.GetLatestAsync() : snapshot;
            if (version != _stationLoadVersion || !ReferenceEquals(slot, SaveSessionManager.ActiveSlot)
                || diskSnapshot.EquipmentSaveInputs is null) return;
            var tools = await LocatedMultiToolService.GenerateSavedReloadStationAsync(system.Value, diskSnapshot.EquipmentSaveInputs);
            if (version != _stationLoadVersion || !ReferenceEquals(slot, SaveSessionManager.ActiveSlot)) return;
            var cards = tools.Select(tool => new StationReloadToolCard(tool.Contents.Tool.DefaultName,
                tool.Contents.RatingIndex switch { 0 => "C-Class", 1 => "B-Class", 2 => "A-Class", 3 => "S-Class", _ => "" },
                string.Create(CultureInfo.InvariantCulture, $"Damage potential {tool.DamagePotential:0.0} · Scanner range {tool.ScannerRange:0.0}"),
                string.Create(CultureInfo.InvariantCulture,
                    $"System position: X {tool.Contents.Location.WorldPosition.X:0.00}, Y {tool.Contents.Location.WorldPosition.Y:0.00}, Z {tool.Contents.Location.WorldPosition.Z:0.00}"))).ToArray();
            StationReloadHint = "Reload this save at its saved location, then visit this system's space station. Changing where you reload can change these tools. Only supported cabinets are shown.";
            StationReloadTools = cards;
            HasStationReloadTools = cards.Length > 0;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationMultiTools] Saved reload station unavailable: {error.Message}");
        }
    }

}
