using ArtifactX.Core.Language;
using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Keeps the milestone "words learned" counters in step with word edits
/// (LanguageWordStats). Run at Save rather than per click, so Reset on the
/// language pages needs nothing extra and the counters come from the final list.
/// Other unsaved stat edits (Milestones, General) are kept: the counters are
/// set on the stat list as it is staged now.
/// </summary>
public static class LanguageWordStatsService
{
    public static void StageCounts()
    {
        if (!SaveSessionManager.HasStagedEditsUnder(NmsLanguagePaths.KnownWordsArrayPath)) return;
        if (SaveSessionManager.GetValue(NmsLanguagePaths.KnownWordsArrayPath) is not JArray words) return;
        if (SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) is not JArray groups) return;
        int index = Enumerable.Range(0, groups.Count)
            .FirstOrDefault(i => groups[i][":rc"]?.Value<string>()?.TrimStart('^') == "GLOBAL_STATS", -1);
        if (index < 0 || SaveSessionManager.GetValue(NmsPlayerStatsPaths.GroupStatsArrayPath(index)) is not JArray stats) return;
        var counted = LanguageWordStats.WithCounts(stats, words);
        if (!JToken.DeepEquals(stats, counted))
            SaveSessionManager.StageEdit(counted, NmsPlayerStatsPaths.GroupStatsArrayPath(index));
    }
}
