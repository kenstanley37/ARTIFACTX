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
/// Alien tool, STARTXRARE the rounded Starter-style tool, and a normal tool
/// is a Pistol or a Rifle by its magazine slot. PRISTINEXRARE is named
/// Experimental from the game's own BASESTATS_PRISTINE_L string.
/// </summary>
public static class MultiToolKinds
{
    public const string MainModel = "MODELS/COMMON/WEAPONS/MULTITOOL/MULTITOOL.SCENE.MBIN";

    private static Dictionary<string, string>? _trees;
    private static DateTime? _stamp;
    private static readonly Dictionary<string, IReadOnlyList<PartGroup>> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The game's own type names (BASESTATS_*_L strings) for the other
    /// models, matched by model file - Atlas = Atlantid, Royal = Exotic, Robot =
    /// Sentinel, Staff = Voltaic Staff. Swarm, Retro and Rod aren't in the
    /// game's type list (expedition / special tools), so they keep their own.
    /// Confirmed against the user's tools 2026-10-15 (Atlas, Ruin and Bone
    /// staffs, Swarm).</summary>
    public static string NameFor(string scenePath, string fallback) =>
        System.IO.Path.GetFileName(scenePath).ToUpperInvariant() switch
        {
            "ATLASMULTITOOL.SCENE.MBIN" => "Atlantid",
            "ROYALMULTITOOL.SCENE.MBIN" => "Exotic",
            "SENTINELMULTITOOL.SCENE.MBIN" => "Sentinel",
            "SENTINELMULTITOOLB.SCENE.MBIN" => "Sentinel (variant B)",
            "STAFFMULTITOOL.SCENE.MBIN" => "Voltaic Staff",
            "STAFFMULTITOOLATLAS.SCENE.MBIN" => "Voltaic Staff (Atlas)",
            "STAFFMULTITOOLBONE.SCENE.MBIN" => "Voltaic Staff (Bone)",
            "STAFFMULTITOOLRUIN.SCENE.MBIN" => "Voltaic Staff (Ruin)",
            "SWARMMULTITOOL.SCENE.MBIN" => "Swarm",
            _ => fallback,
        };

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
        // "Pristine" is the game's own word for Experimental (BASESTATS_PRISTINE_L).
        if (picked.Contains("_MULTITOOL_PRISTINEXRARE")) return "Experimental";
        // A normal tool's front decides it: _MAGSLOT_1 is a short clip (pistol),
        // _MAGSLOT_2 a long barrel (rifle) - whatever stock it has. Matched to a
        // pistol and a rifle bought in game (2026-10-15) and renders mixing the parts.
        if (picked.Contains("_MAGSLOT_1")) return "Pistol";
        if (picked.Contains("_MAGSLOT_2")) return "Rifle";
        return picked.Contains("_MULTITOOL_NORMAL") ? "Pistol or Rifle" : null;
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
