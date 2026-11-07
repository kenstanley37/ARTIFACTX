using System.Text.Json;
using ArtifactX.Core.Unlocks;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>Extracts which expedition each reward belongs to, from the table
    /// DefaultReality names (UnlockableSeasonRewardsTable). Build 184271, inspected
    /// 2026-11-03 with DataCataloger dumpfile: 293 rows, one season id each, and
    /// those ids match the EXPD_BANNERnn numbers - so the Expedition Rewards page no
    /// longer has to guess an expedition from an id's digits.</summary>
    public static void AddExpeditionRewards(string catalogPath, string pcbanksPath)
    {
        const string realityFile = "METADATA/REALITY/DEFAULTREALITY.MBIN";
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (pak.FullPath is not string path) continue;
            try { index.Add(path, reader.ReadHeader(path), reader.Read(path)); }
            catch (IOException) { /* Match the catalog's treatment of unreadable PAKs. */ }
        }
        var extraction = new ExtractionService();
        NMSTemplate Decode(string name)
        {
            if (!index.TryFind(name, out var pak, out var item, out var header))
                throw new InvalidDataException($"Expedition reward data not found: {name}");
            using var data = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak, item, header)!));
            data.Load();
            return data.GetData();
        }
        if (Decode(realityFile) is not GcRealityManagerData reality)
            throw new InvalidDataException("Default reality has an unexpected structure.");
        // Named by its source .MXML; the PAKs hold the compiled .MBIN.
        string file = reality.UnlockableSeasonRewardsTable.Value.Replace(".MXML", ".MBIN", StringComparison.OrdinalIgnoreCase);
        if (Decode(file) is not GcUnlockableSeasonRewards table)
            throw new InvalidDataException("Expedition rewards have an unexpected structure.");
        var rewards = table.Table.Where(r => r.ID.Value.Length > 0 && r.SeasonIds.Count > 0)
            .Select(r => new ExpeditionReward(r.ID.Value, r.SeasonIds[0], r.StageIds.Count > 0 ? r.StageIds[0] : -1))
            .DistinctBy(r => r.ItemId, StringComparer.OrdinalIgnoreCase).ToArray();
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "ExpeditionRewards", "GcUnlockableSeasonReward", file.ToLowerInvariant(), new List<CatalogItem>
        {
            new CatalogItem { GameId = "EXPEDITIONREWARDS", NameEnglish = "Expedition rewards",
                DescriptionEnglish = JsonSerializer.Serialize(rewards) }
        });
        ConsoleStyle.Success($"Wrote {rewards.Length} expedition rewards to {catalogPath}.");
    }
}
