namespace ArtifactX.Core.Recipes;

public sealed record FoodIngestorDefinition(string Id, int Stat, string Effect, float CookingValue,
    float ExplicitBonus, float Minimum, float Maximum, bool Percentage, bool LowerIsBetter);
public sealed record FoodIngestorTables(float BaseDuration, FoodIngestorDefinition[] Foods);

/// <summary>
/// Product bonus helper 0x140ed70f0..0x140ed72af, inspected 2026-11-01:
/// zero-padded 16-byte product ID, FNV-1a, folded 32-bit mix, float cooking
/// interpolation and a deterministic 1..1.1 multiplier. Keep float operations
/// separate: the native instructions do not fuse the multiply and add.
/// This is the food's nominal multiplier, not the player's final equipment stat.
/// All 586 installed food definitions matched native float bits using the
/// research oracle and Core.Checks --food-bonus-vectors on 2026-11-01.
/// </summary>
public static class FoodIngestorBonus
{
    public static float Calculate(FoodIngestorDefinition food)
    {
        if (food.ExplicitBonus > 0) return food.ExplicitBonus;
        if (food.Id.Length > 16 || food.Id.Any(c => c > 127))
            throw new ArgumentException("Product identity must fit the native 16-byte ID.");
        ulong hash = 0xcbf29ce484222325;
        unchecked
        {
            for (int i = 0; i < 16; i++)
                hash = (hash ^ (i < food.Id.Length ? (byte)food.Id[i] : 0UL)) * 0x100000001b3;
            uint low = (uint)hash;
            uint folded = ((low >> 16) | (low << 16)) ^ (uint)(hash >> 32) ^ low;
            uint random = (low == 0 ? 1U : low) * 0x5a76f899 + folded;
            float unit = (float)(random * 2.3283064370807974e-10);
            float jitter = unit * 0.10000002384185791f;
            jitter += 1f;
            float fraction = (food.CookingValue - 0.1f) / 0.9f;
            float value = (food.Maximum - food.Minimum) * fraction;
            value += food.Minimum;
            return jitter * value;
        }
    }
}
