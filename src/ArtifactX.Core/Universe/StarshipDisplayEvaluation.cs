namespace ArtifactX.Core.Universe;

/// <summary>Composes normal-ship selection, damage, adjacency, intrinsic mapping and display arithmetic.
/// The active ship context is explicit: native beam damage and the comparison resource draws can use it.
/// Caller must exclude food, weapon overrides and unresolved model technology overrides.</summary>
public static class StarshipDisplayEvaluation
{
    public static StarshipDisplayedStats? Evaluate(ulong siteKey, ulong modelSeed, int shipClass, int rating,
        GeneratedMultiToolLayout layout, IReadOnlyList<KeyValuePair<string, float>> intrinsicBonuses,
        StarshipEvaluationTables rules, StarshipComparisonContext context, int nativeWealth, int requiredLevel,
        IReadOnlySet<string> knownTechnologies, bool hasTechnologyOverrides)
    {
        if (rules.Weapons.Length != 7 || !rules.UseMultiplierBonus
            || StarshipTechnologyGeneration.SelectNormalLoadout(siteKey, rules.Technology, layout,
                nativeWealth, requiredLevel, knownTechnologies, hasTechnologyOverrides) is not { } technology
            || MultiToolInventoryGeneration.InitialSuperchargedSlots(modelSeed, rating, rules.SuperchargedSlots) is not { } special
            || StarshipTechnologyDamageGeneration.InitialDamage(siteKey, technology.Select(t => t.Id).ToArray(),
                rules.BrokenTechnologyChance) is not { } damage) return null;
        var inventory = new GeneratedMultiToolInventory(layout, technology, special);
        var damaged = technology.Where((_, i) => damage[i]).Select(t => t.Position).ToHashSet();
        var ids = rules.Stats;
        float? Stat(GeneratedMultiToolInventory inv, IReadOnlyList<KeyValuePair<string, float>> bonuses,
            IReadOnlySet<GeneratedInventoryPosition> broken, int id, MultiToolStatCombination combination,
            int filter = 0, bool includeDamaged = true)
        {
            var value = MultiToolStatGeneration.Evaluate(inv, rules.Technology, id, combination, filter,
                damagedTechnologyPositions: broken, includeDamagedTechnology: includeDamaged);
            return value is { } v ? StarshipIntrinsicStatGeneration.Apply(v, id, bonuses,
                rules.IntrinsicTargets, rules.UseMultiplierBonus) : null;
        }
        float? Wreck(int id, MultiToolStatCombination combination, int filter = 0, bool includeDamaged = true)
            => Stat(inventory, intrinsicBonuses, damaged, id, combination, filter, includeDamaged);
        float? Active(int id, MultiToolStatCombination combination, int filter)
            => Stat(context.ActiveTechnology, context.ActiveIntrinsicBonuses, context.ActiveDamagedPositions,
                id, combination, filter, false);
        var potentials = new float[7];
        for (int mode = 0; mode < potentials.Length; mode++)
        {
            var weapon = rules.Weapons[mode];
            float? damageStat = weapon.Potential.IsProjectile ? Wreck(ids.Damage, MultiToolStatCombination.Add, weapon.BaseStat, false) : 0f;
            float? rate = weapon.Potential.IsProjectile ? Wreck(ids.Rate, MultiToolStatCombination.Multiply, weapon.BaseStat, false) : 1f;
            float? shots = weapon.Potential.IsProjectile ? Wreck(ids.Shots, MultiToolStatCombination.Maximum, weapon.BaseStat, false) : 1f;
            float? heat = Wreck(weapon.Potential.IsProjectile ? ids.HeatTime : ids.BeamHeat, MultiToolStatCombination.Multiply, weapon.BaseStat, false);
            float? beam = weapon.Potential.IsProjectile ? 0f : Active(ids.BeamDamage, MultiToolStatCombination.Add, weapon.BaseStat);
            float? range = weapon.Potential.IsProjectile ? Active(ids.Range, MultiToolStatCombination.Multiply, weapon.BaseStat) : 0f;
            if (damageStat is not { } d || rate is not { } r || shots is not { } s || heat is not { } h
                || beam is not { } b || range is not { } distance
                || StarshipWeaponPotentialGeneration.Evaluate(weapon.Potential, new(d, r, s, h, distance, b)) is not { } value)
                return null;
            potentials[mode] = value;
        }
        var intrinsicDamage = intrinsicBonuses.FirstOrDefault(b => b.Key == "SHIP_DAMAGE").Value;
        float? finalDamage = StarshipDisplayStatGeneration.DamagePotential(context.ResourceSeed, intrinsicDamage, potentials);
        // Compare applies the intrinsic shield percentage in its final expression,
        // unlike jump and flight stats; applying it here would count it twice.
        float? shield = MultiToolStatGeneration.Evaluate(inventory, rules.Technology, ids.Shield,
            MultiToolStatCombination.Add, damagedTechnologyPositions: damaged);
        float? jump = Wreck(ids.JumpDistance, MultiToolStatCombination.Add);
        float? pulse = Wreck(ids.PulseSpeed, MultiToolStatCombination.Multiply);
        float? boost = Wreck(ids.BoostManeuverability, MultiToolStatCombination.Multiply);
        float? finalShield = shield is { } shieldStat ? StarshipDisplayStatGeneration.ShieldStrength(
            intrinsicBonuses.FirstOrDefault(b => b.Key == "SHIP_SHIELD").Value, shieldStat) : null;
        float? finalJump = jump is { } jumpStat ? StarshipDisplayStatGeneration.HyperdriveRange(jumpStat) : null;
        float? finalManeuverability = pulse is { } pulseStat && boost is { } boostStat
            ? StarshipDisplayStatGeneration.Maneuverability(context.ResourceSeed, shipClass, rating, rules.Flight, pulseStat, boostStat) : null;
        return finalDamage is { } fd && finalShield is { } fs && finalJump is { } fj && finalManeuverability is { } fm
            ? new(fd, fs, fj, fm) : null;
    }
}
