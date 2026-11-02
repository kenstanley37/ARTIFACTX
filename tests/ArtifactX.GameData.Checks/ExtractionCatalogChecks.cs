using System.Globalization;
using System.Text.Json;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Recipes;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;

internal static class ExtractionCatalogChecks
{
    public static void Sites(string catalog, string save, string? expectedPath = null)
    {
        using var connection = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
        connection.Open();
        T Read<T>(string id)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
            command.Parameters.AddWithValue("$id", id);
            return JsonSerializer.Deserialize<T>((string)command.ExecuteScalar()!)!;
        }
        var tables = new ExtractionWorldTables(Read<BiomeTables>("BIOMETABLES"), Read<PlanetProfileTables>("PLANETPROFILE"), Read<ExtractionTables>("EXTRACTIONTABLES"));
        var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(save));
        string before = root.ToString();
        var sites = BaseExtractionSites.Read(root["vLc"]?["6f="]?["F?0"] as Newtonsoft.Json.Linq.JArray, tables);
        if (root.ToString() != before) throw new InvalidOperationException("Extraction discovery mutated the save model.");
        if (expectedPath is not null)
        {
            var expected = JsonSerializer.Deserialize<BaseExtractionSite[]>(File.ReadAllText(expectedPath))!;
            foreach (var site in expected)
                if (!sites.Contains(site)) throw new InvalidOperationException($"Expected extraction site differs: {site}.");
            Console.WriteLine($"PASS: {expected.Length} supplied extraction-site references; read-only discovery.");
        }
        foreach (var site in sites) Console.WriteLine($"{site.BaseName}: {site.ItemId}, {site.Extractors} {site.Kind} extractors");
    }

    public static void World(string catalog, string reference)
    {
        using var connection = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
        connection.Open();
        T Read<T>(string id)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
            command.Parameters.AddWithValue("$id", id);
            return JsonSerializer.Deserialize<T>((string)command.ExecuteScalar()!)!;
        }
        var extraction = Read<ExtractionTables>("EXTRACTIONTABLES");
        var biomes = Read<BiomeTables>("BIOMETABLES");
        var profiles = Read<PlanetProfileTables>("PLANETPROFILE");
        using var input = JsonDocument.Parse(File.ReadAllText(reference));
        foreach (var row in input.RootElement.EnumerateArray())
        {
            ulong address = row.GetProperty("Address").GetUInt64();
            int planet = (int)((address >> 52) & 15);
            int galaxy = NmsSpaceStationPaths.GalaxyNumber(address) - 1;
            ulong code = ulong.Parse(NmsSpaceStationPaths.PortalAddress(address, planet), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var types = PlanetGenerator.Generate(code, galaxy, biomes);
            var bodies = SystemGenerator.Bodies(code, galaxy);
            var system = SystemGenerator.Attributes(code, galaxy);
            int index = planet - 1;
            var profile = PlanetProfileGenerator.Generate(bodies.PlanetSeeds[index], index, types[index], (int)system.Star,
                PlanetGenerator.GalaxyType(galaxy), system.GasGiantPlanet, profiles with { Objects = null }, system.Uncharted, system.Abandoned);
            var resources = extraction.ResourcesFor(profile, types[index].Biome);
            string actual = extraction.ResourceForFlag(resources, row.GetProperty("HotspotFlag").GetInt32());
            if (actual != row.GetProperty("ExpectedResource").GetString())
                throw new InvalidOperationException($"Generated hotspot resource differs: {actual}.");
            float? radius = PlanetBuildingSites.Radius(types[index].BodyClass);
            if (radius != row.GetProperty("ExpectedRadius").GetSingle())
                throw new InvalidOperationException($"Generated planet radius differs: {radius}.");
            Console.WriteLine($"PASS: generated world resource {actual} and radius {radius} match the supplied reference.");
        }
    }
}
