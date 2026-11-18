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

    // ------------------------------------------------------------ the Multi-Tool page

    private static string I(int index) => index.ToString(CultureInfo.InvariantCulture);

    /// <summary>A tool's whole save entry: name, type, class, stats and tech.</summary>
    public static string[] ToolPath(int index) => NmsInventoryContainer.MultiToolArrayPath.Append(I(index)).ToArray();

    /// <summary>Its base stat rolls (OsQ.@bB): the same values that feed Damage Potential / Scanner Range, independent of
    /// the Type (model) - identical across every model swap tested.</summary>
    public static string[] StatsPath(int index) => NmsInventoryContainer.MultiToolTechnologyPath(index).Append("@bB").ToArray();

    /// <summary>Its model scene (NTx.93M).</summary>
    public static string? Scene(int index) =>
        index < 0 ? null : SaveSessionManager.GetValue("vLc", "6f=", "SuJ", I(index), "NTx", "93M")?.Value<string>();

    /// <summary>
    /// The owned tools, in save order. A slot is owned when it has a model scene (NTx.93M) - not a name: a new save's
    /// starter tool has none yet (found 2026-08-10). The tool in hand is the one whose OsQ items match Kgt's, a live
    /// mirror of the equipped tool (OGV stays true on every tool ever equipped - disproved by a real switch-and-diff).
    /// Compared as (item, position) sets: exact JSON equality broke on harmless field-order/float differences after a
    /// repair. Two tools with the same loadout (Copy Tech Stack makes that easy) can't be told apart - the save holds no
    /// other signal (each tool's @EL seed appears only on its own entry) - so only the first match counts (2026-08-01).
    /// </summary>
    public static IReadOnlyList<OwnedMultiTool> ReadTools()
    {
        if (SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolArrayPath) is not JArray array) return [];
        var held = OccupiedSignature(SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolActivePath)?[":No"] as JArray);
        bool found = false;
        var tools = new List<OwnedMultiTool>();
        for (int i = 0; i < array.Count; i++)
        {
            if (string.IsNullOrEmpty(array[i]?["NTx"]?["93M"]?.Value<string>())) continue;
            string name = array[i]?["NKm"]?.Value<string>() ?? "";
            bool unnamed = name.Length == 0;
            bool active = !found && held.Count > 0 && held.SetEquals(OccupiedSignature(array[i]?["OsQ"]?[":No"] as JArray));
            found |= active;
            tools.Add(new OwnedMultiTool(i, unnamed ? $"Multi-Tool {i + 1}" : name, active, unnamed));
        }
        return tools;
    }

    /// <summary>"ItemId@X,Y" for every item in a :No array - which items sit where, nothing else.</summary>
    private static HashSet<string> OccupiedSignature(JArray? array)
    {
        var result = new HashSet<string>();
        foreach (var entry in array ?? [])
            if (entry["b2n"]?.Value<string>() is string id && entry["3ZH"] is { } pos)
                result.Add($"{id}@{pos[">Qh"]?.Value<int>() ?? -1},{pos["XJ>"]?.Value<int>() ?? -1}");
        return result;
    }

    /// <summary>
    /// Labels for tools the player never named: the game makes a name up from the seed ("Pillar of Ooruz-IX") and so
    /// does ArtifactX (MultiToolNames, 2026-10-16). Before the game data has the name words, the label is the type and
    /// class ("Standard · B class"), worded so it doesn't pass for a name. The same tool twice (same seed) has the same
    /// name in game too, so repeats get "(2)".
    /// </summary>
    public static async Task<IReadOnlyList<OwnedMultiTool>> LabelUnnamedAsync(IReadOnlyList<OwnedMultiTool> tools)
    {
        var labelled = new List<OwnedMultiTool>();
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tools)
        {
            if (!t.IsUnnamed) { labelled.Add(t); continue; }
            string scene = Scene(t.Index) ?? "";
            string? gameName = SeedOf(t.Index) is ulong seed ? await MultiToolKinds.GameNameAsync(seed, scene) : null;
            string? label = gameName;
            if (label is null)
            {
                string? kind = MultiToolKinds.IsMainModel(scene) ? await KindOfAsync(t.Index) : MultiToolKinds.NameFor(scene, "");
                if (string.IsNullOrEmpty(kind)) { labelled.Add(t); continue; }
                label = ClassLetter(t.Index) is { Length: > 0 } cls ? $"{kind} · {cls} class" : kind;
            }
            int n = used[label] = used.GetValueOrDefault(label) + 1;
            labelled.Add(t with { Name = n == 1 ? label : $"{label} ({n})", GameName = gameName });
        }
        return labelled;
    }

    /// <summary>Stages a typed name. Nothing when it's empty or what the box already showed (the player's name, or the
    /// game's for an unnamed tool), so the game's name never becomes a saved custom name by accident.</summary>
    public static bool Rename(OwnedMultiTool tool, string? text)
    {
        string name = text?.Trim() ?? "";
        if (name.Length == 0 || name == (tool.IsUnnamed ? tool.GameName : tool.Name)) return false;
        SaveSessionManager.StageEdit(name, "vLc", "6f=", "SuJ", I(tool.Index), "NKm");
        return true;
    }

    /// <summary>Stages the model scene (NTx.93M), the only field the visual swap needs (in-game test). jl;, which a
    /// reference tool writes too, was tested and has no effect on rendering, so it's left alone.</summary>
    public static void SetType(int index, string scenePath) =>
        SaveSessionManager.StageEdit(scenePath, "vLc", "6f=", "SuJ", I(index), "NTx", "93M");

    public static bool HasEdits(int index) => index >= 0 && SaveSessionManager.HasStagedEditsUnder(ToolPath(index));

    /// <summary>Undoes every staged change to one tool - tech, name and type.</summary>
    public static void RevertTool(int index)
    {
        if (index >= 0) SaveSessionManager.RevertEditsUnder(ToolPath(index));
    }

    /// <summary>The Type choices for a tool, by name. The main model's name is the kind this tool's seed makes (Alien,
    /// Experimental...); other models use the game's type names. IsMainModel: the kind is set by the seed.</summary>
    public static async Task<(IReadOnlyList<MultiToolTypeOption> Options, bool IsMainModel)> TypeOptionsAsync(int index)
    {
        var raw = await CatalogService.GetMultiToolTypesAsync();
        string? current = Scene(index);
        string mainName = MultiToolKinds.IsMainModel(current) && await KindOfAsync(index) is string kind ? kind : "Standard";
        var options = raw.Select(t => new MultiToolTypeOption(
                MultiToolKinds.IsMainModel(t.ScenePath) ? mainName : MultiToolKinds.NameFor(t.ScenePath, t.DisplayName), t.ScenePath))
            .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        return (options, MultiToolKinds.IsMainModel(current));
    }

    /// <summary>A tool as a Full Build template (Save Current, and "save it first" before deleting).</summary>
    public static NmsLoadoutTemplate CaptureFullBuild(ViewModels.InventoryGridViewModel tech, string templateName, OwnedMultiTool tool)
    {
        var template = tech.ExtractTemplate(templateName, tool.Name, "MultiTool");
        template.Scope = "FullBuild";
        template.Stats = StatBonusService.ReadAll(StatsPath(tool.Index));
        template.TypeScenePath = Scene(tool.Index);
        NewFromTemplateService.CaptureMultiTool(template, tool.Index);
        return template;
    }

    public static NmsLoadoutTemplate CaptureTechStack(ViewModels.InventoryGridViewModel tech, string templateName, OwnedMultiTool tool) =>
        tech.ExtractTemplate(templateName, tool.Name, "MultiTool");

    /// <summary>Why a Full Build can't be added as a new tool: no tool yet, or every slot in use.</summary>
    public static string NoRoomText(bool hasTool) => !hasTool
        ? "This save doesn't have a Multi-Tool yet. Get one in game first, then add builds here."
        : $"All {NewFromTemplateService.MultiToolSlotCount} Multi-Tool slots are in use. Sell or scrap one in game, then try again.";

    public static (string Title, string Body) AddAsNewText(NmsLoadoutTemplate template, int slot) =>
        ($"Add \"{template.Name}\" as a new Multi-Tool?",
            $"It goes in empty slot {slot + 1} of {NewFromTemplateService.MultiToolSlotCount} with this build's tech, class, stats and look. "
            + "Your other Multi-Tools aren't changed. Nothing is written to your save until you Save.");

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
