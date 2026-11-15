using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;
using Stat = libMBIN.NMS.GameComponents.GcStatsTypes.StatsTypeEnum;
using Category = libMBIN.NMS.GameComponents.GcTechnologyCategory.TechnologyCategoryEnum;
using ShipClass = libMBIN.NMS.GameComponents.GcSpaceshipClasses.ShipClassEnum;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// Stat groups the name-prefix rule gets wrong: NMS.exe's per-stat table at 0x144AE6484 (6 ints per stat, first =
    /// group; build 184271, read with Ghidra 2026-11-14), which the adjacency count 0x1405B1B90 compares for "same
    /// type". All other stats' groups equal the prefix rule (checked over the whole enum). The fleet stats' group 175
    /// (Freighter_Fleet_Boost) is what makes neighbouring fleet modules of different kinds count as adjacent:
    /// Slot 8 freighter Fleet Coordination 86.0 with prefix groups, 99.7 with these (game 98.7).
    /// </summary>
    private static readonly Dictionary<Stat, Stat> NativeStatGroups = new()
    {
        [Stat.Suit_UnderwaterLifeSupport] = Stat.Suit_Underwater,
        [Stat.Suit_DamageReduce_Cold] = Stat.Suit_Protection, [Stat.Suit_DamageReduce_Heat] = Stat.Suit_Protection,
        [Stat.Suit_DamageReduce_Toxic] = Stat.Suit_Protection, [Stat.Suit_DamageReduce_Radiation] = Stat.Suit_Protection,
        [Stat.Suit_Utility] = Stat.Suit_Jetpack,
        [Stat.Ship_Tech_Slots] = Stat.Ship_Inventory, [Stat.Ship_Cargo_Slots] = Stat.Ship_Inventory,
        [Stat.Ship_WaterLandingJet] = Stat.Ship_CargoShield,
        [Stat.Freighter_Fleet_Speed] = Stat.Freighter_Fleet_Boost, [Stat.Freighter_Fleet_Fuel] = Stat.Freighter_Fleet_Boost,
        [Stat.Freighter_Fleet_Combat] = Stat.Freighter_Fleet_Boost, [Stat.Freighter_Fleet_Trade] = Stat.Freighter_Fleet_Boost,
        [Stat.Freighter_Fleet_Explore] = Stat.Freighter_Fleet_Boost, [Stat.Freighter_Fleet_Mine] = Stat.Freighter_Fleet_Boost,
        [Stat.Vehicle_EngineFuelUse] = Stat.Vehicle_Engine, [Stat.Vehicle_EngineTopSpeed] = Stat.Vehicle_Engine,
        [Stat.Vehicle_Grip] = Stat.Vehicle_Engine, [Stat.Vehicle_SkidGrip] = Stat.Vehicle_Engine,
        [Stat.Vehicle_BoostSpeed] = Stat.Vehicle_Boost, [Stat.Vehicle_BoostTanks] = Stat.Vehicle_Boost,
        [Stat.Vehicle_SubBoostSpeed] = Stat.Vehicle_Boost,
        [Stat.Vehicle_LaserDamage] = Stat.Vehicle_Laser, [Stat.Vehicle_LaserHeatTime] = Stat.Vehicle_Laser,
        [Stat.Vehicle_LaserStrongLaser] = Stat.Vehicle_Laser,
        [Stat.Vehicle_GunDamage] = Stat.Vehicle_Gun, [Stat.Vehicle_GunHeatTime] = Stat.Vehicle_Gun, [Stat.Vehicle_GunRate] = Stat.Vehicle_Gun,
        [Stat.Vehicle_FlameDamage] = Stat.Vehicle_Flame, [Stat.Vehicle_FlameHeatTime] = Stat.Vehicle_Flame,
    };

    /// <summary>Normal-ship comparison metadata. Native category-6 eligibility and stat parents
    /// checked against the executable and two wreck references, 2026-10-31. Candidate order,
    /// Impossible entries and duplicate intrinsic targets are significant.</summary>
    private static StarshipEvaluationTables? StarshipDisplay(Func<string, NMSTemplate?> decode)
    {
        if (decode("METADATA/REALITY/TABLES/NMS_REALITY_GCTECHNOLOGYTABLE.MBIN") is not GcTechnologyTable technology
            || decode("METADATA/REALITY/TABLES/NMS_REALITY_GCPROCEDURALTECHNOLOGYTABLE.MBIN") is not GcProceduralTechnologyTable procedural
            || decode("METADATA/REALITY/DEFAULTREALITY.MBIN") is not GcRealityManagerData reality
            || decode("METADATA/REALITY/TABLES/INVENTORYTABLE.MBIN") is not GcInventoryTable inventory
            || decode("GCPLAYERGLOBALS.GLOBAL.MBIN") is not GcPlayerGlobals player
            || decode("GCGAMEPLAYGLOBALS.GLOBAL.MBIN") is not GcGameplayGlobals gameplay
            || decode("GCSPACESHIPGLOBALS.GLOBAL.MBIN") is not GcSpaceshipGlobals flight
            || decode("GCAISPACESHIPGLOBALS.GLOBAL.MBIN") is not GcAISpaceshipGlobals ai
            || decode("METADATA/PROJECTILES/PROJECTILETABLE.MBIN") is not GcProjectileDataTable projectiles) return null;
        bool Eligible(GcTechnology t) => t.Category.TechnologyCategory is Category.Ship or Category.AllShips or Category.AllShipsExceptAlien;
        var byId = technology.Table.ToDictionary(t => t.ID.Value, StringComparer.Ordinal);
        // SOLAR_SAIL is an executable special-case guard, before the candidate RNG draw.
        var regular = technology.Table.Where(t => !t.IsTemplate && Eligible(t) && t.ID.Value != "SOLAR_SAIL").ToArray();
        var upgrades = procedural.Table.Where(p => !p.IsBiggsProcTech && byId.TryGetValue(p.Template.Value, out var t) && Eligible(t)).ToArray();
        TechnologySelectionDefinition Definition(string id, GcTechnology t, string? group = null) => new(id, t.ID.Value,
            (int)t.Rarity.TechnologyRarity, t.RequiredLevel, t.RequiredTech.Value, t.PrimaryItem, (int)t.BaseStat.StatsType,
            t.StatBonuses.Select(s => (int)s.Stat.StatsType).ToArray(),
            t.StatBonuses.Select(s => new GeneratedModuleStat((int)s.Stat.StatsType, s.Bonus, s.Level)).ToArray(), group ?? t.Group.Value);
        // Lookup includes installed player equipment; selection retains its narrower ordered lists.
        var resolvableModules = procedural.Table.Where(p => byId.ContainsKey(p.Template.Value)).ToArray();
        var definitions = technology.Table.Select(t => Definition(t.ID.Value, t)).Concat(resolvableModules.Select(p =>
            Definition(p.ID.Value, byId[p.Template.Value], p.Group.Value))).ToArray();
        var parents = new Dictionary<int, int>();
        foreach (var stat in Enum.GetValues<Stat>())
        {
            string name = stat.ToString();
            string prefix = name;
            Stat parent = stat;
            while (prefix.LastIndexOf('_') is int split && split > 0)
            {
                prefix = prefix[..split];
                if (Enum.TryParse(prefix, out Stat candidate)) parent = candidate;
            }
            if (stat is Stat.Ship_Boost or Stat.Ship_Maneuverability or Stat.Ship_BoostManeuverability or Stat.Ship_Drift)
                parent = Stat.Ship_PulseDrive;
            if (NativeStatGroups.TryGetValue(stat, out var native)) parent = native;
            parents[(int)stat] = (int)parent;
        }
        var modules = resolvableModules.Select(p => new ProceduralModuleDefinition(p.ID.Value, (int)p.Quality,
            p.NumStatsMin, p.NumStatsMax, (int)p.WeightingCurve.WeightingCurve, p.StatLevels.Select(s =>
                new ProceduralModuleStat((int)s.Stat.StatsType, s.ValueMin, s.ValueMax, (int)s.WeightingCurve.WeightingCurve, s.AlwaysChoose)).ToArray())).ToArray();
        var coefficients = new MultiToolScannerRules((int)Stat.Ship_Armour_Shield_Strength, 0f,
            gameplay.SpecialTechSlotBonus, gameplay.BonusSameTypeElementsMultiply, gameplay.BonusChildTypeElementsMultiply,
            gameplay.MaxNumSameGroupTech, gameplay.BonusSameTypeElementsAdd, gameplay.BonusChildTypeElementsAdd);
        var selection = new MultiToolTechnologyTables(regular.Select(t => t.ID.Value).ToArray(), upgrades.Select(p => p.ID.Value).ToArray(),
            definitions, player.TechRarityData.ToArray(), parents, modules, reality.WeightingCurves.Select(c => (int)c.Curve).ToArray(),
            coefficients, IllegalModuleHighGradeChance: player.ChanceHighGradeIllegal);
        // 0x1404DD5DF: Robot (9) admits RobotShip/AllShips/AllShipsExceptAlien.
        bool RobotEligible(GcTechnology t) => t.Category.TechnologyCategory is Category.RobotShip or Category.AllShips or Category.AllShipsExceptAlien;
        var sentinelSelection = selection with
        {
            TechnologyIds = technology.Table.Where(t => !t.IsTemplate && RobotEligible(t)).Select(t => t.ID.Value).ToArray(),
            ProceduralIds = procedural.Table.Where(p => !p.IsBiggsProcTech && byId.TryGetValue(p.Template.Value, out var t)
                && RobotEligible(t)).Select(p => p.ID.Value).ToArray(),
        };
        GcPlayerSpaceshipControlData Control(ShipClass type) => type switch
        {
            ShipClass.Dropship or ShipClass.Alien => flight.ControlHeavy,
            ShipClass.Fighter or ShipClass.Scientific or ShipClass.Royal or ShipClass.Sail => flight.ControlLight,
            ShipClass.Robot or ShipClass.SwarmDrone => flight.ControlHover,
            ShipClass.Corvette => flight.ControlCorvette,
            _ => flight.Control,
        };
        var controls = Enum.GetValues<ShipClass>().ToDictionary(c => (int)c, c =>
            new StarshipFlightControl(Control(c).PlanetEngine.BoostingTurnDamp, Control(c).PlanetEngine.TurnStrength));
        StarshipFlightClassBonus Bonus(GcPlayerSpaceshipClassBonuses b) => new(b.BoostingTurnDampMax,
            b.BoostingTurnDampMin, b.TurnStrengthMax, b.TurnStrengthMin);
        var flightRules = new StarshipFlightRules(controls, [Bonus(flight.ControlBonusC), Bonus(flight.ControlBonusB), Bonus(flight.ControlBonusA), Bonus(flight.ControlBonusS)]);
        var intrinsic = inventory.BaseStats.Where(b => b.BaseStatID.Value.StartsWith("SHIP_", StringComparison.Ordinal)
                || b.BaseStatID.Value is "ROBOT_SHIP" or "ALIEN_SHIP"
                || b.BaseStatID.Value.StartsWith("FREI_", StringComparison.Ordinal))
            .ToDictionary(b => b.BaseStatID.Value, b => b.StatBonus.Select(s => new StarshipIntrinsicStatTarget((int)s.StatType.StatsType, s.LessIsBetter)).ToArray());
        var beams = projectiles.Lasers.Where(l => l.Id.Value == "PLAYER_SHIP").ToArray();
        if (beams.Length != 1 || reality.ShipWeapons.Length != 7) return null;
        var weapons = reality.ShipWeapons.Select((w, i) => new StarshipWeaponEvaluationRule((int)w.Stat.StatsType,
            new(w.IsProjectile, i == (int)GcShipWeapons.ShipWeaponEnum.Rocket, w.OverheatCoolTime,
                flight.WeaponDamagePotentialReferenceRange, beams[0].DefaultDamage, beams[0].HitRate, flight.LaserOverheatDownTime))).ToArray();
        var special = inventory.GenerationData.GenerationDataPerSizeType[(int)GcInventoryLayoutSizeType.SizeTypeEnum.FgtMedium];
        return new(selection, flightRules, intrinsic, gameplay.UseMultiplierBonus,
            new((int)Stat.Ship_Weapons_Guns_Damage, (int)Stat.Ship_Weapons_Guns_Rate, (int)Stat.Ship_Weapons_Guns_HeatTime,
                (int)Stat.Ship_Weapons_Guns_BulletsPerShot, (int)Stat.Ship_Weapons_Guns_Range, (int)Stat.Ship_Weapons_Lasers_Damage,
                (int)Stat.Ship_Weapons_Lasers_HeatTime, (int)Stat.Ship_Armour_Shield_Strength, (int)Stat.Ship_Hyperdrive_JumpDistance,
                (int)Stat.Ship_Maneuverability, (int)Stat.Ship_BoostManeuverability), weapons,
            new(special.SpecialTechSlotMaxIndex.X, special.SpecialTechSlotMaxIndex.Y, special.MaxNumSpecialTechSlots), ai.CrashedShipBrokenTechChance,
            sentinelSelection,
            // Freighter stats panel (0x1406BAD90): Hyperdrive Range and Fleet Coordination's six averaged stats.
            new((int)Stat.Freighter_Hyperdrive_JumpDistance, [(int)Stat.Freighter_Fleet_Speed, (int)Stat.Freighter_Fleet_Fuel,
                (int)Stat.Freighter_Fleet_Combat, (int)Stat.Freighter_Fleet_Trade, (int)Stat.Freighter_Fleet_Explore,
                (int)Stat.Freighter_Fleet_Mine]));
    }
}
