using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using System.Text.RegularExpressions;
using libMBIN;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// Every alien word group a player can learn (format 71), one catalog row per
    /// group in the "GcAlienLanguageWords" category: GameId = the group id the save
    /// stores at vLc.6f=.MF2 (TRA_ATTACK), UsageCategory = race name, NameEnglish =
    /// the group's forms ("traveller / travellers").
    ///
    /// Source is the game's word table, metadata/reality/tables/
    /// nms_dialog_gcalienspeechtable.mbin (GcAlienSpeechTable): each row is one word
    /// form with its Group and Race, and the game teaches and stores the GROUP.
    /// Build 184271: Gek 1,164 forms in 928 groups, Vy'keen 1,170/936, Korvax
    /// 1,106/864, Autophage 1,136/889, Atlas 269/226. Until format 70 this step took
    /// every TRA_/WAR_/... key in the language file, one per form, so a save that
    /// knew every Gek word still showed ~236 "unknown" (GitHub #31). Checked
    /// 2026-11-04 on the owner's Slot 1, whose game reports Gek complete: it knows
    /// all 928 Gek groups and none of the extra forms. Old saves can also hold a
    /// few groups the table no longer lists (TRA_COLLECTION); they are left alone.
    /// The table's one Race None row (BUI_MINE) is skipped.
    /// </summary>
    public static void AddLanguageWords(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}");
            return;
        }

        if (DecodeMbin(pcbanksPath, "LANGUAGE/NMS_LOC1_ENGLISH.MBIN") is not NMSTemplate locTemplate)
        {
            ConsoleStyle.Error("Could not find/decode language/nms_loc1_english.mbin in any PAK.");
            return;
        }
        var lookup = LocalisationService.BuildEnglishLookup(locTemplate);
        if (DecodeMbin(pcbanksPath, "METADATA/REALITY/TABLES/NMS_DIALOG_GCALIENSPEECHTABLE.MBIN") is not libMBIN.NMS.GameComponents.GcAlienSpeechTable speech)
        {
            ConsoleStyle.Error("Could not find/decode the alien speech table (nms_dialog_gcalienspeechtable.mbin).");
            return;
        }

        var raceNames = new Dictionary<libMBIN.NMS.GameComponents.GcAlienRace.AlienRaceEnum, string>
        {
            [libMBIN.NMS.GameComponents.GcAlienRace.AlienRaceEnum.Traders] = "Gek",
            [libMBIN.NMS.GameComponents.GcAlienRace.AlienRaceEnum.Warriors] = "Vy'keen",
            [libMBIN.NMS.GameComponents.GcAlienRace.AlienRaceEnum.Explorers] = "Korvax",
            [libMBIN.NMS.GameComponents.GcAlienRace.AlienRaceEnum.Builders] = "Autophage",
            // Not a race: a smaller vocabulary with no WORDS_LEARNT stat in Milestones.
            [libMBIN.NMS.GameComponents.GcAlienRace.AlienRaceEnum.Atlas] = "Atlas",
        };
        string English(string key) => lookup.TryGetValue(key, out var text) ? text.Trim() : "";

        var words = speech.Table
            .Where(row => raceNames.ContainsKey(row.Race.AlienRace) && !string.IsNullOrEmpty(row.Group.Value))
            .GroupBy(row => (Race: raceNames[row.Race.AlienRace], Group: row.Group.Value.ToUpperInvariant()))
            .Select(g =>
            {
                var forms = new[] { English(g.Key.Group) }.Concat(g.Select(row => English(row.Text.Value)))
                    .Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return (GameId: g.Key.Group, NameEnglish: forms.Count > 0 ? string.Join(" / ", forms) : g.First().Id.Value.ToLowerInvariant(), g.Key.Race);
            })
            .ToList();

        ConsoleStyle.Header($"Alien speech table: {speech.Table.Count} word forms in {words.Count} learnable groups:");
        foreach (var race in words.GroupBy(w => w.Race))
            ConsoleStyle.Success($"  {race.Key}: {race.Count()}");

        using var db = new CatalogDbContext(catalogPath);
        var rows = words.Select(w => new CatalogItem
        {
            GameId = w.GameId,
            NameEnglish = w.NameEnglish,
            UsageCategory = w.Race
        }).ToList();
        Replace(db, "GcAlienLanguageWords", "GcAlienSpeechEntry", "metadata/reality/tables/nms_dialog_gcalienspeechtable.mbin", rows);

        ConsoleStyle.Success($"Wrote {words.Count} language word groups to {catalogPath}.");
    }

    /// <summary>"DataCataloger add-guide-topics &lt;target-catalog-db-path&gt;" extracts the in-game Guide screen's real topic-to-category grouping from metadata/reality/wiki.mbin (a GcWiki: Categories[].Topics[], found 2026-08-12 via `grep` on a known topic id, then `dumpfile` with bumped depth/listCap since the generic dumper's 4/5 defaults were too shallow to show more than placeholder "[0] [1] ..." for this shape - see DumpStructure's listCap parameter). Confirmed correct: this dump's output exactly matched a reference tool's own Guide tab across all 8 categories/57 categorized topics, screenshot-verified. Writes into the existing catalog DB as a new "GcWiki" category, same pattern as add-language-words - GameId=TopicID, NameEnglish=resolved topic name (merged across EVERY english loc file, not just one - an initial loc4- only version left 10 of the 59 ids showing as raw "UI_GUIDE_TOPIC_*" text in the UI, since some topics' strings live in other numbered loc files instead; same scattered-across-files behavior locidall's own doc comment already describes for ship/vehicle reward names, fixed the same way here), UsageCategory=resolved category name (e.g. "Survival Basics"). ArtifactX's own GuidePage reads this directly rather than a hardcoded Almanac list, so a future game update's new/reordered topics get picked up automatically next time this command is re-run - no code change needed on the app side.  2 of the 59 known UI_GUIDE_TOPIC_* loc ids (EXPLORATION_2 "Finding Resources", TRADE_3A "Trading Basics") genuinely aren't in ANY category's Topics list in this data - not a bug, confirmed by direct inspection of the raw dump. Written with UsageCategory="Uncategorized" rather than dropped, since they're still real, independently toggleable ids in accountdata.hg (confirmed via the same real controlled test that found this whole feature).</summary>
    public static void AddGuideTopics(string catalogPath, string pcbanksPath)
    {
        string guideDbPath = catalogPath;
        if (!File.Exists(guideDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {guideDbPath}");
            return;
        }

        var guideLookup = GetMergedEnglishLookup(pcbanksPath);
        ConsoleStyle.Header($"Merged loc tables decoded: {guideLookup.Count} total entries.");

        var wiki = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcWiki>(
            pcbanksPath,
            f => f.Contains("metadata/reality/wiki.mbin", StringComparison.OrdinalIgnoreCase));
        if (wiki?.Categories is null)
        {
            ConsoleStyle.Error("Could not find/decode metadata/reality/wiki.mbin as GcWiki in any PAK.");
            return;
        }

        var guideRows = new List<(string GameId, string NameEnglish, string CategoryName)>();
        var seenTopicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in wiki.Categories)
        {
            string categoryId = category.CategoryID?.Value ?? "";
            string categoryName = guideLookup.TryGetValue(categoryId, out var catName) ? catName : categoryId;

            if (category.Topics is null) continue;

            foreach (var topic in category.Topics)
            {
                string topicId = topic.TopicID?.Value ?? "";
                if (topicId.Length == 0 || !seenTopicIds.Add(topicId)) continue;

                string topicName = guideLookup.TryGetValue(topicId, out var topName) ? topName : topicId;
                guideRows.Add((topicId, topicName, categoryName));
            }
        }

        // Every real UI_GUIDE_TOPIC_* id from the loc table that this category
        // walk didn't find a home for - see the Uncategorized note above.
        var uncategorizedPattern = new Regex(@"^UI_GUIDE_TOPIC_[A-Z0-9_]+$", RegexOptions.Compiled);
        var uncategorized = guideLookup.Keys
            .Where(id => uncategorizedPattern.IsMatch(id) && seenTopicIds.Add(id))
            .Select(id => (GameId: id, NameEnglish: guideLookup[id], CategoryName: "Uncategorized"))
            .ToList();
        guideRows.AddRange(uncategorized);

        var byCategory = guideRows.GroupBy(r => r.CategoryName).ToDictionary(g => g.Key, g => g.Count());
        ConsoleStyle.Header($"Found {guideRows.Count} Guide topics across {byCategory.Count} categories:");
        foreach (var (cat, count) in byCategory)
            ConsoleStyle.Success($"  {cat}: {count}");

        using var guideDb = new CatalogDbContext(guideDbPath);
        var items = guideRows.Select(row => new CatalogItem
        {
            GameId = row.GameId,
            NameEnglish = row.NameEnglish,
            UsageCategory = row.CategoryName
        }).ToList();
        Replace(guideDb, "GcWiki", "GcWikiTopic", "metadata/reality/wiki.mbin", items);

        ConsoleStyle.Success($"Wrote {guideRows.Count} Guide topic entries to {guideDbPath}.");
    }
}
