using ArtifactX.Core.Recipes;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Helpers.Recipes;

/// <summary>Plain presentation labels for extracted structural creature kinds.
/// Does not invent biome restrictions or expose creature-table IDs.</summary>
internal static class CreatureSourceText
{
    public static string Describe(IEnumerable<CreatureProductSource> sources) => string.Join("\n",
        sources.Select(source =>
        {
            string creature = source.CreatureKind switch
            {
                "Plough" or "ProtoDigger" or "Digger" or "Drill" => source.Habitat == "Ground" ? "burrowing ground creatures" : "burrowing creatures",
                "Fish" or "Shark" or "FishPredator" or "FishPrey" => "aquatic creatures",
                "Crab" => "crab-like creatures",
                "Rodent" => "rodent-like creatures",
                "Cat" => "cat-like creatures",
                "Antelope" => "hoofed creatures",
                _ => source.Habitat switch
                {
                    "Ground" => "ground-dwelling creatures", "Water" => "aquatic creatures",
                    "Air" => "flying creatures", "Space" => "space-dwelling creatures", _ => "wildlife"
                }
            };
            string action = source.Method == "Hunt" ? $"Hunt {creature} to obtain this ingredient."
                : $"Feed {creature}, then collect their produce.";
            return action;
        }).Distinct()) + "\nLook for the source creature; planet type alone does not guarantee this ingredient.";
}
