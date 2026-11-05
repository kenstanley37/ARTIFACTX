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
    /// The rest (40 of 176 menu parts) are in neither shop: found or earned in play.
    /// The game data doesn't say which update added a part, so none is claimed.
    /// </summary>
    private static Dictionary<string, (string Shop, string Type)> CorvettePartSources(string pcbanksPath)
    {
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (pak.FullPath is not string path) continue;
            try { index.Add(path, reader.ReadHeader(path), reader.Read(path)); }
            catch (IOException) { /* Match the catalog's treatment of unreadable PAKs. */ }
        }
        var extraction = new ExtractionService();
        NMSTemplate? Decode(string file)
        {
            if (!index.TryFind(file, out var pakPath, out var entry, out var header)) return null;
            using var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pakPath, entry, header)!));
            mbin.Load();
            return mbin.GetData();
        }

        var result = new Dictionary<string, (string Shop, string Type)>(StringComparer.OrdinalIgnoreCase);
        if (Decode("METADATA/REALITY/DEFAULTREALITY.MBIN") is not GcRealityManagerData reality)
        {
            ConsoleStyle.Warning("Default reality not found - corvette part sources left out.");
            return result;
        }
        var basic = reality.TradeSettings.BiggsBasicShop.AlwaysPresentProducts.Select(p => p.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var barter = reality.TradeSettings.BiggsBarterShop.AlwaysPresentProducts.Select(p => p.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Subtitles live across the language files, update files included.
        var loc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in index.Paths.Where(p => p.StartsWith("LANGUAGE/", StringComparison.OrdinalIgnoreCase)
                     && p.EndsWith("_ENGLISH.MBIN", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            try
            {
                if (Decode(path) is not { } template) continue;
                foreach (var (key, text) in LocalisationService.BuildEnglishLookup(template)) loc.TryAdd(key, text);
            }
            catch (Exception ex) { ConsoleStyle.Warning($"Couldn't read {path}: {ex.Message}"); }
        }

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
                result[id] = (shop, type);
            }
        }
        ConsoleStyle.Info($"Corvette shops: {basic.Count} sold for units, {barter.Count} by swap offer.");
        return result;
    }
}
