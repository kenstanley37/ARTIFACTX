using ArtifactX.Core.Procedural;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// What kind of multi-tool a seed makes (2026-10-15). The main model
/// (MULTITOOL.SCENE - the catalog calls it "Rifle", after its file) makes
/// several kinds, and the tool's seed (SuJ[i].@EL[1], not NTx.@EL) picks one,
/// the same way a ship's seed picks parts (ShipParts, game part trees from
/// the catalog). Its top choice is _MULTITOOL_NORMAL (a tool built from
/// magazine, stock, barrel... parts) or _MULTITOOL_ALTXRARE, which picks one
/// of three rare kinds.
///
/// Confirmed in game against screenshots and renders: ALIENXRARE is the
/// Alien tool, STARTXRARE the rounded Starter-style tool. PRISTINEXRARE and
/// the pistol / rifle split inside NORMAL aren't confirmed yet, so they get
/// neutral names.
/// </summary>
public static class MultiToolKinds
{
    public const string MainModel = "MODELS/COMMON/WEAPONS/MULTITOOL/MULTITOOL.SCENE.MBIN";

    private static Dictionary<string, string>? _trees;
    private static DateTime? _stamp;
    private static readonly Dictionary<string, IReadOnlyList<PartGroup>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsMainModel(string? scenePath) => string.Equals(scenePath, MainModel, StringComparison.OrdinalIgnoreCase);

    /// <summary>The kind this seed makes on the main model, or null when the
    /// game data has no multi-tool part trees yet (built before format 10).</summary>
    public static async Task<string?> KindAsync(ulong seed)
    {
        // Read again after a game-data rebuild (the old data had no multi-tool trees).
        var stamp = GameDataService.Info?.BuiltAtUtc;
        if (_trees is null || _trees.Count == 0 || stamp != _stamp)
        {
            _stamp = stamp;
            lock (Cache) Cache.Clear();
            _trees = await CatalogService.GetShipPartTreesAsync();
        }
        if (Load(ShipParts.DescriptorFor(MainModel)) is null) return null;
        var picked = ShipParts.Generate(MainModel, seed, Load).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (picked.Contains("_MULTITOOL_ALIENXRARE")) return "Alien";
        if (picked.Contains("_MULTITOOL_STARTXRARE")) return "Starter";
        if (picked.Contains("_MULTITOOL_PRISTINEXRARE")) return "Pristine (rare)";
        return picked.Contains("_MULTITOOL_NORMAL") ? "Standard" : null;
    }

    private static IReadOnlyList<PartGroup>? Load(string path)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var tree)) return tree;
            if (_trees is null || !_trees.TryGetValue(path, out var json)) return null;
            return Cache[path] = ShipParts.ParseTree(json);
        }
    }
}
