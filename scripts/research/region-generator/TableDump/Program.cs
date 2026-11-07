// Exports PLANETBUILDINGTABLE's override fields as JSON for placeport.py (scratch research).
// Usage: tabledump <planetbuildingtable.mbin> <out.json>
using System.Text.Json;
using libMBIN;
using libMBIN.NMS.GameComponents;
var mbin = new MBINFile(File.OpenRead(args[0])); mbin.Load();
var t = (GcBuildingDefinitionTable)mbin.GetData();
var rows = t.BuildingPlacement.Select((d, c) => new
{
    cls = c, overrides = d.NumOverridesToGenerate, overridesWater = d.NumOverridesToGenerateWaterworlds,
    water = (int)d.FlattenType.WaterPlacement, radius = d.OverrideRadius, minh = d.MinHeight, maxh = d.MaxHeight,
    noNpc = d.EnabledWhenPlanetHasNoNPCs,
}).ToList();
File.WriteAllText(args[1], JsonSerializer.Serialize(rows));
Console.WriteLine($"{rows.Count} classes, {rows.Count(r => r.overrides != 0 || r.overridesWater > 0)} with overrides");
