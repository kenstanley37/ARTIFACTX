using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The Multi-Tool Finder's search (issue #60): what the space station cabinets of every system around a start sell,
/// for each place the game can be loaded at (Core NearbySystems + CabinetMultiToolSearch, so the mobile app can run the
/// same search from a typed or scanned address). About 0.13 s for the 607 systems of one region around the owner's AC
/// Colony Star (2026-11-12, fauna-check MT). Model, kind and class only - stats need the player's progression and
/// are on Current System's cabinet panel.
/// </summary>
public static class MultiToolFinderService
{
    /// <summary>The cabinet offers within <paramref name="regionRadius"/> regions of <paramref name="start"/>, off the UI
    /// thread; empty without the game data (station, inventory and multi-tool model tables) or when the save's
    /// stations use the legacy layout (as Current System, the cabinets can't be placed then).</summary>
    public static async Task<IReadOnlyList<CabinetMultiToolFind>> FindAsync(ulong start, int regionRadius, JObject? saveInputs,
        IProgress<int>? progress = null, CancellationToken cancel = default)
    {
        var settingsTask = CatalogService.GetGalaxyGenerationSettingsAsync();
        var stationsTask = CatalogService.GetStationEquipmentTablesAsync();
        var inventoryTask = CatalogService.GetInventoryGenerationTablesAsync();
        var modelsTask = CatalogService.GetMultiToolModelTablesAsync();
        var wordsTask = CatalogService.GetProceduralNameWordsAsync();
        await Task.WhenAll(settingsTask, stationsTask, inventoryTask, modelsTask, wordsTask);
        if (settingsTask.Result is not { } settings || stationsTask.Result is not { } stations
            || inventoryTask.Result is not { } inventory || modelsTask.Result is not { } models) return [];
        var words = wordsTask.Result;
        bool? legacy = NmsMultiToolGenerationPaths.ReadSavedLegacyStationRule(saveInputs, stations);
        return await Task.Run(() =>
        {
            var kinds = new MultiToolKindClassifier(models);
            var systems = NearbySystems.Around(start, regionRadius, settings).ToList();
            var found = new ConcurrentBag<CabinetMultiToolFind>();
            int done = 0;
            Parallel.ForEach(systems, new ParallelOptions { CancellationToken = cancel }, system =>
            {
                foreach (var find in CabinetMultiToolSearch.InSystem(system.SystemAddress, legacy, stations, inventory, kinds,
                             key => words.GetValueOrDefault(key)))
                    found.Add(find);
                int n = Interlocked.Increment(ref done);
                if (n % 250 == 0) progress?.Report(n);
            });
            return (IReadOnlyList<CabinetMultiToolFind>)found.ToList();
        }, cancel);
    }

    /// <summary>Whether the save's stations can be searched: false for the legacy station layout (no cabinet places),
    /// null when the save doesn't say.</summary>
    public static async Task<bool?> StationsSupportedAsync(JObject? saveInputs) =>
        await CatalogService.GetStationEquipmentTablesAsync() is { } stations
            ? NmsMultiToolGenerationPaths.ReadSavedLegacyStationRule(saveInputs, stations) is false
            : null;
}
