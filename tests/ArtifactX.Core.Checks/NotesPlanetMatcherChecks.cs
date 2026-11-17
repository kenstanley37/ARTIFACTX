using ArtifactX.Core.Capture;

internal static class NotesPlanetMatcherChecks
{
    /// <summary>The fauna-list capture's planet from a creature's notes: "planet X" decides, a bare name needs its
    /// capitals - a planet named "Heat" took Munat 18/U9's whole list from "despite the heat" (2026-11-10).</summary>
    public static void Check()
    {
        (int Key, string Name)[] planets = [(1, "Heat"), (2, "Munat 18/U9"), (3, "Haka Beta")];
        Expect(NotesPlanetMatcher.Find("First recorded on planet Munat 18/U9, thriving despite the heat.", planets), 2,
            "named planet beats a word in the prose");
        Expect(NotesPlanetMatcher.Find("First recorded on planet Munat 18-U9, thriving despite the heat.", planets), null,
            "a misread name and an ordinary word: nothing");
        Expect(NotesPlanetMatcher.Find("Comfortable amongst dense foliage... lanet Haka Beta, thriving despite storms", planets), 3,
            "the phrase with its first letter hidden");
        Expect(NotesPlanetMatcher.Find("They are reluctant to approach strangers on Haka Beta.", planets), 3,
            "the bare name with its capitals");
        Expect(NotesPlanetMatcher.Find("Thriving in the shattered remains of planet heat.", planets), 1,
            "the phrase decides even in other case");
        Console.WriteLine("PASS: creature notes name their planet (\"planet X\" first, bare names only with capitals).");
    }

    private static void Expect(int? got, int? want, string what)
    {
        if (got != want) throw new InvalidOperationException($"{what}: {got?.ToString() ?? "none"}, expected {want?.ToString() ?? "none"}.");
    }
}
