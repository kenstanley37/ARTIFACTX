using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>
/// One multi-tool a space station cabinet sells: the system, which cabinet (in the station's order), its class
/// (C=0 ... S=3, fixed by the cabinet's site and the system's wealth), the place the game must be loaded at for this
/// tool (0 = in space or at the station, as on arrival; n = planet or moon n), the tool and its kind.
/// </summary>
public sealed record CabinetMultiToolFind(ulong SystemAddress, int Cabinet, int ClassIndex, int ReloadBody,
    SystemMultiTool Tool, string Kind);

/// <summary>
/// What the standard space station cabinets of a star system sell, for every place the game can be loaded at (issue
/// #60, the multi-tool half): the pieces the desktop's Current System readout already uses and the owner confirmed in
/// game (StationPoseGeneration, StationCabinetLocations, StationMultiToolContents - docs/research/MULTITOOL_GENERATION.md),
/// without the player's progression, so only model, kind and class, not stats. A cabinet's class depends only on its
/// site and the system's wealth; its tool on the reload body's pool. Uncharted and abandoned systems have no station;
/// non-pirate stations need the save's station rule (NmsMultiToolGenerationPaths.ReadSavedLegacyStationRule) to be
/// the current, non-legacy layout, as for Current System. Pure, so the mobile app can run it too.
/// </summary>
public static class CabinetMultiToolSearch
{
    /// <summary>The finds of one system; empty when the station layout isn't supported or the tables are missing.</summary>
    public static IEnumerable<CabinetMultiToolFind> InSystem(ulong systemAddress, bool? useLegacyStation,
        StationEquipmentTables stations, InventoryGenerationTables inventory, MultiToolKindClassifier kinds,
        Func<string, IReadOnlyList<string>?>? words = null)
    {
        ulong system = systemAddress & ~(0xFUL << 52);
        var (portal, galaxy) = SystemProfiles.PortalOf(system);
        var attributes = SystemGenerator.Attributes(portal, galaxy);
        if (attributes.Uncharted || attributes.Abandoned) yield break;
        if (!attributes.Pirate && (useLegacyStation is not false || stations.ForceLegacySpaceStations is true || attributes.Wealth is < 1 or > 3))
            yield break;
        int economyRow = attributes.Pirate ? 3 : attributes.Wealth - 1;
        if (StationPoseGeneration.GenerateForSystem(portal, galaxy, stations.SolarLayout) is not { } pose) yield break;
        var locations = StationCabinetLocations.Locate(stations, pose, true, false, attributes.Pirate, attributes.Pirate ? null : false);
        int bodies = attributes.PlanetCount + attributes.PrimePlanetCount;
        for (int body = 0; body <= bodies; body++)
        {
            var reload = new MultiToolReloadContext(system, system | ((ulong)body << 52), bodies);
            if (!reload.IsValid) continue;
            int cabinet = 0;
            foreach (var location in locations)
            {
                cabinet++;
                if (StationMultiToolContents.SelectForReload(location, reload, economyRow, inventory, words) is not { } contents) continue;
                yield return new CabinetMultiToolFind(system, cabinet, contents.RatingIndex, body, contents.Tool, kinds.KindOf(contents.Tool));
            }
        }
    }
}

/// <summary>
/// A multi-tool's kind from its seed, as the game names it: Alien, Experimental ("Pristine" in the game files) and
/// Starter by the main model's chosen parts (_MULTITOOL_ALIENXRARE, _PRISTINEXRARE, _STARTXRARE), Rifle or Pistol by
/// the barrel's weapon size (<see cref="MultiToolModelGeneration.Resolve(ulong, MultiToolModelTables, IReadOnlyList{PartGroup})"/>),
/// Royal and Staff by their own models; "Standard" for a main-model tool whose size can't be told. The descriptor tree
/// is parsed once (thousands of tools in a nearby search).
/// </summary>
public sealed class MultiToolKindClassifier
{
    private readonly MultiToolModelTables _tables;
    private readonly IReadOnlyList<PartGroup> _tree;
    private readonly string _descriptor;

    public MultiToolKindClassifier(MultiToolModelTables tables)
    {
        _tables = tables;
        _tree = ShipParts.ParseTree(tables.DescriptorJson);
        _descriptor = ShipParts.DescriptorFor(tables.ModelPath);
    }

    public string KindOf(SystemMultiTool tool)
    {
        if (tool.ModelPath.Contains("ROYAL", StringComparison.OrdinalIgnoreCase)) return "Royal";
        if (Path.GetFileName(tool.ModelPath).StartsWith("STAFF", StringComparison.OrdinalIgnoreCase)) return "Staff";
        if (!string.Equals(tool.ModelPath, SystemMultiToolGenerator.MainModel, StringComparison.OrdinalIgnoreCase)) return "Standard";
        var picked = ShipParts.Generate(_tables.ModelPath, tool.Seed,
                path => string.Equals(path, _descriptor, StringComparison.OrdinalIgnoreCase) ? _tree : null)
            .Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (picked.Contains("_MULTITOOL_ALIENXRARE")) return "Alien";
        if (picked.Contains("_MULTITOOL_STARTXRARE")) return "Starter";
        if (picked.Contains("_MULTITOOL_PRISTINEXRARE")) return "Experimental";
        return MultiToolModelGeneration.Resolve(tool.Seed, _tables, _tree)?.Kind ?? "Standard";
    }
}
