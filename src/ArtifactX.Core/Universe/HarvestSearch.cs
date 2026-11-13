namespace ArtifactX.Core.Universe;

/// <summary>A kind of place an item grows or lies (from the object tables, catalog format 93): a biome (16 = any), the
/// rules the planet must meet and where on it (GcTerrainTileType: 4 underwater, 5 in caves, else on the ground).</summary>
/// <param name="SubTypes">The biome subtypes whose own biome file places it; null when every subtype does (an optional
/// group or a common list).</param>
public sealed record HarvestRule(int Biome, ObjectGroupRules Rules, int TileType, int Min, int Max, int[]? SubTypes = null);

/// <summary>A body where a search found the item: its packed planet address, planet number (from 1), type and what it
/// gives.</summary>
public sealed record HarvestFind(ulong PlanetAddress, int PlanetIndex, PlanetType Body, PlanetHarvest Harvest);

/// <summary>
/// Where wild plants and objects that give an item are (the Ingredient Finder): the kinds of planet from the object
/// tables alone, and the exact bodies of a system from its generated profiles. Pure, so the desktop app and the future
/// mobile app (which starts from a typed or scanned address, with no save) share it; see <see cref="NearbySystems"/>.
/// </summary>
public static class HarvestSearch
{
    /// <summary>Every kind of place the object tables put <paramref name="itemId"/>: the optional groups (where the
    /// wild cooking and biome plants are), the common lists, and each biome file's own lists. Empty before catalog
    /// format 93 or when no placed object gives it.</summary>
    public static HarvestRule[] Rules(string itemId, ObjectTables tables)
    {
        if (tables.Harvest is not { } harvest) return [];
        HarvestItem? Gives(ObjectList list) => list.Files is { Length: > 0 } files
            && files.All(f => harvest.GetValueOrDefault(f)?.Any(i => i.ItemId == itemId) == true)
            ? harvest[files[0]].First(i => i.ItemId == itemId) : null;

        var rules = new List<HarvestRule>();
        foreach (var group in tables.Groups)
            foreach (var file in group.Files)
                if (Gives(file) is { } item) rules.Add(new(group.Biome, group.Rules, file.TileType, item.Min, item.Max));
        foreach (var list in tables.Common)
            if (Gives(list) is { } item) rules.Add(new(16, ObjectGroupRules.None, list.TileType, item.Min, item.Max));
        for (int b = 0; b < tables.BiomeFiles.Length; b++)
        {
            var subs = new List<int>();
            HarvestItem? found = null; int tile = 0;
            for (int s = 0; s < tables.BiomeFiles[b].Length; s++)
                if (tables.BiomeFiles[b][s] is { } file)
                    foreach (var list in file.Lists)
                        if (Gives(list) is { } item) { subs.Add(s); found ??= item; tile = list.TileType; break; }
            if (found is not null) rules.Add(new(b, ObjectGroupRules.None, tile, found.Min, found.Max, subs.ToArray()));
        }
        // One rule per place; an item listed twice for the same place keeps its widest amounts.
        return rules.GroupBy(r => (r.Biome, r.Rules, r.TileType, Subs: string.Join(",", r.SubTypes ?? [])))
            .Select(g => g.First() with { Min = g.Min(r => r.Min), Max = g.Max(r => r.Max) }).ToArray();
    }

    /// <summary>The bodies of <paramref name="systemAddress"/> whose placed objects give <paramref name="itemId"/>
    /// (PlanetProfile.Harvest), in planet order. About 0.7 ms per system (creature prediction included).</summary>
    public static IEnumerable<HarvestFind> InSystem(ulong systemAddress, string itemId, BiomeTables biomes, PlanetProfileTables tables)
    {
        if (tables.Objects?.Harvest is null) yield break;
        var (portal, galaxy) = SystemProfiles.PortalOf(systemAddress);
        PlanetType[] types;
        try { types = PlanetGenerator.Generate(portal, galaxy, biomes); }
        catch { yield break; }
        foreach (var find in On(systemAddress, types, SystemProfiles.Generate(systemAddress, biomes, tables), itemId))
            yield return find;
    }

    /// <summary>The same from a system's bodies and profiles already generated (so one pass can also look for
    /// creature produce, <see cref="CreatureProduceSearch.On"/>).</summary>
    public static IEnumerable<HarvestFind> On(ulong systemAddress, PlanetType[] types, PlanetProfile?[] profiles, string itemId)
    {
        ulong system = systemAddress & ~(0xFUL << 52);
        for (int i = 0; i < profiles.Length && i < types.Length; i++)
            if (profiles[i]?.Harvest?.FirstOrDefault(h => h.ItemId == itemId) is { } harvest)
                yield return new HarvestFind(system | ((ulong)(i + 1) << 52), i + 1, types[i], harvest);
    }
}
