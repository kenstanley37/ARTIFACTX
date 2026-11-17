using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.GameData;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// A Multi-Tool in the save (vLc.6f=.SuJ[i]): its class, how many tech slots that gives it, its kind and seed, and which tech and upgrade modules it takes. Used by Multi-Tool.
/// </summary>
public static class MultiToolService
{
    public static int TechSlots(Dictionary<string, int> capacity, string? classLetter) =>
        capacity.TryGetValue(MultiToolCapacity.TechCapacityKey(classLetter), out int slots)
            ? slots : MultiToolCapacity.FallbackTechSlots;

    /// <summary>Reads a tool's Class letter directly from the save, BEFORE any
    /// InventoryGridViewModel exists for it - needed up front to size the grid
    /// correctly, since capacity depends on class (MultiToolCapacity).</summary>
    public static string? ClassLetter(int toolIndex) =>
        SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolTechnologyPath(toolIndex).Append("B@N").Append("1o6").ToArray())?.Value<string>();

    /// <summary>Multi-tool module families, from the 94 tools in the real saves
    /// (2026-10-13): each tops out at 3 per tool. UP_SENGUN is a one-off
    /// salvaged find, so it isn't offered.</summary>
    public static readonly string[] ModuleFamilies =
        { "UP_LASER", "UP_SCAN", "UP_BOLT", "UP_SMG", "UP_SHOT", "UP_CANN", "UP_RAIL", "UP_GREN", "UP_TGREN" };

    public static readonly string[] TechGroupOrder = { "Mining", "Scanning", "Weapons", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats), same as Starships.</summary>
    public static string TechGroup(CatalogPart tech)
    {
        string stat = tech.Stat ?? "";
        if (stat is "Weapon_Laser" or "Weapon_SunLaser") return "Mining";
        if (stat.StartsWith("Weapon_Scan")) return "Scanning";
        if (stat is "Weapon_Projectile" or "Weapon_Burst" or "Weapon_Shotgun" or "Weapon_Cannon" or "Weapon_Rail"
            or "Weapon_Grenade" or "Weapon_MineGrenade" or "Weapon_StunGrenade" or "Weapon_Flame" or "Weapon_Stun"
            or "Weapon_Gravity" or "Weapon_SoulLaser") return "Weapons";
        return "Utilities";
    }

    public static async Task<string?> KindOfAsync(int index) =>
        SeedOf(index) is ulong seed ? await MultiToolKinds.KindAsync(seed) : null;

    /// <summary>A tool's own seed (SuJ[i].@EL[1]), which picks its look, kind and name.</summary>
    public static ulong? SeedOf(int index)
    {
        string? seedText = index < 0 ? null
            : SaveSessionManager.GetValue("vLc", "6f=", "SuJ", index.ToString(), "@EL", "1")?.Value<string>();
        return NmsSeedGenerator.TryParse(seedText, out ulong seed) ? seed : null;
    }

    public sealed record OwnedToolPreview(
        ShipMesh? Mesh,
        Func<string, uint> MaterialColour,
        Func<string, ModelTexture?>? Painted,
        string Note);

    /// <summary>Builds the 3D preview for an owned tool in the save (vLc.6f=.SuJ[index]),
    /// using its model, seed (or crafted staff parts in 7DH.SMP), and saved staff colours (7DH.Aak)
    /// or seed palette colours.</summary>
    public static async Task<OwnedToolPreview?> BuildOwnedPreviewAsync(int index, Func<bool> isCancelled)
    {
        string scene = index < 0 ? ""
            : SaveSessionManager.GetValue("vLc", "6f=", "SuJ", index.ToString(CultureInfo.InvariantCulture), "NTx", "93M")?.Value<string>() ?? "";
        if (string.IsNullOrEmpty(scene) || GameDataService.Game is not { } game)
            return null;

        var models = await ModelPreviewService.ModelsAsync(game);
        await MultiToolKinds.EnsureTreesAsync();
        if (isCancelled()) return null;

        ulong seed = SeedOf(index) ?? 0;
        var root = MultiToolKinds.Load(ShipParts.DescriptorFor(scene));
        var options = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ModelPreviewService.AllOptionIds(root, MultiToolKinds.Load, StringComparer.Ordinal);
        // A staff's crafted head/core/pole are kept in the save (SuJ[i].7DH.SMP) and win over the seed.
        var stored = (SaveSessionManager.GetValue("vLc", "6f=", "SuJ", index.ToString(CultureInfo.InvariantCulture), "7DH", "SMP") as JArray)
            ?.Select(p => p.Value<string>() ?? "").Where(p => p.Length > 1).ToList();
        var chosen = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ShipParts.Generate(scene, seed, MultiToolKinds.Load, stored).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var mesh = models is null ? null : await Task.Run(() => models.Build(scene, options, chosen));
        if (isCancelled()) return null;

        var storedColours = StoredColours(index);
        Func<string, ModelTexture?>? painted = null;
        bool fromSeed = false;
        if (models is not null && mesh is not null)
        {
            Func<string, string, float[]?> colour;
            ulong? textureSeed = null;
            if (storedColours.Count > 0)
                colour = (palette, slot) => storedColours.GetValueOrDefault((palette, slot));
            else if (await ModelPreviewService.SeedColoursAsync(seed) is { } fromTable)
            {
                colour = fromTable;
                textureSeed = seed;
                fromSeed = true;
            }
            else
                colour = (_, _) => null;
            var textures = await ModelPreviewService.PaintAsync(models, mesh, colour, textureSeed);
            if (isCancelled()) return null;
            painted = m => textures.GetValueOrDefault(m);
        }

        string note = mesh is null
            ? "This tool's model couldn't be read from your game files."
            : painted is null
                ? "Shape from your game files - drag to turn."
                : fromSeed
                    ? "Shape and colours from your game files and the tool's seed - drag to turn. Colours are close to the game's, not always exact."
                    : storedColours.Count > 0
                        ? "Shape and colours from your game files and save - drag to turn."
                        : "Shape and colours from your game files - drag to turn.";
        return new OwnedToolPreview(mesh, MaterialColour, painted, note);
    }

    /// <summary>The colours a tool keeps in the save, by (palette, slot) - e.g.
    /// ("Custom_Hands", "Alternative2") - as RGB 0-1. Empty for seed-made tools.</summary>
    public static Dictionary<(string, string), float[]> StoredColours(int index)
    {
        var result = new Dictionary<(string, string), float[]>();
        if (index < 0 || SaveSessionManager.GetValue("vLc", "6f=", "SuJ", index.ToString(CultureInfo.InvariantCulture), "7DH", "Aak") is not JArray entries)
            return result;
        foreach (var e in entries)
        {
            string? palette = e["RVl"]?["RVl"]?.Value<string>(), slot = e["RVl"]?["Ty="]?.Value<string>();
            if (palette is null || slot is null || e["xEg"] is not JArray rgba || rgba.Count < 3) continue;
            result[(palette, slot)] = new[] { rgba[0].Value<float>(), rgba[1].Value<float>(), rgba[2].Value<float>() };
        }
        return result;
    }

    /// <summary>Greys by material name, so the parts read apart: glass and lit
    /// parts by what they are, dark trims darker.</summary>
    public static uint MaterialColour(string material)
    {
        string name = System.IO.Path.GetFileName(material).ToUpperInvariant();
        static uint Rgb(int r, int g, int b) => (uint)(r << 16 | g << 8 | b);
        if (Regex.IsMatch(name, "GLASS|SCREEN|LENS|TRANS_")) return Rgb(32, 62, 82);
        if (Regex.IsMatch(name, "LIGHT|GLOW|EMIS|LASER")) return Rgb(255, 214, 140);
        if (Regex.IsMatch(name, "RUBBER|DARK|BLACK|GRIP|GRILL|VENT")) return Rgb(46, 48, 53);
        if (Regex.IsMatch(name, "TRIM|METAL|CHROME")) return Rgb(122, 127, 136);
        return Rgb(158, 162, 170);
    }
}
