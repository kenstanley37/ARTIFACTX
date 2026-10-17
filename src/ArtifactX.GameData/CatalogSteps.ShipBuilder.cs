using System.Text.Json;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    private const string BuilderFile = "METADATA/GAMESTATE/PLAYERDATA/MODULARCUSTOMISATIONDATATABLE.MBIN";
    private const string TextureOptionsFile = "METADATA/GAMESTATE/PLAYERDATA/CHARACTERCUSTOMISATIONTEXTUREOPTIONDATA.MBIN";
    private const string SwatchFile = "METADATA/GAMESTATE/PLAYERDATA/CUSTOMISATIONCOLOURPALETTES.MBIN";

    /// <summary>
    /// The game's starship builder (2026-10-16), for the Ship Designer: what a
    /// space station's ship fabricator offers, read from the player's own files.
    /// Checked against a ship built in game ("The Eagle of Beauty": parts
    /// FIGHT_COCKAA/FIGHT_WINGA/FIGHT_ENGIB, paint style 3 = STEALTH with the
    /// SHIP_METALLIC swatches, primary -> Undercoat/Primary, secondary ->
    /// Paint/Primary, accent -> Paint/Alternative3 - as the save stored it).
    ///
    /// Category "ShipBuilder", one row per kind:
    /// - GameId "SHIPBUILDER:" + type (Fighter, Dropship, Scientific, Shuttle, Sail):
    ///   JSON {type, title, scene, enabled, styleGroup, slots:[{id, label, items:[{item,
    ///   groups, cls}]}], colours:[{option, swatches, groups:[{title, defaultIndex,
    ///   slots:[[palette, alt]]}]}]}. Shuttles can't be built in game (enabled false).
    /// - GameId "SHIPSTYLES:" + group (SHIP_FIGHT...): JSON [{option, name, unlock,
    ///   layers:[[layer, option]]}] - the paint styles, in the builder's order.
    /// - GameId "SWATCHES:" + id (SHIP, SHIP_METALLIC...): JSON [{rgb, name, unlock}]
    ///   - the colours offered, as many as the game names.
    /// </summary>
    public static void AddShipBuilder(string catalogPath, string pcbanksPath)
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
            if (!index.TryFind(path, out var pak, out var entry, out var header)) return null;
            try
            {
                var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak, entry, header)!));
                mbin.Load();
                return mbin.GetData();
            }
            catch (Exception ex) { ConsoleStyle.Warning($"Couldn't read {path}: {ex.Message}"); return null; }
        }

        if (Decode(BuilderFile) is not GcModularCustomisationDataTable table
            || Decode(TextureOptionsFile) is not GcCustomisationTextureOptions textureOptions
            || Decode(SwatchFile) is not GcCustomisationColourPalettes swatchSets)
        {
            ConsoleStyle.Warning("The ship builder's files weren't found - skipped.");
            return;
        }

        static string S(object? value) => value?.ToString()?.Trim() ?? "";
        var shared = (table.SharedSlottableItemLists ?? new())
            .GroupBy(l => S(l.ListID)).ToDictionary(g => g.Key, g => g.First().SlottableItems ?? new());
        // Ships are the configs built on a spacecraft model (the table also holds the
        // staff builder, museum exhibits and station exteriors).
        var ships = (table.ModularCustomisationConfigs ?? Array.Empty<GcModularCustomisationConfig>())
            .Select((config, i) => (Config: config, Type: TypeName(table, i)))
            .Where(c => S(c.Config.BaseResource?.Filename).Contains("/SPACECRAFT/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        // Ships without a palette of their own use the one for ship slots (CustomisationTypePalettes Ship_01).
        int ship01 = Array.IndexOf(Enum.GetNames(typeof(GcCustomisationComponentData.CustomisationDataTypeEnum)), "Ship_01");
        string defaultSwatches = ship01 >= 0 ? S(swatchSets.CustomisationTypePalettes?.ElementAtOrDefault(ship01)) : "";
        if (defaultSwatches.Length == 0) defaultSwatches = "SHIP";

        // The text keys to translate, gathered first: one pass over the language files.
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var styleGroups = ships.SelectMany(s => s.Config.TextureData ?? new()).Select(t => S(t.TextureOptionGroup)).Where(g => g.Length > 0).ToHashSet();
        // Styles come in two shapes: several layers per style (SHIP_FIGHT, SHIP_SAIL) or one
        // layer with a list of options (SHIP_SCI - Explorers: SHINY, DEFAULT).
        var styles = (textureOptions.MultiTextureOptions ?? new())
            .Where(o => styleGroups.Contains(S(o.MultiTextureOptionsID)))
            .Select(o => (Id: S(o.MultiTextureOptionsID), Tips: o.Tips ?? new(), Unlocks: o.ProductsToUnlock ?? new(),
                Options: (o.Options ?? new()).Select(x => (Name: S(x.TextureOptionsID),
                    Layers: (x.SubOptions ?? new()).Select(sub => new[] { S(sub.Layer), S(sub.Option) }).ToList())).ToList()))
            .Concat((textureOptions.TextureOptions ?? new())
                .Where(o => styleGroups.Contains(S(o.TextureOptionsID)))
                .Select(o => (Id: S(o.TextureOptionsID), Tips: o.Tips ?? new(), Unlocks: o.ProductsToUnlock ?? new(),
                    Options: (o.Options ?? new()).Select(x => (Name: S(x), Layers: new List<string[]> { new[] { S(o.Layer), S(x) } })).ToList())))
            .ToList();
        var swatchIds = ships.SelectMany(s => s.Config.ColourDataPriorityList ?? new())
            .Select(c => S(c.PaletteID) is { Length: > 0 } id ? id : defaultSwatches).ToHashSet();
        var swatches = (swatchSets.Palettes ?? new()).Where(p => swatchIds.Contains(S(p.ID))).ToList();
        foreach (var (config, _) in ships)
        {
            wanted.Add(S(config.TitleLocId));
            foreach (var slot in config.Slots ?? new()) wanted.Add(S(slot.LabelLocID));
            foreach (var colour in config.ColourDataPriorityList ?? new())
                foreach (var group in colour.ColourGroups ?? new()) wanted.Add(S(group.Title));
        }
        foreach (var style in styles) foreach (var tip in style.Tips) wanted.Add(S(tip));
        foreach (var set in swatches) foreach (var tip in set.ExtraData?.TipText ?? new()) wanted.Add(S(tip));
        var english = EnglishTexts(index, extraction, wanted);
        string Text(object? key) => english.GetValueOrDefault(S(key)) ?? S(key);

        var rows = new List<CatalogItem>();
        foreach (var (config, type) in ships)
        {
            var slots = (config.Slots ?? new()).Select(slot => new
            {
                id = S(slot.SlotID),
                label = Text(slot.LabelLocID),
                items = (slot.SlottableItems ?? new())
                    .Concat((slot.AdditionalSlottableItemLists ?? new()).SelectMany(l => shared.GetValueOrDefault(S(l)) ?? new()))
                    .Where(item => S(item.ItemID).Length > 0)
                    .Select(item => new
                    {
                        item = S(item.ItemID),
                        groups = (item.DescriptorGroupData ?? new()).Select(g => S(g.ActivatedDescriptorGroupID)).Where(g => g.Length > 0).ToList(),
                        cls = item.SetInventoryClass ? item.InventoryClass?.InventoryClass.ToString() : null,
                    }).ToList(),
            }).ToList();
            var colours = (config.ColourDataPriorityList ?? new()).Select(c => new
            {
                option = S(c.RequiredTextureOption),
                swatches = S(c.PaletteID) is { Length: > 0 } id ? id : defaultSwatches,
                groups = (c.ColourGroups ?? new()).Select(g => new
                {
                    title = Text(g.Title),
                    defaultIndex = g.DefaultColourIndex,
                    slots = (g.Palettes ?? new()).Select(p => new[] { p.Palette.ToString(), p.ColourAlt.ToString() }).ToList(),
                }).ToList(),
            }).ToList();
            rows.Add(new CatalogItem
            {
                GameId = "SHIPBUILDER:" + type,
                NameEnglish = type,
                UsageCategory = S(config.BaseResource?.Filename),
                DescriptionEnglish = JsonSerializer.Serialize(new
                {
                    type,
                    title = Text(config.TitleLocId),
                    scene = S(config.BaseResource?.Filename),
                    enabled = config.IsEnabled,
                    styleGroup = (config.TextureData ?? new()).Select(t => S(t.TextureOptionGroup)).FirstOrDefault() ?? "",
                    slots,
                    colours,
                }),
            });
        }
        foreach (var style in styles)
        {
            rows.Add(new CatalogItem
            {
                GameId = "SHIPSTYLES:" + style.Id,
                NameEnglish = style.Id,
                DescriptionEnglish = JsonSerializer.Serialize(style.Options.Select((o, i) => new
                {
                    option = o.Name,
                    name = style.Tips.ElementAtOrDefault(i) is { } tip ? Text(tip) : o.Name,
                    unlock = S(style.Unlocks.ElementAtOrDefault(i)),
                    layers = o.Layers,
                })),
            });
        }
        foreach (var set in swatches)
        {
            var tips = set.ExtraData?.TipText ?? new();
            var unlocks = set.ExtraData?.ProductToUnlock ?? new();
            var colours = set.PaletteData?.Colours ?? Array.Empty<libMBIN.NMS.Colour>();
            // The game offers as many swatches as it names; the rest of the 64 are unused.
            int count = tips.Count > 0 ? Math.Min(tips.Count, colours.Length) : colours.Length;
            rows.Add(new CatalogItem
            {
                GameId = "SWATCHES:" + S(set.ID),
                NameEnglish = S(set.ID),
                DescriptionEnglish = JsonSerializer.Serialize(Enumerable.Range(0, count).Select(i => new
                {
                    rgb = new[] { colours[i].R, colours[i].G, colours[i].B },
                    name = tips.ElementAtOrDefault(i) is { } tip ? Text(tip) : "",
                    unlock = S(unlocks.ElementAtOrDefault(i)),
                })),
            });
        }

        ConsoleStyle.Header($"Found {ships.Count} ship builders, {styles.Count} paint style sets, {swatches.Count} swatch sets.");
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "ShipBuilder", "GcModularCustomisationConfig", BuilderFile.ToLowerInvariant(), rows);
        ConsoleStyle.Success($"Wrote {rows.Count} ship builder rows to {catalogPath}.");
    }

    /// <summary>The builder table's configs are a fixed array named by the game's
    /// customisation types; libMBIN keeps the names on the enum the array is sized by.</summary>
    private static string TypeName(GcModularCustomisationDataTable table, int i)
    {
        var field = typeof(GcModularCustomisationDataTable).GetField(nameof(GcModularCustomisationDataTable.ModularCustomisationConfigs));
        var names = field?.GetCustomAttributes(typeof(NMSAttribute), false).OfType<NMSAttribute>().FirstOrDefault()?.EnumType is { } e
            ? Enum.GetNames(e) : null;
        return names is not null && i < names.Length ? names[i] : $"Config{i}";
    }

    /// <summary>English text for the given keys, from every English language file.</summary>
    private static Dictionary<string, string> EnglishTexts(GlobalFileIndexService index, ExtractionService extraction, ISet<string> keys)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in index.Paths.Where(p => p.StartsWith("LANGUAGE/", StringComparison.OrdinalIgnoreCase)
                     && p.EndsWith("_ENGLISH.MBIN", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (!index.TryFind(path, out var pak, out var entry, out var header)) continue;
            try
            {
                if (extraction.ExtractEntryBytes(pak, entry, header) is not { } bytes) continue;
                var mbin = new MBINFile(new MemoryStream(bytes));
                mbin.Load();
                foreach (var (id, text) in LocalisationService.BuildEnglishLookup(mbin.GetData()))
                    if (keys.Contains(id)) result.TryAdd(id, text);
            }
            catch { /* unreadable language file - skip */ }
        }
        return result;
    }
}
