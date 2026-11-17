using System.Numerics;
using System.Text.Json;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;

/// <summary>
/// Solar layout and portal placement in a purple gas-giant system: 0x3F3EAFCFFB005, galaxy 235 (index 234), a gas
/// giant (body 1, size class 4) with five moons. Reference from the Slot 8 test save (2026-11-17): the player's portal
/// marker on moon 3 (A1f, ^PORTAL) at system-space (-992420, 320315, -749211.6875).
/// Station pose in a second purple gas-giant system, 0x3EFDAFBFFB004 (galaxy index 218): Slot 8 saved docked
/// (rnc.jk4 InStation, 2026-11-17) at rnc.mEH (22.44, 5058.04, 621.96) facing rnc.l2U (-0.0010643, -0.7219773, 0.6919159).
/// The docked view looks straight back down the station's facing axis: l2U = -facing to 7 digits, and the player
/// sits on that axis about 900 units from the generated centre.
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
        CheckStation(stations.SolarLayout);
        Console.WriteLine($"PASS: purple gas-giant system laid out (4,3,3,3,3,3); moon 3 portal {miss:F2} from the save's marker.");
    }

    private static void CheckStation(SolarLayoutTables tables)
    {
        const ulong code = (0x3EFUL << 32) | 0xFBFFB004;
        var pose = StationPoseGeneration.GenerateForSystem(code, 218, tables)
            ?? throw new InvalidOperationException("Purple system has no station pose.");
        var savedFacing = new Vector3(-0.0010643f, -0.7219773f, 0.6919159f);
        var docked = new Vector3(22.44f, 5058.04f, 621.96f);
        float facingMiss = Vector3.Distance(pose.Facing, -savedFacing);
        var toPlayer = Vector3.Normalize(docked - pose.Position);
        if (facingMiss > 1e-5f || Vector3.Dot(toPlayer, savedFacing) < 0.999f)
            throw new InvalidOperationException($"Purple station pose {pose.Position} facing {pose.Facing} doesn't match the docked save.");
        Console.WriteLine($"PASS: purple system station at {pose.Position}, facing within {facingMiss:E1} of the save.");
    }
}
