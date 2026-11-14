using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Helpers.Recipes;

/// <summary>Plain words for where an item is found (HarvestSearch.Rules): one line per kind of place, e.g. "On Frozen
/// planets · 5–10 each", "Underwater, on any planet with a sea · 1 each", "On infested Lush or Toxic planets"; then
/// creatures, fish and the sources no planet search covers (<see cref="OtherLines"/>). Start-planet exclusions are left
/// out (they don't help a player choose where to go).</summary>
internal static class HarvestPlaceText
{
    /// <summary>The line for an item creatures give when fed (Fresh Milk, Crab 'Apple', Creature Egg).</summary>
    public const string FeedLine = "Feed tamable creatures that produce it. The Analysis Visor shows what each one produces. "
        + "At a base, an Automated Feeder filled with Creature Pellets and a Livestock Unit feed nearby creatures and collect it for you.";

    /// <summary>
    /// Where items no placed object, creature or fish gives come from, from the reward tables (build 185034,
    /// 2026-11-13): Faecium from creature droppings (POOPLOOT on the creature loot entity) and the Gutrot Flower
    /// (farmpoop's PLANT_POOP, POOPPLANT); NipNip Buds from the NipNip plant (farmnip's PLANT_NIP, NIPPLANT), locked boxes
    /// in abandoned buildings (R_ABAND_LOCK) and Merchants' Guild gifts (R_TGUILD_GIFT2-4); GekNip from Gek gifts
    /// (R_TRAGIFT_WLORE, R_TRAGIFT_WTHRT) and abandoned buildings' locked boxes (R_ABAND_LOCK). The "any" ingredients
    /// take any member of their GcProductData GroupID (ANY_FISH_COOKED: the 26 FOOD_F_ dishes) or any fish or bone.
    /// Plant names come from the catalog by id.
    /// </summary>
    public static string[] OtherLines(string itemId, Func<string, string> name) => itemId.ToUpperInvariant() switch
    {
        "FIENDCORE" => ["Break a Whispering Egg with your Multi-Tool and pick up the core inside."],
        "WORMCORE" => ["Kill the small worms that burst out of the ground and pick up what they drop."],
        "PLANT_POOP" => ["Pick up the droppings creatures leave after you feed them.", $"Harvest a {name("POOPPLANT")} you plant at a base."],
        "NIPNIPBUDS" => [$"Harvest a {name("NIPPLANT")} plant you plant at a base.", "Also found in locked boxes in abandoned buildings, and given by the Merchants' Guild."],
        "TRA_CURIO2" => ["The Gek sometimes give it as a gift.", "Also found in locked boxes in abandoned buildings."],
        "ANY_FISH" => ["Any seafood: a fish you catch, or a fish dish from the Nutrient Processor."],
        "ANY_FISH_RAW" => ["Any fish you catch with the Fishing Rig."],
        "ANY_FISH_COOKED" => ["Any fish dish: cook a fish you catch in the Nutrient Processor."],
        "ANY_BONE" => [$"Any edible bone, such as {name("FOOD_V_BONE")} from feeding bony creatures."],
        _ => [],
    };

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
    /// <param name="itemId">The item; one with its own how-to line (<see cref="OtherLines"/>: break an egg, kill a worm)
    /// doesn't also say "mine with your Multi-Tool".</param>
    public static IReadOnlyList<string> Lines(IEnumerable<HarvestRule> rules, string itemId = "") => rules
        .GroupBy(r => (Any: r.Biome == 16, With: With(r), Tile: r.TileType is 4 or 5 ? r.TileType : 0, r.Mined,
            Infested: r.SubTypes is [NmsPlanets.InfestedSubType],
            NotDead: r.Biome == 16 && r.Rules.HasFlag(ObjectGroupRules.NotOnDeadPlanets)))
        .Select(g =>
        {
            int min = g.Min(r => r.Min), max = g.Max(r => r.Max);
            string amount = min == max ? $"{min:N0} each" : $"{min:N0}–{max:N0} each";
            string where = g.Key.Any
                ? (g.Key.Tile == 4 ? "On any planet with a sea" : "On any planet") + g.Key.With + (g.Key.NotDead ? ", except dead worlds" : "")
                : $"On {(g.Key.Infested ? "infested " : "")}{Types(g.Select(r => r.Biome))} planets{g.Key.With}";
            string place = g.Key.Tile switch
            {
                4 => "Underwater, " + Lower(where),
                5 => "In caves, " + Lower(where),
                _ => where,
            };
            return g.Key.Mined && !HowToGiven.Contains(itemId) ? $"{place} · mine with your Multi-Tool · {amount}" : $"{place} · {amount}";
        })
        .Distinct().ToList();

    /// <summary>What to do on a body where only mining gives the item, for the closest-systems cards: "mine it", or
    /// "break a Whispering Egg" / "kill the ground worms" for the two the game drops from those.</summary>
    public static string MinedDetail(string itemId) => itemId.ToUpperInvariant() switch
    {
        "FIENDCORE" => "break a Whispering Egg",
        "WORMCORE" => "kill the ground worms",
        _ => "mine it",
    };

    private static readonly HashSet<string> HowToGiven = new(StringComparer.OrdinalIgnoreCase) { "FIENDCORE", "WORMCORE" };

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
