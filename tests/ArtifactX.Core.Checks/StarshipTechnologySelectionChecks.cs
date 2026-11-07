using System.Text.Json;
using ArtifactX.Core.Universe;

internal static class StarshipTechnologySelectionChecks
{
    public static void CheckNative(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var row = doc.RootElement;
        var tables = row.GetProperty("Tables").Deserialize<MultiToolTechnologyTables>()!;
        if (row.TryGetProperty("ShipClass", out var shipClass))
            tables = StarshipTechnologyGeneration.ForShipClass(shipClass.GetInt32(), tables)
                ?? throw new InvalidOperationException("Ship candidate category was rejected");
        var layout = row.GetProperty("Layout").Deserialize<GeneratedMultiToolLayout>()!;
        var known = row.GetProperty("KnownTechnologies").Deserialize<string[]>()!.ToHashSet(StringComparer.Ordinal);
        ulong seed = row.GetProperty("Seed").GetUInt64();
        var actual = StarshipTechnologyGeneration.SelectNormalLoadout(seed, tables, layout,
            row.GetProperty("NativeWealth").GetInt32(), row.GetProperty("RequiredLevel").GetInt32(), known, false);
        var expected = row.GetProperty("Expected").Deserialize<string[]>()!;
        if (actual is null || !actual.Select(t => t.Id).SequenceEqual(expected))
            throw new InvalidOperationException($"Ship loadout differs: {string.Join(",", actual?.Select(t => t.Id) ?? [])}; native {string.Join(",", expected)}");
        var expectedPositions = row.GetProperty("ExpectedPositions").Deserialize<GeneratedInventoryPosition[]>()!;
        if (!actual.Select(t => t.Position).SequenceEqual(expectedPositions))
            throw new InvalidOperationException("Ship technology positions differ from native");
        var expectedStats = row.GetProperty("ExpectedModuleStats").Deserialize<Dictionary<string, GeneratedModuleStat[]>>()!;
        foreach (var technology in actual.Where(t => t.ModuleStats is not null))
            if (!technology.ModuleStats!.SequenceEqual(expectedStats[technology.Id]))
                throw new InvalidOperationException("Ship module stat records differ from supplied native materialization");
        if (StarshipTechnologyGeneration.SelectNormalLoadout(seed, tables, layout, 0, 0, known, true) is not null)
            throw new InvalidOperationException("Unresolved component overrides accepted");
        Console.WriteLine($"PASS: {actual.Count} ship technologies match native selection and order, seed {seed:X16}.");
    }
}
