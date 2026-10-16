using System.Text.Json;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// The part trees starships are built from (2026-10-14, experiment/ship-parts):
    /// every *.descriptor.mbin under MODELS/COMMON/SPACECRAFT/ (TkModelDescriptorList -
    /// groups of options, an option can hold nested groups and reference other
    /// scenes that have descriptors of their own). With the ship's seed these say
    /// which parts the game picks (ArtifactX.Core ShipParts).
    ///
    /// Category "ShipDescriptors": GameId "DESC:" + lower-case descriptor path,
    /// NameEnglish = the path, DescriptionEnglish = the tree as JSON
    /// [{"t":type,"o":[{"id","n":name,"s":[scene paths],"c":[[groups]...]}]}].
    /// </summary>
    public static void AddShipDescriptors(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}");
            return;
        }

        static List<object> Groups(TkModelDescriptorList? list) =>
            list?.List?.Select(g => (object)new Dictionary<string, object>
            {
                ["t"] = g.TypeId?.Value ?? "",
                ["o"] = g.Descriptors?.Select(d => (object)new Dictionary<string, object>
                {
                    ["id"] = d.Id?.Value ?? "",
                    ["n"] = d.Name?.Value ?? "",
                    ["s"] = d.ReferencePaths?.Select(r => r?.Value ?? "").Where(r => r.Length > 0).ToList() ?? new List<string>(),
                    ["c"] = d.Children?.OfType<TkModelDescriptorList>().Select(c => (object)Groups(c)).ToList() ?? new List<object>(),
                }).ToList() ?? new List<object>(),
            }).ToList() ?? new List<object>();

        var reader = new PakReaderService();
        var extraction = new ExtractionService();
        var rows = new List<CatalogItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            Models.PakHeader header;
            IReadOnlyList<Models.PakEntry> entries;
            try { header = reader.ReadHeader(pak.FullPath); entries = reader.Read(pak.FullPath); }
            catch { continue; }
            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.FileName)
                         && e.FileName.StartsWith("MODELS/COMMON/SPACECRAFT/", StringComparison.OrdinalIgnoreCase)
                         && e.FileName.EndsWith(".DESCRIPTOR.MBIN", StringComparison.OrdinalIgnoreCase)))
            {
                if (!seen.Add(entry.FileName)) continue;
                try
                {
                    var bytes = extraction.ExtractEntryBytes(pak.FullPath, entry, header);
                    if (bytes is null || bytes.Length == 0) continue;
                    var mbin = new MBINFile(new MemoryStream(bytes));
                    mbin.Load();
                    if (mbin.GetData() is not TkModelDescriptorList list) continue;
                    string path = entry.FileName.ToLowerInvariant();
                    rows.Add(new CatalogItem
                    {
                        GameId = "DESC:" + path,
                        NameEnglish = path,
                        UsageCategory = "Spacecraft",
                        DescriptionEnglish = JsonSerializer.Serialize(Groups(list)),
                    });
                }
                catch (Exception ex) { ConsoleStyle.Warning($"{entry.FileName}: {ex.Message}"); }
            }
        }
        ConsoleStyle.Header($"Found {rows.Count} starship part trees.");

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "ShipDescriptors", "TkModelDescriptorList", "models/common/spacecraft/*.descriptor.mbin", rows);
        ConsoleStyle.Success($"Wrote {rows.Count} starship part trees to {catalogPath}.");
    }
}

public static partial class CatalogSteps
{
    /// <summary>
    /// Which ship-builder part (an item with a name and icon, e.g. FIGHT_COCKAA
    /// "Omega Fuselage") switches on which descriptor options ("_COCKPIT_A",
    /// "_ANOSE_A") - charactercustomisationdescriptorgroupsdata.mbin
    /// (GcCustomisationDescriptorGroups; despite the name it covers ships,
    /// multi-tools, freighters... too). Category "PartGroups": GameId
    /// "PARTGROUP:" + group id, NameEnglish = group id (the product's GameId),
    /// UsageCategory = its set ("FIGHT_COCK"), DescriptionEnglish = JSON list of
    /// descriptor ids. Lets the Starships page show a seed's parts by name and icon.
    /// </summary>
    public static void AddPartGroups(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath)) { ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}"); return; }
        var reader = new PakReaderService();
        var extraction = new ExtractionService();
        const string file = "METADATA/GAMESTATE/PLAYERDATA/CHARACTERCUSTOMISATIONDESCRIPTORGROUPSDATA.MBIN";
        libMBIN.NMS.GameComponents.GcCustomisationDescriptorGroups? table = null;
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            Models.PakHeader header;
            IReadOnlyList<Models.PakEntry> entries;
            try { header = reader.ReadHeader(pak.FullPath); entries = reader.Read(pak.FullPath); }
            catch { continue; }
            var entry = entries.FirstOrDefault(e => string.Equals(e.FileName, file, StringComparison.OrdinalIgnoreCase));
            if (entry is null) continue;
            try
            {
                var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak.FullPath, entry, header)!));
                mbin.Load();
                table = mbin.GetData() as libMBIN.NMS.GameComponents.GcCustomisationDescriptorGroups;
            }
            catch (Exception ex) { ConsoleStyle.Warning($"Couldn't read the part groups: {ex.Message}"); }
            break;
        }
        var rows = new List<CatalogItem>();
        foreach (var set in table?.DescriptorGroupSets ?? new())
            foreach (var g in set.DescriptorGroups ?? new())
            {
                string id = g.GroupID?.Value ?? "";
                var descriptors = g.Descriptors?.Select(d => d?.Value ?? "").Where(d => d.Length > 0).ToList() ?? new();
                if (id.Length == 0 || descriptors.Count == 0) continue;
                rows.Add(new CatalogItem
                {
                    GameId = "PARTGROUP:" + id,
                    NameEnglish = id,
                    UsageCategory = set.Id?.Value ?? "",
                    DescriptionEnglish = JsonSerializer.Serialize(descriptors),
                });
            }
        ConsoleStyle.Header($"Found {rows.Count} customisation part groups.");
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "PartGroups", "GcCustomisationDescriptorGroup", file.ToLowerInvariant(), rows);
        ConsoleStyle.Success($"Wrote {rows.Count} part groups to {catalogPath}.");
    }
}

public static partial class CatalogSteps
{
    /// <summary>
    /// The game's 66 base colour palettes (metadata/simulation/solarsystem/
    /// colours/basecolourpalettes.mbin, GcPaletteList) - with a ship's seed they
    /// give its paint colours (ArtifactX.Core ShipColours). Category "Palettes":
    /// GameId "PALETTE:" + index, UsageCategory = NumColours (_1/_4/_8/_16/All/
    /// Inactive), DescriptionEnglish = the 64 colours as JSON [[r,g,b],...]
    /// (exact float32 values - the colour picker compares them).
    /// </summary>
    public static void AddPalettes(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath)) { ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}"); return; }
        const string file = "METADATA/SIMULATION/SOLARSYSTEM/COLOURS/BASECOLOURPALETTES.MBIN";
        var reader = new PakReaderService();
        var extraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcPaletteList? list = null;
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            Models.PakHeader header;
            IReadOnlyList<Models.PakEntry> entries;
            try { header = reader.ReadHeader(pak.FullPath); entries = reader.Read(pak.FullPath); }
            catch { continue; }
            var entry = entries.FirstOrDefault(e => string.Equals(e.FileName, file, StringComparison.OrdinalIgnoreCase));
            if (entry is null) continue;
            try
            {
                var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak.FullPath, entry, header)!));
                mbin.Load();
                list = mbin.GetData() as libMBIN.NMS.GameComponents.GcPaletteList;
            }
            catch (Exception ex) { ConsoleStyle.Warning($"Couldn't read the colour palettes: {ex.Message}"); }
            break;
        }
        var rows = new List<CatalogItem>();
        for (int i = 0; i < (list?.Palettes?.Length ?? 0); i++)
        {
            var p = list!.Palettes[i];
            rows.Add(new CatalogItem
            {
                GameId = "PALETTE:" + i,
                NameEnglish = i.ToString(),
                UsageCategory = p.NumColours.ToString(),
                DescriptionEnglish = JsonSerializer.Serialize(p.Colours.Select(c => new[] { c.R, c.G, c.B })),
            });
        }
        ConsoleStyle.Header($"Found {rows.Count} colour palettes.");
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "Palettes", "GcPaletteData", file.ToLowerInvariant(), rows);
        ConsoleStyle.Success($"Wrote {rows.Count} colour palettes to {catalogPath}.");
    }
}

public static partial class CatalogSteps
{
    /// <summary>
    /// What a ship's surfaces are painted with (2026-10-15): every spacecraft
    /// scene as a pruned node tree, every material those scenes use, and every
    /// procedural texture list those materials sample - with the ship's seed these
    /// give which pattern and colour each surface gets (ArtifactX.Core ShipLook,
    /// port of NMS Shipwright's textures.py).
    ///
    /// Category "ShipScenes": GameId "SCENE:" + lower-case path, DescriptionEnglish =
    /// the node tree as JSON {"n":name,"m":material,"r":referenced scene,"c":[children]},
    /// keeping only nodes on the way to a material or a reference.
    /// Category "ShipMaterials": GameId "MATERIAL:" + path, DescriptionEnglish = JSON
    /// list of the texture lists its samplers use (repeated per sampler, as the game
    /// counts them). Category "TextureLists": GameId "TEXLIST:" + path,
    /// DescriptionEnglish = JSON layers [{"n","g","p","m","o":[{"n","a","pal","w","u"}]}].
    /// </summary>
    public static void AddShipLooks(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath)) { ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}"); return; }
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            try { index.Add(pak.FullPath, reader.ReadHeader(pak.FullPath), reader.Read(pak.FullPath)); }
            catch { /* unreadable PAK - skip */ }
        }
        var extraction = new ExtractionService();
        NMSTemplate? Decode(string path)
        {
            if (!index.TryFind(path.ToUpperInvariant(), out var pakPath, out var entry, out var header)) return null;
            try
            {
                var bytes = extraction.ExtractEntryBytes(pakPath, entry, header);
                if (bytes is null) return null;
                var mbin = new MBINFile(new MemoryStream(bytes));
                mbin.Load();
                return mbin.GetData();
            }
            catch { return null; }
        }

        // Scenes: every procedural ship root, then everything they reference.
        var scenes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var materials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var todo = new Stack<string>(index.Paths
            .Where(p => p.StartsWith("MODELS/COMMON/SPACECRAFT/", StringComparison.OrdinalIgnoreCase)
                     && p.EndsWith("_PROC.SCENE.MBIN", StringComparison.OrdinalIgnoreCase)));
        Dictionary<string, object>? Prune(libMBIN.NMS.Toolkit.TkSceneNodeData node, HashSet<string> refs)
        {
            string material = "", reference = "";
            foreach (var a in node.Attributes ?? new())
            {
                string name = a.Name?.Value ?? "", value = (a.Value?.Value ?? "").Replace('\\', '/');
                if (name == "MATERIAL") material = value;
                else if (name == "SCENEGRAPH") reference = value;
            }
            var kids = (node.Children ?? new()).Select(c => Prune(c, refs)).Where(c => c is not null).ToList();
            if (material.Length == 0 && reference.Length == 0 && kids.Count == 0) return null;
            if (material.Length > 0) materials.Add(material);
            if (reference.Length > 0) refs.Add(reference);
            return new Dictionary<string, object> { ["n"] = node.Name?.Value ?? "", ["m"] = material, ["r"] = reference, ["c"] = kids };
        }
        while (todo.Count > 0)
        {
            string path = todo.Pop();
            if (scenes.ContainsKey(path)) continue;
            if (Decode(path) is not libMBIN.NMS.Toolkit.TkSceneNodeData root) { scenes[path] = new Dictionary<string, object>(); continue; }
            var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            scenes[path] = Prune(root, refs) ?? new Dictionary<string, object>();
            foreach (var r in refs) if (!scenes.ContainsKey(r)) todo.Push(r);
        }

        // Materials -> the texture lists their samplers use.
        var materialLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var textureLists = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var ext = new System.Text.RegularExpressions.Regex(@"(\.[^./]*){1,4}$");
        foreach (var material in materials)
        {
            if (Decode(material) is not libMBIN.NMS.Toolkit.TkMaterialData data) continue;
            var lists = new List<string>();
            foreach (var sampler in data.Samplers ?? new())
            {
                string map = (sampler.Map?.Value ?? "").Replace('\\', '/');
                if (map.Length == 0) continue;
                string list = ext.Replace(map, "") + ".TEXTURE.MBIN";
                if (!textureLists.ContainsKey(list))
                {
                    if (Decode(list) is not libMBIN.NMS.Toolkit.TkProceduralTextureList tl) continue;
                    textureLists[list] = (tl.Layers ?? Array.Empty<libMBIN.NMS.Toolkit.TkProceduralTextureLayer>()).Select(l => new Dictionary<string, object>
                    {
                        ["n"] = l.Name?.Value ?? "", ["g"] = l.Group?.Value ?? "", ["p"] = l.Probability, ["m"] = l.SelectToMatchBase,
                        ["o"] = (l.Textures ?? new()).Select(t => new Dictionary<string, object>
                        {
                            ["n"] = t.Name?.Value ?? "", ["a"] = (int)t.Palette.ColourAlt, ["pal"] = (int)t.Palette.Palette,
                            ["w"] = t.Probability, ["u"] = (int)t.TextureGameplayUse,
                        }).ToList(),
                    }).ToList();
                }
                lists.Add(list);
            }
            if (lists.Count > 0) materialLists[material] = lists;
        }

        static CatalogItem Row(string prefix, string path, object json) => new()
        {
            GameId = prefix + path.ToLowerInvariant(),
            NameEnglish = path.ToLowerInvariant(),
            DescriptionEnglish = JsonSerializer.Serialize(json),
        };
        ConsoleStyle.Header($"Found {scenes.Count} ship scenes, {materialLists.Count} painted materials and {textureLists.Count} texture lists.");
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "ShipScenes", "TkSceneNodeData", "models/common/spacecraft/*.scene.mbin", scenes.Select(s => Row("SCENE:", s.Key, s.Value)).ToList());
        Replace(db, "ShipMaterials", "TkMaterialData", "*.material.mbin", materialLists.Select(m => Row("MATERIAL:", m.Key, m.Value)).ToList());
        Replace(db, "TextureLists", "TkProceduralTextureList", "*.texture.mbin", textureLists.Select(t => Row("TEXLIST:", t.Key, t.Value)).ToList());
        ConsoleStyle.Success($"Wrote ship looks to {catalogPath}.");
    }
}
