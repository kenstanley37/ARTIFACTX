using ArtifactX.Core;

/// <summary>ReleaseNotes.Parse on the shape of a real GitHub release body (the v1.2.1 CHANGELOG section).</summary>
internal static class ReleaseNotesChecks
{
    public static void Check()
    {
        const string body = "## 1.2.1 — 2026-11-18\r\n\r\n### Changed\r\n- Game data: **faster** updates.\r\n- Current System: roll steps\r\n  continue here.\r\n\r\n### Fixed\r\n- A rare `error`.\r\n";
        var sections = ReleaseNotes.Parse(body);
        if (sections.Count != 2) throw new InvalidOperationException($"Release notes: {sections.Count} sections, expected 2");
        if (sections[0].Heading != "Changed" || sections[1].Heading != "Fixed")
            throw new InvalidOperationException($"Release notes headings: {sections[0].Heading}, {sections[1].Heading}");
        if (sections[0].Items is not ["Game data: faster updates.", "Current System: roll steps continue here."])
            throw new InvalidOperationException($"Release notes items: {string.Join(" | ", sections[0].Items)}");
        if (sections[1].Items is not ["A rare error."])
            throw new InvalidOperationException($"Release notes fixed: {string.Join(" | ", sections[1].Items)}");

        var loose = ReleaseNotes.Parse("Just a sentence.");
        if (loose.Count != 1 || loose[0].Heading != "" || loose[0].Items[0] != "Just a sentence.")
            throw new InvalidOperationException("Release notes: plain text without headings");
        if (ReleaseNotes.Parse(null).Count != 0) throw new InvalidOperationException("Release notes: null");

        Console.WriteLine("Release notes: headings, bullets, wrapped lines OK");
    }
}
