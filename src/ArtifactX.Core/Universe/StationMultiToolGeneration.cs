namespace ArtifactX.Core.Universe;

/// <summary>Established station and reload-scenario inputs; no live-session state is inferred.</summary>
public sealed record StationMultiToolGenerationRequest(MultiToolReloadContext Reload,
    MultiToolGenerationRuntime Runtime, bool StationPresent, bool Abandoned, bool Pirate, bool? UseLegacyStation);

/// <summary>Composes address-to-cabinet generation for the supported standard and pirate stations.</summary>
public static class StationMultiToolGeneration
{
    /// <summary>
    /// Resolves pose, selected scene, interaction origins, pool member, model components,
    /// inventory and displayed stats. Unsupported contexts produce no records; unsupported
    /// individual models are omitted. The caller establishes station and runtime state.
    /// Returned records describe the supplied reload scenario, not an observed live pool.
    /// </summary>
    public static IReadOnlyList<LocatedMultiTool> Generate(StationMultiToolGenerationRequest request,
        StationEquipmentTables stations, InventoryGenerationTables inventory,
        MultiToolTechnologyTables technology, MultiToolModelTables models,
        Func<string, IReadOnlyList<string>?>? words = null)
    {
        if (!request.Reload.IsValid || !request.StationPresent || request.Abandoned
            || (!request.Pirate && (request.UseLegacyStation is not false || stations.ForceLegacySpaceStations is true))
            || request.Runtime.NativeWealth is < 0 or > 3 || request.Runtime.HasActiveSpecialTechnology) return [];
        ulong address = request.Reload.SystemAddress;
        ulong portal = ((address >> 40) & 0xFFF) << 32 | (address & 0xFFFFFFFF);
        int galaxy = (int)((address >> 32) & 255);
        var attributes = SystemGenerator.Attributes(portal, galaxy);
        if (attributes.Uncharted || attributes.Abandoned || attributes.Pirate != request.Pirate
            || attributes.PlanetCount + attributes.PrimePlanetCount != request.Reload.PlanetCount
            || (attributes.Pirate ? 3 : attributes.Wealth - 1) != request.Runtime.NativeWealth) return [];
        var pose = StationPoseGeneration.GenerateForSystem(portal, galaxy, stations.SolarLayout);
        if (pose is null) return [];
        var locations = StationCabinetLocations.Locate(stations, pose, request.StationPresent,
            request.Abandoned, request.Pirate, request.UseLegacyStation);
        var result = new List<LocatedMultiTool>();
        foreach (var location in locations)
        {
            var contents = StationMultiToolContents.SelectForReload(location, request.Reload,
                request.Runtime.NativeWealth, inventory, words);
            if (contents is null) continue;
            var model = MultiToolModelGeneration.Resolve(contents.Tool.Seed, models);
            if (model is null) continue;
            var tool = LocatedMultiToolGeneration.Generate(contents, model, request.Runtime, inventory, technology);
            // Pirate geometry, identity, class and scanner are independently checked.
            // The default OVERHEAT damage model does not reproduce the pirate reference;
            // retain the record but do not expose that unverified damage value.
            if (tool is not null) result.Add(request.Pirate ? tool with { HasSupportedDamagePotential = false } : tool);
        }
        return result;
    }
}
