namespace ArtifactX.Core.Universe;

/// <summary>Selected control values from GcSpaceshipGlobals, not inferred from the ship's appearance.</summary>
public sealed record StarshipFlightControl(float BoostingTurnDamp, float TurnStrength);

/// <summary>The two class ranges read by the Compare maneuverability evaluator, in native max/min order.</summary>
public sealed record StarshipFlightClassBonus(float BoostingTurnDampMax, float BoostingTurnDampMin,
    float TurnStrengthMax, float TurnStrengthMin);

/// <summary>Extracted spaceship controls and class ranges, keyed by native spaceship class.</summary>
public sealed record StarshipFlightRules(Dictionary<int, StarshipFlightControl> Controls,
    StarshipFlightClassBonus[] ClassBonuses);

/// <summary>
/// Final Compare-screen arithmetic after technology evaluation. Inputs must already include
/// the game's intrinsic-stat mapping, supercharge, adjacency and damaged-technology rules.
/// This does not generate a loadout or establish which resource seed the comparison supplies.
/// Native routines inspected 2026-10-29, Steam build 26295378: 0x1417EBB60 (damage),
/// 0x1406BBA60 (shield), 0x1406BBC80 (range), and 0x1406BBF50 (maneuverability).
/// Checked against 1,200 isolated native evaluations (300 input sets, all 12 ship classes):
/// every output float matched bit for bit. Supplied technology totals were stub inputs;
/// this establishes only the final arithmetic, not complete generated wreck stats.
/// </summary>
public static class StarshipDisplayStatGeneration
{
    /// <summary>
    /// Damage's seven already evaluated weapon modes, in native order. Divisors and
    /// the 50-point contribution are executable arithmetic constants, not MBIN defaults.
    /// The caller must supply the comparison's resource seed explicitly.
    /// </summary>
    public static float? DamagePotential(ulong comparisonResourceSeed, float intrinsicDamageBonus,
        IReadOnlyList<float> weaponPotentials)
    {
        if (!float.IsFinite(intrinsicDamageBonus) || weaponPotentials.Count != 7
            || weaponPotentials.Any(v => !float.IsFinite(v))) return null;
        ReadOnlySpan<float> divisors = [1500f, 4000f, 18000f, 10000f, 8500f, 5000f, 2000f];
        float roll = (float)(SolarLayoutGeneration.RandomFor(comparisonResourceSeed).NextWord()
            * 2.3283064370807974e-10);
        float core = intrinsicDamageBonus / 60f * 215f + (roll * 4f + 33f);
        float contribution = 0f;
        for (int i = 0; i < 7; i++)
            contribution += Math.Clamp(weaponPotentials[i] / divisors[i], 0f, 1f) * 50f;
        return Finite(contribution + core);
    }

    /// <summary>
    /// Shield total from the intrinsic percentage and the evaluated additive shield-strength stat.
    /// Damaged technology still contributes in the Compare evaluator; its adjacency is a separate stage.
    /// </summary>
    public static float? ShieldStrength(float intrinsicShieldBonus, float evaluatedShieldStrength)
    {
        if (!float.IsFinite(intrinsicShieldBonus) || !float.IsFinite(evaluatedShieldStrength)
            || evaluatedShieldStrength <= 0f) return null;
        float core = intrinsicShieldBonus / 85f + 1.65f;
        float extra = (1f - .65f / evaluatedShieldStrength) * core;
        return Finite((extra + core) * 100f);
    }

    /// <summary>Range after complete JumpDistance evaluation, including the intrinsic hyperdrive bonus.</summary>
    public static float? HyperdriveRange(float evaluatedJumpDistance) =>
        evaluatedJumpDistance >= 0f ? Finite(evaluatedJumpDistance + 1f) : null;

    /// <summary>
    /// 0x1406BBF50: two resource-seeded draws, selected planet-engine controls and the cargo class.
    /// Pulse speed and boost maneuverability must be fully evaluated technology stats. In particular,
    /// INVENTORYTABLE's SHIP_AGILE mapping contains repeated target stats; callers must retain duplicates.
    /// </summary>
    public static float? Maneuverability(ulong comparisonResourceSeed, int shipClass, int ratingIndex,
        StarshipFlightRules? rules, float evaluatedPulseSpeed, float evaluatedBoostManeuverability)
    {
        if (ratingIndex is < 0 or > 3 || rules?.ClassBonuses is not { Length: 4 } bonuses
            || !rules.Controls.TryGetValue(shipClass, out var control)
            || !float.IsFinite(evaluatedPulseSpeed) || !float.IsFinite(evaluatedBoostManeuverability)) return null;
        var bonus = bonuses[ratingIndex];
        var rng = SolarLayoutGeneration.RandomFor(comparisonResourceSeed);
        float dampRoll = (float)(rng.NextWord() * 2.3283064370807974e-10);
        float turnRoll = (float)(rng.NextWord() * 2.3283064370807974e-10);
        float turn = turnRoll * (bonus.TurnStrengthMax - bonus.TurnStrengthMin) + bonus.TurnStrengthMin;
        turn = (turn + control.TurnStrength) * 15f;
        float damp = dampRoll * (bonus.BoostingTurnDampMax - bonus.BoostingTurnDampMin) + bonus.BoostingTurnDampMin;
        damp = (damp + control.BoostingTurnDamp) * 35f;
        float baseValue = turn + damp;
        float pulse = (evaluatedPulseSpeed - 1f) * 50f;
        float boost = (evaluatedBoostManeuverability - 1f) * 9f;
        return Finite(((pulse + baseValue) + boost) * 10f + (ratingIndex + 1) * 5f);
    }

    private static float? Finite(float value) => float.IsFinite(value) ? value : null;
}
