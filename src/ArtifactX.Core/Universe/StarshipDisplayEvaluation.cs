namespace ArtifactX.Core.Universe;

/// <summary>Composes normal-ship selection, damage, adjacency, intrinsic mapping and display arithmetic.
/// The active ship context is explicit: native beam damage and the comparison resource draws can use it.
/// Caller must exclude food, weapon overrides and unresolved model technology overrides.</summary>
public static class StarshipDisplayEvaluation
{
    /// <summary>Normal inventory category only, with catalog-confirmed absence of
    /// component overrides across the model's scenes. Sail and Sentinel use their
    /// own candidate lists; Sentinel Compare regenerates special cells from the site seed.</summary>
    public static bool SupportsComparisonModel(SystemStarshipType type, InventoryGenerationTables? tables) =>
        type is SystemStarshipType.Shuttle or SystemStarshipType.Fighter or SystemStarshipType.Hauler
            or SystemStarshipType.Explorer or SystemStarshipType.Exotic or SystemStarshipType.Solar or SystemStarshipType.SentinelInterceptor
        && tables?.ShipTechnologyOverrides is { } overrides
        && overrides.TryGetValue(type.ToString(), out bool? hasOverrides) && hasOverrides == false;

    public static StarshipDisplayedStats? Evaluate(ulong siteKey, ulong modelSeed, int shipClass, int rating,
        GeneratedMultiToolLayout layout, IReadOnlyList<KeyValuePair<string, float>> intrinsicBonuses,
        StarshipEvaluationTables rules, StarshipComparisonContext context, int nativeWealth, int requiredLevel,
        IReadOnlySet<string> knownTechnologies, bool hasTechnologyOverrides)
    {
        // Compare inventory materialization regenerates Sentinel special cells from
        // its site inventory seed (0x1408F6993..0x1408F69BA). Ciss L16 screenshot
        // confirms the rocket cell (2,1), distinct from the visor/model cell (3,1).
        var selectionTables = shipClass == 9 ? rules.SentinelTechnology
            : StarshipTechnologyGeneration.ForShipClass(shipClass, rules.Technology);
        if (rules.Weapons.Length != 7 || !rules.UseMultiplierBonus || selectionTables is null
            || StarshipTechnologyGeneration.SelectNormalLoadout(siteKey, selectionTables, layout,
                nativeWealth, requiredLevel, knownTechnologies, hasTechnologyOverrides) is not { } technology
            || MultiToolInventoryGeneration.InitialSuperchargedSlots(shipClass == 9 ? siteKey : modelSeed,
                rating, rules.SuperchargedSlots) is not { } special
            || StarshipTechnologyDamageGeneration.InitialDamage(siteKey, technology.Select(t => t.Id).ToArray(),
                shipClass == 9 ? 0f : rules.BrokenTechnologyChance) is not { } damage) return null;
        var inventory = new GeneratedMultiToolInventory(layout, technology, special);
        var damaged = technology.Where((_, i) => damage[i]).Select(t => t.Position).ToHashSet();
        return Compose(inventory, intrinsicBonuses, damaged, shipClass, rating, rules, context);
    }

    /// <summary>
    /// An owned ship's displayed stats (#17): the same final stage as a wreck on Compare, run on the
    /// ship's saved technology, supercharged cells, damage and cargo intrinsic bonuses
    /// (<see cref="ArtifactX.Core.NmsModels.NmsStarshipComparisonPaths.Read"/>), with the ship as its own
    /// active context (photon range, beam damage and the resource-seeded rolls come from the ship itself).
    /// When the ship is the active one this is exactly the left-hand side of the Compare screen that matched
    /// the owner's wreck screenshots on 2026-10-31. Not yet checked: a non-active ship against its own in-game
    /// stats screen (the resource seed is assumed to be the ship's own NTx seed).
    /// Confirmed in game 2026-11-14 (Slot 8, analysis visor): with the ship active, its four values are
    /// the ship-as-own-context result (MainShip, Sentinel μ-Flameborn 414.3/184.0/645.0/356.4, Living Ship).
    /// The visor rates a NON-active ship against the active one like Compare: pass
    /// <paramref name="activeShip"/> (its resource seed, beam and range) - μ-Flameborn with the Living Ship
    /// active showed 378.9/184.0/645.0/377.5, exactly this with the Living Ship as activeShip.
    /// </summary>
    public static StarshipDisplayedStats? EvaluateOwned(StarshipComparisonContext ship, int shipClass, int rating,
        StarshipEvaluationTables rules, StarshipComparisonContext? activeShip = null) =>
        rules.Weapons.Length == 7 && rules.UseMultiplierBonus
            ? Compose(ship.ActiveTechnology, ship.ActiveIntrinsicBonuses, ship.ActiveDamagedPositions, shipClass, rating, rules,
                activeShip ?? ship)
            : null;

    /// <summary>GcSpaceshipClasses.Corvette.</summary>
    private const int CorvetteClass = 10;

    /// <summary>
    /// A corvette's weapons get no adjacency bonus in Damage Potential; its shield, hyperdrive and handling do.
    /// Controlled in-game test 2026-11-17, Slot 8 corvette Jiyodat RR1 (stats screen, build 185034): the game showed
    /// 220.6 where full adjacency gives 226.10; without weapon adjacency it's 220.55. Moving Nonlinear Optics off the
    /// Photon Cannon (adjacency predicted -3.2) and then Large Rocket Tubes off the Rocket Launcher (-2.3) left the
    /// game at 220.6 both times. Dropping ALL adjacency instead breaks the confirmed Shield 394.9 (-> 392.7) and
    /// Hyperdrive 1344.0 (-> 1293.0). Other ship classes keep weapon adjacency: unconfirmed either way, since no
    /// Slot 8 ship has an upgrade beside an uncapped weapon.
    /// </summary>
    private static StarshipDisplayedStats? Compose(GeneratedMultiToolInventory inventory,
        IReadOnlyList<KeyValuePair<string, float>> intrinsicBonuses, IReadOnlySet<GeneratedInventoryPosition> damaged,
        int shipClass, int rating, StarshipEvaluationTables rules, StarshipComparisonContext context)
    {
        var ids = rules.Stats;
        var unlinked = new HashSet<int> { ids.Shots };
        bool weaponAdjacency = shipClass != CorvetteClass;
        float? Stat(GeneratedMultiToolInventory inv, IReadOnlyList<KeyValuePair<string, float>> bonuses,
            IReadOnlySet<GeneratedInventoryPosition> broken, int id, MultiToolStatCombination combination,
            int filter = 0, bool includeDamaged = true, bool adjacency = true)
        {
            var value = MultiToolStatGeneration.Evaluate(inv, rules.Technology, id, combination, filter,
                damagedTechnologyPositions: broken, includeDamagedTechnology: includeDamaged, unlinkedStats: unlinked,
                includeAdjacency: adjacency);
            return value is { } v ? StarshipIntrinsicStatGeneration.Apply(v, id, bonuses,
                rules.IntrinsicTargets, rules.UseMultiplierBonus) : null;
        }
        float? Wreck(int id, MultiToolStatCombination combination, int filter = 0, bool includeDamaged = true)
            => Stat(inventory, intrinsicBonuses, damaged, id, combination, filter, includeDamaged);
        float? Weapon(int id, MultiToolStatCombination combination, int filter)
            => Stat(inventory, intrinsicBonuses, damaged, id, combination, filter, false, weaponAdjacency);
        float? Active(int id, MultiToolStatCombination combination, int filter)
            => Stat(context.ActiveTechnology, context.ActiveIntrinsicBonuses, context.ActiveDamagedPositions,
                id, combination, filter, false);
        var potentials = new float[7];
        for (int mode = 0; mode < potentials.Length; mode++)
        {
            var weapon = rules.Weapons[mode];
            float? damageStat = weapon.Potential.IsProjectile ? Weapon(ids.Damage, MultiToolStatCombination.Add, weapon.BaseStat) : 0f;
            float? rate = weapon.Potential.IsProjectile ? Weapon(ids.Rate, MultiToolStatCombination.Multiply, weapon.BaseStat) : 1f;
            float? shots = weapon.Potential.IsProjectile ? Weapon(ids.Shots, MultiToolStatCombination.Maximum, weapon.BaseStat) : 1f;
            float? heat = Weapon(weapon.Potential.IsProjectile ? ids.HeatTime : ids.BeamHeat, MultiToolStatCombination.Multiply, weapon.BaseStat);
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
            MultiToolStatCombination.Add, damagedTechnologyPositions: damaged, unlinkedStats: unlinked);
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
