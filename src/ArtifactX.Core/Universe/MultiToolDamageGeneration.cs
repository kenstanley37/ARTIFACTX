namespace ArtifactX.Core.Universe;

public sealed record MultiToolDamageStatIds(int LaserDamage, int LaserHeat, int LaserReload,
    int ProjectileDamage, int ProjectileRate, int ProjectileClip, int ProjectileReload,
    int ExtraBullets, int MaximumCharge, int ChargeTime);

public sealed record MultiToolWeaponMode(string Name, int WeaponClass, int BaseStat,
    int BeamDamage = 0, float BeamHitInterval = 0f);

/// <summary>Complete catalog mode list and standard OVERHEAT duration, with resolved intrinsic mapping.</summary>
public sealed record MultiToolDamageRules(MultiToolDamageStatIds Stats, string DamageBonusId,
    float OverheatDuration, int ExpectedModeCount, MultiToolWeaponMode[] Modes);

/// <summary>The display maximum over all catalog weapon modes for supported standard multi-tools.</summary>
public static class MultiToolDamageGeneration
{
    /// <summary>
    /// Standard OVERHEAT animation and default runtime flags only. NMS build 180836
    /// walks every catalog mode, including inactive modes, and takes the maximum.
    /// Verified against 100 native maxima and 2100 native individual mode values,
    /// including nonzero intrinsic damage, charged projectiles and the rail beam.
    /// All four generated reference tools reproduce their screenshot damage values.
    /// </summary>
    public static float? StandardPotential(GeneratedMultiToolInventory inventory, MultiToolTechnologyTables tables,
        IReadOnlyDictionary<string, float> intrinsicBonuses, bool hasActiveSpecialTechnology = false)
    {
        var modes = StandardModePotentials(inventory, tables, intrinsicBonuses, hasActiveSpecialTechnology);
        return modes is null ? null : modes.Max();
    }

    /// <summary>Returns one supported potential per catalog mode in native mode order.</summary>
    public static IReadOnlyList<float>? StandardModePotentials(GeneratedMultiToolInventory inventory, MultiToolTechnologyTables tables,
        IReadOnlyDictionary<string, float> intrinsicBonuses, bool hasActiveSpecialTechnology = false)
    {
        if (hasActiveSpecialTechnology || tables.DamageRules is not { } rules || rules.ExpectedModeCount <= 0
            || rules.Stats is null || rules.Modes is null
            || string.IsNullOrWhiteSpace(rules.DamageBonusId)
            || rules.Modes.Length != rules.ExpectedModeCount || rules.Modes.Any(m => m is null || string.IsNullOrWhiteSpace(m.Name))
            || rules.Modes.Select(m => m.Name).Distinct(StringComparer.Ordinal).Count() != rules.ExpectedModeCount
            || !intrinsicBonuses.TryGetValue(rules.DamageBonusId, out float bonus) || !float.IsFinite(bonus)) return null;
        var potentials = new List<float>();
        foreach (var mode in rules.Modes)
        {
            if (mode.WeaponClass is not (1 or 2 or 3))
            {
                potentials.Add(0f); // Other classes return zero in 0x141416930.
                continue;
            }
            float? Stat(int id, MultiToolStatCombination combination) => MultiToolStatGeneration.Evaluate(
                inventory, tables, id, combination, mode.BaseStat);
            int damageStat = mode.WeaponClass == 3 ? rules.Stats.LaserDamage : rules.Stats.ProjectileDamage;
            if (Stat(damageStat, MultiToolStatCombination.Add) is not { } damage) return null;
            damage += (bonus * damage) * .01f;
            if (!float.IsFinite(damage) || damage < 0f || damage >= int.MaxValue) return null;
            float perHit = (int)damage;
            float value;
            if (mode.WeaponClass == 3)
            {
                perHit = Math.Max(1f, perHit) * mode.BeamDamage;
                var heat = Stat(rules.Stats.LaserHeat, MultiToolStatCombination.Multiply);
                var reload = Stat(rules.Stats.LaserReload, MultiToolStatCombination.Multiply);
                var potential = heat is { } h && reload is { } r
                    ? MultiToolEquippedStats.LaserPotential(perHit, h, r, rules.OverheatDuration, mode.BeamHitInterval) : null;
                if (potential is not { } laser) return null;
                value = laser;
            }
            else if (mode.WeaponClass == 1)
            {
                var rate = Stat(rules.Stats.ProjectileRate, MultiToolStatCombination.Multiply);
                var bullets = Stat(rules.Stats.ExtraBullets, MultiToolStatCombination.Maximum);
                var clip = Stat(rules.Stats.ProjectileClip, MultiToolStatCombination.Add);
                var reload = Stat(rules.Stats.ProjectileReload, MultiToolStatCombination.Multiply);
                var potential = rate is { } f && bullets is { } b && clip is { } c && reload is { } r
                    ? MultiToolEquippedStats.ProjectilePotential(perHit, f, b, c, r) : null;
                if (potential is not { } projectile) return null;
                value = projectile;
            }
            else
            {
                if (Stat(rules.Stats.MaximumCharge, MultiToolStatCombination.Add) is not { } charge
                    || Stat(rules.Stats.ChargeTime, MultiToolStatCombination.Multiply) is not { } duration
                    || charge < 0f || charge >= int.MaxValue || duration <= 0f) return null;
                // Fixed executable coefficient at 0x144b29a40, not a technology/table value.
                value = (((int)charge * perHit) * 2.015f) / duration;
            }
            if (!float.IsFinite(value) || value < 0f) return null;
            potentials.Add(value);
        }
        return potentials;
    }
}
