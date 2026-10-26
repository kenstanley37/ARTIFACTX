using ArtifactX.Core.Procedural;
using System.Text.Json;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Selects the standard TypeB root's launch branch from its extracted descriptor and
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
        if (tables.StandardTypeBRootDescriptor is not { Length: > 0 } json
            || tables.StandardTypeBVariants is not { Length: > 0 } variants) return null;
        IReadOnlyList<PartGroup> root;
        try { root = ShipParts.ParseTree(json); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { return null; }
        var descriptorPath = ShipParts.DescriptorFor(tables.StationRoots.Standard);
        var selected = ShipParts.Generate(tables.StationRoots.Standard, stationSeed,
            path => string.Equals(path, descriptorPath, StringComparison.OrdinalIgnoreCase) ? root : null)
            .Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matching = variants.Where(v => v.LaunchReference is { Length: > 0 } reference
            && selected.Contains(reference)
            && string.Equals(v.RootScene, tables.StationRoots.Standard, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matching.Length == 1 ? matching[0] : null;
    }
}
