namespace ArtifactX.Core.Universe;

/// <summary>Rejects unknown food effects without hiding tools for unrelated bonus targets.</summary>
public static class MultiToolFoodGuard
{
    /// <summary>
    /// Build 180836, 0x14057a9ed..0x14057a9f6: food adjustment is considered only
    /// when ProductData.FoodBonusStat equals the exact stat being evaluated.
    /// This does not calculate food bonus strength or support a matching target.
    /// </summary>
    public static bool HasNoDisplayedStatEffect(string? productId, MultiToolTechnologyTables tables)
    {
        if (string.IsNullOrEmpty(productId) || tables.ProductFoodBonusStats is not { } products
            || !products.TryGetValue(productId, out int target) || target < 0
            || tables.ScannerRules is not { } scanner || tables.DamageRules?.Stats is not { } damage) return false;
        return !new[] { scanner.RadiusStat, damage.LaserDamage, damage.LaserHeat, damage.LaserReload,
            damage.ProjectileDamage, damage.ProjectileRate, damage.ProjectileClip, damage.ProjectileReload,
            damage.ExtraBullets, damage.MaximumCharge, damage.ChargeTime }.Contains(target);
    }
}
