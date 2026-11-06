using System.Globalization;
using System.Text.Json;
using ArtifactX.Core.Universe;
using ArtifactX.GameData;
using ArtifactX.GameData.Data;

/// <summary>
/// BuildingLayoutGenerator against L-system samples the running game stored (scripts/diagnostics/read_lsystem_samples.py,
/// read-only), through a scratch catalog built from the installed game: node models in order, positions and rotations
/// in the root's frame, radius (half the live bounds diagonal, at least MinRadius) and the re-centring root offset.
/// The samples file is the player's own local read and stays out of the repository.
/// </summary>
internal static class BuildingLayoutChecks
{
    public static void Live(string pcbanks, string scratchCatalog, string samplesPath)
    {
        string output = Path.GetFullPath(scratchCatalog);
        if (File.Exists(output)) throw new InvalidOperationException("The check requires a new scratch catalog.");
        using (var db = new CatalogDbContext(output)) db.Database.EnsureCreated();
        CatalogSteps.AddPlanetGeneration(output, pcbanks);
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={output};Mode=ReadOnly");
        connection.Open();
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='BUILDINGLAYOUTS'";
        string json = (string?)query.ExecuteScalar() ?? throw new InvalidOperationException("No BUILDINGLAYOUTS row.");
        var tables = JsonSerializer.Deserialize<BuildingLayoutTables>(json)!;
        Console.WriteLine($"BUILDINGLAYOUTS: {tables.LSystems.Count} L-systems, {tables.Scenes.Count} scenes, {json.Length / 1024} KB");

        using var samples = JsonDocument.Parse(File.ReadAllText(samplesPath));
        int total = 0, layouts = 0, radii = 0;
        foreach (var ls in samples.RootElement.EnumerateArray())
        {
            string path = ls.GetProperty("path").GetString()!;
            var diag = ls.GetProperty("diag").EnumerateArray().Select(d => d.GetDouble()).ToArray();
            int k = 0;
            foreach (var s in ls.GetProperty("samples").EnumerateArray())
            {
                total++;
                ulong seed = ulong.Parse(s.GetProperty("seed").GetString()!, NumberStyles.HexNumber);
                var layout = BuildingLayoutGenerator.Generate(tables, path, seed)
                    ?? throw new InvalidOperationException($"{path}: no layout");
                var want = s.GetProperty("nodes").EnumerateArray().ToArray();
                bool names = layout.Nodes.Length == want.Length && layout.Nodes.Zip(want).All(p =>
                    p.First.Model == p.Second.GetProperty("model").GetString()!.Replace('\\', '/').ToUpperInvariant());
                double pos = 0, rot = 0;
                foreach (var (node, live) in layout.Nodes.Zip(want).Skip(1))
                {
                    var q = live.GetProperty("m").EnumerateArray().Select(v => v.GetDouble()).ToArray();   // rows: X, Y, Z axes, position
                    for (int i = 0; i < 3; i++)
                    {
                        pos = Math.Max(pos, Math.Abs(node.World[9 + i] - q[12 + i]));
                        for (int j = 0; j < 3; j++) rot = Math.Max(rot, Math.Abs(node.World[i * 3 + j] - q[j * 4 + i]));
                    }
                }
                double liveRadius = Math.Max(diag[k] / 2, tables.MinRadius);
                var root = want[0].GetProperty("m").EnumerateArray().Select(v => v.GetDouble()).ToArray();
                double offset = Math.Max(Math.Abs(layout.RootOffset.X - root[12]), Math.Abs(layout.RootOffset.Z - root[14]));
                bool ok = names && pos < 1e-3 && rot < 1e-4;
                layouts += ok ? 1 : 0;
                radii += Math.Abs(layout.Radius - liveRadius) < 5e-4 ? 1 : 0;
                Console.WriteLine($"{path.Split('/')[^1],-44} {k}: layout {(ok ? "OK  " : "DIFF")} {layout.Nodes.Length}/{want.Length} nodes " +
                    $"(pos {pos:F4}, rot {rot:E1}); radius {layout.Radius,8:F3} live {liveRadius,8:F3}; root offset error {offset:F3}");
                k++;
            }
        }
        Console.WriteLine($"{layouts}/{total} layouts, {radii}/{total} radii exact");
        if (layouts != total) throw new InvalidOperationException("Layouts differ from the live samples.");
        Console.WriteLine("PASS");
    }
}
