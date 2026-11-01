using ArtifactX.Core.Recipes;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>Product, DefaultReality FoodStatValues and gameplay duration,
    /// confirmed in installed MBIN tables on 2026-10-31. Effect identities use
    /// the vendored stat enum; the UI supplies plain labels for those identities.</summary>
    private static FoodIngestorTables? FoodIngestor(Func<string, NMSTemplate?> decode)
    {
        if (decode("METADATA/REALITY/TABLES/NMS_REALITY_GCPRODUCTTABLE.MBIN") is not GcProductTable products
            || decode("METADATA/REALITY/DEFAULTREALITY.MBIN") is not GcRealityManagerData reality
            || decode("GCGAMEPLAYGLOBALS.GLOBAL.MBIN") is not GcGameplayGlobals gameplay) return null;
        var foods = products.Table.Where(p => p.CookingValue > 0 && (int)p.FoodBonusStat.StatsType < reality.FoodStatValues.Length)
            .Select(p =>
            {
                int stat = (int)p.FoodBonusStat.StatsType;
                var range = reality.FoodStatValues[stat];
                string name = p.FoodBonusStat.StatsType.ToString();
                return new FoodIngestorDefinition(p.ID.Value, stat, name, p.CookingValue,
                    p.FoodBonusStatAmount, range.Min, range.Max,
                    p.FoodBonusStat.StatsType != GcStatsTypes.StatsTypeEnum.Suit_AutoTranslator,
                    p.FoodBonusStat.StatsType == GcStatsTypes.StatsTypeEnum.Weapon_Scan_Recharge_Time);
            }).ToArray();
        return new(gameplay.BaseFoodDuration, foods);
    }
}
