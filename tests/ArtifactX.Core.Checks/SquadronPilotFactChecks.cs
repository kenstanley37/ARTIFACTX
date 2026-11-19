using ArtifactX.Core.Squadron;
using ArtifactX.Core.Universe;

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

        // The same pilots' names from their pilot-and-ship name seeds; all four are Vy'keen (GcAlienRace Warriors = 1).
        // WAR_TITLE_20 Liquidator, 4 Captain, 29 Scout, 16 Recruit.
        (ulong Seed, string Expected)[] names =
        [
            (0xAB87F8A10DB2B4CF, "WAR_TITLE_20 Uduoh"),
            (0x441316044221C4DC, "WAR_TITLE_4 Yongomord"),
            (0x125DA81C641DA997, "WAR_TITLE_29 Ribxi"),
            (0xFF4EE721EEF1E2AD, "WAR_TITLE_16 Gangolag"),
        ];
        foreach (var (seed, expected) in names)
        {
            var name = NpcNames.Generate(seed, 1);
            string actual = name is null ? "(none)" : $"{name.TitleKey} {name.Name}";
            if (actual != expected)
                throw new InvalidOperationException($"Squadron pilot name {seed:X16}: {actual}, expected {expected}.");
        }
        Console.WriteLine($"PASS: squadron pilot names ({names.Length} in-game pilots).");

        // Find Best Traits: each rank's limit, and a seed the search found per rank (2026-11-19) scoring exactly that.
        (int Rank, int Best, ulong Seed)[] limits =
        [
            (0, 4, 0xC0B1B5F2BA89E5DD), (1, 3, 0xB8E255D69869BB74), (2, 2, 0x8DC319EB9D407AF0), (3, 1, 0x7F511023288D100A),
        ];
        foreach (var (rank, best, seed) in limits)
        {
            if (SquadronTraitSearch.BestGrade(rank, ranges) != best)
                throw new InvalidOperationException($"Rank {rank} best grade {SquadronTraitSearch.BestGrade(rank, ranges)}, expected {best}.");
            var grades = SquadronPilotFacts.Generate(seed, rank, ranges).Skills.Select(k => k.Grade).ToArray();
            if (!grades.SequenceEqual(new[] { best, best, best, 1 }))
                throw new InvalidOperationException($"Rank {rank} seed {seed:X16}: grades {string.Join(",", grades)}.");
        }
        Console.WriteLine("PASS: squadron best traits limits (4 ranks).");
    }
}
