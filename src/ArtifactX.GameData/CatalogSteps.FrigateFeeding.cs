using System.Text.Json;
using ArtifactX.Core.Frigates;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// What organic frigate feeding needs (format 66, ArtifactX.Core
    /// OrganicFrigateFeeding): frigatetraittable.mbin in order, the fleet
    /// globals' class primary traits, DeepSpaceCommonPrimaryTraits and stat
    /// alterations, and the cooking ingredients offered as food. Written as one
    /// row, GameId FRIGATEFEEDING, holding the rules as JSON. Which items the
    /// game's feed menu lists is not confirmed: cooking ingredients is a guess.
    /// </summary>
    public static void AddFrigateFeeding(string catalogPath, string pcbanksPath)
    {
        const string traitFile = "METADATA/REALITY/TABLES/FRIGATETRAITTABLE.MBIN";
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
        if (Decode(traitFile) is not GcFrigateTraitTable traits)
            throw new InvalidDataException($"Frigate trait table not found: {traitFile}");
        if (Decode("GCFLEETGLOBALS.GLOBAL.MBIN") is not GcFleetGlobals fleet)
            throw new InvalidDataException("Fleet globals not found.");
        if (Decode("METADATA/REALITY/TABLES/NMS_REALITY_GCPRODUCTTABLE.MBIN") is not GcProductTable products
            || Decode("METADATA/REALITY/TABLES/NMS_REALITY_GCSUBSTANCETABLE.MBIN") is not GcSubstanceTable substances)
            throw new InvalidDataException("Product or substance table not found.");

        var rules = new FrigateFeedingRules(
            traits.Traits.Select(t => new FrigateTraitRule(t.ID.Value, (int)t.FrigateStatType.FrigateStatType,
                (int)t.Strength.FrigateTraitStrength, t.ChanceOfBeingOffered.ToArray())).ToList(),
            fleet.FrigateInitialStats.FrigateClass.Select(c => c.InitialTrait.Value).ToList(),
            fleet.DeepSpaceCommonPrimaryTraits.Select(t => t.Value).ToList(),
            fleet.FrigateTraitStrengths.FrigateStatType.Select(s => s.StatAlteration.ToArray()).ToList(),
            products.Table.Where(p => p.CookingIngredient).Select(p => p.ID.Value)
                .Concat(substances.Table.Where(s => s.CookingIngredient).Select(s => s.ID.Value)).ToList());

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "FrigateFeeding", "GcFrigateTraitTable", traitFile.ToLowerInvariant(), new List<CatalogItem>
        {
            new CatalogItem { GameId = "FRIGATEFEEDING", NameEnglish = "Organic frigate feeding rules",
                DescriptionEnglish = JsonSerializer.Serialize(rules) }
        });
        ConsoleStyle.Success($"Wrote frigate feeding rules ({rules.Traits.Count} traits, {rules.Foods.Count} foods) to {catalogPath}.");
    }
}
