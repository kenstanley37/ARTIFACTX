using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Helpers.Recipes;

/// <summary>Plain words for where an item is found (HarvestSearch.Rules): one line per kind of place, e.g. "On Frozen
/// planets · 5–10 each", "Underwater, on any planet with a sea · 1 each". The infested and ordinary variants of a biome
/// read as one line; start-planet exclusions are left out (they don't help a player choose where to go).</summary>
internal static class HarvestPlaceText
{
    public static IReadOnlyList<string> Lines(IEnumerable<HarvestRule> rules) => rules
        .GroupBy(r => (Where: Where(r), r.TileType is 4 or 5 ? r.TileType : 0))
        .Select(g =>
        {
            int min = g.Min(r => r.Min), max = g.Max(r => r.Max);
            string amount = min == max ? $"{min:N0} each" : $"{min:N0}–{max:N0} each";
            string place = g.Key.Item2 switch
            {
                4 => "Underwater, " + Lower(g.Key.Where),
                5 => "In caves, " + Lower(g.Key.Where),
                _ => g.Key.Where,
            };
            return $"{place} · {amount}";
        })
        // Anomaly worlds of three colours read as one line.
        .Distinct().ToList();

    private static string Lower(string text) => text.Length > 0 ? char.ToLowerInvariant(text[0]) + text[1..] : text;

    private static string Where(HarvestRule r)
    {
        var extra = new List<string>();
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnExtremeWeather)) extra.Add("with extreme weather");
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnExtremeSentinels)) extra.Add("with aggressive Sentinels");
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnCorruptSentinels)) extra.Add("with corrupted Sentinels");
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnDeepWater)) extra.Add("with deep oceans");
        if (r.Rules.HasFlag(ObjectGroupRules.OnlyOnScrapWorlds)) extra.Add("covered in scrap");
        string with = extra.Count > 0 ? " " + string.Join(" and ", extra) : "";
        if (r.Biome == 16)
        {
            string any = r.TileType == 4 ? "on any planet with a sea" : "on any planet";
            return (any + with + (r.Rules.HasFlag(ObjectGroupRules.NotOnDeadPlanets) ? ", except dead worlds" : ""))
                .Replace("on any planet", "On any planet");
        }
        string name = r.Biome is 8 or 9 or 10 ? "anomaly" : NmsPlanets.TypeName(r.Biome);
        return $"On {name} planets{with}";
    }
}
