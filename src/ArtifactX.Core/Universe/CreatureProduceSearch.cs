namespace ArtifactX.Core.Universe;

/// <summary>A body whose predicted creatures produce an item when fed: where (packed planet address, planet number
/// from 1), the body and the creatures, in prediction order.</summary>
public sealed record ProduceFind(ulong PlanetAddress, int PlanetIndex, PlanetType Body, PredictedCreature[] Creatures);

/// <summary>
/// Where to feed creatures for an item (Fresh Milk, Crab 'Apple', Creature Egg...): the Ingredient Finder's creature
/// half beside <see cref="HarvestSearch"/>, from the predicted fauna and what each kind produces
/// (<see cref="CreatureTables.ProduceOf"/>: GcCreatureGlobals.HarvestingProducts by creature id, else Wild Milk, the
/// analysis visor's "Produces"). Needs only a starting system, so the mobile app can run it too.
/// </summary>
public static class CreatureProduceSearch
{
    /// <summary>Whether some creature produces <paramref name="itemId"/> (false before catalog format 80).</summary>
    public static bool IsProduce(string itemId, CreatureTables? tables) =>
        tables?.Produce is { } produce
        && (produce.Values.Contains(itemId, StringComparer.OrdinalIgnoreCase) || string.Equals(itemId, DefaultProduce, StringComparison.OrdinalIgnoreCase));

    /// <summary>What a creature without its own HarvestingProducts entry produces (NMS.exe 0x1412431d0's default).</summary>
    public const string DefaultProduce = "FOOD_V_MILK";

    /// <summary>The bodies of <paramref name="systemAddress"/> with creatures that produce <paramref name="itemId"/>,
    /// from the system's bodies and profiles already generated.</summary>
    public static IEnumerable<ProduceFind> On(ulong systemAddress, PlanetType[] types, PlanetProfile?[] profiles, string itemId, CreatureTables tables)
    {
        ulong system = systemAddress & ~(0xFUL << 52);
        for (int i = 0; i < profiles.Length && i < types.Length; i++)
        {
            if (profiles[i]?.PredictedCreatures is not { Length: > 0 } predicted) continue;
            var creatures = predicted.Where(c => string.Equals(tables.ProduceOf(c), itemId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (creatures.Length > 0) yield return new ProduceFind(system | ((ulong)(i + 1) << 52), i + 1, types[i], creatures);
        }
    }
}
