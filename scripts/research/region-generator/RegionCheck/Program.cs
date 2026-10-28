using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.Globals;
using System.Diagnostics;
using System.Text.Json;

if (args.Length > 0 && args[0] == "noise") { Noise.Check(args[1]); return; }
if (args.Length > 0 && args[0] == "positions") { Positions.Check(args[1]); return; }
string data = Environment.GetEnvironmentVariable("NMS_EMU_DATA")!;
T Load<T>(string name) where T : class
{
    var file = new MBINFile(File.OpenRead(Path.Combine(data, "globals", name)));
    file.Load();
    return (T)(object)file.GetData();
}
var galaxyGlobals = Load<GcGalaxyGlobals>("gcgalaxyglobals.global.mbin");
var gameplay = Load<GcGameplayGlobals>("gcgameplayglobals.global.mbin");
var g = galaxyGlobals.DefaultGeneration;
var settings = new GalaxyGenerationSettings(g.BaseGenerationThreshold, g.StarGenerationThreshold, g.BaseTurbulenceGain,
    g.BaseTurbulenceLac, g.BaseTurbulenceScale, g.SpiralFormChance, gameplay.GoalGapVoxelDist);
Console.WriteLine($"settings {settings}  FinalTransitionInterpolationValue {galaxyGlobals.FinalTransitionInterpolationValue}");
var placement = new GalaxyPlacementSettings(g.ConnectionAttractorMax.X, g.ConnectionAttractorMax.Y, g.ConnectionAttractorMin.X,
    g.ConnectionAttractorMin.Y, g.ConnectionDistortion.X, g.ConnectionDistortion.Y, g.ConnectionDistortionTMult, g.InnerFieldScales.Z,
    g.SpiralInclusion.X, g.SpiralInclusion.Y, g.SpiralFlex.X, g.SpiralFlex.Y, g.SpiralTwistMult, g.SpiralPull.X, g.SpiralPull.Y, g.SpiralPull.Z, g.InnerFieldScales.Y);
if (args.Length > 0 && args[0] == "spiralfind") { SpiralFind.Show(int.Parse(args[1]), Convert.ToInt32(args[2], 16), Convert.ToInt32(args[3], 16), Convert.ToInt32(args[4], 16), settings, placement); return; }
if (args.Length > 0 && args[0] == "neighbours") { Neighbours.Show(Convert.ToInt32(args[1], 16), Convert.ToInt32(args[2], 16), Convert.ToInt32(args[3], 16), args[4], settings, placement); return; }
if (args.Length > 0 && args[0] == "ordinary") { Console.WriteLine($"placement {placement}"); Positions.Check(args[1], settings, placement); return; }

var rows = JsonDocument.Parse(File.ReadAllLines(args[0])[1]).RootElement;
int exact = 0, total = 0;
var timer = Stopwatch.StartNew();
foreach (var row in rows.EnumerateArray())
{
    var w = row.GetProperty("words").EnumerateArray().Select(v => v.GetInt32()).ToArray(); // x, z, y
    var count = RegionStarGeneration.Count(0, w[0] & 0xFFF, w[2] & 0xFF, w[1] & 0xFFF, settings);
    int n = row.GetProperty("n").GetInt32(), t = row.GetProperty("total").GetInt32();
    bool ok = count.Ordinary == n && count.Ordinary + count.Purple == t;
    exact += ok ? 1 : 0; total++;
    if (!ok) Console.WriteLine($"DIFF x={w[0]} y={w[2]} z={w[1]}: game {n}/{t}, port {count.Ordinary}/{count.Ordinary + count.Purple}");
}
Console.WriteLine($"{exact}/{total} live regions exact; {timer.Elapsed.TotalMilliseconds / Math.Max(1, total):0.0} ms per region");
if (args.Length > 1)
{
    int vexact = 0, vtotal = 0;
    foreach (var row in JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.EnumerateArray())
    {
        int gx = row.GetProperty("galaxy").GetInt32(), x = row.GetProperty("x").GetInt32(), y = row.GetProperty("y").GetInt32(), z = row.GetProperty("z").GetInt32();
        var count = RegionStarGeneration.Count(gx, x, y, z, settings);
        int n = row.GetProperty("n").GetInt32(), t = row.GetProperty("total").GetInt32();
        bool ok = count.Ordinary == n && count.Ordinary + count.Purple == t;
        vexact += ok ? 1 : 0; vtotal++;
        if (!ok) Console.WriteLine($"DIFF galaxy {gx} {x:X3}/{y:X2}/{z:X3}: emulator {n}/{t}, port {count.Ordinary}/{count.Ordinary + count.Purple}");
    }
    Console.WriteLine($"{vexact}/{vtotal} emulator reference regions exact");
}
