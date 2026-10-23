namespace ArtifactX.Core.Universe;

/// <summary>Global modifier lists for creature generation from GcCreatureGenerationData.</summary>
public sealed record CreatureGlobals(
    float[] SandwormChance,
    float[] AirGroups,
    float[] CaveGroups,
    float[] DensityMods,
    float[] GroundGroups,
    float[] LifeChance,
    float[] LifeDensity,
    float[] RarityFreq,
    float[] RoleFreq,
    float[] WaterGroups,
    float HerdPenalty);

/// <summary>An individual creature definition from GcCreatureDataTable (filtered by GcCreatureFilenameTable).</summary>
public sealed record CreatureEntry(
    string Id,
    int Type,
    bool Eco,
    bool ForcedOnly,
    int HerbMod,
    int PredMod,
    int Rarity,
    string[] Tags);

/// <summary>An archetype option entry with an ID and a weight.</summary>
public sealed record CreatureWeightedEntry(string Id, float Weight);

/// <summary>Archetype weighted lists across the 4 domains (Air, Cave, Ground, Water).</summary>
public sealed record CreatureDomainWeightedLists(
    CreatureWeightedEntry[] Air,
    CreatureWeightedEntry[] Cave,
    CreatureWeightedEntry[] Ground,
    CreatureWeightedEntry[] Water);

/// <summary>An optional archetype list with a probability and domain choices.</summary>
public sealed record CreatureOptWeightedList(float Prob, CreatureDomainWeightedLists Domains);

/// <summary>The collection of archetype lists from GcCreatureGenerationData.</summary>
public sealed record CreatureArchetypeLists(
    CreatureOptWeightedList[] SubBiomes,
    CreatureOptWeightedList[] Biomes,
    CreatureOptWeightedList Abandoned,
    CreatureOptWeightedList Empty,
    CreatureOptWeightedList Purple,
    CreatureDomainWeightedLists Generic,
    CreatureWeightedEntry[] AirEmpty);

/// <summary>A table reference within a domain archetype.</summary>
public sealed record CreatureDomainTableEntry(string Table, int DensityMod);

/// <summary>Additional table options within a domain archetype.</summary>
public sealed record CreatureDomainAdditional(
    CreatureDomainTableEntry[] Tables,
    int Count,
    float ChanceHemi,
    int MaxHemi);

/// <summary>A domain archetype from GcCreatureGenerationArchetypes.</summary>
public sealed record CreatureDomainArchetype(
    float ChanceHemi,
    int MaxHemi,
    CreatureDomainTableEntry[] Tables,
    CreatureDomainAdditional[] Additional);

/// <summary>A creature role candidate within an ecosystem role table (GcCreatureRoleDescriptionTable).</summary>
public sealed record CreatureRoleCandidate(
    string ForceId,
    string ReqTag,
    int Density,
    int ForceType,
    float Prob,
    int Role);

/// <summary>All creature generation data extracted from the game files.</summary>
public sealed record CreatureTables(
    CreatureGlobals Globals,
    CreatureEntry[] Creatures,
    CreatureArchetypeLists ArchetypeLists,
    Dictionary<string, Dictionary<string, CreatureDomainArchetype>> DomainArchetypes,
    Dictionary<string, CreatureRoleCandidate[]> EcosystemTables);

public static partial class PlanetProfileGenerator
{
    private static readonly string[] DomainNames = ["ground", "air", "water", "cave"];

    private sealed class RngStack
    {
        private readonly Rng[] _stack = new Rng[8];
        private int _count;

        public RngStack(ulong rootSeed)
        {
            var root = Rng.Start(rootSeed);
            ulong lo = root.Next(), hi = root.Next();
            ulong initSeed = Mix64((hi << 32) | lo);
            _stack[0] = Rng.Start(initSeed);
            _count = 1;
        }

        public ref Rng Top => ref _stack[_count - 1];

        public void Push()
        {
            ref var top = ref Top;
            ulong lo = top.Next(), hi = top.Next();
            ulong childSeed = Mix64((hi << 32) | lo);
            _count++;
            _stack[_count - 1] = Rng.Start(childSeed);
        }

        public void Pop()
        {
            ref var top = ref Top;
            top.Next();
            top.Next();
            _count--;
        }

        public uint Next() => Top.Next();
        public float NextFraction() => Top.NextFraction();
    }

    private static string? PickWeighted(RngStack stack, CreatureWeightedEntry[]? entries)
    {
        if (entries is null || entries.Length == 0) return null;
        stack.Push();
        float totalWeight = 0f;
        for (int i = 0; i < entries.Length; i++) totalWeight += entries[i].Weight;
        float roll = stack.NextFraction() * totalWeight;
        string? picked = null;
        for (int i = 0; i < entries.Length; i++)
        {
            roll -= entries[i].Weight;
            if (roll <= 0f)
            {
                picked = entries[i].Id;
                break;
            }
        }
        stack.Pop();
        return picked;
    }

    private static void SelectArchetype(RngStack stack, CreatureOptWeightedList optList,
        ref string? ground, ref string? water, ref string? air, ref string? cave,
        int biome, float sea, bool hasCaves, bool forceEmpty = true)
    {
        stack.Push();
        float prob = optList.Prob;
        float draw = stack.NextFraction();
        if (prob < draw)
        {
            stack.Pop();
            return;
        }
        if (biome != 14)
        {
            string? g = PickWeighted(stack, optList.Domains.Ground);
            if (g is not null) ground = g;
        }
        if (sea >= 120.0f)
        {
            string? w = PickWeighted(stack, optList.Domains.Water);
            if (w is not null) water = w;
        }
        string? a = PickWeighted(stack, optList.Domains.Air);
        if (a is not null) air = a;
        if (hasCaves)
        {
            string? c = PickWeighted(stack, optList.Domains.Cave);
            if (c is not null) cave = c;
        }
        if (forceEmpty)
        {
            ground ??= "EMPTY";
            water ??= "EMPTY";
            air ??= "EMPTY";
            cave ??= "EMPTY";
        }
        stack.Pop();
    }

    /// <summary>
    /// Predicts the planet's fauna total (number of creature roles) from its seed and environment.
    /// Reverse-engineered from NMS.exe 0x14169f4a0 (child seed 10).
    /// </summary>
    public static int GenerateFaunaCount(ulong planetSeed, int biome, int subType, int planetClass,
        int star, int faunaLevel, float sea, bool hasCaves, bool emptySystem, bool abandonedSystem,
        CreatureTables tables)
    {
        if (faunaLevel <= 0) return 0;
        var seeds = ChildSeeds(planetSeed);
        var stack = new RngStack(seeds[10]);

        // 1. Sandworm and Infested
        stack.Push();
        float f1 = stack.NextFraction();
        bool sandworm = biome >= 0 && biome < tables.Globals.SandwormChance.Length
            && f1 < tables.Globals.SandwormChance[biome] && faunaLevel > 0;
        float f2 = stack.NextFraction();
        if (subType == 0x18) sandworm = true;
        _ = sandworm;
        stack.Pop();

        // 2. Archetypes
        stack.Push();
        string? ground = null, water = null, air = null, cave = null;

        if (planetClass == 1)
        {
            stack.Push();
            ground = "DEFAULT";
            water = sea >= 120.0f ? "DEFAULT" : "EMPTY";
            air = "DEFAULT";
            cave = hasCaves ? "DEFAULT" : "EMPTY";
            stack.Pop();
        }
        else
        {
            if (emptySystem)
            {
                SelectArchetype(stack, tables.ArchetypeLists.Empty, ref ground, ref water, ref air, ref cave, biome, sea, hasCaves);
            }
            else if (star == 4)
            {
                SelectArchetype(stack, tables.ArchetypeLists.Purple, ref ground, ref water, ref air, ref cave, biome, sea, hasCaves, forceEmpty: false);
            }
            else if (abandonedSystem)
            {
                SelectArchetype(stack, tables.ArchetypeLists.Abandoned, ref ground, ref water, ref air, ref cave, biome, sea, hasCaves);
            }

            if (ground is null && biome == 7)
            {
                if (subType >= 0 && subType < tables.ArchetypeLists.SubBiomes.Length)
                {
                    SelectArchetype(stack, tables.ArchetypeLists.SubBiomes[subType], ref ground, ref water, ref air, ref cave, biome, sea, hasCaves);
                }
            }

            if (ground is null && biome == 14)
            {
                if (14 < tables.ArchetypeLists.Biomes.Length)
                {
                    SelectArchetype(stack, tables.ArchetypeLists.Biomes[14], ref ground, ref water, ref air, ref cave, biome, sea, hasCaves);
                }
            }
        }

        // 3. Shuffle
        stack.Push();
        uint r8 = stack.Top.Next();
        uint burnCount = (r8 % 11) + 3;
        for (uint i = 0; i < burnCount; i++) stack.Top.Next();
        stack.Pop();

        // 4. Fallbacks
        stack.Push();
        if (biome == 14)
        {
            ground = "EMPTY";
            cave = "EMPTY";
        }

        if (ground is null)
            ground = PickWeighted(stack, tables.ArchetypeLists.Generic.Ground);
        if (air is null)
        {
            if (ground is null || ground == "EMPTY")
                air = PickWeighted(stack, tables.ArchetypeLists.AirEmpty);
            else
                air = PickWeighted(stack, tables.ArchetypeLists.Generic.Air);
        }
        if (water is null && sea >= 120.0f)
            water = PickWeighted(stack, tables.ArchetypeLists.Generic.Water);
        if (cave is null && hasCaves)
            cave = PickWeighted(stack, tables.ArchetypeLists.Generic.Cave);
        stack.Pop();
        stack.Pop();

        // 5. Tables
        var collectedTables = new List<CreatureDomainTableEntry>();
        stack.Push();
        string?[] chosen = [ground, air, water, cave];

        for (int d = 0; d < 4; d++)
        {
            string? aid = chosen[d];
            if (aid is not null
                && tables.DomainArchetypes.TryGetValue(DomainNames[d], out var darchs)
                && darchs.TryGetValue(aid, out var arch))
            {
                stack.Push();
                stack.Push();
                if (arch.ChanceHemi > stack.NextFraction())
                {
                    for (int m = 0; m < arch.MaxHemi; m++)
                    {
                        _ = (uint)(((ulong)stack.Next() * (ulong)arch.Tables.Length) >> 32);
                    }
                }
                stack.Pop();
                for (int t = 0; t < arch.Tables.Length; t++)
                {
                    if (!string.IsNullOrEmpty(arch.Tables[t].Table))
                        collectedTables.Add(arch.Tables[t]);
                }
                stack.Pop();
            }
        }

        stack.Push();
        for (int d = 0; d < 4; d++)
        {
            string? aid = chosen[d];
            if (aid is not null
                && tables.DomainArchetypes.TryGetValue(DomainNames[d], out var darchs)
                && darchs.TryGetValue(aid, out var arch))
            {
                stack.Push();
                for (int a = 0; a < arch.Additional.Length; a++)
                {
                    var ae = arch.Additional[a];
                    stack.Push();
                    if (ae.ChanceHemi > stack.NextFraction())
                    {
                        for (int m = 0; m < ae.MaxHemi; m++)
                        {
                            _ = (uint)(((ulong)stack.Next() * (ulong)ae.Tables.Length) >> 32);
                        }
                    }
                    stack.Pop();

                    int countToPick = ae.Count;
                    var avail = new List<CreatureDomainTableEntry>(ae.Tables);
                    if (countToPick > avail.Count) countToPick = avail.Count;
                    for (int p = 0; p < countToPick; p++)
                    {
                        if (avail.Count == 0) break;
                        stack.Push();
                        int idx = (int)(((ulong)stack.Next() * (ulong)avail.Count) >> 32);
                        stack.Pop();
                        var te = avail[idx];
                        avail.RemoveAt(idx);
                        if (!string.IsNullOrEmpty(te.Table))
                            collectedTables.Add(te);
                    }
                }
                stack.Pop();
            }
        }
        stack.Pop();
        stack.Pop();

        // 6. Roles
        int chosenRoleCount = 0;
        for (int i = 0; i < collectedTables.Count; i++)
        {
            var te = collectedTables[i];
            if (!tables.EcosystemTables.TryGetValue(te.Table, out var roles)) continue;
            stack.Push();
            stack.Push();
            stack.Pop();
            for (int r = 0; r < roles.Length; r++)
            {
                var role = roles[r];
                stack.Push();
                stack.Push();
                stack.Pop();
                stack.Push();
                stack.Pop();
                float draw = stack.NextFraction();
                if (draw <= role.Prob && chosenRoleCount < 22)
                {
                    float totalWeight = 0.0f;
                    for (int cIdx = 0; cIdx < tables.Creatures.Length; cIdx++)
                    {
                        var c = tables.Creatures[cIdx];
                        if (c.Type != role.ForceType) continue;
                        if (!c.Eco) continue;
                        if (c.ForcedOnly && role.ForceId != c.Id) continue;
                        if (!string.IsNullOrEmpty(role.ForceId) && role.ForceId != c.Id) continue;
                        if (!string.IsNullOrEmpty(role.ReqTag) && !ContainsTag(c.Tags, role.ReqTag)) continue;

                        float roleMod;
                        if (role.ForceId == c.Id) roleMod = 1.0f;
                        else if (role.Role is 1 or 2) roleMod = tables.Globals.RoleFreq[c.PredMod];
                        else if (role.Role is 3 or 4) roleMod = tables.Globals.RoleFreq[c.HerbMod];
                        else roleMod = 1.0f;

                        float rarityMod = tables.Globals.RarityFreq[c.Rarity];
                        if (string.IsNullOrEmpty(role.ForceId) && string.IsNullOrEmpty(role.ReqTag))
                        {
                            for (int t = 0; t < c.Tags.Length; t++)
                                rarityMod *= tables.Globals.HerdPenalty;
                        }

                        totalWeight += rarityMod * roleMod;
                    }

                    if (totalWeight > 0.0f)
                    {
                        _ = stack.NextFraction() * totalWeight;
                        stack.Push();
                        _ = stack.NextFraction();
                        stack.Pop();
                        chosenRoleCount++;
                    }
                }
                stack.Pop();
            }
            stack.Pop();
        }
        return chosenRoleCount;
    }

    private static bool ContainsTag(string[] tags, string reqTag)
    {
        for (int i = 0; i < tags.Length; i++)
            if (string.Equals(tags[i], reqTag, StringComparison.Ordinal)) return true;
        return false;
    }
}

