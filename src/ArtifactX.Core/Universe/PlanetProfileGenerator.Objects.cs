namespace ArtifactX.Core.Universe;

/// <summary>An external object list (GcExternalObjectListOptions): a biome file's own, one of
/// biomefilenames' common lists, or a file of an optional group.</summary>
/// <param name="TileType">GcTerrainTileType: 4 Underwater, 5 Cave, 7 Liquid matter here.</param>
/// <param name="Options">How many model files it picks from (one more draw when placed).</param>
/// <param name="Hint">ResourceHint, the scanner line it adds (PLANT_LUSH, UI_BONES_HINT...), or "".</param>
public sealed record ObjectList(string Name, int Order, float Probability, int TileType, int Options, string Hint = "");

/// <summary>Conditions an optional object group (GcExternalObjectFileList) puts on a planet.</summary>
[Flags]
public enum ObjectGroupRules
{
    None = 0,
    NotOnDeadPlanets = 1 << 0, NotOnExtremePlanets = 1 << 1, NotOnGasGiant = 1 << 2, NotOnInfested = 1 << 3,
    NotOnScrapWorlds = 1 << 4, NotOnStartPlanets = 1 << 5, NotOnWeirdPlanets = 1 << 6, OnlyOnCorruptSentinels = 1 << 7,
    OnlyOnDeepWater = 1 << 8, OnlyOnExtremeSentinels = 1 << 9, OnlyOnExtremeWeather = 1 << 10, OnlyOnInfested = 1 << 11,
    OnlyOnScrapWorlds = 1 << 12,
}

/// <summary>One of biomefilenames' OptionalExternalObjectLists.</summary>
/// <param name="Biome">OnlyOnBiome (GcBiomeType), 16 = any.</param>
/// <param name="SubBiomeProbability">Active chance per subtype; below 0 = use Probability.</param>
public sealed record ObjectGroup(string Id, ObjectList[] Files, int MinFiles, int MaxFiles, int Biome, float Probability,
    float[] SubBiomeProbability, ObjectGroupRules Rules);

/// <summary>What a biome file (GcBiomeData) gives the terrain and object steps: its
/// TerrainControls chances and its own object lists.</summary>
public sealed record BiomeObjects(float WaterActive, float HighWaterActive, float RockTile, float SubstanceTile,
    float[] NoiseLayers, float[] Features, float Caves, ObjectList[] Lists);

/// <summary>A voxelgeneratorsettings.mbin entry's Min settings, as far as random draws go:
/// which noise layers (and their voxel type) and features can be on, and caves. MinLayers / MaxLayers (the entry's
/// Min and Max noise layers) and NoSeaBaseLevel are what <see cref="PlanetTerrain.Generate"/> draws a planet's terrain
/// from; catalogs built before format 48 don't have them.</summary>
public sealed record TerrainSetting(bool[] NoiseActive, int[] NoiseVoxelType, bool[] FeatureActive, bool Caves,
    TerrainNoiseLayer[]? MinLayers = null, TerrainNoiseLayer[]? MaxLayers = null, float NoSeaBaseLevel = 0f);

/// <summary>Terrain settings and gcterrainglobals' index ranges and sea levels.</summary>
public sealed record TerrainTables(TerrainSetting[] Settings, int PrimeIndexStart, int PurpleIndexStart, bool LockIndex,
    float SeaMoon, float SeaGasGiant, float SeaWaterWorld, float SeaHigh, float SeaStandard,
    float PurpleMaxHighWater, bool ForcePurpleHighWater);

/// <summary>The terrain step's choices that the planet's terrain data (TkVoxelGeneratorData) is built from: the
/// voxelgeneratorsettings entry, how many draws of the step's stream come before its noise layers, the sea level and
/// the biome file whose TerrainControls chances the layers draw against.</summary>
public sealed record TerrainChoice(int SettingIndex, int DrawsBeforeLayers, float Sea, BiomeObjects BiomeFile);

/// <param name="BiomeFiles">[biome][subtype], null where the game has no file for that subtype.</param>
public sealed record ObjectTables(BiomeObjects?[][] BiomeFiles, ObjectList[] Common, ObjectGroup[] Groups, TerrainTables Terrain);

public static partial class PlanetProfileGenerator
{
    /// <summary>What the object step checks a planet against.</summary>
    private sealed record PlanetFacts(int Biome, int SubType, int Class, int Sentinels, bool ExtremeLooking,
        bool ScrapWorld, float Sea, bool Caves, float SeaStandard);

    private static BiomeObjects? BiomeFile(ObjectTables t, int biome, int subType)
    {
        if (biome < 0 || biome >= t.BiomeFiles.Length) biome = 0;
        var bySub = t.BiomeFiles[biome];
        if (subType >= 0 && subType < bySub.Length && bySub[subType] is { } own) return own;
        foreach (var f in bySub) if (f is not null) return f;
        return null;
    }

    /// <summary>The terrain step's choices for a planet (see <see cref="TerrainChoice"/>); null without the object tables
    /// or a biome file. Checked 2026-10-28 against the live terrain data of the 6 planets of system 0x11476E2.</summary>
    public static TerrainChoice? TerrainChoiceFor(ulong planetSeed, PlanetType body, int star, PlanetProfileTables t)
    {
        if (t.Objects is not { } objects || BiomeFile(objects, body.Biome, body.SubType) is not { } file) return null;
        var r = Terrain(Rng.Start(ChildSeeds(planetSeed)[4]), file, objects.Terrain, body, star);
        return new TerrainChoice(r.Index, r.DrawsBeforeLayers, r.Sea, file);
    }

    /// <summary>"Extreme" as the object step tests it (0x1409aa2a0): extreme weather, but not on
    /// lush or barren worlds, nor with clear, dusty, humid, bubbling, weird or cold-clear weather.</summary>
    private static bool ExtremeLooking(int biome, int subType, int weather, bool extreme)
    {
        int seen = subType == SwampSub ? Swamp : subType == LavaSub ? Lava : biome;
        return extreme && seen != Lush && biome != Barren && !(weather <= 15 && ((0xB007 >> weather) & 1) == 1);
    }

    /// <summary>
    /// The terrain step (0x141695420, 0x141694280; child seed 4), only as far as it decides the
    /// sea and caves: a terrain settings index (prime and purple systems have ranges of their
    /// own), the sea, then each noise layer and feature that may be on - a draw each, and the
    /// draws that set it up (26 or 12, plus rock/substance picks) when it is - then caves.
    /// Matched the game's code (emulated, with the voxel settings loaded) on 808 of 808 planets.
    /// </summary>
    private static (float Sea, bool Caves, int Index, int DrawsBeforeLayers) Terrain(Rng rng, BiomeObjects b, TerrainTables t, PlanetType body, int star)
    {
        int Index()
        {
            uint w = rng.Next();
            if (star == 4) return t.PurpleIndexStart + (int)(((ulong)w * (ulong)(31 - t.PurpleIndexStart)) >> 32);
            if (body.Prime)
            {
                int from = t.PrimeIndexStart + 1;
                return from + (int)(((ulong)w * (ulong)Math.Max(0, t.PurpleIndexStart - from)) >> 32);
            }
            return (int)(((ulong)w * (ulong)t.PrimeIndexStart) >> 32);
        }
        int index = Index();
        if (!t.LockIndex) index = Index();
        var setting = t.Settings[Math.Clamp(index, 0, t.Settings.Length - 1)];

        // The sea (0x141695520): none on moons outside purple systems, start planets, dead and
        // weird worlds; always on water worlds; otherwise a draw.
        int biome = body.Biome;
        bool water;
        if (body.BodyClass == 3 && star != 4) water = false;
        else if (body.Class == 1) water = false;
        else if (biome == Waterworld) water = true;
        else if (biome is Dead or Weird) water = false;
        else water = b.WaterActive > rng.NextFraction();
        float sea = 0f;
        if (water)
        {
            bool high = star == 4 && (Math.Min(b.HighWaterActive, t.PurpleMaxHighWater) > rng.NextFraction() || t.ForcePurpleHighWater);
            sea = body.BodyClass == 3 ? t.SeaMoon : biome == GasGiant ? t.SeaGasGiant : biome == Waterworld ? t.SeaWaterWorld
                : high ? t.SeaHigh : t.SeaStandard;
        }

        int drawsBeforeLayers = rng.Draws;
        for (int i = 0; i < setting.NoiseActive.Length; i++)
        {
            bool on = setting.NoiseActive[i] && i < b.NoiseLayers.Length && b.NoiseLayers[i] > rng.NextFraction();
            if (i == 7)
            {
                if (sea > t.SeaStandard) on = false;
                else if (body.DeadToWeird || sea > 1f) on = true;
            }
            if (!on) continue;
            int voxel = setting.NoiseVoxelType[i];
            if (voxel is 8 or 9) // RandomRock, RandomRockOrSubstance (0x1416924c0)
            {
                if (b.RockTile > rng.NextFraction()) rng.Next();
                else if (voxel == 9 && b.SubstanceTile > rng.NextFraction()) rng.Next();
            }
            for (int k = 0; k < 26; k++) rng.Next();
        }
        for (int i = 0; i < setting.FeatureActive.Length; i++)
            if (setting.FeatureActive[i] && i < b.Features.Length && b.Features[i] > rng.NextFraction())
                for (int k = 0; k < 12; k++) rng.Next();
        bool caves = setting.Caves && b.Caves > rng.NextFraction();
        return (sea, caves, Math.Clamp(index, 0, t.Settings.Length - 1), drawsBeforeLayers);
    }

    /// <summary>
    /// The object step (0x14169bf50; child seed 7) and the scanner hints of what it places
    /// (0x141697090). Two draws seed a stream of its own; every list - the biome file's, the
    /// common ones, then the optional groups, stably sorted by Order (a group by its files'
    /// highest) - draws from it: a list a chance and, when placed, its model; a group a
    /// chance, then how many files, which are picked on the main stream (a picked file's
    /// chance leaves the total, so a 2-of-2 group gets both). Underwater and
    /// liquid lists need a sea, cave lists caves. Bones, scrap and bugs matched a real save's
    /// records on 776 of 808 planets (2026-10-22; most misses are records of another planet).
    /// </summary>
    private static string[] PlacedHints(Rng main, BiomeObjects b, ObjectTables t, PlanetFacts p)
    {
        ulong lo = main.Next(), hi = main.Next();
        var own = Rng.Start(Mix64((hi << 32) | lo));

        var entries = new List<(ObjectList? List, ObjectGroup? Group, int Key)>();
        foreach (var l in b.Lists) entries.Add((l, null, l.Order));
        foreach (var l in t.Common) entries.Add((l, null, l.Order));
        foreach (var g in t.Groups) entries.Add((null, g, Math.Max(0, g.Files.Length == 0 ? 0 : g.Files.Max(f => f.Order))));
        var sorted = entries.Select((e, i) => (e, i)).OrderBy(x => x.e.Key).ThenBy(x => x.i).Select(x => x.e);

        // MAIN and MAIN_ULTRA are alternatives by graphics setting; either costs the same draws.
        bool hasUltra = entries.Any(e => e.List?.Name == "MAIN_ULTRA");
        bool Blocked(ObjectList l) => (l.TileType is 4 or 7 && p.Sea <= 0f) || (l.TileType == 5 && !p.Caves);
        int seen = p.SubType == SwampSub ? Swamp : p.SubType == LavaSub ? Lava : p.Biome;

        var placed = new HashSet<string>(StringComparer.Ordinal);
        var hints = new List<string>();
        foreach (var (list, group, _) in sorted)
        {
            if (list is not null)
            {
                if (hasUltra && list.Name == "MAIN") continue;
                float f = own.NextFraction();
                if (Blocked(list) || f > list.Probability) continue;
                own.Next();
                placed.Add(list.Name);
                continue;
            }
            var g = group!;
            float chance = own.NextFraction();
            if (!Applies(g, p, seen)) continue;
            float active = p.SubType >= 0 && p.SubType < g.SubBiomeProbability.Length && g.SubBiomeProbability[p.SubType] >= 0f
                ? g.SubBiomeProbability[p.SubType] : g.Probability;
            if (!(active > chance)) continue;
            int count = g.MinFiles + (int)(((ulong)own.Next() * (ulong)Math.Max(1, g.MaxFiles - g.MinFiles + 1)) >> 32);
            float total = 0f;
            foreach (var file in g.Files) if (!Blocked(file) && !(1f > file.Probability)) total += file.Probability;
            for (int c = 0; c < count; c++)
            {
                float target = main.NextFraction() * total, cum = 0f;
                foreach (var file in g.Files)
                {
                    if (placed.Contains(file.Name) || Blocked(file) || 1f > file.Probability) continue;
                    if (!(target < cum) && cum + file.Probability >= target)
                    {
                        main.Next();
                        placed.Add(file.Name);
                        total -= file.Probability;
                        if (file.Hint.Length > 0 && !hints.Contains(file.Hint)) hints.Add(file.Hint);
                        break;
                    }
                    cum += file.Probability;
                }
            }
        }
        return hints.ToArray();
    }

    /// <summary>A group's conditions (0x14169c240), as the game sees the planet: Swamp and Lava
    /// subtypes count as those types.</summary>
    private static bool Applies(ObjectGroup g, PlanetFacts p, int seen)
    {
        var r = g.Rules;
        if (r.HasFlag(ObjectGroupRules.OnlyOnExtremeWeather) && !p.ExtremeLooking) return false;
        if (r.HasFlag(ObjectGroupRules.OnlyOnExtremeSentinels) && p.Sentinels != 2) return false;
        if (r.HasFlag(ObjectGroupRules.OnlyOnCorruptSentinels) && p.Sentinels != 3) return false;
        if (r.HasFlag(ObjectGroupRules.OnlyOnDeepWater) && !(p.Sea > p.SeaStandard)) return false;
        if (r.HasFlag(ObjectGroupRules.NotOnExtremePlanets) && (p.ExtremeLooking || p.Sentinels == 2)) return false;
        if (r.HasFlag(ObjectGroupRules.NotOnStartPlanets) && p.Class is 1 or 2) return false;
        if (g.Biome != 16 && g.Biome != seen) return false;
        if (r.HasFlag(ObjectGroupRules.NotOnWeirdPlanets) && seen == Weird) return false;
        if (r.HasFlag(ObjectGroupRules.NotOnGasGiant) && seen == GasGiant) return false;
        if (r.HasFlag(ObjectGroupRules.NotOnDeadPlanets) && seen == Dead) return false;
        if (r.HasFlag(ObjectGroupRules.NotOnInfested) && p.SubType == Infested) return false;
        if (r.HasFlag(ObjectGroupRules.OnlyOnInfested) && p.SubType != Infested) return false;
        if (r.HasFlag(ObjectGroupRules.OnlyOnScrapWorlds) && !p.ScrapWorld) return false;
        if (r.HasFlag(ObjectGroupRules.NotOnScrapWorlds) && p.ScrapWorld) return false;
        return true;
    }
}
