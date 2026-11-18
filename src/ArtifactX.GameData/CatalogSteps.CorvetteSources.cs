using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// Where each corvette part comes from and what kind it is (format 72), for the
    /// Corvette Cache's filters. Build 184271, read 2026-11-04:
    ///   - DefaultReality TradeSettings.BiggsBasicShop.AlwaysPresentProducts: the 14
    ///     parts the Corvette Workshop sells for units ("Purchase basic modules";
    ///     6 on offer at a time). BiggsBarterShop.AlwaysPresentProducts: the 127 it
    ///     trades by swap offer ("Trade advanced modules"; 12 at a time). Matches the
    ///     owner's workshop screens (Thunderbird-Class Cockpit, Landing Gear, Titan
    ///     Sublight Thruster, Medusa-Class Reactor, Tractor Beam for units; Titan- and
    ///     Ambassador-Class Cockpits by swap).
    ///   - nms_basepartproducts.mbin: each part's Subtitle (the game's "Corvette
    ///     Cockpit" line) and, for the 10 sold nowhere (TradeCategory SpecialShop),
    ///     GiveRewardOnSpecialPurchase (RS_S19_*, RS_S20_*), the expedition reward
    ///     that hands them out; the expedition itself comes from the Expedition
    ///     Rewards category.
    ///   - Format 100: each part's BuildableShipTechID, the corvette upgrade module it
    ///     puts in the corvette's technology (B_HAB_C -> CV_INV2, B_LND_B -> CV_LAUN2,
    ///     B_WNG_C -> CV_PULSE3). The game shows that module with the part's icon.
    /// The rest (40 of 176 menu parts) are in neither shop: found or earned in play.
    /// The game data doesn't say which update added a part, so none is claimed.
    /// </summary>
    private static Dictionary<string, (string Shop, string Type, string Tech)> CorvettePartSources(string pcbanksPath)
    {
        var index = GetIndex(pcbanksPath); // shared, cached for the build
        var extraction = new ExtractionService();
        NMSTemplate? Decode(string file)
        {
            if (!index.TryFind(file, out var pakPath, out var entry, out var header)) return null;
            using var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pakPath, entry, header)!));
            mbin.Load();
            return mbin.GetData();
        }

        var result = new Dictionary<string, (string Shop, string Type, string Tech)>(StringComparer.OrdinalIgnoreCase);
        if (Decode("METADATA/REALITY/DEFAULTREALITY.MBIN") is not GcRealityManagerData reality)
        {
            ConsoleStyle.Warning("Default reality not found - corvette part sources left out.");
            return result;
        }
        var basic = reality.TradeSettings.BiggsBasicShop.AlwaysPresentProducts.Select(p => p.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var barter = reality.TradeSettings.BiggsBarterShop.AlwaysPresentProducts.Select(p => p.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Subtitles: the shared English text (US over UK, as the rest of the catalog), cached for the build.
        var loc = GetMergedEnglishLookup(pcbanksPath);

        foreach (string table in new[] { "METADATA/REALITY/TABLES/NMS_BASEPARTPRODUCTS.MBIN", "METADATA/REALITY/TABLES/NMS_REALITY_GCPRODUCTTABLE.MBIN" })
        {
            if (Decode(table) is not GcProductTable products) continue;
            foreach (var product in products.Table)
            {
                string id = product.ID.Value;
                if (result.ContainsKey(id)) continue;
                string shop = basic.Contains(id) ? "basic" : barter.Contains(id) ? "barter"
                    : product.GiveRewardOnSpecialPurchase.Value is { Length: > 0 } reward ? "reward:" + reward : "";
                string type = loc.TryGetValue(product.Subtitle.Value, out var subtitle) ? subtitle.Trim() : "";
                result[id] = (shop, type, product.BuildableShipTechID.Value ?? "");
            }
        }
        ConsoleStyle.Info($"Corvette shops: {basic.Count} sold for units, {barter.Count} by swap offer.");
        return result;
    }
}
