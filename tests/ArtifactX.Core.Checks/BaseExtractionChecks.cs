using ArtifactX.Core.Recipes;
using Newtonsoft.Json.Linq;
using System.Text.Json;

internal static class BaseExtractionChecks
{
    public static void Check()
    {
        var tank = new ExtractionNetworkRule("tank", "Mineral", 1, 1000, 2, 0, 3, false);
        var pipe = new ExtractionNetworkRule("pipe", "None", 0, 0, 3, 1, 0.1, false);
        var rules = new Dictionary<string, ExtractionNetworkRule> { ["tank"] = tank, ["pipe"] = pipe };
        JObject Obj(string part, double x, ulong units = 0, double displacement = 0) => new()
        {
            ["r<7"] = "^" + part, ["wMC"] = new JArray(x, 0, 0),
            ["aNu"] = new JArray(displacement, 0, 0), ["CVX"] = units << 32
        };
        var nearby = new JArray(Obj("tank", 0, 10), Obj("tank", 1, 20));
        if (BaseExtractionNetworks.Read(nearby, rules).Count != 2)
            throw new InvalidOperationException("Nearby equipment joined without a compatible connection.");
        var objects = new JArray(Obj("tank", 0, 10), Obj("tank", 10, 20), Obj("tank", 30, 40),
            Obj("pipe", 3.05, displacement: 6.95), Obj("pipe", 3.05, displacement: 6.95));
        string before = objects.ToString();
        var networks = BaseExtractionNetworks.Read(objects, rules).OrderByDescending(n => n.Members.Count).ToArray();
        if (networks.Length != 2 || networks[0].CapacityUnits != 2000 || networks[0].StoredUnits != 30
            || networks[0].ConnectorIndices.Count != 2 || networks[1].StoredUnits != 40)
            throw new InvalidOperationException("Pipe endpoints, summed radii, disconnected storage or unique membership differ.");
        if (objects.ToString() != before) throw new InvalidOperationException("Network discovery mutated the save.");
        rules["tank"] = tank with { UseMinDistance = true };
        try { BaseExtractionNetworks.Read(nearby, rules); throw new Exception("Unsupported threshold accepted."); }
        catch (InvalidOperationException) { }
        Console.WriteLine("PASS: extraction masks, summed radii, pipe displacement, isolated storage, unique stock and read-only discovery.");
    }

    public static void Reference(string path)
    {
        var json = JObject.Parse(File.ReadAllText(path));
        var rules = JsonSerializer.Deserialize<ExtractionNetworkRule[]>(json["Rules"]!.ToString())!
            .ToDictionary(r => r.PartId);
        var networks = BaseExtractionNetworks.Read((JArray)json["Objects"]!, rules);
        long expected = json["ExpectedMainCapacity"]!.Value<long>();
        if (!networks.Any(n => n.CapacityUnits == expected)) throw new InvalidOperationException("Displayed network capacity differs.");
        var members = networks.SelectMany(n => n.Members).ToArray();
        if (members.Select(m => m.ObjectIndex).Distinct().Count() != members.Length)
            throw new InvalidOperationException("Saved equipment counted in more than one network.");
        Console.WriteLine($"PASS: displayed main-network capacity matched; {networks.Count} candidate groups, unique equipment membership.");
    }
}
