using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;
// Lists the Outpost (trading post) L-systems per race and every Shop-type interaction in the models they place.
public static class Outpost
{
    public static void Run()
    {
        var table = (GcBuildingDefinitionTable)Paks.Decode("METADATA/SIMULATION/ENVIRONMENT/PLANETBUILDINGTABLE.MBIN");
        var files = new SortedSet<string>();
        for (int race = 0; race < table.BuildingFiles.Length; race++)
        {
            var row = table.BuildingFiles[race]?.BuildingFiles;
            if (row is null || row.Length <= 6) continue;
            var all = row[6].LSystem.Concat(row[6].Scene).Select(f => f?.Value ?? "").ToArray();
            Console.WriteLine($"race {race}: L {string.Join(" | ", row[6].LSystem.Select(f => f?.Value))} ; S {string.Join(" | ", row[6].Scene.Select(f => f?.Value))}");
            foreach (var f in row[6].LSystem) if (f?.Value is { Length: > 0 } v) files.Add(Paks.Norm(v));
            foreach (var f in row[6].Scene) if (f?.Value is { Length: > 0 } v) { Console.WriteLine($"   scene {v}"); Scan.Run(v, onlyShop: true); }
        }
        foreach (var file in files)
        {
            if (Paks.Decode(file) is not TkLSystemRulesData rules) { Console.WriteLine($"? {file}"); continue; }
            var models = rules.Rules.Select(r => r.Model.Value).Concat(rules.Rules.SelectMany(r => r.Rules.SelectMany(i => i.Entries.Select(e => e.Model.Value))))
                .Where(m => m.Length > 0).Select(Paks.Norm).Distinct().ToList();
            Console.WriteLine($"== {file}: {models.Count} models");
            foreach (var m in models) { Console.WriteLine($"   model {m}"); Scan.Run(m, onlyShop: true); }
        }
    }
}
