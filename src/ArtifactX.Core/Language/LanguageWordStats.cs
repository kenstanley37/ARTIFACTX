using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.Language;

/// <summary>
/// The "words learned" counters the game keeps beside the known-word list
/// (NmsLanguagePaths): GLOBAL_STATS WORDS_LEARNT counts every entry, and each
/// race's xWORDS_LEARNT counts that race's entries. The game recounts them only
/// when it teaches a word, so after a word edit the milestone screen showed the
/// old number until the next word learned in game. Controlled diff 2026-11-04
/// (Slot 8, Korvax "gift"): both counters went up by one and every counter then
/// equalled its list count (TWORDS 142, WWORDS 147, EWORDS 97, BWORDS 141,
/// WORDS_LEARNT 606 including Atlas's 79). Atlas has no counter of its own.
/// The game recounts from the list rather than adding one: after six words
/// written by ArtifactX and one learned in game, Korvax went 89 -> 96.
/// Saves whose lists were filled by an editor keep low counters until then
/// (Slot 1: 889 Autophage words, BWORDS 258; Slot 4: 860 Korvax, EWORDS 0);
/// `ArtifactX.Core.Checks --language-word-stats` only holds for saves the
/// game wrote itself.
/// </summary>
public static class LanguageWordStats
{
    /// <summary>Counter id for each word id prefix; null = only the total.</summary>
    private static readonly (string Prefix, string? StatId)[] Races =
    [
        ("TRA_", "TWORDS_LEARNT"), ("WAR_", "WWORDS_LEARNT"), ("EXP_", "EWORDS_LEARNT"),
        ("BUI_", "BWORDS_LEARNT"), ("ATLAS_", null),
    ];

    public const string TotalStatId = "WORDS_LEARNT";

    /// <summary>The counters as the game would set them for <paramref name="knownWords"/>
    /// (the MF2 entries).</summary>
    public static Dictionary<string, int> Counts(JArray knownWords)
    {
        var ids = knownWords.Select(e => e["MYl"]?.Value<string>()?.TrimStart('^') ?? "").ToList();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal) { [TotalStatId] = ids.Count };
        foreach (var (prefix, statId) in Races)
            if (statId is not null) counts[statId] = ids.Count(id => id.StartsWith(prefix, StringComparison.Ordinal));
        return counts;
    }

    /// <summary>A copy of a GLOBAL_STATS stat list with the word counters set from
    /// <paramref name="knownWords"/>. A counter the save doesn't have yet is added
    /// in the game's shape ({"b2n": "^ID", ">MX": {">vs": n}}) only when non-zero.</summary>
    public static JArray WithCounts(JArray globalStats, JArray knownWords)
    {
        var result = (JArray)globalStats.DeepClone();
        foreach (var (statId, count) in Counts(knownWords))
        {
            var entry = result.OfType<JObject>().FirstOrDefault(s => s["b2n"]?.Value<string>()?.TrimStart('^') == statId);
            if (entry is null)
            {
                if (count == 0) continue;
                result.Add(new JObject { ["b2n"] = "^" + statId, [">MX"] = new JObject { [">vs"] = count } });
            }
            else if (entry[">MX"] is JObject value) value[">vs"] = count;
            else entry[">MX"] = new JObject { [">vs"] = count };
        }
        return result;
    }
}
