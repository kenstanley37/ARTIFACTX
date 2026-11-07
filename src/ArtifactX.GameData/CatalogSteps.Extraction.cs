using ArtifactX.Core.Recipes;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>Hotspot weights, biome gases, yields and resource-link settings
    /// from the installed tables, inspected 2026-11-02. Structural identities
    /// come from vendored enums. This does not infer a base's mined resource.</summary>
    private static ExtractionTables? ExtractionTables(Func<string, NMSTemplate?> decode)
    {
        if (decode("METADATA/SIMULATION/SCANNING/REGIONHOTSPOTSTABLE.MBIN") is not GcRegionHotspotsTable hotspots
            || decode("METADATA/REALITY/TABLES/BASEBUILDINGOBJECTSTABLE.MBIN") is not GcBaseBuildingTable buildings) return null;
        var types = Enum.GetValues<GcRegionHotspotTypes.HotspotTypeEnum>()
            .Where(t => t != GcRegionHotspotTypes.HotspotTypeEnum.None).ToArray();
        if (types.Length != hotspots.RegionHotspots.Length)
            throw new InvalidDataException("Hotspot categories do not match the installed template.");
        var categories = hotspots.RegionHotspots.Select((h, i) => new HotspotDefinition((int)types[i], types[i].ToString(),
            new(h.ProbabilityWeighting, h.ClassStrengths.ToArray(), h.ClassWeightings.ToArray(), h.MinRange, h.MaxRange))).ToArray();
        var gases = hotspots.RegionHotspotBiomeGases.Select((g, i) => new HotspotBiomeGases(i, g.Gas1Id.Value, g.Gas2Id.Value)).ToArray();
        var yields = hotspots.RegionHotspotSubstances.Select(s => new ExtractionSubstance(s.SubstanceId.Value, s.AmountCost, s.SubstanceYeild)).ToArray();
        var networks = buildings.Objects.Where(b => b.LinkGridData?.Connection?.Network?.LinkNetworkType == GcLinkNetworkTypes.LinkNetworkTypeEnum.Resources)
            .Select(b =>
            {
                var grid = b.LinkGridData;
                var connection = grid.Connection;
                return new ExtractionNetworkRule(b.ID.Value, grid.DependsOnHotspots.ToString(), grid.Rate, grid.Storage,
                    connection.NetworkMask, connection.NetworkSubGroup, connection.ConnectionDistance, connection.UseMinDistance);
            }).ToArray();
        return new(new(hotspots.RegionHotspotsPoleSpacing, hotspots.RegionHotspotsPerPoleMin, hotspots.RegionHotspotsPerPoleMax,
            hotspots.RegionHotspotsMinSameCategorySpacing, hotspots.RegionHotspotsMaxDifferentCategoryOverlap), categories, gases, yields, networks);
    }
}
