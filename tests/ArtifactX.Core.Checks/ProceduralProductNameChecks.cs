using System.Text.Json;
using ArtifactX.Core.Procedural;

internal static class ProceduralProductNameChecks
{
    /// <summary>Generated names against the owner's Slot 8 Treasures screen (2026-11-03).
    /// `path` is the catalog's PROCEDURALPRODUCTNAMES DescriptionEnglish (format 65),
    /// whose English text spells "Carbonised" where the US game shows "Carbonized".</summary>
    public static void Native(string path)
    {
        var naming = JsonSerializer.Deserialize<List<ProceduralProductNaming>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("No naming in " + path);
        (string Id, string Expected)[] cases =
        [
            ("PROC_LOOT#06686", "Shiny Bones"),
            ("PROC_BIO#15819", "Precious Hatchlings"),
            ("PROC_FOSS#41022", "Carbonised Beetle Antennae"),
            ("PROC_PLNT#00902", "Pulpoak Cultivar"),
        ];
        foreach (var (id, expected) in cases)
        {
            var product = naming.Single(n => id.StartsWith(n.ProductId + "#", StringComparison.Ordinal));
            string? actual = ProceduralProductNames.Generate(id, product);
            if (actual != expected) throw new InvalidOperationException($"{id}: '{actual}', expected '{expected}'.");
        }
        Console.WriteLine($"PASS: {cases.Length} procedural product names.");
    }
}
