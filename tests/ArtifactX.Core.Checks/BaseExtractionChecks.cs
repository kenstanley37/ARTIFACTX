using ArtifactX.Core.Recipes;
using Newtonsoft.Json.Linq;
using System.Text.Json;

internal static class BaseExtractionChecks
{
    public static void PolesNative(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        int poles = 0, hotspots = 0;
        float maximumError = 0;
        foreach (var row in json.RootElement.EnumerateArray())
        {
            var categories = JsonSerializer.Deserialize<HotspotCategory[]>(row.GetProperty("Configs"))!;
            var actual = HotspotPole.Generate(row.GetProperty("Address").GetUInt64(), row.GetProperty("Radius").GetSingle(),
                row.GetProperty("PoleA").GetInt32(), row.GetProperty("PoleB").GetInt32(), new(1000, 1.9f, 3.8f, 10, 150), categories);
            var expected = row.GetProperty("Results");
            if (actual.Count != expected.GetArrayLength()) throw new InvalidOperationException($"Pole {poles} count differs.");
            for (int i = 0; i < actual.Count; i++)
            {
                var item = actual[i];
                var target = expected[i];
                var direction = target.GetProperty("Direction");
                var point = new System.Numerics.Vector3(direction[0].GetSingle(), direction[1].GetSingle(), direction[2].GetSingle());
                float error = System.Numerics.Vector3.Distance(item.Direction, point);
                maximumError = Math.Max(maximumError, error);
                if (!float.IsFinite(error) || error > 0.000001f || item.Attributes.Flag != target.GetProperty("Flag").GetInt32()
                    || item.Attributes.Class != target.GetProperty("Class").GetInt32()
                    || item.Attributes.Range != target.GetProperty("Range").GetSingle()
                    || item.Attributes.Strength != target.GetProperty("Strength").GetSingle())
                    throw new InvalidOperationException($"Pole {poles}, hotspot {i} differs from native.");
                hotspots++;
            }
            poles++;
        }
        Console.WriteLine($"PASS: {poles} native poles, {hotspots} accepted hotspots; maximum direction error {maximumError:R}.");
    }

    public static void AttributesNative(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        int count = 0;
        int geometryCount = 0;
        float maximumDirectionError = 0;
        foreach (var row in json.RootElement.EnumerateArray())
        {
            var categories = JsonSerializer.Deserialize<HotspotCategory[]>(row.GetProperty("Configs"))!;
            var actual = HotspotAttributes.Roll(row.GetProperty("Seed").GetUInt64(), categories);
            if (row.TryGetProperty("Orientation", out var orientation))
            {
                var direction = HotspotOrientation.Direction(row.GetProperty("Address").GetUInt64());
                float[] components = [direction.X, direction.Y, direction.Z];
                for (int i = 0; i < 3; i++)
                    if (BitConverter.SingleToInt32Bits(components[i]) != BitConverter.SingleToInt32Bits(orientation[i].GetSingle()))
                        throw new InvalidOperationException($"Hotspot orientation {count}, component {i} differs: {components[i]} vs {orientation[i]}.");
                var input = row.GetProperty("RotationInput");
                var candidate = HotspotCandidate.Generate(row.GetProperty("Address").GetUInt64(), row.GetProperty("Radius").GetSingle(),
                    row.GetProperty("PoleA").GetInt32(), row.GetProperty("PoleB").GetInt32(), 1000,
                    row.GetProperty("Seed").GetUInt64(), categories);
                if (BitConverter.SingleToInt32Bits(candidate.Latitude) != BitConverter.SingleToInt32Bits(input[1].GetSingle())
                    || BitConverter.SingleToInt32Bits(candidate.Longitude) != BitConverter.SingleToInt32Bits(input[2].GetSingle()))
                    throw new InvalidOperationException($"Candidate angles {count} differ: {candidate.Latitude}, {candidate.Longitude} vs {input}.");
                var rotated = HotspotOrientation.Rotate(direction, input[1].GetSingle(), input[2].GetSingle(), input[0].GetSingle());
                var expectedDirection = row.GetProperty("Direction");
                var expected = new System.Numerics.Vector3(expectedDirection[0].GetSingle(), expectedDirection[1].GetSingle(), expectedDirection[2].GetSingle());
                float error = System.Numerics.Vector3.Distance(rotated, expected);
                maximumDirectionError = Math.Max(maximumDirectionError, error);
                geometryCount++;
                if (!float.IsFinite(error) || error > 0.000001f)
                    throw new InvalidOperationException($"Hotspot rotation {count} differs: {rotated} vs {expected}.");
            }
            if (actual.Flag != row.GetProperty("Flag").GetInt32()
                || actual.Class != row.GetProperty("Class").GetInt32()
                || actual.Strength != row.GetProperty("Strength").GetSingle()
                || actual.HighestStrength != row.GetProperty("HighestStrength").GetSingle()
                || BitConverter.SingleToInt32Bits(actual.Range) != BitConverter.SingleToInt32Bits(row.GetProperty("Range").GetSingle())
                || actual.FinalSeed != row.GetProperty("FinalSeed").GetUInt64())
                throw new InvalidOperationException($"Hotspot attribute vector {count} differs from native: {actual}; expected {row}.");
            count++;
        }
        Console.WriteLine($"PASS: {count} native hotspot type, range bits, class, strength and final RNG states.");
        if (geometryCount > 0)
            Console.WriteLine($"PASS: {geometryCount} bit-exact address orientations; maximum rotation direction error {maximumDirectionError:R}.");
    }

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
        if (ExtractionStock.DisplayAmount(10033886, new("test", 360000, 250)) != 6750
            || ExtractionStock.DisplayAmount(10134456, new("test", 360000, 250)) != 7000)
            throw new InvalidOperationException("Whole-yield stock conversion differs from the displayed reference.");
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

    public static void StockNative(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        int count = 0;
        foreach (var row in json.RootElement.EnumerateArray())
        {
            var resource = new ExtractionSubstance("synthetic", row.GetProperty("AmountCost").GetInt32(), row.GetProperty("Yield").GetInt32());
            long actual = ExtractionStock.DisplayAmount(row.GetProperty("StoredUnits").GetInt64(), resource);
            if (actual != row.GetProperty("Expected").GetInt64()) throw new InvalidOperationException("Stock conversion differs from native.");
            count++;
        }
        Console.WriteLine($"PASS: {count} extraction stock conversions match native output.");
    }

    public static void ResourceNative(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        int count = 0;
        foreach (var row in json.RootElement.EnumerateArray())
        {
            var resources = JsonSerializer.Deserialize<HotspotResources>(row)!;
            if (resources.ForType(row.GetProperty("Type").GetString()!) != row.GetProperty("Expected").GetString())
                throw new InvalidOperationException("Hotspot resource selection differs from native.");
            count++;
        }
        Console.WriteLine($"PASS: {count} hotspot resource selections match native output.");
    }
}
