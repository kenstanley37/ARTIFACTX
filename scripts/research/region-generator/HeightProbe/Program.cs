using System.Numerics;
using Microsoft.Data.Sqlite;
using ArtifactX.Core.Universe;
// HeightProbe <catalog> <address> <body> <points.json> <out.json>: Core Height and BaseHeight (full precision) at each
// planet-relative point's direction on the base sphere.
T? Row<T>(string id) where T : class { using var c = new SqliteConnection($"Data Source={args[0]};Mode=ReadOnly"); c.Open(); var cmd = c.CreateCommand(); cmd.CommandText = "SELECT i.DescriptionEnglish FROM Items i JOIN Categories k ON k.Id=i.CategoryId WHERE k.TemplateType='PlanetGeneration' AND i.GameId=$id"; cmd.Parameters.AddWithValue("$id", id); return cmd.ExecuteScalar() is string j ? System.Text.Json.JsonSerializer.Deserialize<T>(j) : null; }
var biomes = Row<BiomeTables>("BIOMETABLES")!; var profiles = Row<PlanetProfileTables>("PLANETPROFILE")!;
ulong address = Convert.ToUInt64(args[1], 16); int index = int.Parse(args[2]);
ulong code = (((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF); int galaxy = (int)((address >> 32) & 0xFF);
var types = PlanetGenerator.Generate(code, galaxy, biomes); var seeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
var body = types[index - 1]; var attrs = SystemGenerator.Attributes(code, galaxy);
float radius = PlanetBuildingSites.Radius(body.BodyClass)!.Value;
var tt = profiles.Objects!.Terrain!; var choice = PlanetProfileGenerator.TerrainChoiceFor(seeds[index - 1], body, (int)attrs.Star, profiles)!;
var terrain = PlanetTerrain.Generate(seeds[index - 1], tt.Settings[choice.SettingIndex], choice, tt.SeaStandard, body.BodyClass == 3, body.DeadToWeird)!;
var pts = System.Text.Json.JsonSerializer.Deserialize<double[][]>(File.ReadAllText(args[3]))!;
var res = pts.Select(q => { var p = Vector3.Normalize(new Vector3((float)q[0], (float)q[1], (float)q[2])) * radius;
    return new[] { (double)PlanetTerrain.Height(terrain, p.X, p.Y, p.Z), (double)PlanetTerrain.BaseHeight(terrain, p.X, p.Y, p.Z, radius) }; }).ToArray();
File.WriteAllText(args[4], System.Text.Json.JsonSerializer.Serialize(res));
Console.WriteLine($"{res.Length} points");
