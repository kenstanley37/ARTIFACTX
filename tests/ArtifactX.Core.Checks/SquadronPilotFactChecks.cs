using ArtifactX.Core.Squadron;

internal static class SquadronPilotFactChecks
{
    /// <summary>SquadronPilotFacts against the owner's four Squadron Pilots screenshots (2026-11-19): each pilot's
    /// traits seed and rank from the save, and the skills, grades, kills and notes the game showed, as language keys.
    /// Ranges are GcPlayerSquadronConfig.PilotRankTraitRanges from that game build.</summary>
    public static void Check()
    {
        (float, float)[] ranges = [(0f, 0.7f), (0.2f, 0.8f), (0.4f, 0.9f), (0.5f, 1f)];
        (ulong Seed, int Rank, string Expected)[] pilots =
        [
            (0xC3D7BAA94713F397, 2,
                "UI_SQUADRON_FACT_PILOT2=3 UI_SQUADRON_FACT_PILOT6=4 UI_SQUADRON_FACT_GOOD14=4 UI_SQUADRON_FACT_ROGUE3=2 kills=27 UI_SQUADRON_SHIP_FACT4"),
            (0x1411A5FDB5BF820F, 2,
                "UI_SQUADRON_FACT_PILOT2=4 UI_SQUADRON_FACT_PILOT8=4 UI_SQUADRON_FACT_GOOD2=3 UI_SQUADRON_FACT_ROGUE19=2 kills=96 UI_SQUADRON_SHIP_FACT21"),
            (0xE2CA12B9B36128D0, 2,
                "UI_SQUADRON_FACT_PILOT9=4 UI_SQUADRON_FACT_PILOT10=5 UI_SQUADRON_FACT_GOOD12=6 UI_SQUADRON_FACT_ROGUE15=1 kills=93 UI_SQUADRON_SHIP_FACT11"),
            (0xB6748788BB187223, 3,
                "UI_SQUADRON_FACT_PILOT10=3 UI_SQUADRON_FACT_PILOT8=3 UI_SQUADRON_FACT_GOOD11=3 UI_SQUADRON_FACT_ROGUE4=2 kills=134 UI_SQUADRON_SHIP_FACT25"),
        ];
        foreach (var (seed, rank, expected) in pilots)
        {
            var f = SquadronPilotFacts.Generate(seed, rank, ranges);
            string actual = string.Join(" ", f.Skills.Select(s => $"{s.Key}={s.Grade}")) + $" kills={f.Kills} {f.NoteKey}";
            if (actual != expected)
                throw new InvalidOperationException($"Squadron pilot {seed:X16}: {actual}, expected {expected}.");
        }
        Console.WriteLine($"PASS: squadron pilot stat cards ({pilots.Length} in-game pilots).");
    }
}
