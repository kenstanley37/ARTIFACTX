using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>How the game picks a fish's size from the water's depth (one size band, optionally
/// for one planet type), from the catalog's "FishingRules" (GcFishSizeProbability).</summary>
public sealed record FishDepthRule(string Size, string Biome, int Base, int Modifier, int Min, int Max);

/// <summary>What a bait favours (GcBaitData): a multiplier per quality (Junk..Legendary) and
/// for day, night and storms.</summary>
public sealed record FishBait(string Id, double[] Rarity, double Day, double Night, double Storm);

/// <summary>
/// How to catch each fish (#23): the water depth its size bites in and the bait that favours
/// it most, from the player's own game files (catalog step add-fish, category "FishingRules").
/// Depth is in the game's units (metres); read 2026-10-22 as Small 0-1 (depth barely matters),
/// Medium 5-15, Large 30-65, Extra large 45-90 - water worlds 50-900, gas giants ignore depth.
/// </summary>
public sealed class FishingRules
{
    private const int Waterworld = 14, GasGiant = 15;

    public FishingRules(IReadOnlyList<FishDepthRule> depths, IReadOnlyList<FishBait> baits)
    {
        Depths = depths;
        Baits = baits;
    }

    public IReadOnlyList<FishDepthRule> Depths { get; }
    public IReadOnlyList<FishBait> Baits { get; }
    public bool IsEmpty => Depths.Count == 0 && Baits.Count == 0;

    /// <summary>"Deep water (30-65 m)" for the fish's size, on the planet type it lives on
    /// when only water worlds or gas giants have it (those change the bands).</summary>
    public string DepthText(FishInfo fish)
    {
        string biome = fish.Biomes.Length > 0 && fish.Biomes.All(b => b == Waterworld) ? "Waterworld"
            : fish.Biomes.Length > 0 && fish.Biomes.All(b => b == GasGiant) ? "GasGiant" : "";
        var rule = Depths.FirstOrDefault(d => d.Size == fish.Size && d.Biome == biome)
            ?? Depths.FirstOrDefault(d => d.Size == fish.Size && d.Biome.Length == 0);
        if (rule is null) return "";
        if (rule.Modifier <= 1) return biome == "GasGiant" ? "Any depth" : "Shallow water (any depth)";
        string word = fish.Size switch { "Medium" => "Moderate depth", "Large" => "Deep water", _ => "The deepest water" };
        return $"{word} ({rule.Min.ToString(CultureInfo.CurrentCulture)}-{rule.Max.ToString(CultureInfo.CurrentCulture)} m)";
    }

    /// <summary>The bait that favours this fish most - its rarity, and its time of day or storm -
    /// with how much ("Spicy Chum ×5"); empty when no bait helps it more than a little.</summary>
    public string BestBait(FishInfo fish)
    {
        int quality = fish.Quality switch { "Common" => 1, "Rare" => 2, "Epic" => 3, "Legendary" => 4, _ => 0 };
        var best = Baits
            .Select(b => (Bait: b, Score: (quality < b.Rarity.Length ? b.Rarity[quality] : 1)
                * (fish.Time == "Day" ? b.Day : fish.Time == "Night" ? b.Night : 1)
                * (fish.NeedsStorm ? b.Storm : 1)))
            .OrderByDescending(x => x.Score).FirstOrDefault();
        if (best.Bait is null || best.Score < 1.2) return "";
        string name = CatalogService.TryGet(best.Bait.Id)?.DisplayName is { Length: > 0 } n
            ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(n.ToLowerInvariant()) : best.Bait.Id;
        return $"{name} ×{best.Score.ToString("0.#", CultureInfo.CurrentCulture)}";
    }

    /// <summary>The rules from the catalog; empty when it was built before format 14.</summary>
    public static async Task<FishingRules> LoadAsync()
    {
        string? dbPath = CatalogService.ResolveDbPath();
        if (dbPath is null) return new([], []);
        try
        {
            return await Task.Run(() =>
            {
                var depths = new List<FishDepthRule>();
                var baits = new List<FishBait>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT i.GameId, i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                                        WHERE c.TemplateType = 'FishingRules'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(1)) continue;
                    using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(1));
                    var r = json.RootElement;
                    if (reader.GetString(0).StartsWith("DEPTH:", StringComparison.Ordinal))
                        depths.Add(new(r.GetProperty("size").GetString() ?? "", r.GetProperty("biome").GetString() ?? "",
                            r.GetProperty("base").GetInt32(), r.GetProperty("mod").GetInt32(), r.GetProperty("min").GetInt32(), r.GetProperty("max").GetInt32()));
                    else
                        baits.Add(new(r.GetProperty("id").GetString() ?? "", r.GetProperty("rarity").EnumerateArray().Select(v => v.GetDouble()).ToArray(),
                            r.GetProperty("day").GetDouble(), r.GetProperty("night").GetDouble(), r.GetProperty("storm").GetDouble()));
                }
                return new FishingRules(depths, baits);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FishingRules] load failed: {ex.Message}");
            return new([], []);
        }
    }
}
