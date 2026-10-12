using ArtifactX.WinUI3.Models;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A template as written to a shared file: a small wrapper so the
/// file says what it is and which format version it uses.</summary>
public sealed class SharedTemplateFile
{
    public string Format { get; set; } = "";
    public int Version { get; set; }
    public string App { get; set; } = "";
    public NmsLoadoutTemplate? Template { get; set; }
}

/// <summary>
/// Share loadout templates (Starships, Multi-Tool, Freighter) as files, so
/// players can swap builds. Export writes the template with a small wrapper;
/// import treats the file as untrusted - it's checked field by field (size,
/// kind, scope, grid positions, amounts, item ids against the catalog, stat
/// keys, seed and model path shapes) and only then saved as a NEW template.
/// Nothing touches a save until the player clicks Apply, which goes through
/// the page's normal Apply (backups included).
/// </summary>
public static partial class TemplateSharingService
{
    public const string FormatName = "ArtifactX template";
    public const int FormatVersion = 1;
    private const long MaxFileBytes = 512 * 1024;
    private const int MaxTechItems = 200;
    private const int MaxGrid = 16;

    [GeneratedRegex(@"^\^?[A-Z0-9_]{1,40}(#\d{1,6})?$")]
    private static partial Regex ItemIdPattern();

    [GeneratedRegex(@"^\^[A-Z0-9_]{1,40}$")]
    private static partial Regex StatKeyPattern();

    [GeneratedRegex(@"^0x[0-9A-Fa-f]{1,16}$")]
    private static partial Regex SeedPattern();

    [GeneratedRegex(@"^MODELS/[A-Za-z0-9_/.]{1,200}\.SCENE\.MBIN$", RegexOptions.IgnoreCase)]
    private static partial Regex ScenePathPattern();

    /// <summary>What players call each template kind.</summary>
    public static string KindLabel(string kind) => kind switch
    {
        "Ship" => "Starship",
        "MultiTool" => "Multi-Tool",
        "Freighter" => "Freighter",
        _ => kind,
    };

    /// <summary>Asks where to save, then writes the template; the button shows the result briefly.</summary>
    public static async Task ExportAsync(Button button, NmsLoadoutTemplate template)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedFileName = SafeFileName($"{template.Name} - ArtifactX {KindLabel(template.SourceKind)} template"),
        };
        picker.FileTypeChoices.Add("ArtifactX template", new List<string> { ".json" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        object original = button.Content;
        try
        {
            var shared = new SharedTemplateFile
            {
                Format = FormatName,
                Version = FormatVersion,
                App = AppVersionService.DisplayVersion,
                Template = template,
            };
            string json = JsonSerializer.Serialize(shared, SharedTemplateJsonContext.Default.SharedTemplateFile);
            string path = file.Path;
            await File.WriteAllTextAsync(path, json);
            button.Content = "Saved ✓";
            ToolTipService.SetToolTip(button, path);
        }
        catch (Exception ex)
        {
            button.Content = "Failed";
            ToolTipService.SetToolTip(button, ex.Message);
        }
        await Task.Delay(2500);
        button.Content = original;
    }

    /// <summary>Asks for a template file, checks it, and saves it as a new
    /// template. Returns a message for the player (success or what's wrong),
    /// or null if they cancelled.</summary>
    public static async Task<(bool Ok, string Message)?> ImportAsync(string expectedKind)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;

        try
        {
            var info = new FileInfo(file.Path);
            if (info.Length > MaxFileBytes) return (false, "That file is too big to be a template.");
            string json = await File.ReadAllTextAsync(file.Path);

            SharedTemplateFile? shared;
            try { shared = JsonSerializer.Deserialize(json, SharedTemplateJsonContext.Default.SharedTemplateFile); }
            catch (JsonException) { return (false, "That file isn't an ArtifactX template."); }
            if (shared is null || shared.Format != FormatName || shared.Template is null)
                return (false, "That file isn't an ArtifactX template.");
            if (shared.Version > FormatVersion)
                return (false, "That template was made by a newer ArtifactX. Update ArtifactX to import it.");

            var template = shared.Template;
            if (template.SourceKind != expectedKind)
                return (false, $"That's a {KindLabel(template.SourceKind)} template - import it on the {KindLabel(template.SourceKind)} page.");

            string? problem = await CheckAsync(template);
            if (problem is not null) return (false, $"That template can't be used: {problem}");

            // Always a new template, so an import never replaces one the player already has.
            template.Id = Guid.NewGuid().ToString("N");
            template.CreatedAt = DateTimeOffset.Now;
            template.Name = template.Name.Trim();
            if (template.Name.Length == 0) template.Name = $"Imported {KindLabel(expectedKind)} template";
            if (template.Name.Length > 80) template.Name = template.Name[..80];
            template.SourceToolName = template.SourceToolName is { Length: > 0 } source
                ? $"{(source.Length > 60 ? source[..60] : source)} (imported)" : "imported";
            await LoadoutTemplateService.SaveAsync(template);
            // Full Builds add a new tool or ship (Freighter: only one per save, so it Applies).
            string action = template.Scope == "FullBuild" && template.SourceKind != "Freighter" ? "Add as new" : "Apply";
            return (true, $"Imported \"{template.Name}\". Use {action} when you're ready - nothing in your save changes until then.");
        }
        catch (Exception ex)
        {
            return (false, $"Couldn't read that file: {ex.Message}");
        }
    }

    /// <summary>Field-by-field check of an imported template; null when it's fine.</summary>
    private static async Task<string?> CheckAsync(NmsLoadoutTemplate t)
    {
        if (t.Scope is not ("TechStack" or "FullBuild")) return "it's not a Tech Stack or Full Build template.";
        if (t.TechItems.Count > MaxTechItems || t.UnlockedPositions.Count > MaxGrid * MaxGrid) return "it has too many slots.";
        if (t.SourceClass is { Length: > 0 } cls && cls is not ("S" or "A" or "B" or "C")) return "its class isn't S, A, B or C.";

        foreach (var p in t.UnlockedPositions)
            if (p.X < 0 || p.Y < 0 || p.X >= MaxGrid || p.Y >= MaxGrid) return "a slot is outside the grid.";

        var ids = new List<string>();
        var seen = new HashSet<(int, int)>();
        foreach (var item in t.TechItems)
        {
            if (item.X < 0 || item.Y < 0 || item.X >= MaxGrid || item.Y >= MaxGrid) return "an item is outside the grid.";
            if (!seen.Add((item.X, item.Y))) return "two items share a slot.";
            if (string.IsNullOrEmpty(item.ItemId) || !ItemIdPattern().IsMatch(item.ItemId)) return "an item id isn't valid.";
            if (item.Amount < -1 || item.MaxAmount < 0 || item.Amount > 100_000 || item.MaxAmount > 100_000) return "an item amount isn't valid.";
            if (!double.IsFinite(item.MalfunctionSeverity)) return "an item value isn't valid.";
            if (item.CategoryLabel is { Length: > 40 }) return "an item category isn't valid.";
            ids.Add(item.ItemId);
        }

        // Every item has to be one this version of the game data knows.
        await CatalogService.WarmCacheAsync(ids);
        var unknown = ids.Where(id => CatalogService.TryGet(id) is null).Select(CatalogService.NormalizeId).Distinct().ToList();
        if (unknown.Count > 0) return $"it has item{(unknown.Count == 1 ? "" : "s")} this version of ArtifactX doesn't know ({string.Join(", ", unknown.Take(5))}).";

        if (t.Scope == "FullBuild")
        {
            foreach (var stat in t.Stats)
                if (!StatKeyPattern().IsMatch(stat.Key) || !double.IsFinite(stat.Value) || Math.Abs(stat.Value) > 1_000_000) return "a stat isn't valid.";
            if (t.Seed is { Length: > 0 } seed && !SeedPattern().IsMatch(seed)) return "its look (seed) isn't valid.";
            if (t.TypeScenePath is { Length: > 0 } scene && !ScenePathPattern().IsMatch(scene)) return "its model isn't valid.";
            foreach (var p in (t.SuperchargedPositions ?? new()).Concat(t.CargoUnlockedPositions ?? new()))
                if (p.X < 0 || p.Y < 0 || p.X >= MaxGrid || p.Y >= MaxGrid) return "a slot is outside the grid.";
            if ((t.SuperchargedPositions?.Count ?? 0) > MaxGrid * MaxGrid || (t.CargoUnlockedPositions?.Count ?? 0) > MaxGrid * MaxGrid) return "it has too many slots.";
            if (t.PrimaryMode is < 0 or > 1000 || t.SecondaryMode is < 0 or > 1000) return "its weapon mode isn't valid.";
            if (t.Customisation is { Length: > 0 } paint && !IsCustomisation(paint)) return "its paint and parts aren't valid.";
            if (t.SourceKind != "MultiTool")
            {
                t.IsLarge = null;
                t.PrimaryMode = t.SecondaryMode = null;
                t.UseLegacyColours = null;
            }
            if (t.SourceKind is not ("Ship" or "Freighter")) t.CargoUnlockedPositions = null;
            if (t.SourceKind == "Freighter")
            {
                if (CheckInterior(t) is string interiorProblem) return interiorProblem;
            }
            else
            {
                t.InteriorObjects = null;
                t.InteriorPosition = t.FreighterAt = t.FreighterUp = null;
            }
        }
        else
        {
            // Tech Stack templates never carry these; drop anything extra.
            t.Stats.Clear();
            t.Seed = null;
            t.TypeScenePath = null;
            t.SuperchargedPositions = null;
            t.CargoUnlockedPositions = null;
            t.Customisation = null;
            t.IsLarge = null;
            t.PrimaryMode = t.SecondaryMode = null;
            t.UseLegacyColours = null;
            t.InteriorObjects = null;
            t.InteriorPosition = t.FreighterAt = t.FreighterUp = null;
        }
        return null;
    }

    /// <summary>A freighter interior: part ids in the game's own id shape,
    /// sane positions (parts sit within about 60 units of the base's anchor on
    /// every freighter checked; allow far more) and unit-ish directions.</summary>
    private static string? CheckInterior(NmsLoadoutTemplate t)
    {
        static bool Vector(double[]? v, double limit) => v is { Length: 3 } && v.All(c => double.IsFinite(c) && Math.Abs(c) <= limit);

        if (t.InteriorObjects is { } parts)
        {
            if (parts.Count > 20_000) return "its interior has too many parts.";
            foreach (var part in parts)
            {
                if (!ItemIdPattern().IsMatch(part.Id)) return "an interior part isn't valid.";
                if (!Vector(part.Position, 10_000) || !Vector(part.Up, 2) || !Vector(part.At, 2)) return "an interior part's position isn't valid.";
            }
        }
        if (t.InteriorPosition is not null && !Vector(t.InteriorPosition, 100_000)) return "its interior position isn't valid.";
        if (t.FreighterAt is not null && !Vector(t.FreighterAt, 2)) return "its facing isn't valid.";
        if (t.FreighterUp is not null && !Vector(t.FreighterUp, 2)) return "its facing isn't valid.";
        return null;
    }

    /// <summary>Paint and parts must be the game's own customisation shape and
    /// nothing else: a Multi-Tool's CustomisationData, or a Starship's
    /// customisation slot ({VFd: preset, wnR: CustomisationData}), small, with
    /// only known keys. It's written into the save as-is on Add as new.</summary>
    private static bool IsCustomisation(string json)
    {
        if (json.Length > 32_768) return false;
        Newtonsoft.Json.Linq.JObject obj;
        try { obj = Newtonsoft.Json.Linq.JObject.Parse(json); } catch { return false; }

        static bool IsData(Newtonsoft.Json.Linq.JToken? data) =>
            data is Newtonsoft.Json.Linq.JObject d
            && d.Properties().All(p => p.Name is "SMP" or "FCx" or "Aak" or "T>1" or "gsg" or "unY")
            && d["SMP"] is null or Newtonsoft.Json.Linq.JArray
            && d["FCx"] is null or { Type: Newtonsoft.Json.Linq.JTokenType.String };

        return obj.ContainsKey("wnR")
            ? obj.Properties().All(p => p.Name is "VFd" or "wnR") && obj["VFd"] is null or { Type: Newtonsoft.Json.Linq.JTokenType.String } && IsData(obj["wnR"])
            : IsData(obj);
    }

    private static string SafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        return name.Length > 100 ? name[..100] : name;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SharedTemplateFile))]
internal partial class SharedTemplateJsonContext : JsonSerializerContext { }
