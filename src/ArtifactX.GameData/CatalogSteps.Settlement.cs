using System.Text;
using System.Text.Json.Nodes;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// What settlement buildings need, and where trade goods are sold
    /// (2026-10-14, for "where do I buy this for my settlement?").
    ///
    /// Category "SettlementCosts": one row per building type and construction
    /// stage that costs something. GameId "SETTLE:&lt;class&gt;:&lt;stage&gt;",
    /// UsageCategory = building class (GcBuildingClassification, e.g.
    /// "Settlement_Market"), DescriptionEnglish = JSON {"class","stage",
    /// "stageName","item","amount","currency"}. Each stage asks for exactly one
    /// item (every stage checked has one product or one substance).
    ///
    /// Read straight from gcsettlementglobals.mbin's bytes: COSMOS changed the
    /// file after this table (GUID 0xBD3DB884EEA3FB52, libMBIN still has the
    /// old 0x6D657AD8EC707E5D and fails at GekGifts, 0xAFC8), but the table
    /// itself - SettlementBuildingCosts at 0x0020, 63 building classes x 9
    /// stages of GcSettlementBuildingCostData (0x30 each: Products list,
    /// Substances list, AmountMax, AmountMin, Currency) - reads cleanly: all 98
    /// item ids found resolve to real catalog items. The step refuses to write
    /// anything if any id doesn't, so a future layout change can't fill the
    /// catalog with garbage.
    ///
    /// Category "TradeClasses": which trade category each economy sells and
    /// needs (tradingclassdatatable.mbin, GcTradingClassTable). GameId
    /// "TRADECLASS:&lt;class&gt;" (Mining, HighTech, Trading, Manufacturing, Fusion,
    /// Scientific, PowerGeneration), NameEnglish = category sold (Mineral, Tech,
    /// Commodity, Component, Alloy, Exotic, Energy), UsageCategory = category needed.
    /// </summary>
    public static void AddSettlementCosts(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}");
            return;
        }

        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            try { index.Add(pak.FullPath, reader.ReadHeader(pak.FullPath), reader.Read(pak.FullPath)); }
            catch { /* unreadable PAK - skip */ }
        }
        var extraction = new ExtractionService();
        byte[]? Bytes(string path) =>
            index.TryFind(path, out var pakPath, out var entry, out var header) ? extraction.ExtractEntryBytes(pakPath, entry, header) : null;

        using var db = new CatalogDbContext(catalogPath);
        var known = db.Items.Where(i => i.NameEnglish != null || i.NameLowerEnglish != null)
            .Select(i => i.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ---- building costs
        var costRows = new List<CatalogItem>();
        const string settlementFile = "GCSETTLEMENTGLOBALS.MBIN"; // top level of NMSARC.globals.pak
        byte[]? b = Bytes(settlementFile);
        if (b is null)
        {
            ConsoleStyle.Warning("Settlement settings not found - skipping settlement costs.");
        }
        else
        {
            const int dataStart = 0x20, tableAt = 0x20, classes = 63, stages = 9, stageSize = 0x30, classSize = stages * stageSize;
            string Str(int at)
            {
                int n = 0;
                while (n < 16 && at + n < b.Length && b[at + n] != 0) n++;
                return Encoding.ASCII.GetString(b, at, n);
            }
            List<string> ListAt(int field)
            {
                long rel = BitConverter.ToInt64(b, field);
                int count = BitConverter.ToInt32(b, field + 8);
                var list = new List<string>();
                if (count <= 0 || count > 64 || field + rel < 0 || field + rel + count * 16 > b.Length) return list;
                for (int i = 0; i < count; i++) list.Add(Str((int)(field + rel) + i * 16));
                return list;
            }
            var classNames = Enum.GetNames<GcBuildingClassification.BuildingClassEnum>();
            var stageNames = Enum.GetNames<GcSettlementConstructionLevel.SettlementConstructionLevelEnum>();
            var unknown = new List<string>();
            for (int c = 0; c < classes; c++)
                for (int s = 0; s < stages; s++)
                {
                    int at = dataStart + tableAt + c * classSize + s * stageSize;
                    if (at + stageSize > b.Length) break;
                    var items = ListAt(at).Concat(ListAt(at + 0x10)).Where(x => x.Length > 0).ToList();
                    if (items.Count == 0) continue;
                    int max = BitConverter.ToInt32(b, at + 0x20), min = BitConverter.ToInt32(b, at + 0x24);
                    unknown.AddRange(items.Where(i => !known.Contains(i)));
                    foreach (var item in items)
                        costRows.Add(new CatalogItem
                        {
                            GameId = $"SETTLE:{c}:{s}",
                            NameEnglish = item,
                            UsageCategory = c < classNames.Length ? classNames[c] : c.ToString(),
                            DescriptionEnglish = new JsonObject
                            {
                                ["class"] = c,
                                ["stage"] = s,
                                ["stageName"] = s < stageNames.Length ? stageNames[s] : s.ToString(),
                                ["item"] = item,
                                ["amountMin"] = min,
                                ["amountMax"] = max,
                            }.ToJsonString(),
                        });
                }
            if (unknown.Count > 0)
            {
                ConsoleStyle.Warning($"Settlement costs look wrong ({unknown.Count} unknown items, e.g. {unknown[0]}) - the game file's layout may have changed. Skipped.");
                costRows.Clear();
            }
            else
            {
                ConsoleStyle.Header($"Found {costRows.Count} settlement building costs across {costRows.Select(r => r.UsageCategory).Distinct().Count()} building types.");
            }
        }

        // ---- what each economy sells
        var tradeRows = new List<CatalogItem>();
        const string tradeFile = "METADATA/REALITY/TABLES/TRADINGCLASSDATATABLE.MBIN";
        if (Bytes(tradeFile) is { } tradeBytes)
        {
            try
            {
                var mbin = new MBINFile(new MemoryStream(tradeBytes));
                mbin.Load();
                if (mbin.GetData() is GcTradingClassTable table)
                {
                    var names = Enum.GetNames<GcTradingClass.TradingClassEnum>();
                    for (int i = 0; i < table.TradingClassesData.Length && i < names.Length; i++)
                        tradeRows.Add(new CatalogItem
                        {
                            GameId = "TRADECLASS:" + names[i],
                            NameEnglish = table.TradingClassesData[i].Sells.TradeCategory.ToString(),
                            UsageCategory = table.TradingClassesData[i].Needs.TradeCategory.ToString(),
                        });
                    ConsoleStyle.Header($"Found what {tradeRows.Count} economy types sell.");
                }
            }
            catch (Exception ex)
            {
                ConsoleStyle.Warning($"Couldn't read the trading table: {ex.Message}");
            }
        }

        // ---- building names
        // Each building type has a pool of names (UI_SETTLEMENT_POI_NAME_FACT_1..5:
        // "Ore Extraction Unit", "Manufacturing Unit", "Workshop"...) and another
        // once upgraded (..._UP_FAC_n); each settlement shows one of them -
        // Inuyam's Cove's factory is a "Manufacturing Unit" (2026-10-14). The
        // unnumbered id is the type's own name ("Factory Module"). The prefix for
        // each building class is the only hand-made part; the names are the game's.
        var prefixes = new Dictionary<string, (string Base, string? Upgraded)>
        {
            ["Settlement_LandingZone"] = ("PAD", "UP_PAD"), ["Settlement_Bar"] = ("BAR", "UP_BAR"),
            ["Settlement_Tower"] = ("TOWER", "UP_TWR"), ["Settlement_Market"] = ("MARKET", "UP_MKT"),
            ["Settlement_Small"] = ("SMALL", "UP_SML"), ["Settlement_SmallIndustrial"] = ("UNIT", "UP_UNT"),
            ["Settlement_Medium"] = ("MEDIUM", "UP_MED"), ["Settlement_Large"] = ("LARGE", "UP_LRG"),
            ["Settlement_SheriffsOffice"] = ("SHERIFF", "UP_OFF"), ["Settlement_Double"] = ("DOUBLE", "UP_DBL"),
            ["Settlement_Farm"] = ("FARM", "UP_FRM"), ["Settlement_Factory"] = ("FACT", "UP_FAC"),
            ["Settlement_FishPond"] = ("FISH", null), ["Settlement_Builders_RoboArm"] = ("RARM", null),
            ["Settlement_Hub"] = ("HUB", null), ["Settlement_Clump"] = ("CLUMP", null),
        };
        var loc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in new[] { "LANGUAGE/NMS_LOC4_ENGLISH.MBIN", "LANGUAGE/NMS_LOC5_ENGLISH.MBIN", "LANGUAGE/NMS_LOC7_ENGLISH.MBIN", "LANGUAGE/NMS_LOC1_ENGLISH.MBIN" })
        {
            if (Bytes(path) is not { } locBytes) continue;
            try
            {
                var mbin = new MBINFile(new MemoryStream(locBytes));
                mbin.Load();
                foreach (var (key, text) in LocalisationService.BuildEnglishLookup(mbin.GetData()))
                    if (key.StartsWith("UI_SETTLEMENT_POI_NAME_", StringComparison.OrdinalIgnoreCase)) loc.TryAdd(key, text);
            }
            catch (Exception ex) { ConsoleStyle.Warning($"Couldn't read {path}: {ex.Message}"); }
        }
        List<string> Names(string prefix) => loc
            .Where(kv => System.Text.RegularExpressions.Regex.IsMatch(kv.Key, $"^UI_SETTLEMENT_POI_NAME_{prefix}(_[0-9]+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Select(kv => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(kv.Value.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var nameRows = new List<CatalogItem>();
        foreach (var (buildingClass, (basePrefix, upgradedPrefix)) in prefixes)
        {
            var baseNames = Names(basePrefix);
            if (baseNames.Count == 0) continue;
            string main = loc.TryGetValue($"UI_SETTLEMENT_POI_NAME_{basePrefix}", out var m) ? System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(m.Trim()) : baseNames[0];
            nameRows.Add(new CatalogItem
            {
                GameId = "SETTLENAME:" + buildingClass,
                NameEnglish = main,
                UsageCategory = buildingClass,
                DescriptionEnglish = new JsonObject
                {
                    ["names"] = new JsonArray(baseNames.Select(n => (JsonNode)n).ToArray()),
                    ["upgraded"] = new JsonArray((upgradedPrefix is null ? new List<string>() : Names(upgradedPrefix)).Select(n => (JsonNode)n).ToArray()),
                }.ToJsonString(),
            });
        }
        ConsoleStyle.Header($"Found names for {nameRows.Count} settlement building types.");

        Replace(db, "SettlementCosts", "GcSettlementBuildingCostData", settlementFile, costRows);
        Replace(db, "SettlementBuildingNames", "LocalisedText", "language/nms_loc*_english.mbin", nameRows);
        Replace(db, "TradeClasses", "GcTradingClassData", tradeFile, tradeRows);
        ConsoleStyle.Success($"Wrote {costRows.Count} settlement costs and {tradeRows.Count} economy types to {catalogPath}.");
    }

    /// <summary>Replaces a category's rows (idempotent re-runs).</summary>
    private static void Replace(CatalogDbContext db, string templateType, string rowType, string source, List<CatalogItem> rows)
    {
        var existing = db.Categories.FirstOrDefault(c => c.TemplateType == templateType);
        if (existing != null)
        {
            db.Items.RemoveRange(db.Items.Where(i => i.CategoryId == existing.Id));
            db.Categories.Remove(existing);
            db.SaveChanges();
        }
        if (rows.Count == 0) return;
        var category = new CatalogCategory { TemplateType = templateType, RowType = rowType, SourceMbinPath = source };
        db.Categories.Add(category);
        db.SaveChanges();
        foreach (var row in rows) row.CategoryId = category.Id;
        db.Items.AddRange(rows);
        db.SaveChanges();
    }
}
