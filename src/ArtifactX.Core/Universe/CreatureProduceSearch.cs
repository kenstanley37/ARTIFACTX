namespace ArtifactX.Core.Universe;

/// <summary>A body whose predicted creatures give an item: where (packed planet address, planet number from 1), the
/// body, the creatures that produce it when fed and the ones that drop it when hunted, in prediction order.</summary>
public sealed record ProduceFind(ulong PlanetAddress, int PlanetIndex, PlanetType Body, PredictedCreature[] Fed, PredictedCreature[] Hunted);

/// <summary>
/// Where to find creatures for an item (Fresh Milk, Crab 'Apple', Creature Egg from feeding; Raw Steak, Scaly Meat
/// from hunting): the Ingredient Finder's creature half beside <see cref="HarvestSearch"/>, from the predicted fauna.
/// Feeding uses <see cref="CreatureTables.ProduceOf"/> (GcCreatureGlobals.HarvestingProducts, else Wild Milk, the
/// analysis visor's "Produces"); hunting uses GcCreatureGlobals.KillingProducts by the same creature id (catalog
/// CREATUREPRODUCTS, format 56), with no default for kinds it doesn't list. Needs only a starting system, so the
/// mobile app can run it too.
/// </summary>
public static class CreatureProduceSearch
{
    /// <summary>What a creature without its own HarvestingProducts entry produces (NMS.exe 0x1412431d0's default).</summary>
    public const string DefaultProduce = "FOOD_V_MILK";

    /// <summary>Whether some creature produces <paramref name="itemId"/> when fed (false before catalog format 80).</summary>
    public static bool IsProduce(string itemId, CreatureTables? tables) =>
        tables?.Produce is { } produce
        && (produce.Values.Contains(itemId, StringComparer.OrdinalIgnoreCase) || string.Equals(itemId, DefaultProduce, StringComparison.OrdinalIgnoreCase));

    /// <summary>The creature ids whose kill drops <paramref name="itemId"/>, from KillingProducts' (item, creature id)
    /// pairs.</summary>
    public static HashSet<string> HuntIds(string itemId, IEnumerable<(string ItemId, string CreatureId)> killing) =>
        killing.Where(k => string.Equals(k.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            .Select(k => k.CreatureId).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The bodies of <paramref name="systemAddress"/> with creatures that give <paramref name="itemId"/>, from
    /// the system's bodies and profiles already generated. <paramref name="fed"/> false skips feeding; an empty
    /// <paramref name="huntIds"/> skips hunting.</summary>
    public static IEnumerable<ProduceFind> On(ulong systemAddress, PlanetType[] types, PlanetProfile?[] profiles, string itemId,
        CreatureTables tables, bool fed, IReadOnlySet<string> huntIds)
    {
        ulong system = systemAddress & ~(0xFUL << 52);
        for (int i = 0; i < profiles.Length && i < types.Length; i++)
        {
            if (profiles[i]?.PredictedCreatures is not { Length: > 0 } predicted) continue;
            var feed = fed ? predicted.Where(c => string.Equals(tables.ProduceOf(c), itemId, StringComparison.OrdinalIgnoreCase)).ToArray() : [];
            var hunt = huntIds.Count > 0 ? predicted.Where(c => huntIds.Contains(KindOf(c))).ToArray() : [];
            if (feed.Length + hunt.Length > 0) yield return new ProduceFind(system | ((ulong)(i + 1) << 52), i + 1, types[i], feed, hunt);
        }
    }

    /// <summary>The id the creature globals' product lists match: the spawned species' id, else the creature's own.</summary>
    public static string KindOf(PredictedCreature creature) =>
        string.IsNullOrEmpty(creature.SpeciesId) ? creature.Id : creature.SpeciesId;
}
