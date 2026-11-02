using ArtifactX.Core.Recipes;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>Confirmed in installed game tables on 2026-11-01: KillingProducts
    /// maps FOOD_M_DIGGER to PLOUGH and PROTODIGGER. HarvestingProducts provides
    /// the distinct collect-after-feeding mappings. No biome or spawn guarantee
    /// is inferred from the product ID or the generic food description.</summary>
    private static CreatureProductSource[]? CreatureProducts(Func<string, NMSTemplate?> decode)
    {
        if (decode("GCCREATUREGLOBALS.MBIN") is not GcCreatureGlobals globals
            || decode(CreatureDataTableFile) is not GcCreatureDataTable creatures) return null;
        var byId = creatures.Table.ToDictionary(c => c.Id.Value, StringComparer.OrdinalIgnoreCase);
        CreatureProductSource Source(string item, string id, string method, int size)
        {
            byId.TryGetValue(id, out var creature);
            return new(item, id, creature?.RealType?.CreatureType.ToString() ?? "",
                creature?.MoveArea.ToString() ?? "", method, size);
        }
        return globals.KillingProducts.Where(p => !string.IsNullOrWhiteSpace(p.Item.Value))
            .Select(p => Source(p.Item.Value, p.CreatureType.Value, "Hunt", 0))
            .Concat(globals.HarvestingProducts.Where(p => !string.IsNullOrWhiteSpace(p.Item.Value))
                .Select(p => Source(p.Item.Value, p.CreatureType.Value, "Harvest", p.MinBlobs)))
            .Distinct().ToArray();
    }
}
