using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// Eligible candidates for ordinary category-3 weapon inventory, using the size-to-category
    /// value 12 at 0x1404d4880. Native filtering at 0x1404d4760 maps these entries to Weapon.
    /// Keep file order and do not remove Impossible-rarity entries: they consume random draws.
    /// </summary>
    private static MultiToolTechnologyTables? MultiToolTechnology(Func<string, NMSTemplate?> decode)
    {
        const string technologyFile = "METADATA/REALITY/TABLES/NMS_REALITY_GCTECHNOLOGYTABLE.MBIN";
        const string proceduralFile = "METADATA/REALITY/TABLES/NMS_REALITY_GCPROCEDURALTECHNOLOGYTABLE.MBIN";
        if (decode(technologyFile) is not GcTechnologyTable technology
            || decode(proceduralFile) is not GcProceduralTechnologyTable procedural) return null;
        var templates = (technology.Table ?? new()).ToDictionary(t => t.ID.Value, StringComparer.Ordinal);
        static bool Weapon(GcTechnology t) => t.Category?.TechnologyCategory == GcTechnologyCategory.TechnologyCategoryEnum.Weapon;
        string[] regular = (technology.Table ?? new()).Where(t => !t.IsTemplate && Weapon(t))
            .Select(t => t.ID.Value).ToArray();
        string[] upgrades = (procedural.Table ?? new()).Where(t => !t.IsBiggsProcTech
                && templates.TryGetValue(t.Template.Value, out var template) && Weapon(template))
            .Select(t => t.ID.Value).ToArray();
        TechnologySelectionDefinition Definition(string id, GcTechnology t) => new(id, t.ID.Value,
            (int)t.Rarity.TechnologyRarity, t.RequiredLevel, t.RequiredTech?.Value ?? "", t.PrimaryItem,
            (int)t.BaseStat.StatsType, (t.StatBonuses ?? new()).Select(s => (int)s.Stat.StatsType).ToArray(),
            (t.StatBonuses ?? new()).Select(s => new GeneratedModuleStat((int)s.Stat.StatsType, s.Bonus, s.Level)).ToArray(),
            t.Group?.Value);
        var proceduralById = (procedural.Table ?? new()).ToDictionary(t => t.ID.Value, StringComparer.Ordinal);
        var definitions = regular.Select(id => Definition(id, templates[id]))
            .Concat(upgrades.Select(id => Definition(id, templates[proceduralById[id].Template.Value])
                with { Group = proceduralById[id].Group?.Value })).ToArray();
        // Weapon stat names encode their primary group. Checked against the native stat
        // metadata at 0x144a1b444 for every extracted Weapon technology bonus in build 180836.
        var parents = new Dictionary<int, int>();
        foreach (int stat in definitions.SelectMany(d => d.StatTypes)
            .Concat(upgrades.SelectMany(id => proceduralById[id].StatLevels ?? new())
                .Select(s => (int)s.Stat.StatsType)).Distinct())
        {
            string? name = Enum.GetName((GcStatsTypes.StatsTypeEnum)stat);
            string[] parts = name?.Split('_') ?? [];
            if (parts.Length >= 2 && parts[0] == "Weapon"
                && Enum.TryParse<GcStatsTypes.StatsTypeEnum>(parts[0] + "_" + parts[1], out var parent))
                parents[stat] = (int)parent;
        }
        float[]? weights = decode("GCPLAYERGLOBALS.GLOBAL.MBIN") is GcPlayerGlobals player
            ? player.TechRarityData?.ToArray() : null;
        var modules = upgrades.Select(id => proceduralById[id]).Select(p => new ProceduralModuleDefinition(
            p.ID.Value, (int)p.Quality, p.NumStatsMin, p.NumStatsMax, (int)p.WeightingCurve.WeightingCurve,
            (p.StatLevels ?? new()).Select(s => new ProceduralModuleStat((int)s.Stat.StatsType,
                s.ValueMin, s.ValueMax, (int)s.WeightingCurve.WeightingCurve, s.AlwaysChoose)).ToArray())).ToArray();
        int[]? curves = decode("METADATA/REALITY/DEFAULTREALITY.MBIN") is GcRealityManagerData reality
            ? reality.WeightingCurves?.Select(c => (int)c.Curve).ToArray() : null;
        MultiToolScannerRules? scannerRules = null;
        if (decode("GCGAMEPLAYGLOBALS.GLOBAL.MBIN") is GcGameplayGlobals gameplay
            && decode("METADATA/SIMULATION/SCANNING/SCANDATATABLE.MBIN") is GcScanDataTable scans)
        {
            var toolRows = (scans.ScanData ?? new()).Where(s => s.ID.Value == "TOOL").ToArray();
            if (toolRows.Length == 1)
                scannerRules = new MultiToolScannerRules((int)GcStatsTypes.StatsTypeEnum.Weapon_Scan_Radius,
                    toolRows[0].ScanData.PulseRange, gameplay.SpecialTechSlotBonus,
                    gameplay.BonusSameTypeElementsMultiply, gameplay.BonusChildTypeElementsMultiply,
                    gameplay.MaxNumSameGroupTech, gameplay.BonusSameTypeElementsAdd, gameplay.BonusChildTypeElementsAdd);
        }
        MultiToolLaserRules? laserRules = null;
        const string entityFile = "MODELS/COMMON/WEAPONS/MULTITOOL/MULTITOOL/ENTITIES/MULTITOOL.ENTITY.MBIN";
        if (decode("METADATA/PROJECTILES/PROJECTILETABLE.MBIN") is GcProjectileDataTable projectiles
            && decode(entityFile) is TkAttachmentData entity)
        {
            var beams = (projectiles.Lasers ?? new()).Where(l => l.Id.Value == "PLAYER").ToArray();
            var animations = (entity.Components ?? new()).Select(c => c.Template).OfType<TkAnimationComponentData>()
                .SelectMany(c => c.Anims ?? new()).Where(a => a.Anim.Value == "OVERHEAT").ToArray();
            if (beams.Length == 1 && animations.Length == 1 && animations[0].FrameEnd > animations[0].FrameStart
                && float.IsFinite(animations[0].Speed) && animations[0].Speed > 0f)
            {
                var animation = animations[0];
                float duration = ((animation.FrameEnd - animation.FrameStart) * (animation.Has30HzFrames ? 1f / 30f : 1f / 60f)) / animation.Speed;
                laserRules = new MultiToolLaserRules((int)GcStatsTypes.StatsTypeEnum.Weapon_Laser,
                    (int)GcStatsTypes.StatsTypeEnum.Weapon_Laser_Damage,
                    (int)GcStatsTypes.StatsTypeEnum.Weapon_Laser_HeatTime,
                    (int)GcStatsTypes.StatsTypeEnum.Weapon_Laser_ReloadTime,
                    beams[0].DefaultDamage, beams[0].HitRate, duration);
            }
        }
        MultiToolProjectileRules? boltRules = null;
        if (decode("METADATA/REALITY/TABLES/PLAYERWEAPONPROPERTIESTABLE.MBIN") is GcPlayerWeaponPropertiesTable weaponModes
            && weaponModes.PropertiesData is { } modes && modes.Length > (int)GcPlayerWeapons.WeaponModeEnum.Bolt
            && decode("GCGAMEPLAYGLOBALS.GLOBAL.MBIN") is GcGameplayGlobals { UseMultiplierBonus: true }
            && decode("METADATA/REALITY/TABLES/INVENTORYTABLE.MBIN") is GcInventoryTable inventory)
        {
            var bolt = modes[(int)GcPlayerWeapons.WeaponModeEnum.Bolt];
            var damageSources = (inventory.BaseStats ?? new()).Where(s => (s.StatBonus ?? new())
                .Any(b => b.StatType.StatsType == GcStatsTypes.StatsTypeEnum.Weapon_Projectile_Damage)).ToArray();
            if (!bolt.UsesCustomBiomeStats && bolt.WeaponClass.WeaponClass == GcPlayerWeaponClass.WeaponClassEnum.Projectile
                && bolt.Stat.StatsType == GcStatsTypes.StatsTypeEnum.Weapon_Projectile
                && damageSources.Length == 1 && damageSources[0].StatBonus.Count(b =>
                    b.StatType.StatsType == GcStatsTypes.StatsTypeEnum.Weapon_Projectile_Damage) == 1
                && !damageSources[0].StatBonus.Single(b => b.StatType.StatsType == GcStatsTypes.StatsTypeEnum.Weapon_Projectile_Damage).LessIsBetter)
                boltRules = new MultiToolProjectileRules((int)bolt.Stat.StatsType,
                    (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_Damage,
                    (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_Rate,
                    (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_BulletsPerShot,
                    (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_ClipSize,
                    (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_ReloadTime, damageSources[0].BaseStatID.Value);
        }
        return new MultiToolTechnologyTables(regular, upgrades, definitions, weights, parents, modules, curves, scannerRules, laserRules, boltRules,
            MultiToolDamage(decode, laserRules));
    }
}
