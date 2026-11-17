using System.Text.RegularExpressions;

namespace ArtifactX.Core.Capture;

/// <summary>
/// Which planet a creature's notes name, read off the game's Discoveries page ("P. Crabceclus was first recorded in
/// the shattered remains of planet Haka Beta, thriving despite the nuclear storms"). The game writes "planet
/// %PLANET%", so that phrase decides first (the model can cover the start of it: "lanet"). Only then a planet's name
/// on its own, and with its capitals: a planet called "Heat" matched "despite the heat" in another planet's notes and
/// its whole fauna list was filed there (2026-11-10, found 2026-11-12: Munat 18/U9's seven creatures under Heat).
/// </summary>
public static class NotesPlanetMatcher
{
    /// <summary>The one candidate the text names, or default when none or several do.</summary>
    public static T? Find<T>(string text, IEnumerable<(T Key, string Name)> candidates) where T : struct
    {
        string norm = Regex.Replace(text, @"\s+", " ");
        var list = candidates.Where(c => c.Name.Length > 3).ToList();
        T? One(Func<string, bool> names)
        {
            var hits = list.Where(c => names(c.Name)).Select(c => c.Key).Distinct().ToList();
            return hits.Count == 1 ? hits[0] : null;
        }
        // "planet Haka Beta": the game's own phrase, any case, the first letters possibly hidden.
        var phrased = One(name => Regex.IsMatch(norm, $@"(?:p?l?anet|net)\s+{Regex.Escape(name)}(?![\p{{L}}])", RegexOptions.IgnoreCase));
        if (phrased is not null) return phrased;
        // The name alone, capitals as the game writes it.
        return One(name => Regex.IsMatch(norm, $@"(?<![\p{{L}}]){Regex.Escape(name)}(?![\p{{L}}])"));
    }
}
