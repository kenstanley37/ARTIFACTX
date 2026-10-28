using libMBIN;
using libMBIN.NMS.GameComponents;

/// <summary>"buildtable file.mbin": every building class's PLANETBUILDINGTABLE placement densities by density level
/// (Dead, Low, Mid, Full, Weird, HalfWeird, Waterworld, GasGiant) and its other placement fields, for the slot-table port.</summary>
static class BuildingTable
{
    public static void Show(string file)
    {
        var mbin = new MBINFile(File.OpenRead(file));
        mbin.Load();
        var table = (GcBuildingDefinitionTable)mbin.GetData();
        for (int c = 0; c < table.BuildingPlacement.Length; c++)
        {
            var d = table.BuildingPlacement[c];
            Console.WriteLine($"{c,2} {(GcBuildingClassification.BuildingClassEnum)c,-28} density [{string.Join(", ", d.Density.Select(x => x.ToString("0.#####")))}]" +
                $" models {d.NumModelsToGenerate} overrides {d.NumOverridesToGenerate}/{d.NumOverridesToGenerateWaterworlds} cluster {d.ClusterLayout} spacing {d.ClusterSpacing}" +
                $" restrict {Restrictions(d.PlanetRestrictions)} noNPC {d.EnabledWhenPlanetHasNoNPCs} height {d.MinHeight}..{d.MaxHeight}");
        }
    }

    static string Restrictions(GcPlanetaryBuildingRestrictions r) =>
        string.Concat(r.RequiresCorruptSentinels ? "C" : "-", r.RequiresRelicWorld ? "R" : "-", r.RequiresScrapWorld ? "S" : "-", r.RequiresWater ? "W" : "-");
}
