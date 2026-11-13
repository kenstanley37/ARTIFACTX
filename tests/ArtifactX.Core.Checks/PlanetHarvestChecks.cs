using ArtifactX.Core.Universe;

/// <summary>What placed object lists give (PlanetProfileGenerator.Harvest, catalog format 93), on synthetic tables
/// shaped like build 185034's: the lush farm and wild lists, the underwater rare list and a multi-file list.</summary>
internal static class PlanetHarvestChecks
{
    public static void Check()
    {
        const string Farm = "PLANT/LUSH.MBIN", Wild = "PLANT/LUSHWILD.MBIN", Water = "RARE/UNDERWATERSPHERES.MBIN",
            ManyA = "UNDERWATER/A.MBIN", ManyB = "UNDERWATER/B.MBIN";
        var harvest = new Dictionary<string, HarvestItem[]>
        {
            [Farm] = [new("PLANT_LUSH", 10, 15)],
            [Wild] = [new("FOOD_P_LUSHWILD", 5, 10)],
            [Water] = [new("CLAMPEARL", 1, 1), new("FISHCORE", 1, 1)],
            [ManyA] = [new("FISHCORE", 1, 2), new("VENTGEM", 1, 1)],
            [ManyB] = [new("FISHCORE", 1, 1)],
        };
        var tables = new ObjectTables([], [], [], new TerrainTables([], 0, 0, false, 0, 0, 0, 0, 0, 0, false), harvest);
        ObjectList List(string name, int tile, params string[] files) => new(name, 0, 1f, tile, files.Length, "", files);

        var found = PlanetProfileGenerator.Harvest([
            List("FARMLUSH", 0, Farm), List("WILDLUSH", 0, Wild), List("WATER_HAZ", 4, Water),
            List("MANY", 0, ManyA, ManyB), List("NOFILES", 0)], tables);
        var byId = found.ToDictionary(h => h.ItemId);

        Require(byId["PLANT_LUSH"] is { Min: 10, Max: 15, TileType: 0 }, "Star Bulb from the farm list");
        Require(byId.ContainsKey("FOOD_P_LUSHWILD"), "Impulse Beans from the wild list");
        Require(byId["CLAMPEARL"].TileType == 4, "clam pearls stay underwater");
        // Hadal Core is underwater in one list and on a multi-file ground list both files share: ground wins, widest range.
        Require(byId["FISHCORE"] is { TileType: 0, Min: 1, Max: 2 }, "Hadal Core merged across lists");
        // Only one of the multi-file list's files gives Vent Gems, so the planet isn't promised any.
        Require(!byId.ContainsKey("VENTGEM"), "items of only some of a list's files are left out");
        Require(found.Length == 4, $"expected 4 items, got {found.Length}");

        Require(PlanetProfileGenerator.Harvest([List("FARMLUSH", 0, Farm)], tables with { Harvest = null }).Length == 0,
            "catalogs before format 93 give nothing");
        Console.WriteLine("Planet harvest: lists, underwater, multi-file and old catalogs OK");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Planet harvest: " + message);
    }
}
