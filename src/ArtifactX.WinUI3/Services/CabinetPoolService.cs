using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The multi-tools a system's standard cabinets (space station and planet shops) sell, by the place the game was
/// loaded at: the pool of that body's address (SystemMultiToolGenerator.GeneratePool), Sentinel pillar types left
/// out. Confirmed 2026-11-05 on Kaim 30/19 (Slot 7, Ihweyf) with three cabinets and two reloads: each cabinet sold the
/// loaded place's pool pick for its site seed (docs/research/MULTITOOL_GENERATION.md). Arriving by warp counts as a
/// load in space, so the station's pool (body 0) is what a player sees until they save and reload on a planet.
/// Cached per address.
/// </summary>
public static class CabinetPoolService
{
    private static readonly ConcurrentDictionary<ulong, Task<IReadOnlyList<SystemMultiTool>>> Pools = new();

    static CabinetPoolService() => CatalogService.Reloaded += Pools.Clear;

    /// <summary>The cabinet tools when the game is loaded at <paramref name="body"/> (0 = space or the station) of
    /// <paramref name="system"/>; empty when the catalog doesn't have the pool rules yet.</summary>
    public static Task<IReadOnlyList<SystemMultiTool>> ForAsync(ulong system, int body)
    {
        ulong address = (system & ~(0xFUL << 52)) | ((ulong)(body & 15) << 52);
        var task = Pools.GetOrAdd(address, LoadAsync);
        return task;
    }

    /// <summary>The body of <paramref name="system"/> the save loads at, or null when it was saved in another system.</summary>
    public static int? SavedLoadBody(ulong system, Newtonsoft.Json.Linq.JObject? equipmentSaveInputs)
    {
        var saved = NmsMultiToolGenerationPaths.ReadActiveSavedState(equipmentSaveInputs, 0)?.ReloadUniverseAddress;
        return saved is { } load && (load & ~(0xFUL << 52)) == (system & ~(0xFUL << 52)) ? (int)((load >> 52) & 15) : null;
    }

    private static async Task<IReadOnlyList<SystemMultiTool>> LoadAsync(ulong address)
    {
        var words = await CatalogService.GetProceduralNameWordsAsync();
        var tables = PlanetTypesService.InventoryTables ?? await CatalogService.GetInventoryGenerationTablesAsync();
        if (tables?.MultiToolPools is not { Length: > 0 } pools)
        {
            Pools.TryRemove(address, out _);   // retry once the catalog is ready
            return [];
        }
        int body = (int)((address >> 52) & 15);
        return await Task.Run(() => (IReadOnlyList<SystemMultiTool>)SystemMultiToolGenerator.GeneratePool(address,
                body == 0 ? MultiToolPoolSource.SpaceStation : MultiToolPoolSource.Planet, body == 0 ? "Space Station" : $"Planet {body}",
                k => words.GetValueOrDefault(k), pools)
            .Where(t => t.PoolType == "Standard").ToArray());
    }
}
