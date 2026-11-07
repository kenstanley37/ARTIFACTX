using System.Collections;
using System.Text.Json;
using ArtifactX.Core.IndustrialCleanup;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>Extracts normal hauling tutorial instructions and lifetime-stat
    /// mappings. Build 184271, inspected 2026-11-03. Expedition hauling distances
    /// and community targets are deliberately excluded from this general guide.</summary>
    public static void AddIndustrialCleanup(string catalogPath, string pcbanksPath)
    {
        const string file = "METADATA/SIMULATION/MISSIONS/TABLES/TUTORIALMISSIONTABLE.MBIN";
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (pak.FullPath is not string path) continue;
            try { index.Add(path, reader.ReadHeader(path), reader.Read(path)); }
            catch (IOException) { }
        }
        if (!index.TryFind(file, out var source, out var entry, out var header))
            throw new InvalidDataException("Industrial cleanup mission definitions were not found.");
        var extraction = new ExtractionService();
        using var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(source, entry, header)!));
        mbin.Load();
        if (mbin.GetData() is not GcMissionTable table) throw new InvalidDataException("Unexpected tutorial mission table.");
        var tutorial = table.Missions.Single(m => m.MissionID.Value == "HAUL_TUT");
        var loop = table.Missions.Single(m => m.MissionID.Value == "HAUL_LOOP");
        var instructions = Walk(tutorial).OfType<GcMissionSequenceGroup>()
            .Where(g => g.ObjectiveID.Value.StartsWith("UI_HAUL_TUT_OBJ", StringComparison.Ordinal)
                && g.ObjectiveTipID.Value.Length > 0)
            .DistinctBy(g => g.ObjectiveID.Value).ToArray();
        var formats = Walk(loop).OfType<GcMissionConditionFormatStat>()
            .DistinctBy(f => f.Stat.Value).ToArray();
        string[] fixedKeys = ["UI_HAUL_LOOP_TITLE", "UI_HAUL_LOOP_DESC", "UI_HAUL_LOOP_OBJ1_TIP", "UI_GUIDE_TOPIC_SCRAP", "UI_GUIDE_BODY_SCRAP"];
        var keys = fixedKeys.Concat(instructions.SelectMany(g => new[] { g.ObjectiveID.Value, g.ObjectiveTipID.Value }))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var texts = EnglishTexts(index, extraction, keys);
        string Text(string key) => texts.TryGetValue(key, out var text) && text.Length > 0 ? text
            : throw new InvalidDataException($"Missing Industrial Cleanup localization: {key}");
        var tip = Text("UI_HAUL_LOOP_OBJ1_TIP");
        var counters = formats.Select(f =>
        {
            string token = f.TextTagToUse.Value;
            string? line = tip.Split('\n').FirstOrDefault(l => l.Contains(token, StringComparison.Ordinal));
            if (line is null || token.Length == 0) throw new InvalidDataException("Lifetime counter label not found.");
            return new CleanupCounter(f.Stat.Value, line[..line.IndexOf(token, StringComparison.Ordinal)].Trim());
        }).ToArray();
        if (instructions.Length < 4 || counters.Length != 5) throw new InvalidDataException("Cleanup guide definitions changed; inspect before publishing.");
        var definition = new IndustrialCleanupDefinition(Text(fixedKeys[0]), Text(fixedKeys[1]), Text(fixedKeys[3]), Text(fixedKeys[4]),
            instructions.Select(g => new CleanupInstruction(g.ObjectiveID.Value, Text(g.ObjectiveID.Value), Text(g.ObjectiveTipID.Value))).ToArray(), counters);
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "IndustrialCleanup", "GcMissionTable", file.ToLowerInvariant(), new List<CatalogItem>
        {
            new() { GameId = "INDUSTRIALCLEANUP", NameEnglish = definition.Title, DescriptionEnglish = JsonSerializer.Serialize(definition) }
        });
    }

    private static IEnumerable<NMSTemplate> Walk(NMSTemplate value)
    {
        yield return value;
        foreach (var field in value.GetType().GetFields())
        {
            if (field.GetValue(value) is NMSTemplate child)
                foreach (var nested in Walk(child)) yield return nested;
            else if (field.GetValue(value) is IEnumerable items)
                foreach (var item in items.OfType<NMSTemplate>())
                    foreach (var nested in Walk(item)) yield return nested;
        }
    }
}
