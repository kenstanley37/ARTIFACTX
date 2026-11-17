namespace ArtifactX.Core.Universe;

/// <summary>One predicted procedural creature for a planet. Seed is the creature's GcCreatureRoleData.Seed (NMS.exe
/// 184271 0x1416d60e0: the mixed seed of the next two draws of its role's stream, right after the creature is picked).
/// CreatureType (GcCreatureTypes) and Diet (GcCreatureDiet) are the role data's Type and Diet, set in the same routine:
/// Type is the creature's RealType, else its ForceType, else role + 22 (Predator, PlayerPredator, Prey, Passive);
/// Diet is Robot on ROBOT worlds, Carnivore for role 2, a roll for roles 1 and 7 (Omnivore above 0.5, else
/// Carnivore), Herbivore otherwise. CreatureType feeds the genus seed (SpeciesNames.FaunaSeeds); Diet is kept for
/// the creature's traits. MinSize/MaxSize are the role description's size-class range (GcCreatureSizeClasses).
/// Scale is the species' spawn scale and SizeClass its GcCreatureSizeClasses bucket (Small 0 to Huge 3), which
/// feeds the genus seed; both 0 on catalogs before format 79 (see <see cref="PlanetProfileGenerator.SpeciesScale"/>).
/// SpeciesId is the creature ID its species seed is made from: the scene's own creature component Id (CRAB spawns
/// SPIDER's scene data), else Id. HemiSphere is the role data's GcCreatureHemiSphere (0 Any, 1 Northern, 2 Southern):
/// the discovery list's "Found in the North / South" (see <see cref="PlanetProfileGenerator.HemiSphereFor"/>).</summary>
public sealed record PredictedCreature(string Id, string Domain, int Role, ulong Seed = 0, int CreatureType = 0, int Diet = 0,
    int MinSize = 0, int MaxSize = 0, float Scale = 0, int SizeClass = 0, string SpeciesId = "", PredictedCreatureInfo? Info = null,
    int Density = 0, float GroupsPerKm = 0, int ActiveTime = 2, int HemiSphere = 0, int TileType = -1, string Filter = "")
{
    /// <summary>The terrain the habitat word is read from (NMS.exe 184271 0x14077b2e0): Air (0) for creatures of type
    /// Bird, FlyingLizard, FlyingSnake, Butterfly (1-4) or ProtoFlyer (48), wherever they spawn; else the role's
    /// TileType, its table's (GcCreatureRoleDescriptionTable +0x1C, copied in 0x1416d60e0); null when the game data
    /// predates TileType (catalog format 85).</summary>
    public int? HabitatTile => CreatureType is >= 1 and <= 4 or 48 ? 0 : TileType >= 0 ? TileType : null;

    /// <summary>The discovery list's habitat word (NMS.exe 184271 0x14077b2e0): the role's terrain, except that
    /// creatures of type Bird, FlyingLizard, FlyingSnake, Butterfly (1-4) or ProtoFlyer (48) always read "Flying",
    /// even from a ground table (owner's Unst Prime list, 2026-11-12: large butterflies and a small bird from the
    /// ground tables show "Flying").</summary>
    public string Habitat => HabitatTile switch
    {
        0 => "Flying",
        3 => "Mountainous",
        4 => "Underwater",
        5 => "Underground",
        >= 1 => "Ground",
        _ => DomainHabitat,
    };

    /// <summary>The habitat by domain, for game data without TileType.</summary>
    private string DomainHabitat => Domain switch
    {
        "air" => "Flying",
        "water" => "Underwater",
        "cave" => "Underground",
        _ => "Ground"
    };

    public string DefaultDisplayName => CreatureKindNames.Name(Id);
}

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

/// <summary>An individual creature definition from GcCreatureDataTable (filtered by GcCreatureFilenameTable).
/// Type is its ForceType (what roles filter on); RealType (catalog format 78; 0 on older catalogs) is the type the
/// spawned creature reports. MinScale/MaxScale (format 79) bound its spawn scale. TagRarities (format 79) is each
/// tag's RarityOverride, the rarity a role requiring that tag weighs it by. SpeciesId (format 79; "" when unknown) is
/// the Id in its scene's creature component, which names the species (the CRAB scene says SPIDER). Scene (format 80)
/// is its model, GcCreatureFilenameTable's Filename. ForcedAffinity (format 82) is its pet-battle element when set
/// (GcPetBattlerAffinity, 1-8; 0 = by biome). PetBattler (format 83; null on older catalogs) is its
/// CanBeUsedInPetBattler: whether players can adopt it as a battle pet (46 of 86 kinds in build 184271; no fish, birds,
/// diggers, worms or deep-sea creatures). ModelScene (format 89; "" when none) is GcCreatureFilenameTable's
/// ExtraFilename: the model actually drawn when Scene is a flock or a shared rig (fish flock -> small fish, the
/// anomalous crystal -> WEIRDRIGGROUND, whose bodies are Beam / BoneSpire / Hexagon / Pillar / ShardSingle).</summary>
public sealed record CreatureEntry(
    string Id,
    int Type,
    bool Eco,
    bool ForcedOnly,
    int HerbMod,
    int PredMod,
    int Rarity,
    string[] Tags,
    int RealType = 0,
    float MinScale = 0,
    float MaxScale = 0,
    int[]? TagRarities = null,
    string SpeciesId = "",
    string Scene = "",
    int ForcedAffinity = 0,
    bool? PetBattler = null,
    string ModelScene = "");

/// <summary>An archetype option entry with an ID and a weight.</summary>
public sealed record CreatureWeightedEntry(string Id, float Weight);

/// <summary>Archetype weighted lists across the 4 domains (Air, Cave, Ground, Water).</summary>
public sealed record CreatureDomainWeightedLists(
    CreatureWeightedEntry[] Air,
    CreatureWeightedEntry[] Cave,
    CreatureWeightedEntry[] Ground,
    CreatureWeightedEntry[] Water);

/// <summary>An optional archetype list with a probability and domain choices (GcCreatureGenerationOptionalWeightedList).
/// OverrideAllDomains: when the list is used, every domain it didn't set becomes EMPTY. Read per list since catalog
/// format 78; null on older catalogs, which keep the port's old assumption (on for every list but the purple-star one,
/// the same values the game files have).</summary>
public sealed record CreatureOptWeightedList(float Prob, CreatureDomainWeightedLists Domains, bool? OverrideAllDomains = null);

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

/// <summary>A creature role candidate within an ecosystem role table (GcCreatureRoleDescriptionTable). MinSize and
/// MaxSize (GcCreatureSizeClasses, catalog format 79; 0 on older catalogs) bound the creature's size class.
/// ActiveTime (GcCreatureActiveTime, format 80; AnyTime on older catalogs) is when it's about. TileType (format 85;
/// -1 on older catalogs) is its table's GcTerrainTileType, which the role data copies and the habitat word reads.
/// Filter (format 88) is the role's descriptor filter ("FLOAT", "ROLL", "CRYSTAL" on the anomalous tables): the body
/// its model must take.</summary>
public sealed record CreatureRoleCandidate(
    string ForceId,
    string ReqTag,
    int Density,
    int ForceType,
    float Prob,
    int Role,
    int MinSize = 0,
    int MaxSize = 0,
    int ActiveTime = 2,
    int TileType = -1,
    string Filter = "");

/// <summary>All creature generation data extracted from the game files. HemiSphereWords (catalog format 84; null before)
/// are UI_CRE_HEMISPHERE_NORTH / _SOUTH ("Found in the North", "Found in the South"); TerrainWords (format 85) the
/// habitat words UI_CRE_TERRAIN_* in GcTerrainTileType order (Air "Flying", Base "Ground" ... Cave "Underground").</summary>
public sealed record CreatureTables(
    CreatureGlobals Globals,
    CreatureEntry[] Creatures,
    CreatureArchetypeLists ArchetypeLists,
    Dictionary<string, Dictionary<string, CreatureDomainArchetype>> DomainArchetypes,
    Dictionary<string, CreatureRoleCandidate[]> EcosystemTables,
    CreatureSizeGlobals? Sizes = null,
    string[]? GenderWords = null,
    string[]? ActivityWords = null,
    string[]? RarityWords = null,
    string[]? RobotGenderWords = null,
    CreatureTextWords? Text = null,
    Dictionary<string, string>? Produce = null,
    Dictionary<string, string>? ProduceNames = null,
    PetBattleTables? PetBattle = null,
    string[]? HemiSphereWords = null,
    string[]? TerrainWords = null,
    string[]? BiomeSubTypeNames = null)
{
    /// <summary>
    /// What narrows a creature's parts before its seed picks (issue #15): its rig filter name
    /// (<see cref="PartFilterName"/>; <see cref="ExcludedParts"/> turns it into parts left out) and, on an exotic
    /// (Weird, biome 7) planet, the planet's sub-type - the anomalous bodies are named after the exotic sub-biomes
    /// (GcBiomeSubType: FractCube, Bubble, Shards, WireCell, HydroGarden floaters; Structure, Contour, Beam rollers;
    /// names from catalog format 88), which the sub-type forces (<see cref="ForcedOptions"/>). Owner's B. Bloatlexia on
    /// the HydroGarden planet Azzaviu XII (2026-11-12) is the glowing-orb floater, _FLOAT_HYDROGARDENXOR, where the
    /// seed alone picked _FLOAT_SHARDS.
    /// </summary>
    public (string Filter, string SubType) ForcedPartTerms(PredictedCreature creature, int biome, int subType) =>
        (PartFilterName(creature), biome == 7 && BiomeSubTypeNames is { } names && subType >= 0 && subType < names.Length ? names[subType] : "");

    /// <summary>
    /// The name a creature's rig filter entry is looked up by (NMS.exe 184271 FUN_1416d5080): the role's Filter when it
    /// has one (the anomalous rig's FLOAT / ROLL / CRYSTAL), else by role - Predator and PlayerPredator (1, 2)
    /// "PREDATOR", Prey and Passive (3, 4) "HERBIBVORE" (FUN_1416d4f90; the game's own spelling, so no HERBIVORE entry
    /// ever matches), "" for the rest.
    /// </summary>
    public static string PartFilterName(PredictedCreature creature) =>
        creature.Filter.Length > 0 ? creature.Filter
        : creature.Role is 1 or 2 ? "PREDATOR"
        : creature.Role is 3 or 4 ? "HERBIBVORE" : "";

    /// <summary>
    /// The parts a creature can't have: the FilteredResources of the first entry of its rig's filter file
    /// (&lt;scene&gt;.FILTER.MBIN, TkResourceFilterList) whose name contains <paramref name="filterName"/> (NMS.exe
    /// 184271 FUN_142ddea90; ShipParts leaves them out). Evidence: the running game's chosen-part lists (2026-11-13) -
    /// a predator strider (role seed 8C510BE47B88D477, strider.filter.mbin PREDATOR = _HEAD_FISH) has the flat head
    /// where the seed alone picks the fish head; every other species on its planet matched without a filter; the
    /// owner's herbivore V. Snakwercus (5B0A6E3C129C03D5) keeps its fish head and rare crest. A weird-rig FLOAT filter
    /// leaves only _WEIRD_FLOAT in its group, still with a draw - as the owner's R. Gentdiffoe showed (2026-11-12).
    /// </summary>
    public static IReadOnlyList<string> ExcludedParts(IEnumerable<(string Name, IReadOnlyList<string> Parts)>? filters, string filterName)
    {
        if (filters is null || filterName.Length == 0) return Array.Empty<string>();
        foreach (var (name, parts) in filters)
            if (name.Contains(filterName, StringComparison.Ordinal)) return parts;
        return Array.Empty<string>();
    }

    /// <summary>The option ids an exotic planet's sub-type forces in a part tree, matched on the options' names (ids
    /// are cut to 15 characters: "_CRYSTAL_BONESP" is named "_Crystal_BoneSpire"): a name containing the sub-type name
    /// ("_Float_HydroGardenxOR", "_Roll_BeamStone", "_Crystal_BoneSpire"). The role's filter excludes instead
    /// (<see cref="ExcludedParts"/>).</summary>
    public static IReadOnlyList<string> ForcedOptions(IEnumerable<(string Id, string Name)> options, string subType)
    {
        var forced = new List<string>();
        foreach (var (id, name) in options)
            if ((subType.Length > 0 && name.Contains(subType, StringComparison.OrdinalIgnoreCase))
                // "Shards" names the crystal body "_Crystal_ShardSingle".
                || (subType.Length > 3 && subType.EndsWith('s') && name.Contains(subType[..^1], StringComparison.OrdinalIgnoreCase)))
                forced.Add(id);
        return forced;
    }

    /// <summary>The discovery list's habitat word ("Flying", "Underground"), from the game's words when the game data
    /// has them (format 85), else the English fallback (<see cref="PredictedCreature.Habitat"/>).</summary>
    public string HabitatOf(PredictedCreature creature) =>
        TerrainWords is { } words && creature.HabitatTile is int tile && tile >= 0 && tile < words.Length && words[tile].Length > 0
            ? words[tile] : creature.Habitat;

    /// <summary>The item a kind of creature produces when fed (the analysis visor's "Produces"): its HarvestingProducts
    /// entry by the spawned creature's id, else Wild Milk (FOOD_V_MILK, NMS.exe 0x1412431d0's default); null on game
    /// data before format 80.</summary>
    public string? ProduceOf(PredictedCreature creature) => Produce is null ? null
        : Produce.TryGetValue(string.IsNullOrEmpty(creature.SpeciesId) ? creature.Id : creature.SpeciesId, out var item) ? item
        : Produce.TryGetValue(creature.Id, out item) ? item : "FOOD_V_MILK";

    /// <summary>Whether players can adopt this kind of creature as a battle pet (the spawned creature data's
    /// CanBeUsedInPetBattler, by species id then kind); null when the game data predates format 83.</summary>
    public bool? CanBattle(PredictedCreature creature)
    {
        string id = string.IsNullOrEmpty(creature.SpeciesId) ? creature.Id : creature.SpeciesId;
        var data = Array.Find(Creatures, c => c.Id == id) ?? Array.Find(Creatures, c => c.Id == creature.Id);
        return data?.PetBattler;
    }
}

/// <summary>Pet-battle tables (catalog format 82, GcGameTableGlobals): ClassRolls are the C/B/A/S weights per core stat
/// (GcPetBattlerCoreStat order), BiomeAffinities the element per biome, AffinityWords the element words, Labels the
/// stat names in the same order then the panel's heading ("Trait Potential").</summary>
public sealed record PetBattleTables(int[][] ClassRolls, int[] BiomeAffinities, string[] AffinityWords, string[] Labels);

/// <summary>Behaviour and diet words (catalog format 80): per role and per diet the numbered words a creature's info
/// picks from (GcCreatureGlobals Temperments / Diets / WaterDiets prefixes), and per biome the plant and meat words
/// that fill %BIOME_PLANT% / %BIOME_MEAT% (DietVeg / DietMeat); Notes are the UI_CREATURE_NOTE_&lt;n&gt; words, and the
/// Robot* lists robots' (UI_CREA_ROBOT_BEHAVE_ / _DIET_ / _NOTE_), Ages AGE1..10 (Infant ... Perpetual).</summary>
public sealed record CreatureTextWords(string[][] Behaviours, string[][] Diets, string[][] WaterDiets, string[] Plants, string[] Meats,
    string[]? Notes = null, string[]? RobotBehaviours = null, string[]? RobotDiets = null, string[]? RobotNotes = null,
    string[]? Ages = null);

/// <summary>GcCreatureGlobals' size settings (catalog format 79; null on older catalogs): the scales where the
/// Medium, Large and Huge size classes start, and the largest scale any creature spawns at.</summary>
public sealed record CreatureSizeGlobals(float MedMinSize, float LargeMinSize, float HugeMinSize, float MaxCreatureSize);

public static partial class PlanetProfileGenerator
{
    private static readonly string[] DomainNames = ["ground", "air", "water", "cave"];

    private sealed class RngStack
    {
        private readonly Rng[] _stack = new Rng[8];
        private int _count;
        private Rng _root;

        /// <summary>The generator's own stream (NMS.exe 184271 cGcPlanetGenerator +0xba4): two draws seed the stack,
        /// then it carries on through every creature's info (<see cref="CreatureInfoFor"/>), creature by creature.</summary>
        public ref Rng Root => ref _root;

        public RngStack(ulong rootSeed)
        {
            _root = Rng.Start(rootSeed);
            ref var root = ref _root;
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

        /// <summary>The seed a child stream would start from (two draws, mixed), without starting it.</summary>
        public ulong NextSeed()
        {
            ref var top = ref Top;
            ulong lo = top.Next(), hi = top.Next();
            return Mix64((hi << 32) | lo);
        }
        public float NextFraction() => Top.NextFraction();
    }

    /// <summary>NMS.exe 184271 0x1416d85f0: pushes and draws even for an empty list (then returns nothing), so an
    /// empty list still moves the parent stream on.</summary>
    private static string? PickWeighted(RngStack stack, CreatureWeightedEntry[]? entries)
    {
        if (entries is null || entries.Length == 0)
        {
            stack.Push();
            stack.NextFraction();
            stack.Pop();
            return null;
        }
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

    /// <summary>NMS.exe 184271 0x1416d87e0: one roll against the list's probability, then a pick per domain, and EMPTY
    /// for the domains left unset only when the list says OverrideAllDomains.</summary>
    private static void SelectArchetype(RngStack stack, CreatureOptWeightedList optList,
        ref string? ground, ref string? water, ref string? air, ref string? cave,
        int biome, float sea, bool hasCaves, bool overrideOnOldCatalog = true)
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
        if (optList.OverrideAllDomains ?? overrideOnOldCatalog)
        {
            ground ??= "EMPTY";
            water ??= "EMPTY";
            air ??= "EMPTY";
            cave ??= "EMPTY";
        }
        stack.Pop();
    }

    private static bool HasAnyEntry(CreatureDomainWeightedLists d) =>
        d.Ground.Length + d.Air.Length + d.Water.Length + d.Cave.Length > 0;

    /// <summary>
    /// Predicts the planet's fauna total and creature roles from its seed and environment.
    /// Reverse-engineered from NMS.exe 0x14169f4a0 (child seed 10).
    /// </summary>
    public static (int Count, PredictedCreature[] Creatures) GenerateFauna(ulong planetSeed, int biome, int subType, int planetClass,
        int star, int faunaLevel, float sea, bool hasCaves, bool emptySystem, bool abandonedSystem,
        CreatureTables tables)
    {
        if (faunaLevel <= 0) return (0, []);
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
                SelectArchetype(stack, tables.ArchetypeLists.Purple, ref ground, ref water, ref air, ref cave, biome, sea, hasCaves, overrideOnOldCatalog: false);
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

            // The biome's own list applies only when no earlier list set ANY domain (NMS.exe 184271 0x1416d60e0: all
            // four slots empty, and the list not empty). Waterworlds are purple-star planets whose purple list may
            // already have set water or air; applying the waterworld list on "ground unset" alone overwrote those
            // picks and wiped their fliers (2026-11-11: waterworld fauna totals 5/9 -> see fauna-check).
            if (ground is null && water is null && air is null && cave is null
                && biome >= 0 && biome < tables.ArchetypeLists.Biomes.Length
                && HasAnyEntry(tables.ArchetypeLists.Biomes[biome].Domains))
            {
                SelectArchetype(stack, tables.ArchetypeLists.Biomes[biome], ref ground, ref water, ref air, ref cave, biome, sea, hasCaves);
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
        // Each collected table carries how many of its roles are limited to one hemisphere (cGcRoleTableData +8).
        var collectedTables = new List<(CreatureDomainTableEntry Entry, string Domain, int[] Hemi)>();
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
                var hemi = HemiSphereCounts(stack, arch.ChanceHemi, arch.MaxHemi, arch.Tables.Length);
                stack.Pop();
                for (int t = 0; t < arch.Tables.Length; t++)
                {
                    if (!string.IsNullOrEmpty(arch.Tables[t].Table))
                        collectedTables.Add((arch.Tables[t], DomainNames[d], [hemi[t]]));
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
                    var hemi = HemiSphereCounts(stack, ae.ChanceHemi, ae.MaxHemi, ae.Tables.Length);
                    stack.Pop();

                    // Picks by index into the tables still available; the hemisphere counts follow the original index.
                    int countToPick = ae.Count;
                    var avail = Enumerable.Range(0, ae.Tables.Length).ToList();
                    if (countToPick > avail.Count) countToPick = avail.Count;
                    for (int p = 0; p < countToPick; p++)
                    {
                        if (avail.Count == 0) break;
                        stack.Push();
                        int idx = (int)(((ulong)stack.Next() * (ulong)avail.Count) >> 32);
                        stack.Pop();
                        int original = avail[idx];
                        avail.RemoveAt(idx);
                        var te = ae.Tables[original];
                        if (!string.IsNullOrEmpty(te.Table))
                            collectedTables.Add((te, DomainNames[d], [hemi[original]]));
                    }
                }
                stack.Pop();
            }
        }
        stack.Pop();
        stack.Pop();

        // 6. Roles
        var chosenCreatures = new List<PredictedCreature>();
        for (int i = 0; i < collectedTables.Count; i++)
        {
            var (te, domain, hemiLeft) = collectedTables[i];
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
                // Hemisphere, on its own child stream (so it never moves the picks): above 1 / role count, while
                // the table has hemisphere-limited roles left, one more draw - even Southern, odd Northern.
                stack.Push();
                int hemiSphere = 0;
                if (HemiSphereChance / roles.Length < stack.NextFraction() && hemiLeft[0] > 0)
                {
                    hemiSphere = (int)(~stack.Next() & 1) + 1;
                    hemiLeft[0]--;
                }
                stack.Pop();
                float draw = stack.NextFraction();
                if (draw <= role.Prob && chosenCreatures.Count < 22)
                {
                    float totalWeight = 0.0f;
                    for (int cIdx = 0; cIdx < tables.Creatures.Length; cIdx++)
                    {
                        float weight = PickWeight(tables.Creatures[cIdx], role, tables.Globals);
                        if (weight >= 0) totalWeight += weight;
                    }

                    if (totalWeight > 0.0f)
                    {
                        float roll = stack.NextFraction() * totalWeight;
                        string chosenId = "";
                        CreatureEntry? picked = null;
                        float acc = 0.0f;
                        for (int cIdx = 0; cIdx < tables.Creatures.Length; cIdx++)
                        {
                            var c = tables.Creatures[cIdx];
                            float weight = PickWeight(c, role, tables.Globals);
                            if (weight < 0) continue;
                            acc += weight;
                            if (acc >= roll)
                            {
                                chosenId = c.Id;
                                picked = c;
                                break;
                            }
                        }

                        // The creature's own seed, then its diet: robot ground life is Robot (3); role 2 Carnivore (0);
                        // roles 1 and 7 roll in a child stream, Omnivore (1) above 0.5 (0x144bf56f8), else Carnivore;
                        // the rest Herbivore (2).
                        ulong seed = stack.NextSeed();
                        int diet;
                        if (ground == "ROBOT") diet = 3;
                        else if (role.Role == 2) diet = 0;
                        else if (role.Role is 1 or 7)
                        {
                            stack.Push();
                            diet = 0.5f < stack.NextFraction() ? 1 : 0;
                            stack.Pop();
                        }
                        else diet = 2;
                        int type = picked is null ? 0
                            : picked.RealType != 0 ? picked.RealType
                            : picked.Type != 0 ? picked.Type
                            : role.Role + 22;
                        var (scale, sizeClass) = picked is null || tables.Sizes is null ? (0f, 0)
                            : SpeciesScale(seed, picked, role.MinSize, role.MaxSize, tables.Sizes);
                        string speciesId = string.IsNullOrEmpty(picked?.SpeciesId) ? chosenId : picked.SpeciesId;
                        chosenCreatures.Add(new PredictedCreature(chosenId, domain, role.Role, seed, type, diet, role.MinSize,
                            role.MaxSize, scale, sizeClass, speciesId, Density: role.Density,
                            GroupsPerKm: GroupsPerKm(tables.Globals, domain, role.Density, te.DensityMod, faunaLevel),
                            ActiveTime: role.ActiveTime, HemiSphere: hemiSphere, TileType: role.TileType, Filter: role.Filter));
                    }
                }
                stack.Pop();
            }
            stack.Pop();
        }
        // Each creature's info, in order, from the generator's own stream (0x1416cfcc0 per creature).
        var withInfo = new PredictedCreature[chosenCreatures.Count];
        for (int i = 0; i < withInfo.Length; i++)
            withInfo[i] = chosenCreatures[i] with { Info = CreatureInfoFor(ref stack.Root, chosenCreatures[i], ground, biome, subType, tables) };
        return (withInfo.Length, withInfo);
    }

    /// <summary>
    /// A species' spawn scale and size class (NMS.exe 184271, Ghidra 2026-11-11). 0x1416d9650 builds a role's spawn
    /// data: with min = the creature data's MinScale and max = its MaxScale capped at MaxCreatureSize, the role
    /// description's size range picks a band, lo = min + (max - min) * 0.25 * MinSize and hi = min + (max - min) *
    /// 0.25 * (MaxSize + 1), and one draw f from the role seed (StartState, one step) gives lo + (hi - lo) * f,
    /// clamped to [min, max]; spawn MinScale and MaxScale are both set to it, so every individual shares it (babies
    /// are scaled down, and 0x1412345f0 divides that back out by GroupBabyScale before bucketing). 0x1412345f0 then
    /// buckets it: Small below CreatureMedMinSize, Medium below CreatureLargeMinSize, Large below CreatureHugeMinSize,
    /// else Huge.
    /// </summary>
    public static (float Scale, int SizeClass) SpeciesScale(ulong roleSeed, CreatureEntry creature, int minSize, int maxSize,
        CreatureSizeGlobals sizes)
    {
        uint lo = (uint)roleSeed, hi = (uint)(roleSeed >> 32);
        ulong state = ((ulong)(((lo >> 16) | (lo << 16)) ^ hi ^ lo) + (ulong)(lo == 0 ? 1u : lo) * 0x5A76F899UL);
        float f = (float)((uint)state * 2.3283064370807974e-10);
        float min = creature.MinScale;
        float max = Math.Min(creature.MaxScale, sizes.MaxCreatureSize);
        float low = minSize * 0.25f * (max - min) + min;
        float high = (maxSize + 1) * 0.25f * (max - min) + min;
        float scale = (high - low) * f + low;
        if (scale < min) scale = min;
        if (scale > max) scale = max;
        int sizeClass = scale < sizes.MedMinSize ? 0 : scale < sizes.LargeMinSize ? 1 : scale < sizes.HugeMinSize ? 2 : 3;
        return (scale, sizeClass);
    }

    private const float HemiSphereChance = 1.0f; // 0x144bf5c48

    /// <summary>
    /// How many roles of each of a domain's tables are limited to one hemisphere (NMS.exe 184271 0x1416d8c20, Ghidra
    /// 2026-11-12): one draw against the archetype's ChanceOfHemisphereLimit, and when under it MaxToHemisphereLimit
    /// draws, each adding one to the table it lands on. Which roles, and which hemisphere, is decided per role
    /// (<see cref="HemiSphereFor"/>).
    /// </summary>
    private static int[] HemiSphereCounts(RngStack stack, float chance, int max, int tableCount)
    {
        var counts = new int[tableCount];
        if (chance > stack.NextFraction())
            for (int m = 0; m < max; m++)
                counts[(int)(((ulong)stack.Next() * (ulong)tableCount) >> 32)]++;
        return counts;
    }

    /// <summary>The discovery list's hemisphere word, "North" or "South" ("Found in the South"), for
    /// <see cref="PredictedCreature.HemiSphere"/> 1 / 2; empty for creatures found anywhere. GenerateCreatureRoles
    /// (0x1416d60e0) decides it per role on a child stream: a draw above 1 / the table's role count while the table
    /// has hemisphere-limited roles left, then one draw, even = Southern, odd = Northern.</summary>
    public static string HemiSphereFor(PredictedCreature creature) => creature.HemiSphere switch
    {
        1 => "North",
        2 => "South",
        _ => "",
    };

    /// <summary>Predicts the planet's fauna total count.</summary>
    public static int GenerateFaunaCount(ulong planetSeed, int biome, int subType, int planetClass,
        int star, int faunaLevel, float sea, bool hasCaves, bool emptySystem, bool abandonedSystem,
        CreatureTables tables) =>
        GenerateFauna(planetSeed, biome, subType, planetClass, star, faunaLevel, sea, hasCaves, emptySystem, abandonedSystem, tables).Count;

    /// <summary>The role data's GroupsPerSquareKm (GenerateCreatureRoles, 184271): the domain's groups per km for the
    /// role's density, times the table's density modifier, times the planet's fauna-level modifier.</summary>
    private static float GroupsPerKm(CreatureGlobals g, string domain, int density, int densityMod, int faunaLevel)
    {
        float[] groups = domain switch { "air" => g.AirGroups, "water" => g.WaterGroups, "cave" => g.CaveGroups, _ => g.GroundGroups };
        static float At(float[] a, int i) => i >= 0 && i < a.Length ? a[i] : 0;
        return At(groups, density) * At(g.DensityMods, densityMod) * At(g.LifeDensity, faunaLevel);
    }

    /// <summary>
    /// A creature's weight for a role, or -1 when it can't fill it (NMS.exe 184271 GenerateCreatureRoles,
    /// 0x1416d60e0, both passes): its ForceType must be the role's, it must be an ecosystem creature, a forced id
    /// (or an id that only spawns when forced) must match, and a required tag must be among its tags. Weight = role
    /// factor (1 for the forced id, else the predator or herbivore frequency for roles 1-2 / 3-4, else 1) times the
    /// rarity frequency. With a required tag the rarity is that tag's RarityOverride; without one it's the
    /// creature's own rarity, times the herd penalty once per tag it has.
    /// </summary>
    private static float PickWeight(CreatureEntry c, CreatureRoleCandidate role, CreatureGlobals globals)
    {
        if (c.Type != role.ForceType || !c.Eco) return -1;
        if ((c.ForcedOnly || !string.IsNullOrEmpty(role.ForceId)) && role.ForceId != c.Id) return -1;
        int tagIndex = -1;
        if (!string.IsNullOrEmpty(role.ReqTag))
        {
            tagIndex = Array.FindIndex(c.Tags, t => string.Equals(t, role.ReqTag, StringComparison.Ordinal));
            if (tagIndex < 0) return -1;
        }

        float roleMod;
        if (role.ForceId == c.Id) roleMod = 1.0f;
        else if (role.Role is 1 or 2) roleMod = globals.RoleFreq[c.PredMod];
        else if (role.Role is 3 or 4) roleMod = globals.RoleFreq[c.HerbMod];
        else roleMod = 1.0f;

        float rarityMod;
        if (tagIndex >= 0)
            rarityMod = globals.RarityFreq[c.TagRarities is { } rarities && tagIndex < rarities.Length ? rarities[tagIndex] : c.Rarity];
        else
        {
            rarityMod = globals.RarityFreq[c.Rarity];
            for (int t = 0; t < c.Tags.Length; t++)
                rarityMod *= globals.HerdPenalty;
        }
        return rarityMod * roleMod;
    }

}

