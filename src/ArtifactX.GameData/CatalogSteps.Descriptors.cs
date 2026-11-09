using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using System.Text.RegularExpressions;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>"DataCataloger add-system-descriptors &lt;target-catalog-db-path&gt;" adds the words the game uses to describe a star system's economy, wealth and conflict (system info panel / Galaxy Map), e.g. "Mathematical", "Opulent", "Formidable". Each is a loc string named for its category: UI_ECON_CLASS_&lt;TRADE|MINING|MANUFACT|TECH|SCIENCE|POWER|ALLOY&gt;_&lt;n&gt; UI_ECON_LEVEL_&lt;LOW|MED|HIGH&gt;_&lt;n&gt;        (wealth) UI_CONFLICT_LEVEL_&lt;LOW|MED|HIGH&gt;_&lt;n&gt; found 2026-10-11 via `locval` on words from a user screenshot (they live in nms_update3_english). Written as category "SystemDescriptors": GameId = loc id, NameEnglish = the word, UsageCategory = Economy/Wealth/Conflict, DescriptionEnglish = the tier (TRADE, HIGH...). ArtifactX's Planets page uses these so players can pick the exact word the game shows them. Same patch-in-place pattern as add-guide-topics; idempotent.</summary>
    public static void AddSystemDescriptors(string catalogPath, string pcbanksPath)
    {
        string descDbPath = catalogPath;
        if (!File.Exists(descDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {descDbPath}");
            return;
        }

        var descLookup = GetMergedEnglishLookup(pcbanksPath);

        var descPattern = new Regex(@"^UI_(ECON_CLASS|ECON_LEVEL|CONFLICT_LEVEL)_([A-Z]+)_\d+$", RegexOptions.Compiled);
        var descriptors = descLookup
            .Select(kv => (Id: kv.Key, Word: kv.Value, Match: descPattern.Match(kv.Key)))
            .Where(x => x.Match.Success && !string.IsNullOrWhiteSpace(x.Word))
            .Select(x => (x.Id, x.Word,
                Group: x.Match.Groups[1].Value switch { "ECON_CLASS" => "Economy", "ECON_LEVEL" => "Wealth", _ => "Conflict" },
                Tier: x.Match.Groups[2].Value))
            .OrderBy(x => x.Group).ThenBy(x => x.Tier).ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var group in descriptors.GroupBy(d => (d.Group, d.Tier)))
            ConsoleStyle.Success($"  {group.Key.Group}/{group.Key.Tier}: {string.Join(", ", group.Select(d => d.Word))}");

        using var descDb = new CatalogDbContext(descDbPath);
        var rows = descriptors.Select(d => new CatalogItem
        {
            GameId = d.Id,
            NameEnglish = d.Word,
            DescriptionEnglish = d.Tier,
            UsageCategory = d.Group
        }).ToList();
        Replace(descDb, "SystemDescriptors", "LocString", "language/nms_*_english.mbin", rows);

        ConsoleStyle.Success($"Wrote {descriptors.Count} system descriptor words to {descDbPath}.");
    }

    /// <summary>"DataCataloger add-planet-descriptors &lt;target-catalog-db-path&gt;" adds the words the game's discovery screen uses to describe a planet, which the save doesn't store: weather (WEATHER_&lt;biome&gt;&lt;n&gt;, e.g. WEATHER_LUSHEXTREME6 "Painfully Hot Rain", in nms_update3_english), sentinels (SENTINEL_&lt;level&gt;&lt;n&gt;, e.g. SENTINEL_HIGH2 "Frenzied") and flora/fauna (RARITY_&lt;level&gt;&lt;n&gt;, e.g. RARITY_HIGH6 "Generous"). Found 2026-10-13 via `locval` on words from a user screenshot. Category "PlanetDescriptors": GameId = loc id, NameEnglish = the word, UsageCategory = Weather/Sentinels/Life, DescriptionEnglish = the tier (biome or level). Same pattern as add-system-descriptors; idempotent.</summary>
    public static void AddPlanetDescriptors(string catalogPath, string pcbanksPath)
    {
        string planetDescDbPath = catalogPath;
        if (!File.Exists(planetDescDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {planetDescDbPath}");
            return;
        }

        var planetDescLookup = GetMergedEnglishLookup(pcbanksPath);

        // Tiers can contain "_" (WEATHER_LAVA_CLEAR1 "Sulphurous Haze" was missed
        // by [A-Z]+, 2026-10-14).
        var planetDescPattern = new Regex(@"^(WEATHER|SENTINEL|RARITY)_([A-Z_]+?)_?\d+$", RegexOptions.Compiled);
        var planetDescriptors = planetDescLookup
            .Select(kv => (Id: kv.Key, Word: kv.Value.Trim(), Match: planetDescPattern.Match(kv.Key)))
            .Where(x => x.Match.Success && x.Word.Length is > 0 and <= 40 && !x.Word.Contains('%') && !x.Word.Contains('<'))
            .Select(x => (x.Id, x.Word,
                Group: x.Match.Groups[1].Value switch { "WEATHER" => "Weather", "SENTINEL" => "Sentinels", _ => "Life" },
                Tier: x.Match.Groups[2].Value))
            // SENTINEL_ also names drone status text (STATE_PATROL "Patrolling...",
            // MISSION_GUARD, ROUTINE_ALERT...) - keep only the planet levels.
            .Where(x => x.Group != "Sentinels" || x.Tier is "NONE" or "RARE" or "LOW" or "DEFAULT" or "HIGH" or "AGGRESSIVE" or "CORRUPT")
            // The special finds a planet's Resources list can show besides
            // substances: "Ancient Bones" (UI_BONES_HINT), "Salvageable Scrap"
            // (UI_SCRAP_HINT), Vile Brood, Swarm wreckage (2026-10-14).
            .Concat(planetDescLookup
                .Where(kv => kv.Key is "UI_BONES_HINT" or "UI_SCRAP_HINT" or "UI_BUGS_HINT" or "UI_SWARM_HINT")
                .Select(kv => (Id: kv.Key, Word: kv.Value.Trim(), Group: "Resource", Tier: "SPECIAL")))
            .OrderBy(x => x.Group).ThenBy(x => x.Tier).ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var group in planetDescriptors.GroupBy(d => (d.Group, d.Tier)))
            ConsoleStyle.Success($"  {group.Key.Group}/{group.Key.Tier}: {string.Join(", ", group.Select(d => d.Word))}");

        using var planetDescDb = new CatalogDbContext(planetDescDbPath);
        var rows = planetDescriptors.Select(d => new CatalogItem
        {
            GameId = d.Id,
            NameEnglish = d.Word,
            DescriptionEnglish = d.Tier,
            UsageCategory = d.Group
        }).ToList();
        Replace(planetDescDb, "PlanetDescriptors", "LocString", "language/nms_*_english.mbin", rows);

        ConsoleStyle.Success($"Wrote {planetDescriptors.Count} planet descriptor words to {planetDescDbPath}.");
    }
}
