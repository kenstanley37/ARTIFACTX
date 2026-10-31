namespace ArtifactX.Core.Universe;

public sealed record StarshipDisplayStatIds(int Damage, int Rate, int HeatTime, int Shots, int Range,
    int BeamDamage, int BeamHeat, int Shield, int JumpDistance, int PulseSpeed, int BoostManeuverability);
public sealed record StarshipWeaponEvaluationRule(int BaseStat, StarshipWeaponPotentialRules Potential);

/// <summary>Catalog-owned inputs to the native normal-ship comparison calculation.</summary>
public sealed record StarshipEvaluationTables(MultiToolTechnologyTables Technology, StarshipFlightRules Flight,
    Dictionary<string, StarshipIntrinsicStatTarget[]> IntrinsicTargets, bool UseMultiplierBonus,
    StarshipDisplayStatIds Stats, StarshipWeaponEvaluationRule[] Weapons,
    SuperchargedSlotRules SuperchargedSlots, float BrokenTechnologyChance);

public sealed record StarshipComparisonContext(ulong ResourceSeed, GeneratedMultiToolInventory ActiveTechnology,
    IReadOnlyList<KeyValuePair<string, float>> ActiveIntrinsicBonuses,
    IReadOnlySet<GeneratedInventoryPosition> ActiveDamagedPositions);
public sealed record StarshipDisplayedStats(float Damage, float Shield, float Hyperdrive, float Maneuverability);
