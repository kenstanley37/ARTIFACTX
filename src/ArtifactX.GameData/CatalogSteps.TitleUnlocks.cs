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
    /// <summary>Extracts the installed title-to-special reward rules and their
    /// localized instructions. Build 184271's Colossus mappings were inspected
    /// 2026-11-02. Thresholds alone must never be displayed as granted unlocks:
    /// the owner's account has the recycling titles but lacks linked parts.</summary>
    public static void AddTitleUnlocks(string catalogPath, string pcbanksPath)
    {
        const string file = "METADATA/GAMESTATE/PLAYERDATA/PLAYERTITLEDATA.MBIN";
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (pak.FullPath is not string pakPath) continue;
            try { index.Add(pakPath, reader.ReadHeader(pakPath), reader.Read(pakPath)); }
            catch (IOException) { /* Match the catalog's treatment of unreadable PAKs. */ }
        }
        if (!index.TryFind(file, out var path, out var entry, out var header))
            throw new InvalidDataException("Player title definitions were not found.");
        var extraction = new ExtractionService();
        using var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(path, entry, header)!));
        mbin.Load();
        if (mbin.GetData() is not GcPlayerTitleData table)
            throw new InvalidDataException("Player title definitions have an unexpected structure.");
        var titles = table.Titles.Where(t => t.TitleUnlocksSpecials.Count > 0).ToArray();
        var keys = titles.SelectMany(t => new[] { t.Title.Value, t.UnlockDescription.Value, t.AlreadyUnlockedDescription.Value })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var texts = EnglishTexts(index, extraction, keys);
        // Missing localization stays empty; raw localization ids must never reach players.
        string Text(string key) => texts.GetValueOrDefault(key, "");
        var rules = titles.Select(t => new TitleUnlockRule(t.ID.Value, Text(t.Title.Value),
            Text(t.UnlockDescription.Value), Text(t.AlreadyUnlockedDescription.Value),
            t.UnlockedByStat.Value, t.UnlockedByStatValue, t.UnlockedByLeveledStatRank,
            t.UnlockedByMission.Value, t.UnlockedByProductRecipe.Value, t.UnlockedByTrophy.Value,
            t.UnlockedByInteraction.InteractionType.ToString(), t.UnlockedByInteractionIndex,
            t.UnlockedByInteractionRace.AlienRace.ToString(), t.UnlockedByInteractionOnlyTestMainRaces, t.RevealedBy.Value,
            t.BlockedInSeasons.ToArray(), t.TitleUnlocksSpecials.Select(s => s.Value).ToArray())).ToArray();
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "TitleUnlocks", "GcPlayerTitle", file.ToLowerInvariant(), new List<CatalogItem>
        {
            new CatalogItem { GameId = "TITLEUNLOCKS", NameEnglish = "Title unlock requirements",
                DescriptionEnglish = JsonSerializer.Serialize(rules) }
        });
        ConsoleStyle.Success($"Wrote {rules.Length} title reward rules to {catalogPath}.");
    }
}
