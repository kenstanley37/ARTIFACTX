using ArtifactX.Core.Procedural;
using System.Text.Json;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Selects the standard TypeB launch branch or pirate outer shape from its extracted descriptor and
/// SpaceStationSpawn.Seed. The root's nested groups are resolved before reference scenes,
/// whose independent streams cannot change the root's entrance choice. This selects a
/// branch only; it does not establish interaction reward rules or equipped inventory stats.
/// Verified 2026-10-25 against 100 seeds using the native weighted selector at 0x142d6b5a0;
/// the reference spawn seed selects LaunchTubeC. See station research notes for oracle limits.
/// </summary>
public static class StationSceneSelection
{
    public static StationEquipmentScene? SelectStandardTypeB(StationEquipmentTables tables, ulong stationSeed)
    {
        return Select(tables.StationRoots.Standard, tables.StandardTypeBRootDescriptor,
            tables.StandardTypeBVariants, stationSeed, pirate: false);
    }

    public static StationEquipmentScene? SelectPirate(StationEquipmentTables tables, ulong stationSeed) =>
        Select(tables.StationRoots.Pirate, tables.PirateRootDescriptor, tables.PirateVariants, stationSeed, pirate: true);

    private static StationEquipmentScene? Select(string rootScene, string? json,
        StationEquipmentScene[]? variants, ulong stationSeed, bool pirate)
    {
        if (json is not { Length: > 0 } || variants is not { Length: > 0 }) return null;
        IReadOnlyList<PartGroup> root;
        try { root = ShipParts.ParseTree(json); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { return null; }
        var descriptorPath = ShipParts.DescriptorFor(rootScene);
        var selected = ShipParts.Generate(rootScene, stationSeed,
            path => string.Equals(path, descriptorPath, StringComparison.OrdinalIgnoreCase) ? root : null)
            .Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matching = variants.Where(v => (pirate ? v.RootDescriptorId : v.LaunchReference) is { Length: > 0 } reference
            && selected.Contains(reference)
            && string.Equals(v.RootScene, rootScene, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matching.Length == 1 ? matching[0] : null;
    }
}
