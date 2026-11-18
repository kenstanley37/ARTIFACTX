using ArtifactX.Core.Universe;

namespace ArtifactX.Maui.Services;

/// <summary>An item the finder can locate: its id and display name.</summary>
public sealed record FindableItem(string Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// The phone's Ingredient Finder: the systems nearest a start whose planets give a wild ingredient, creature produce
/// or fish - Core IngredientSearch, the desktop's search, on the rules file. Without the desktop's creature names
/// and in-region star positions, creatures read "feed its creatures" and nearby systems "This region".
/// </summary>
public static class IngredientFinderService
{
    private static Task<IReadOnlyList<FindableItem>>? _items;

    /// <summary>Every item a planet gives (a plant or deposit, creature produce or loot, a fish), by name; empty
    /// without the rules file.</summary>
    public static Task<IReadOnlyList<FindableItem>> ItemsAsync() => _items ??= Task.Run(async () =>
    {
        if (await GameRulesService.GetAsync() is not { Profiles: { } tables } rules) return (IReadOnlyList<FindableItem>)[];
        var hunted = (rules.CreatureSources ?? []).Where(s => s.Method == "Hunt").Select(s => s.ItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rules.ItemNames
            .Where(n => rules.Fish.ContainsKey(n.Key) || hunted.Contains(n.Key) || CreatureProduceSearch.IsProduce(n.Key, tables.Creatures)
                || (tables.Objects is { } objects && HarvestSearch.Rules(n.Key, objects).Length > 0))
            .Select(n => new FindableItem(n.Key, n.Value))
            .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    });

    /// <summary>The systems nearest <paramref name="start"/> that give <paramref name="itemId"/>, nearest first;
    /// null without the rules file.</summary>
    public static async Task<IReadOnlyList<IngredientPlace>?> FindAsync(ulong start, string itemId, CancellationToken cancel)
    {
        if (await GameRulesService.GetAsync() is not { GalaxySettings: { } settings, Biomes: { } biomes, Profiles: { } tables } rules)
            return null;
        var hunt = CreatureProduceSearch.HuntIds(itemId,
            (rules.CreatureSources ?? []).Where(s => s.Method == "Hunt").Select(s => (s.ItemId, s.CreatureId)));
        return await Task.Run(() => IngredientSearch.Find(start, itemId, settings, biomes, tables,
            rules.Fish.GetValueOrDefault(itemId), hunt, null, null, cancel), cancel);
    }
}
