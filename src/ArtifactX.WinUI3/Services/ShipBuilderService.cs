using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Data loading, colour lookup, and save-staging operations for the Ship Designer
/// (2026-10-16, GitHub #5).
///
/// Writing a design into the save follows the way the game stores a ship built at a
/// station (in-game build diff, "The Eagle of Beauty"):
/// - the ship entry: model (NTx.93M), a new seed in NTx.@EL and pMa.@EL, no name
///   of its own (NKm "", so the game names it from the seed);
/// - its customisation entry (l:j[3 + slot], slots 6-11 at 17+): the parts by
///   part group (SMP "^FIGHT_COCKAA"), the swatch set (FCx "^SHIP_METALLIC"), the
///   style (T>1 [{@6c "^SHIP_FIGHT", =Cv "^STEALTH"}]) and all 66 palettes x 5
///   colours (Aak). The builder fills the slots the player doesn't pick from the
///   swatch set; most were the dark metal grey (0.351), which is used here for all.
/// Edits are staged like every other page: nothing is written until Save.
/// </summary>
public static class ShipBuilderService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] Alts = { "Primary", "Alternative1", "Alternative2", "Alternative3", "Alternative4" };

    public static async Task<ShipBuilderCatalogData> LoadCatalogDataAsync()
    {
        var rows = await CatalogService.GetJsonCategoryAsync("ShipBuilder");
        static bool IsList(string json, string field) =>
            json.TrimStart().StartsWith("[", StringComparison.Ordinal) && json.Contains($"\"{field}\"", StringComparison.Ordinal);

        var types = rows.Where(r => r.Value.TrimStart().StartsWith("{", StringComparison.Ordinal))
            .Select(r => JsonSerializer.Deserialize<BuilderType>(r.Value, Json)).OfType<BuilderType>()
            .Where(t => t.Enabled && t.Slots.Count > 0)
            .OrderBy(t => t.Type switch { "Fighter" => 0, "Dropship" => 1, "Scientific" => 2, "Sail" => 3, _ => 4 })
            .ToList();

        // Style rows list options, swatch rows colours ("SHIP_METALLIC" is a swatch set).
        var styles = rows.Where(r => IsList(r.Value, "option"))
            .ToDictionary(r => r.Key, r => JsonSerializer.Deserialize<List<PaintStyle>>(r.Value, Json) ?? new());
        var swatches = rows.Where(r => IsList(r.Value, "rgb"))
            .ToDictionary(r => r.Key, r => JsonSerializer.Deserialize<List<Swatch>>(r.Value, Json) ?? new());

        if (types.Count == 0)
            return new ShipBuilderCatalogData(types, styles, swatches, new(StringComparer.OrdinalIgnoreCase));

        var partGroups = (await CatalogService.GetPartGroupsAsync())
            .GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Descriptors, StringComparer.OrdinalIgnoreCase);

        // Part names and icons, and the names of the items that unlock paint styles.
        await CatalogService.WarmCacheAsync(types.SelectMany(t => t.Slots).SelectMany(s => s.Items).Select(i => i.Item)
            .Concat(styles.Values.SelectMany(l => l).Select(s => s.Unlock).Where(u => u.Length > 0)));
        await ShipPartsService.EnsureLoadedAsync();

        return new ShipBuilderCatalogData(types, styles, swatches, partGroups);
    }

    /// <summary>The colour settings for the chosen style: the entry for its option, or the type's only one.</summary>
    public static ColourData? CurrentColours(BuilderType? type, PaintStyle? style) =>
        type is null ? null
            : type.Colours.FirstOrDefault(c => c.Option == style?.Option) ?? type.Colours.FirstOrDefault(c => c.Option.Length == 0)
              ?? type.Colours.FirstOrDefault();

    /// <summary>The design's colours: each colour group sets its palette slots to the picked
    /// swatch; every other slot is the dark metal grey the game itself filled in on a
    /// built ship (0.351 - most palettes of "The Eagle of Beauty").</summary>
    public static Func<string, string, float[]?> DesignColours(
        BuilderType? type,
        PaintStyle? style,
        IReadOnlyDictionary<string, List<Swatch>> swatchesBySet,
        IReadOnlyDictionary<string, int> colourPicks)
    {
        var set = new Dictionary<(string, string), float[]>();
        var data = CurrentColours(type, style);
        var swatches = data is null ? new() : swatchesBySet.GetValueOrDefault(data.Swatches) ?? new();
        foreach (var group in data?.Groups ?? new())
        {
            int index = colourPicks.GetValueOrDefault(group.Title, group.DefaultIndex);
            if (index < 0 || index >= swatches.Count) continue;
            foreach (var slot in group.Slots.Where(s => s.Length > 1)) set[(slot[0], slot[1])] = swatches[index].Rgb;
        }
        var grey = new[] { 0.351f, 0.351f, 0.351f };
        return (palette, slot) => set.GetValueOrDefault((palette, slot == "Unique" ? "Alternative3" : slot)) ?? grey;
    }

    /// <summary>The ships a design can be applied to: regular ships only - a corvette's
    /// look is its build, and living and Sentinel ships have no builder.</summary>
    public static List<(int Index, string Scene, string Name)> RegularShips()
    {
        var result = new List<(int, string, string)>();
        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath) is not JArray ships) return result;
        for (int i = 0; i < ships.Count; i++)
        {
            string scene = ships[i]?["NTx"]?["93M"]?.Value<string>() ?? "";
            string upper = scene.ToUpperInvariant();
            if (scene.Length == 0 || ShipBaseService.IsCorvetteModel(scene) || upper.Contains("/BIOPARTS/")
                || upper.Contains("/SENTINELSHIP/") || upper.Contains("/VINTAGEINTERCEPTOR.")) continue;
            result.Add((i, scene, ships[i]?["NKm"]?.Value<string>() ?? ""));
        }
        return result;
    }

    public static ulong? TryParseSeed(int index)
    {
        string text = SaveSessionManager.GetValue(NmsInventoryContainer.ShipModelSeedPath(index))?.Value<string>()?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong seed) ? seed : null;
    }

    public static string[] CustomisationPath(int slot) =>
        new[] { "vLc", "6f=", "l:j", NewFromTemplateService.ShipCustomisationIndex(slot).ToString(CultureInfo.InvariantCulture) };

    public static string? ReactorClass(BuilderType type, IReadOnlyDictionary<string, string> parts) =>
        type.Slots.Where(s => s.Id.EndsWith("_CORE", StringComparison.Ordinal))
            .SelectMany(s => s.Items).FirstOrDefault(i => parts.Values.Contains(i.Item))?.Cls;

    /// <summary>Model and seed, as the station builder writes them.</summary>
    public static void WriteLook(JObject ship, string scene, ulong seed)
    {
        string seedHex = "0x" + seed.ToString("X16", CultureInfo.InvariantCulture);
        ship["NTx"]!["93M"] = scene;
        ship["NTx"]!["@EL"] = new JArray(true, seedHex);
        if (ship["pMa"] is JObject layout) layout["@EL"] = new JArray(true, seedHex);
    }

    /// <summary>The customisation entry for the design (see the class notes).</summary>
    public static JObject CustomisationEntry(
        BuilderType type,
        PaintStyle? style,
        IReadOnlyDictionary<string, string> parts,
        IReadOnlyDictionary<string, List<Swatch>> swatchesBySet,
        IReadOnlyDictionary<string, int> colourPicks)
    {
        var data = CurrentColours(type, style);
        var colour = DesignColours(type, style, swatchesBySet, colourPicks);
        var aak = new JArray();
        foreach (var palette in Enum.GetNames(typeof(libMBIN.NMS.Toolkit.TkPaletteTexture.PaletteEnum)).Take(66))
            foreach (var alt in Alts)
            {
                var rgb = colour(palette, alt) ?? new[] { 0.351f, 0.351f, 0.351f };
                aak.Add(new JObject
                {
                    ["RVl"] = new JObject { ["RVl"] = palette, ["Ty="] = alt, ["3ZH"] = -1 },
                    ["xEg"] = new JArray(rgb[0], rgb[1], rgb[2], 1.0f),
                });
            }
        var styles = new JArray();
        if (style is not null)
            styles.Add(new JObject { ["@6c"] = "^" + type.StyleGroup, ["=Cv"] = "^" + style.Option });
        return new JObject
        {
            ["VFd"] = "^",
            ["wnR"] = new JObject
            {
                // The parts as part-group ids, slot by slot - not item ids. They're the same
                // for most parts (FIGHT_COCKAA), but an Explorer's wings share item ids
                // across sides (SCIEN_WINGE) with side groups (SCIEN_L_WINGE / SCIEN_R_WINGE):
                // the game dropped item-id wings on load and kept only the hull (2026-10-17).
                // A reactor has no group - a crafting input, not a look part.
                ["SMP"] = new JArray(type.Slots
                    .SelectMany(slot => slot.Items.FirstOrDefault(i => i.Item == parts.GetValueOrDefault(slot.Id))?.Groups ?? new())
                    .Select(group => (object)("^" + group)).ToArray()),
                ["FCx"] = "^" + (data?.Swatches ?? "SHIP"),
                ["Aak"] = aak,
                ["T>1"] = styles,
                ["gsg"] = new JArray(),
                ["unY"] = 1.0,
            },
        };
    }

    public static void StageNewShip(
        int skeleton,
        int slot,
        BuilderType type,
        ulong seed,
        PaintStyle? style,
        IReadOnlyDictionary<string, string> parts,
        IReadOnlyDictionary<string, List<Swatch>> swatchesBySet,
        IReadOnlyDictionary<string, int> colourPicks)
    {
        var ship = (JObject)SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath.Append(skeleton.ToString(CultureInfo.InvariantCulture)).ToArray())!.DeepClone();
        ship["NKm"] = "";
        WriteLook(ship, type.Scene, seed);
        var cargo = (JObject)ship[";l5"]!;
        cargo[":No"] = new JArray();
        if (ship["gan"] is JObject legacyCargo) legacyCargo[":No"] = new JArray();
        if (ship["FA8"] is JArray) ship["FA8"] = new JArray();
        // The reactor sets the class, as at a station.
        if (ReactorClass(type, parts) is string cls)
            foreach (var store in new[] { ship["PMT"], ship[";l5"] }.OfType<JObject>()) store["B@N"] = new JObject { ["1o6"] = cls };
        SaveSessionManager.StageEdit(ship, NmsInventoryContainer.ShipArrayPath.Append(slot.ToString(CultureInfo.InvariantCulture)).ToArray());
        SaveSessionManager.StageEdit(CustomisationEntry(type, style, parts, swatchesBySet, colourPicks), CustomisationPath(slot));
    }

    public static void StageApplyDesign(
        int target,
        bool clearCustomName,
        BuilderType type,
        ulong seed,
        PaintStyle? style,
        IReadOnlyDictionary<string, string> parts,
        IReadOnlyDictionary<string, List<Swatch>> swatchesBySet,
        IReadOnlyDictionary<string, int> colourPicks)
    {
        var path = NmsInventoryContainer.ShipArrayPath.Append(target.ToString(CultureInfo.InvariantCulture)).ToArray();
        var ship = (JObject)SaveSessionManager.GetValue(path)!.DeepClone();
        WriteLook(ship, type.Scene, seed);
        if (clearCustomName) ship["NKm"] = "";
        SaveSessionManager.StageEdit(ship, path);
        SaveSessionManager.StageEdit(CustomisationEntry(type, style, parts, swatchesBySet, colourPicks), CustomisationPath(target));
    }

    public static async Task<ShipPaintTarget?> ReadPaintTargetAsync(int index, IReadOnlyList<BuilderType> types)
    {
        var ship = SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath.Append(index.ToString(CultureInfo.InvariantCulture)).ToArray()) as JObject;
        string scene = ship?["NTx"]?["93M"]?.Value<string>() ?? "";
        var type = types.FirstOrDefault(t => string.Equals(t.Scene.Replace('\\', '/'), scene.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
        if (ship is null || type is null) return null;

        ulong seed = TryParseSeed(index) ?? 0;
        string name = ship["NKm"]?.Value<string>() ?? "";
        string shipName = name.Length > 0 ? name : await StarshipService.ShipNameAsync(seed, scene) ?? $"Starship {index + 1}";
        var data = SaveSessionManager.GetValue(CustomisationPath(index).Append("wnR").ToArray()) as JObject;
        var smpClone = data?["SMP"]?.DeepClone();
        var storedParts = (data?["SMP"] as JArray)?.Select(t => t.ToString().TrimStart('^')).Where(t => t.Length > 0).ToList() ?? new();
        bool hasPaint = (data?["Aak"] as JArray)?.Count > 0;
        return new ShipPaintTarget(index, type, seed, shipName, data, smpClone, storedParts, hasPaint);
    }

    /// <summary>Starts from the ship's own paint: its style, and for each colour the
    /// nearest swatch to what the save holds.</summary>
    public static PaintStyle? PickCurrentPaint(
        JObject data,
        BuilderType type,
        IReadOnlyDictionary<string, List<PaintStyle>> stylesByGroup,
        IReadOnlyDictionary<string, List<Swatch>> swatchesBySet,
        PaintStyle? currentStyle,
        Dictionary<string, int> colourPicks)
    {
        string option = (data["T>1"] as JArray)?.FirstOrDefault()?["=Cv"]?.ToString().TrimStart('^') ?? "";
        if ((stylesByGroup.GetValueOrDefault(type.StyleGroup) ?? new()).FirstOrDefault(s => s.Option == option) is { } style)
        {
            currentStyle = style;
            colourPicks.Clear();
            foreach (var group in CurrentColours(type, currentStyle)?.Groups ?? new())
                colourPicks[group.Title] = group.DefaultIndex;
        }
        var colours = CurrentColours(type, currentStyle);
        var swatches = colours is null ? new() : swatchesBySet.GetValueOrDefault(colours.Swatches) ?? new();
        var stored = (data["Aak"] as JArray ?? new JArray()).OfType<JObject>().ToList();
        foreach (var group in colours?.Groups ?? new())
        {
            if (group.Slots.FirstOrDefault(s => s.Length > 1) is not { } slot) continue;
            var entry = stored.FirstOrDefault(e => e["RVl"]?["RVl"]?.ToString() == slot[0] && e["RVl"]?["Ty="]?.ToString() == slot[1]);
            if (entry?["xEg"] is not JArray rgb || rgb.Count < 3 || swatches.Count == 0) continue;
            float r = rgb[0].Value<float>(), g = rgb[1].Value<float>(), b = rgb[2].Value<float>();
            colourPicks[group.Title] = Enumerable.Range(0, swatches.Count).MinBy(i =>
                MathF.Pow(swatches[i].Rgb[0] - r, 2) + MathF.Pow(swatches[i].Rgb[1] - g, 2) + MathF.Pow(swatches[i].Rgb[2] - b, 2));
        }
        return currentStyle;
    }

    public static void StageRepaint(
        int index,
        JToken? paintShipSmp,
        BuilderType type,
        PaintStyle? style,
        IReadOnlyDictionary<string, string> parts,
        IReadOnlyDictionary<string, List<Swatch>> swatchesBySet,
        IReadOnlyDictionary<string, int> colourPicks)
    {
        var entry = CustomisationEntry(type, style, parts, swatchesBySet, colourPicks);
        // The parts stay the ship's own: a built ship's list as it was, otherwise none.
        ((JObject)entry["wnR"]!)["SMP"] = paintShipSmp?.DeepClone() ?? new JArray();
        SaveSessionManager.StageEdit(entry, CustomisationPath(index));
    }

    /// <summary>Reads the account's unlocked rewards, which <see cref="IsPaintStyleUnlocked"/> needs. Safe to call
    /// again; a new save session loads its own.</summary>
    public static Task LoadPaintUnlocksAsync() => VehicleUnlockEditor.Current.LoadAsync();

    /// <summary>Whether a paint style's reward item (PaintStyle.Unlock, the game's ProductsToUnlock, e.g.
    /// EXPD_SHIPMAT17 for Titan Stripes) is unlocked; null when that can't be told (no save, or the account's
    /// rewards aren't read) - never shown as locked then.
    /// The unlock is ACCOUNT-WIDE, not per save. In-game test 2026-11-11: Slot 8's KnownSpecials (24&lt;) holds none
    /// of EXPD_SHIPMAT13 / EXPD_SHIP_METAL / EXPD_SHIPMAT17, yet its space station fabricator offered all five
    /// Fighter paint styles - the owner's account owns the three rewards. (The owner's other saves do list them in
    /// 24&lt;, so the game copies them there, but the list isn't what gates the fabricator.) So unlocked = in the
    /// account's rewards (accountdata B89 lists and the user settings, as <see cref="VehicleUnlockEditor"/> reads
    /// them) or in the save's 24&lt;.
    /// NOT YET CONFIRMED in game: a style showing locked, and <see cref="StageUnlockPaintStyle"/> unlocking it - the
    /// owner's account has every style (GitHub #1).</summary>
    public static bool? IsPaintStyleUnlocked(string unlockId)
    {
        if (!SaveSessionManager.IsSaveLoaded || VehicleUnlockEditor.Current.Account is not { } account) return null;
        string id = CatalogService.NormalizeId(unlockId);
        return account.Rewards.Contains(id)
            || (SaveSessionManager.GetValue(NmsGameCatalogPaths.KnownSpecialsPath) as JArray)?.Any(t => CatalogService.NormalizeId(t.Value<string>() ?? "") == id) == true;
    }

    /// <summary>Stages the reward the way the app grants any expedition customisation reward
    /// (<see cref="VehicleUnlockEditor.Set"/>: the save's 24&lt;, the account's reward list and the user settings),
    /// written by the title bar's Save and undone by its Reset. Returns why it couldn't, or null when staged.</summary>
    public static string? StageUnlockPaintStyle(string unlockId)
    {
        try
        {
            VehicleUnlockEditor.Current.Set(CatalogService.NormalizeId(unlockId), [], true);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public static void StageRemovePaint(int index)
    {
        SaveSessionManager.StageEdit(new JObject
        {
            ["VFd"] = "^",
            ["wnR"] = new JObject
            {
                ["SMP"] = new JArray(), ["FCx"] = "^", ["Aak"] = new JArray(), ["T>1"] = new JArray(), ["gsg"] = new JArray(), ["unY"] = 1.0,
            },
        }, CustomisationPath(index));
    }

    public static async Task<byte[]> EncodeThumbnailPngAsync(byte[] pixels, int width, int height)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();
        var bytes = new byte[stream.Size];
        stream.Seek(0);
        await stream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
        return bytes;
    }

    public static async Task<BitmapImage?> DecodeBitmapAsync(byte[] png)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(png.AsBuffer());
            stream.Seek(0);
            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
