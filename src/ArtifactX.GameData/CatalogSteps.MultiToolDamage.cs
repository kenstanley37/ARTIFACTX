using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>Complete mode metadata for the standard multi-tool display routine at 0x14140f400.</summary>
    private static MultiToolDamageRules? MultiToolDamage(Func<string, NMSTemplate?> decode, MultiToolLaserRules? standardLaser)
    {
        if (standardLaser is null
            || decode("GCGAMEPLAYGLOBALS.GLOBAL.MBIN") is not GcGameplayGlobals { UseMultiplierBonus: true }
            || decode("METADATA/REALITY/TABLES/PLAYERWEAPONPROPERTIESTABLE.MBIN") is not GcPlayerWeaponPropertiesTable weaponTable
            || decode("METADATA/PROJECTILES/PROJECTILETABLE.MBIN") is not GcProjectileDataTable projectiles
            || decode("METADATA/REALITY/TABLES/INVENTORYTABLE.MBIN") is not GcInventoryTable inventory) return null;
        var modeEnums = Enum.GetValues<GcPlayerWeapons.WeaponModeEnum>();
        if (weaponTable.PropertiesData is not { } data || data.Length != modeEnums.Length) return null;
        var statIds = new MultiToolDamageStatIds((int)GcStatsTypes.StatsTypeEnum.Weapon_Laser_Damage,
            (int)GcStatsTypes.StatsTypeEnum.Weapon_Laser_HeatTime, (int)GcStatsTypes.StatsTypeEnum.Weapon_Laser_ReloadTime,
            (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_Damage, (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_Rate,
            (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_ClipSize, (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_ReloadTime,
            (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_BulletsPerShot, (int)GcStatsTypes.StatsTypeEnum.Weapon_Projectile_MaximumCharge,
            (int)GcStatsTypes.StatsTypeEnum.Weapon_ChargedProjectile_ChargeTime);
        string? damageId = null;
        foreach (int stat in new[] { statIds.LaserDamage, statIds.ProjectileDamage })
        {
            var sources = (inventory.BaseStats ?? new()).Where(s => (s.StatBonus ?? new()).Any(b => (int)b.StatType.StatsType == stat)).ToArray();
            if (sources.Length != 1) return null;
            var bonuses = sources[0].StatBonus.Where(b => (int)b.StatType.StatsType == stat).ToArray();
            if (bonuses.Length != 1 || bonuses[0].LessIsBetter || string.IsNullOrWhiteSpace(sources[0].BaseStatID.Value)) return null;
            if (damageId is not null && damageId != sources[0].BaseStatID.Value) return null;
            damageId = sources[0].BaseStatID.Value;
        }
        var modes = new List<MultiToolWeaponMode>();
        foreach (var mode in modeEnums)
        {
            var properties = data[(int)mode];
            int weaponClass = (int)properties.WeaponClass.WeaponClass;
            if (weaponClass is 1 or 2 or 3 && properties.UsesCustomBiomeStats) return null;
            int beamDamage = 0;
            float beamInterval = 0f;
            if (properties.WeaponClass.WeaponClass == GcPlayerWeaponClass.WeaponClassEnum.Laser)
            {
                string beamId = mode == GcPlayerWeapons.WeaponModeEnum.Rail ? "PLAYER_RAIL" : "PLAYER";
                var beams = (projectiles.Lasers ?? new()).Where(b => b.Id.Value == beamId).ToArray();
                if (beams.Length != 1 || beams[0].DefaultDamage < 0 || !float.IsFinite(beams[0].HitRate) || beams[0].HitRate <= 0f) return null;
                beamDamage = beams[0].DefaultDamage;
                beamInterval = beams[0].HitRate;
            }
            modes.Add(new MultiToolWeaponMode(mode.ToString(), weaponClass, (int)properties.Stat.StatsType, beamDamage, beamInterval));
        }
        return new MultiToolDamageRules(statIds, damageId!, standardLaser.OverheatDuration, modeEnums.Length, modes.ToArray());
    }
}
