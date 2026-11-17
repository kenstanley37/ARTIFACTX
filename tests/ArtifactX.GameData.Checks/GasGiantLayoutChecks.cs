using System.Numerics;
using System.Text.Json;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;

/// <summary>
/// Solar layout and portal placement in a purple gas-giant system: 0x3F3EAFCFFB005, galaxy 235 (index 234), a gas
/// giant (body 1, size class 4) with five moons. Reference from the Slot 8 test save (2026-11-17): the player's portal
/// marker on moon 3 (A1f, ^PORTAL) at system-space (-992420, 320315, -749211.6875).
/// </summary>
internal static class GasGiantLayoutChecks
{
    private const ulong PortalCode = (0x3F3UL << 32) | 0xFCFFB005;
    private const int Galaxy = 234;
    private static readonly Vector3 PortalMarker = new(-992420f, 320315f, -749211.6875f);

    public static void Run(string catalog)
    {
        using var connection = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
        connection.Open();
        T Read<T>(string id)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
            command.Parameters.AddWithValue("$id", id);
            return JsonSerializer.Deserialize<T>((string)command.ExecuteScalar()!)!;
        }
        var stations = Read<StationEquipmentTables>("STATIONEQUIPMENT");
        var layout = SolarLayoutGeneration.ForSystem(PortalCode, Galaxy, stations.SolarLayout, true)
            ?? throw new InvalidOperationException("Gas-giant system has no layout.");
        if (!layout.Sizes.SequenceEqual(new[] { 4, 3, 3, 3, 3, 3 }) || layout.PrimaryCount != 0)
            throw new InvalidOperationException($"Gas-giant layout sizes {string.Join(",", layout.Sizes)}, primary {layout.PrimaryCount}.");

        var bodies = PlanetOverridePlacement.ForSystem(PortalCode, Galaxy, Read<BiomeTables>("BIOMETABLES"),
            Read<PlanetProfileTables>("PLANETPROFILE"), stations.SolarLayout, Read<BuildingOverrideRule[]>("BUILDINGOVERRIDES"));
        if (bodies[0] is not null) throw new InvalidOperationException("The gas giant itself must have no surface placements.");
        var portal = bodies[2]?.FirstOrDefault(b => b.Class == 16 && b.Placed)
            ?? throw new InvalidOperationException("Moon 3 has no placed portal.");
        float miss = Vector3.Distance(portal.Position, PortalMarker);
        if (miss > 1f) throw new InvalidOperationException($"Moon 3 portal is {miss:F2} from the save's portal marker.");
        Console.WriteLine($"PASS: purple gas-giant system laid out (4,3,3,3,3,3); moon 3 portal {miss:F2} from the save's marker.");
    }
}
