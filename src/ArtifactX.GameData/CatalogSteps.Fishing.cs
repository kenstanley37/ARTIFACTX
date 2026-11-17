using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>"DataCataloger add-fish &lt;target-catalog-db-path&gt;" adds where/when every fish can be caught, from the game's GcFishTable (found by type, not by path): ProductID, Quality, Size, Time (Day/Night/Both), Biome (bool[17] in GcBiomeType order - the same order as a planet's biome in the save's discovery records), NeedsStorm, RequiresMissionActive. Category "Fish"; GameId = "FISH:" + product id (prefixed so it never collides with the fish item's own catalog row), NameEnglish = product id (only so the row survives `trim` - the app names fish through the normal item lookup), DescriptionEnglish = JSON {"id","quality","size","time","storm","mission", "biomes":[indexes]}. Added 2026-10-11 for the Fishing tool; idempotent.</summary>
    public static void AddFish(string catalogPath, string pcbanksPath)
    {
        string fishDbPath = catalogPath;
        if (!File.Exists(fishDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {fishDbPath}");
            return;
        }

        libMBIN.NMS.GameComponents.GcFishTable? fishTable = null;
        libMBIN.NMS.Globals.GcFishingGlobals? fishingGlobals = null;
        libMBIN.NMS.GameComponents.GcBaitTable? baitTable = null;
        string fishSource = "";

        foreach (var (pakPath, header, entries) in GetPakEntries(pcbanksPath))
        {
            if (fishTable is not null && fishingGlobals is not null && baitTable is not null) break;

            // The fish table, the fishing globals (gcfishingglobals.global.mbin: how depth picks a
            // fish's size) and the bait table (metadata/reality/tables/baitdatatable.mbin).
            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.FileName)
                         && e.FileName.EndsWith(".mbin", StringComparison.OrdinalIgnoreCase)
                         && !e.FileName.Contains("/anims/", StringComparison.OrdinalIgnoreCase)
                         && (e.FileName.Contains("fish", StringComparison.OrdinalIgnoreCase) || e.FileName.Contains("bait", StringComparison.OrdinalIgnoreCase))))
            {
                byte[]? bytes;
                try { bytes = SharedExtraction.ExtractEntryBytes(pakPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    switch (mbin.GetData())
                    {
                        case libMBIN.NMS.GameComponents.GcFishTable table when fishTable is null:
                            fishTable = table;
                            fishSource = entry.FileName!;
                            break;
                        case libMBIN.NMS.Globals.GcFishingGlobals globals when fishingGlobals is null:
                            fishingGlobals = globals;
                            break;
                        case libMBIN.NMS.GameComponents.GcBaitTable baits when baitTable is null:
                            baitTable = baits;
                            break;
                    }
                }
                catch { continue; }
            }
        }

        if (fishTable?.Fish is null)
        {
            ConsoleStyle.Error("Could not find/decode a GcFishTable in any PAK.");
            return;
        }

        var fishRows = new List<CatalogItem>();
        foreach (var f in fishTable.Fish)
        {
            string id = f.ProductID?.Value ?? "";
            if (id.Length == 0) continue;
            var biomes = new System.Text.Json.Nodes.JsonArray();
            for (int i = 0; i < (f.Biome?.Length ?? 0); i++)
                if (f.Biome![i]) biomes.Add(i);
            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = id,
                ["quality"] = f.Quality?.ItemQuality.ToString() ?? "",
                ["size"] = f.Size?.FishSize.ToString() ?? "",
                ["time"] = f.Time?.FishingTime.ToString() ?? "",
                ["storm"] = f.NeedsStorm,
                ["mission"] = !string.IsNullOrEmpty(f.RequiresMissionActive?.Value),
                ["biomes"] = biomes,
            };
            fishRows.Add(new CatalogItem
            {
                GameId = "FISH:" + id,
                NameEnglish = id,
                UsageCategory = f.Quality?.ItemQuality.ToString() ?? "",
                DescriptionEnglish = payload.ToJsonString(),
            });
        }

        ConsoleStyle.Header($"Found {fishRows.Count} fish in {fishSource}.");
        foreach (var sample in fishRows.Take(5))
            ConsoleStyle.Success($"  {sample.GameId} {sample.DescriptionEnglish}");

        using var fishDb = new CatalogDbContext(fishDbPath);
        Replace(fishDb, "Fish", "GcFishData", fishSource, fishRows);

        ConsoleStyle.Success($"Wrote {fishRows.Count} fish to {fishDbPath}.");
        AddFishingRules(fishDb, fishingGlobals, baitTable);
    }

    /// <summary>Category "FishingRules" (#23): how the game picks a fish's size from the water's
    /// depth and what each bait favours, from the player's own game files. Rows:
    /// "DEPTH:&lt;size&gt;" (and "DEPTH:&lt;size&gt;:&lt;biome&gt;" for the planet types that override it -
    /// Waterworld, GasGiant) with JSON {"size","biome","base","mod","min","max"} - GcFishSizeProbability;
    /// "BAIT:&lt;product&gt;" with JSON {"id","rarity":[Junk..Legendary],"size":[S..XL],"day","night","storm"} -
    /// GcBaitData. Read 2026-10-22: Small 0-1 (base 50, depth barely matters), Medium 5-15, Large 30-65,
    /// ExtraLarge 45-90; water worlds 50-900; gas giants ignore depth. The game's own descriptions
    /// agree ("shallower water", "deep water", "the deepest of water").</summary>
    private static void AddFishingRules(CatalogDbContext db, libMBIN.NMS.Globals.GcFishingGlobals? globals, libMBIN.NMS.GameComponents.GcBaitTable? baits)
    {
        var existing = db.Categories.FirstOrDefault(c => c.TemplateType == "FishingRules");
        if (existing != null)
        {
            db.Items.RemoveRange(db.Items.Where(i => i.CategoryId == existing.Id));
            db.Categories.Remove(existing);
            db.SaveChanges();
        }
        if (globals is null && baits is null)
        {
            ConsoleStyle.Error("No fishing globals or bait table found - fishing rules left out.");
            return;
        }

        string[] sizes = { "Small", "Medium", "Large", "ExtraLarge" };
        var rows = new List<CatalogItem>();
        void Depth(libMBIN.NMS.GameComponents.GcFishSizeProbability[] weights, string? biome)
        {
            for (int i = 0; i < weights.Length && i < sizes.Length; i++)
            {
                var w = weights[i];
                rows.Add(new CatalogItem
                {
                    GameId = "DEPTH:" + sizes[i] + (biome is null ? "" : ":" + biome),
                    NameEnglish = sizes[i],
                    DescriptionEnglish = new System.Text.Json.Nodes.JsonObject
                    {
                        ["size"] = sizes[i], ["biome"] = biome ?? "",
                        ["base"] = w.BaseWeight, ["mod"] = w.DepthModifier, ["min"] = w.DepthRangeMin, ["max"] = w.DepthRangeMax,
                    }.ToJsonString(),
                });
            }
        }
        if (globals?.SizeWeights is { } general) Depth(general, null);
        foreach (var o in globals?.SizeWeightsBiomeOverrides ?? new())
            if (o.SizeWeights is { } w) Depth(w, o.Biome?.Biome.ToString());

        foreach (var bait in baits?.Bait ?? new())
        {
            string id = bait.ProductID?.Value ?? "";
            if (id.Length == 0) continue;
            rows.Add(new CatalogItem
            {
                GameId = "BAIT:" + id,
                NameEnglish = id,
                DescriptionEnglish = new System.Text.Json.Nodes.JsonObject
                {
                    ["id"] = id,
                    ["rarity"] = new System.Text.Json.Nodes.JsonArray((bait.RarityBoosts ?? Array.Empty<float>()).Select(v => (System.Text.Json.Nodes.JsonNode)(double)v).ToArray()),
                    ["size"] = new System.Text.Json.Nodes.JsonArray((bait.SizeBoosts ?? Array.Empty<float>()).Select(v => (System.Text.Json.Nodes.JsonNode)(double)v).ToArray()),
                    ["day"] = bait.DayTimeBoost, ["night"] = bait.NightTimeBoost, ["storm"] = bait.StormBoost,
                }.ToJsonString(),
            });
        }

        Replace(db, "FishingRules", "GcFishSizeProbability/GcBaitData", "gcfishingglobals.global.mbin + baitdatatable.mbin", rows);
        ConsoleStyle.Success($"Wrote {rows.Count} fishing rules (depth by size, baits).");
    }
}
