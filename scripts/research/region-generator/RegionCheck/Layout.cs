using ArtifactX.Core.Universe;
using System.Text.Json;

/// <summary>"layout tables.json address...": generated primary-body centres (SolarLayoutGeneration, via the station pose
/// inputs) and the station position for system addresses in save hex form, to compare with the save's rounded centres.
/// tables.json is the catalog's STATIONEQUIPMENT row.</summary>
static class Layout
{
    public static void Show(string tablesFile, IEnumerable<string> addresses)
    {
        var tables = JsonSerializer.Deserialize<StationEquipmentTables>(File.ReadAllText(tablesFile))!;
        foreach (string text in addresses)
        {
            ulong address = Convert.ToUInt64(text, 16);
            int galaxy = (int)((address >> 32) & 0xFF);
            ulong code = (((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF);
            var attributes = SystemGenerator.Attributes(code, galaxy);
            var bodies = SystemGenerator.Bodies(code, galaxy);
            int count = attributes.PlanetCount;
            var sizes = new int[count];
            var hosts = new int[count];
            int sizeIndex = 0;
            for (int i = 0; i < count; i++)
            {
                hosts[i] = bodies.Hosts[i] > 0 ? bodies.Hosts[i] - 1 : -1;
                sizes[i] = hosts[i] >= 0 ? 3 : bodies.Sizes[sizeIndex++];
            }
            ulong packed = (address & ~(0xFUL << 52));
            var centres = SolarLayoutGeneration.PrimaryCenters(packed, sizes, hosts, tables.SolarLayout, true);
            var pose = StationPoseGeneration.GenerateForSystem(code, galaxy, tables.SolarLayout);
            Console.WriteLine($"{text}: primary {count} of {bodies.Hosts.Length}, gas giant {attributes.GasGiant}; station {pose?.Position}");
            if (centres is null) { Console.WriteLine("   no centres"); continue; }
            for (int i = 0; i < centres.Length; i++)
                Console.WriteLine($"   {i + 1}: {centres[i].X:F0}, {centres[i].Y:F0}, {centres[i].Z:F0}");
        }
    }
}
