using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Helpers.Recipes;

/// <summary>Plain words for where an item is found (HarvestSearch.Rules): one line per kind of place, e.g. "On Frozen
/// planets · 5–10 each", "Underwater, on any planet with a sea · 1 each". The infested and ordinary variants of a biome
/// read as one line; start-planet exclusions are left out (they don't help a player choose where to go).</summary>
internal static class HarvestPlaceText
{
    /// <summary>The line for an item creatures give when fed (Fresh Milk, Crab 'Apple', Creature Egg).</summary>
    public const string FeedLine = "Feed tamable creatures that produce it. The Analysis Visor shows what each one produces.";

    /// <summary>A fish's line: "Fish in the water of Lush planets · at night · in storms" (time and storms only when
    /// they matter; the four haunted biomes read as one, NmsPlanets.TypeList).</summary>
    public static string FishLine(FishInfo fish)
    {
        string where = fish.Biomes.Contains(FishInfo.AnyPlanet) ? "Fish in the water of any planet"
            : fish.Biomes.All(b => b == FishSearch.GasGiant) ? "Fish on gas giants"
            : $"Fish in the water of {NmsPlanets.TypeList(fish.Biomes, " or ")} planets";
        if (fish.Time is "Day" or "Night") where += fish.Time == "Day" ? " · by day" : " · at night";
        if (fish.NeedsStorm) where += " · in storms";
        return where;
    }

    /// <summary>The line for an item creatures drop when hunted (Raw Steak, Scaly Meat).</summary>
    public const string HuntLine = "Hunt the creatures that drop it.";

    /// <summary>One line per kind of place. Rules that differ only by planet type read as one line naming the types
    /// ("On Lush, Frozen or Swamp planets · mine with your Multi-Tool · 2–55 each"), so Carbon, which every plant but
    /// a gas giant's gives, takes one line, not fifteen.</summary>
    public static IReadOnlyList<string> Lines(IEnumerable<HarvestRule> rules) => rules
        .GroupBy(r => (Any: r.Biome == 16, With: With(r), Tile: r.TileType is 4 or 5 ? r.TileType : 0, r.Mined,
            NotDead: r.Biome == 16 && r.Rules.HasFlag(ObjectGroupRules.NotOnDeadPlanets)))
        .Select(g =>
        {
            int min = g.Min(r => r.Min), max = g.Max(r => r.Max);
            string amount = min == max ? $"{min:N0} each" : $"{min:N0}–{max:N0} each";
            string where = g.Key.Any
                ? (g.Key.Tile == 4 ? "On any planet with a sea" : "On any planet") + g.Key.With + (g.Key.NotDead ? ", except dead worlds" : "")
                : $"On {Types(g.Select(r => r.Biome))} planets{g.Key.With}";
            string place = g.Key.Tile switch
            {
                4 => "Underwater, " + Lower(where),
                5 => "In caves, " + Lower(where),
                _ => where,
            };
            return g.Key.Mined ? $"{place} · mine with your Multi-Tool · {amount}" : $"{place} · {amount}";
        })
        .Distinct().ToList();

    private static string Lower(string text) => text.Length > 0 ? char.ToLowerInvariant(text[0]) + text[1..] : text;

    /// <summary>"Lush, Frozen or anomaly": the planet types in biome order, the three anomaly colours as one word.</summary>
    private static string Types(IEnumerable<int> biomes)
    {
        var names = biomes.Distinct().Order().Select(b => b is 8 or 9 or 10 ? "anomaly" : NmsPlanets.TypeName(b)).Distinct().ToList();
        return names.Count > 1 ? string.Join(", ", names.Take(names.Count - 1)) + " or " + names[^1] : names[0];
    }

    /// <summary>" with extreme weather and aggressive Sentinels", or "" for a place with no conditions.</summary>
    private static string With(HarvestRule r)
    {
        var extra = new List<string>();
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnExtremeWeather)) extra.Add("with extreme weather");
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnExtremeSentinels)) extra.Add("with aggressive Sentinels");
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnCorruptSentinels)) extra.Add("with corrupted Sentinels");
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnDeepWater)) extra.Add("with deep oceans");
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnScrapWorlds)) extra.Add("covered in scrap");
        return extra.Count > 0 ? " " + string.Join(" and ", extra) : "";
    }
}
