namespace ArtifactX.Core;

/// <summary>One part of a release's notes: a heading ("Changed", "Fixed") and its bullet points.</summary>
public sealed record ReleaseNoteSection(string Heading, IReadOnlyList<string> Items);

/// <summary>
/// Release notes as a player reads them. The GitHub release body is the CHANGELOG.md section for that version
/// (release.yml): "### Changed" headings and "- " bullets. The update window showed that markdown as raw text
/// (owner, 2026-11-18), so it is split into headings and plain bullet lines here.
/// </summary>
public static class ReleaseNotes
{
    /// <summary>The sections of <paramref name="markdown"/> in order. A "## 1.2.1 — date" version line is
    /// skipped (the window already names the version); text before the first heading goes under an empty
    /// heading; a line that isn't a bullet joins the bullet above it (a wrapped line). Bold/code marks are
    /// removed. Empty sections are dropped.</summary>
    public static IReadOnlyList<ReleaseNoteSection> Parse(string? markdown)
    {
        var sections = new List<ReleaseNoteSection>();
        string heading = "";
        var items = new List<string>();

        void Flush()
        {
            if (items.Count > 0) sections.Add(new ReleaseNoteSection(heading, items.ToArray()));
            items.Clear();
        }

        foreach (string raw in (markdown ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("###", StringComparison.Ordinal))
            {
                Flush();
                heading = Plain(line.TrimStart('#'));
                continue;
            }
            if (line.StartsWith('#')) continue;
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
                items.Add(Plain(line[2..]));
            else if (items.Count > 0)
                items[^1] += " " + Plain(line);
            else
                items.Add(Plain(line));
        }
        Flush();
        return sections;
    }

    private static string Plain(string text) => text.Replace("**", "").Replace("`", "").Trim();
}
