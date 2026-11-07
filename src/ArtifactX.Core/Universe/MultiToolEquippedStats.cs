namespace ArtifactX.Core.Universe;

/// <summary>
/// Native display-stat arithmetic on already resolved equipped values. This does not
/// resolve technologies, procedural upgrades, adjacency, special-slot modifiers or the
/// maximum over all weapon modes; those inputs must be established before displaying results.
/// </summary>
public static class MultiToolEquippedStats
{
    /// <summary>
    /// Projectile-class branch of build 180836, 0x141416b20. Keep native integer truncation
    /// and float32 operation order, including its rate-times-clip active-time term.
    /// Verified against 100 native formula executions with equipped-stat lookups supplied
    /// explicitly. Iridescent's uncharged BOLT inputs give 1362.7289, displayed as 1362.7.
    /// </summary>
    public static float? ProjectilePotential(float damagePerHit, float fireRate, float additionalBulletsPerShot,
        float clipSize, float reloadTime)
    {
        if (!Finite(damagePerHit, fireRate, additionalBulletsPerShot, clipSize, reloadTime) || damagePerHit < 0f
            || fireRate <= 0f || additionalBulletsPerShot < 0f || additionalBulletsPerShot >= int.MaxValue - 1f
            || clipSize < 0f || clipSize >= int.MaxValue || reloadTime < 0f) return null;
        float rate = ((int)additionalBulletsPerShot + 1) * fireRate;
        float activeTime = (int)clipSize * rate;
        float uptime = activeTime / (reloadTime + activeTime);
        float result = uptime * (rate * damagePerHit);
        return float.IsFinite(result) ? result : null;
    }

    /// <summary>Laser branch at 0x141416991, using equipped heat/reload and actual animation duration.</summary>
    public static float? LaserPotential(float damagePerHit, float heatTime, float reloadMultiplier,
        float overheatDuration, float hitInterval)
    {
        if (!Finite(damagePerHit, heatTime, reloadMultiplier, overheatDuration, hitInterval)
            || damagePerHit < 0f || heatTime <= 0f || reloadMultiplier < 0f
            || overheatDuration < 0f || hitInterval <= 0f) return null;
        float uptime = heatTime / (reloadMultiplier * overheatDuration + heatTime);
        float result = ((1f / hitInterval) * damagePerHit) * uptime;
        return float.IsFinite(result) ? result : null;
    }

    /// <summary>Scanner radius from its resolved technology factor, intrinsic percentage and scan-table range.</summary>
    public static float? ScannerRange(float technologyFactor, float intrinsicBonusPercent, float pulseRange)
    {
        if (!Finite(technologyFactor, intrinsicBonusPercent, pulseRange) || technologyFactor < 0f || pulseRange < 0f)
            return null;
        float adjustment = (intrinsicBonusPercent * technologyFactor) * 0.01f;
        float result = (technologyFactor + adjustment) * pulseRange;
        return float.IsFinite(result) && result >= 0f ? result : null;
    }

    private static bool Finite(params float[] values) => values.All(float.IsFinite);
}
