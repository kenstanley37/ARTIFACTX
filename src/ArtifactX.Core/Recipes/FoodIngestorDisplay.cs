namespace ArtifactX.Core.Recipes;

/// <summary>
/// The amount the Nutrient Ingestor shows for a food ("+65% Cold Resistance"), from
/// the bonus FoodIngestorBonus calculates. Port of the build-184271 stat formatter
/// 0x1405a98d0, called by the ingestor's effect text at 0x1406afbea (UI_FOOD_UNIT_EFFECT_FORMAT
/// "%AMOUNT% %STAT%"), read 2026-11-03:
///   display mode = field 0 of the 24-byte per-stat table at 0x144AE6494 (.rdata);
///   modes 1 and 3: x = (bonus - 1) * 100; 0 &lt; x &lt; 1 becomes 1, -1 &lt; x &lt; 0 becomes -1;
///     x &gt; 0 is rounded up (ceilf), x &lt; 0 down (floorf); some stats are negated
///     (the bit sets at 0x1405a9cd6..0x1405a9d13); a positive amount gets "+".
///   Mode 0: truncate(bonus / reference * 100), at least 1, where the reference is read
///   from the player's live stat data (0x1405aa020); only the scanner rewards' reference
///   (1) is known (RewardStats), so other mode 0 stats return null.
/// Checked against the owner's Slot 8 ingestor 2026-11-03: Ice Cream's bonus 1.6485
/// shows "+65% Cold Resistance" (the old display truncated the raw bonus to +164%) and
/// Anomalous Doughnut "+6,443% Fauna Analysis Rewards" (Core.Checks --food-display).
/// The table and bit sets live in NMS.exe, not the game data, so the food stats'
/// modes are listed here.
/// </summary>
public static class FoodIngestorDisplay
{
    // Mode 1 among the stats foods boost (table field 0 = 1). The others - 61, 62, 63,
    // 78, 79, 81, 100, 104 - are mode 0; 112 (translator) is formatted as a plain number.
    private static readonly HashSet<int> Multiplier = [3, 11, 58, 80, 94, 95, 96, 97, 98, 102];
    private static readonly HashSet<int> Negated = [3, 0x15, 0x69, 0x7b, 0x7e, 0x9c, 0xb1, 0xc2, 0xc6, 0xc7];

    // Mode 0 scanner rewards, whose reference is 1: the owner's Anomalous Doughnut
    // (FOOD_DNUT_AJAM, Fauna Analysis Rewards, stat 61) has bonus 64.4329 and the
    // ingestor shows "+6,443%" = truncate(64.4329 / 1 * 100), as the mode 0 branch does
    // (cvttss2si, at least 1). Flora (62) and Mineral (63) are the same reward family;
    // that their reference is also 1 is inferred, not seen.
    private static readonly HashSet<int> RewardStats = [61, 62, 63];

    /// <summary>The whole-percent amount the game shows, or null when it depends on
    /// the player's own stats (other mode 0 stats) or isn't a percentage.</summary>
    public static int? Amount(int stat, float bonus)
    {
        if (RewardStats.Contains(stat)) return Math.Max((int)(bonus * 100f), 1);
        if (!Multiplier.Contains(stat)) return null;
        float x = (bonus - 1f) * 100f;
        int amount;
        if (x > 0) amount = (int)MathF.Ceiling(MathF.Max(x, 1f));
        else if (x < 0) amount = (int)MathF.Floor(MathF.Min(x, -1f));
        else amount = 0;
        return Negated.Contains(stat) ? -amount : amount;
    }

    /// <summary>"+65%", "-30%", or null (see Amount).</summary>
    public static string? Text(int stat, float bonus) =>
        Amount(stat, bonus) is int amount ? $"{(amount > 0 ? "+" : "")}{amount:N0}%" : null;

    /// <summary>The plain name of a food's Ingestor effect ("Cold resistance", "Mining speed").</summary>
    public static string EffectName(string effect) => effect switch
    {
        "Weapon_Scan_Discovery_Creature" => "Fauna analysis rewards", "Weapon_Scan_Discovery_Flora" => "Flora analysis rewards",
        "Weapon_Scan_Discovery_Mineral" => "Mineral analysis rewards", "Weapon_Laser_MiningBonus" => "Resources mined",
        "Weapon_Laser_Mining_Speed" => "Mining speed", "Suit_Protection" => "Hazard protection battery",
        "Suit_Armour_Shield_Strength" => "Shield strength", "Suit_Energy" => "Life support tanks",
        "Suit_Energy_Regen" => "Solar panel power",
        "Suit_Stamina_Strength" => "Sprint distance", "Suit_Stamina_Recovery" => "Sprint recovery time",
        "Suit_Jetpack_Tank" => "Jetpack tanks", "Suit_AutoTranslator" => "Words translated",
        "Suit_Protection_WaterDrain" => "Breathing efficiency", "Suit_Protection_Cold" => "Cold resistance",
        "Suit_Protection_Heat" => "Heat resistance", "Suit_Protection_Toxic" => "Toxic resistance",
        "Suit_Protection_Radiation" => "Radiation resistance", "Weapon_Scan_Recharge_Time" => "Recharge time",
        "Suit_Protection_HeatDrain" => "Heat resistance", "Suit_Protection_ColdDrain" => "Cold resistance",
        "Suit_Protection_ToxDrain" => "Toxic resistance", "Suit_Protection_RadDrain" => "Radiation resistance",
        _ => "Other Ingestor effect"
    };

    /// <summary>A food's Ingestor line: "+65% Cold resistance · 2:30 per item". Stats whose amount depends on the
    /// player's equipment show the effect alone ("Boosts shield strength") rather than a made-up number;
    /// <paramref name="baseDuration"/> is FoodIngestorTables.BaseDuration (seconds, times the food's cooking value).</summary>
    public static string Summary(FoodIngestorDefinition food, float bonus, float baseDuration)
    {
        double seconds = Math.Max(0, baseDuration * food.CookingValue);
        string effect = Text(food.Stat, bonus) is { } amount
            ? $"{amount} {EffectName(food.Effect)}" : $"Boosts {EffectName(food.Effect).ToLowerInvariant()}";
        return $"{effect} · {TimeSpan.FromSeconds(seconds):m\\:ss} per item";
    }
}
