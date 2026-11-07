namespace ArtifactX.Core.Universe;

/// <summary>Extracted GcShipWeaponData and spaceship/laser globals for one weapon mode.</summary>
public sealed record StarshipWeaponPotentialRules(bool IsProjectile, bool IsRocket,
    float OverheatCoolTime, float ReferenceRange, int BeamDefaultDamage, float BeamHitRate,
    float BeamOverheatDownTime);

/// <summary>Already evaluated technology totals. BeamDamage comes from the active player's
/// ship when the native beam path supplies null inventories, not from the predicted wreck.</summary>
public sealed record StarshipWeaponPotentialInputs(float Damage, float Rate, float Shots,
    float HeatTime, float OwnerRange, float BeamDamage);

/// <summary>Weapon-potential arithmetic at 0x1417EC4F0/0x1417EC7F0 (build 26295378).
/// Inputs must already respect the caller's damage filter and intrinsic adjustments.
/// Biome, custom damage and difficulty overrides must be resolved separately.
/// Checked 2026-10-31 against 700 native mode evaluations (100 input sets x 7 modes),
/// including integer shot/beam quantization and clamped owner range: exact floats.
/// This arithmetic alone does not establish a generated inventory or its active context.</summary>
public static class StarshipWeaponPotentialGeneration
{
    public static float? Evaluate(StarshipWeaponPotentialRules rules, StarshipWeaponPotentialInputs input)
    {
        if (!float.IsFinite(input.Damage) || !float.IsFinite(input.Rate) || !float.IsFinite(input.Shots)
            || input.Shots is < 0f or >= 2147483648f || !float.IsFinite(input.HeatTime)
            || !float.IsFinite(input.OwnerRange) || !float.IsFinite(input.BeamDamage)
            || input.BeamDamage is < 0f or >= 2147483648f) return null;
        float result;
        if (rules.IsProjectile)
        {
            if (!float.IsFinite(rules.ReferenceRange) || rules.ReferenceRange <= 0f
                || !float.IsFinite(rules.OverheatCoolTime) || rules.OverheatCoolTime <= 0f
                || input.HeatTime < 0f) return null;
            float range = Math.Clamp(input.OwnerRange / rules.ReferenceRange, 0f, 1f);
            float damage = ((int)input.Shots * input.Rate) * input.Damage;
            result = rules.IsRocket ? (damage * range) / rules.OverheatCoolTime
                : ((input.HeatTime / (rules.OverheatCoolTime + input.HeatTime)) * damage) * range;
        }
        else
        {
            if (rules.BeamDefaultDamage < 0 || !float.IsFinite(rules.BeamHitRate) || rules.BeamHitRate <= 0f
                || !float.IsFinite(rules.BeamOverheatDownTime) || rules.BeamOverheatDownTime <= 0f
                || input.HeatTime < 0f) return null;
            long rawDamage = (long)Math.Max(1, (int)input.BeamDamage) * rules.BeamDefaultDamage;
            if (rawDamage > int.MaxValue) return null;
            float beam = (1f / rules.BeamHitRate) * rawDamage;
            result = (input.HeatTime / (input.HeatTime + rules.BeamOverheatDownTime)) * beam;
            result += result;
        }
        return float.IsFinite(result) ? result : null;
    }
}
