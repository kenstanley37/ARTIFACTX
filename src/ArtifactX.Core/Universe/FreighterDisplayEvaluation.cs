namespace ArtifactX.Core.Universe;

/// <summary>GcStatsTypes ids the freighter stats panel reads (catalog, from libMBIN's enum).</summary>
public sealed record FreighterDisplayStatIds(int JumpDistance, int[] FleetStats);

/// <summary>The two freighter values the game's stats panel shows, as displayed ("%0.1f").</summary>
public sealed record FreighterDisplayedStats(float Hyperdrive, float FleetCoordination);

/// <summary>
/// The freighter stats panel (NMS.exe 0x1406BAD90, build 184271, Ghidra 2026-11-14). Hyperdrive Range
/// (0x1406BCF40, flag 1): equipped Freighter_Hyperdrive_JumpDistance with intrinsic bonuses, plus 1.0
/// (0x144BF5C48). Fleet Coordination (0x1406BD550): the six equipped Freighter_Fleet_* stats (Speed, Fuel,
/// Combat, Trade, Explore, Mine) with intrinsic bonuses, summed, divided by 6.0 (0x144BF6004) and times 20.0
/// (0x144BF60BC). Combination comes from the stat table (0x144AE6484, column 4: 0 add, 1/3 multiply): jump distance
/// adds, the six fleet stats multiply. Supercharge and adjacency follow the shared evaluator (0x140584F90,
/// 0x1405B1970, 0x1405B1B90); the fleet stats share group 175 (catalog format 98). Slot 8 'Trinity' 2026-11-14:
/// app 5,383.8 / 99.7, game 5,302.1 / 98.7 - close, not exact; the module rolls are the next suspect.
/// </summary>
public static class FreighterDisplayEvaluation
{
    public static FreighterDisplayedStats? Evaluate(GeneratedMultiToolInventory technology,
        IReadOnlySet<GeneratedInventoryPosition> damagedPositions, IReadOnlyList<KeyValuePair<string, float>> bonuses,
        StarshipEvaluationTables rules)
    {
        if (rules.Freighter is not { FleetStats.Length: 6 } ids) return null;
        float? Stat(int id, MultiToolStatCombination combination) =>
            MultiToolStatGeneration.Evaluate(technology, rules.Technology, id, combination,
                damagedTechnologyPositions: damagedPositions) is { } value
                ? StarshipIntrinsicStatGeneration.Apply(value, id, bonuses, rules.IntrinsicTargets, rules.UseMultiplierBonus)
                : null;
        if (Stat(ids.JumpDistance, MultiToolStatCombination.Add) is not { } jump) return null;
        float sum = 0f;
        foreach (int id in ids.FleetStats)
        {
            if (Stat(id, MultiToolStatCombination.Multiply) is not { } fleet) return null;
            sum += fleet;
        }
        float hyperdrive = jump + 1f, coordination = sum / 6f * 20f;
        return float.IsFinite(hyperdrive) && float.IsFinite(coordination) ? new(hyperdrive, coordination) : null;
    }
}
