namespace ArtifactX.Core.Universe;

/// <summary>
/// Explicit reload inputs for one system. This describes a specified reload scenario;
/// it does not assert that a running game has loaded that pool.
/// PlanetCount is the total number of addressable bodies, including prime bodies.
/// </summary>
public sealed record MultiToolReloadContext(ulong SystemAddress, ulong ReloadUniverseAddress, int PlanetCount)
{
    public bool IsValid => PlanetCount is >= 0 and <= 15 && SystemAddress >> 52 == 0
        && ReloadUniverseAddress >> 56 == 0
        && (ReloadUniverseAddress & ~(0xFUL << 52)) == SystemAddress
        && (int)((ReloadUniverseAddress >> 52) & 15) <= PlanetCount;
}

/// <summary>A selected cabinet model and actual class, without a claim about its equipped stats.</summary>
public sealed record StationMultiToolContents(StationCabinetLocation Location, SystemMultiTool Tool, int RatingIndex)
{
    /// <summary>Retained when selected through the explicit reload-context API.</summary>
    public MultiToolReloadContext? ReloadContext { get; init; }

    /// <summary>
    /// Builds and selects from exactly one specified reload-body pool, preserving its
    /// address on the result. Rejects another system/galaxy, invalid body indices and
    /// missing catalog definitions. Station presence and site geometry remain caller
    /// preconditions; this method does not discover the running game's reload state.
    /// </summary>
    public static StationMultiToolContents? SelectForReload(StationCabinetLocation location,
        MultiToolReloadContext reload, int economyProbabilityRow, InventoryGenerationTables tables,
        Func<string, IReadOnlyList<string>?>? words = null)
    {
        if (!reload.IsValid || tables.MultiToolPools is not { Length: > 0 }) return null;
        int body = (int)((reload.ReloadUniverseAddress >> 52) & 15);
        var pool = SystemMultiToolGenerator.GeneratePool(reload.ReloadUniverseAddress,
            body == 0 ? MultiToolPoolSource.SpaceStation : MultiToolPoolSource.Planet,
            body == 0 ? "Space Station" : $"Planet {body}", words, tables.MultiToolPools);
        var contents = Select(location, pool, economyProbabilityRow, tables);
        return contents is null ? null : contents with { ReloadContext = reload };
    }

    /// <summary>
    /// Applies native site selection and class rolling to an explicitly supplied reload-body
    /// pool. The caller must resolve reload state and economy row; neither is inferred here.
    /// Reseeding interactions and missing extracted pool rules yield no result.
    /// Probability rows use GcWealthClass indices (Poor=0, Average=1, Wealthy=2,
    /// Pirate=3), not SystemAttributes.Wealth's player-facing 1..3 scale.
    /// </summary>
    public static StationMultiToolContents? Select(StationCabinetLocation location,
        IReadOnlyList<SystemMultiTool> reloadBodyPool, int economyProbabilityRow, InventoryGenerationTables tables)
    {
        if (location.Definition.ReseedAfterRewardSuccess || location.Definition.PoolType is not { Length: > 0 } type)
            return null;
        var tool = SystemMultiToolGenerator.SelectForSite(reloadBodyPool, location.SiteSeed, type);
        int? rating = MultiToolSiteGeneration.InventoryClass(location.SiteSeed, economyProbabilityRow, tables);
        return tool is null || rating is null ? null : new StationMultiToolContents(location, tool, rating.Value);
    }
}
