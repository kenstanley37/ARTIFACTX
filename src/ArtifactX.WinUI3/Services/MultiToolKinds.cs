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
/// Alien tool, STARTXRARE the rounded Starter-style tool. PRISTINEXRARE is
/// named Experimental from the game's own BASESTATS_PRISTINE_L string.
///
/// A normal tool is a Pistol or a Rifle by the game's own rule, read from its
/// code (2026-10-16): the weapon tag on its barrel - large or medium makes a
/// rifle, small a pistol (ShipModels.WeaponTags). Not its looks: compact tools
/// with a medium barrel are rifles. The game shows the class nowhere in its UI.
/// Guessing from the magazine slot was wrong for five of eight tools.
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
        await EnsureTreesAsync();
        if (Load(ShipParts.DescriptorFor(MainModel)) is null) return null;
        var picked = ShipParts.Generate(MainModel, seed, Load).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (picked.Contains("_MULTITOOL_ALIENXRARE")) return "Alien";
        if (picked.Contains("_MULTITOOL_STARTXRARE")) return "Starter";
        // "Pristine" is the game's own word for Experimental (BASESTATS_PRISTINE_L).
        if (picked.Contains("_MULTITOOL_PRISTINEXRARE")) return "Experimental";
        if (!picked.Contains("_MULTITOOL_NORMAL")) return null;
        // Pistol or rifle, the game's way: the weapon tags on the chosen parts
        // (ShipModels.WeaponTags - a large or medium barrel makes a rifle).
        if (GameDataService.Game is { } game && await ModelPreviewService.ModelsAsync(game) is { } models)
        {
            var tags = await Task.Run(() => models.WeaponTags(MainModel));
            if (tags.Count > 0)
                return picked.Any(p => tags.TryGetValue(p, out var t) && t is "WEAPON_LARGE" or "WEAPON_MEDIUM") ? "Rifle" : "Pistol";
        }
        return "Standard";
    }

    private static Dictionary<string, IReadOnlyList<string>>? _words;
    private static DateTime? _wordsStamp;

    /// <summary>The name the game gives a tool nobody renamed ("Pillar of
    /// Ooruz-IX", "Dozhgor-κ") - MultiToolNames, with the word lists from the
    /// catalog. Null until the game data has them (format 11).</summary>
    public static async Task<string?> GameNameAsync(ulong seed, string scenePath)
    {
        var words = await NameWordsAsync();
        if (words.Count == 0) return null;
        // Staffs (WeaponStatClass Staff) have their own patterns and adjectives.
        bool staff = System.IO.Path.GetFileName(scenePath).StartsWith("STAFF", StringComparison.OrdinalIgnoreCase);
        return MultiToolNames.Generate(seed, staff, list => words.GetValueOrDefault(list));
    }

    /// <summary>The name the game gives a starship nobody renamed ("Ashes of the
    /// Suitaka") - ShipNames, words from the catalog (format 12). Null for living
    /// ships and Sentinel ships (their own name routines, not read yet) and corvettes.</summary>
    public static async Task<string?> ShipNameAsync(ulong seed, string scenePath)
    {
        string path = scenePath.ToUpperInvariant();
        if (seed == 0 || path.Contains("/BIOPARTS/") || path.Contains("/SENTINELSHIP/") || path.Contains("/VINTAGEINTERCEPTOR.")
            || ShipBaseService.IsCorvetteModel(scenePath))
            return null;
        var words = await NameWordsAsync();
        return words.Count == 0 ? null : ShipNames.Generate(seed, list => words.GetValueOrDefault(list));
    }

    /// <summary>The game's name word lists by list name ("WEAP_NOUN", "SHIP_ADJ"...),
    /// read again after a game-data rebuild.</summary>
    private static async Task<Dictionary<string, IReadOnlyList<string>>> NameWordsAsync()
    {
        var stamp = GameDataService.Info?.BuiltAtUtc;
        if (_words is null || _words.Count == 0 || stamp != _wordsStamp)
        {
            _wordsStamp = stamp;
            var raw = await CatalogService.GetJsonCategoryAsync("MultiToolNameWords");
            _words = raw.ToDictionary(kv => kv.Key,
                kv => (IReadOnlyList<string>)(System.Text.Json.JsonSerializer.Deserialize<List<string>>(kv.Value) ?? new()),
                StringComparer.OrdinalIgnoreCase);
        }
        return _words;
    }

    /// <summary>Reads the part trees from the catalog - again after a game-data
    /// rebuild (the old data had no multi-tool trees).</summary>
    public static async Task EnsureTreesAsync()
    {
        var stamp = GameDataService.Info?.BuiltAtUtc;
        if (_trees is not null && _trees.Count > 0 && stamp == _stamp) return;
        _stamp = stamp;
        lock (Cache) Cache.Clear();
        _trees = await CatalogService.GetShipPartTreesAsync();
    }

    /// <summary>A model's part tree by descriptor path, once EnsureTreesAsync has run.</summary>
    public static IReadOnlyList<PartGroup>? Load(string path)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var tree)) return tree;
            if (_trees is null || !_trees.TryGetValue(path, out var json)) return null;
            return Cache[path] = ShipParts.ParseTree(json);
        }
    }
}
