using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>The key of one discovery record (fDu/ETO/OsQ/?fB 8P3): &lt;Dn <see cref="Kind"/>, 5L6
/// <see cref="Address"/> (the planet's packed address, planet index in bits 52-55) and bEr <see cref="Ber"/>.</summary>
public sealed record DiscoveryRecordKey(string Kind, ulong Address, ulong[] Ber);

/// <summary>
/// Every discovery record a star system can give - its planets, their creatures, plants and minerals - from the
/// generators alone, keyed exactly as the game keys them (save format from the owner's Slot 8 diff of 2026-11-18, the
/// pieces in <see cref="DiscoveryKeys"/> and <see cref="PlanetProfileGenerator.FloraAndMinerals"/>). Pure, for the
/// "discover this system" save edit (#43) and its check.
/// </summary>
public static class SystemDiscoveries
{
    /// <summary>The records for every body of the system at <paramref name="systemAddress"/> (planet bits ignored), in
    /// planet order: the planet, then its creatures, plants and minerals. A body the generators can't predict gives none.</summary>
    public static List<DiscoveryRecordKey> ForSystem(ulong systemAddress, BiomeTables biomes, PlanetProfileTables tables)
    {
        ulong system = systemAddress & ~(0xFUL << 52);
        var (portal, galaxy) = SystemProfiles.PortalOf(system);
        var records = new List<DiscoveryRecordKey>();
        PlanetType[] types;
        ulong[] seeds;
        SystemAttributes attributes;
        try
        {
            types = PlanetGenerator.Generate(portal, galaxy, biomes);
            seeds = SystemGenerator.Bodies(portal, galaxy).PlanetSeeds;
            attributes = SystemGenerator.Attributes(portal, galaxy);
        }
        catch { return records; }

        int galaxyType = PlanetGenerator.GalaxyType(galaxy);
        for (int i = 0; i < types.Length && i < seeds.Length; i++)
        {
            ulong planet = system | ((ulong)(i + 1) << 52);
            var body = types[i];
            records.Add(new DiscoveryRecordKey("Planet", planet, [seeds[i], PlanetValue(body)]));
            try
            {
                var profile = PlanetProfileGenerator.Generate(seeds[i], i, body, (int)attributes.Star, galaxyType,
                    attributes.GasGiantPlanet, tables, attributes.Uncharted, attributes.Abandoned);
                foreach (var creature in profile.PredictedCreatures ?? [])
                    if (AnimalKey(creature, planet, tables.Creatures) is { } ber)
                        records.Add(new DiscoveryRecordKey("Animal", planet, ber));
                foreach (var d in PlanetProfileGenerator.FloraAndMinerals(seeds[i], i, body, (int)attributes.Star, galaxyType,
                             attributes.GasGiantPlanet, tables))
                    records.Add(new DiscoveryRecordKey(d.Kind, planet, [d.Seed, d.TypeHash]));
            }
            catch { /* this body's life is unknown; its planet record stays */ }
        }
        return records;
    }

    /// <summary>A Planet record's bEr[1]: the biome, with the planet's class (PlanetType.Class, 1 = the start planet)
    /// in bits 16 and up. Matched the generator on 1,282 of the 1,292 planet records in the owner's Slot 8 save
    /// (2026-11-19); the rest are records from older game versions.</summary>
    public static ulong PlanetValue(PlanetType body) => (ulong)(uint)(body.Biome | (body.Class << 16));

    /// <summary>
    /// An Animal record's bEr: [role seed, type hash of its model, species seed, genus seed, Mix64(role seed)]
    /// (NMS.exe 184271 key builder 0x14044e1e0; species and genus from <see cref="SpeciesNames.FaunaSeeds"/>). The model
    /// is the creature's ModelScene (GcCreatureFilenameTable ExtraFilename, what is drawn) when it has one, else its
    /// Scene. Null when the creature lacks a seed, its species data or its scene (catalogs before format 89).
    /// The game adds the fifth part only for some creatures (273 of the 492 Animal records in the owner's Slot 8, the
    /// same models either way; 0x14044e1e0 adds it when a flag on the creature's node is set, which no trait of the
    /// predicted creature matched). It never records a creature twice, so the seed identifies it; this always gives
    /// five parts, like the bird of the 2026-11-18 diff. Matched 265 of 273 five-part records (the rest are older
    /// genus seeds). Whether the game's Log then counts the creature found is for the in-game test (#43).
    /// </summary>
    public static ulong[]? AnimalKey(PredictedCreature creature, ulong planetAddress, CreatureTables? tables)
    {
        if (creature.Seed == 0 || creature.Scale <= 0 || creature.CreatureType == 0 || tables is null) return null;
        var entry = tables.Creatures.FirstOrDefault(c => c.Id == creature.Id);
        string scene = entry is null ? "" : entry.ModelScene.Length > 0 ? entry.ModelScene : entry.Scene;
        if (scene.Length == 0) return null;
        var (species, genus) = SpeciesNames.FaunaSeeds(creature.Seed,
            string.IsNullOrEmpty(creature.SpeciesId) ? creature.Id : creature.SpeciesId, planetAddress,
            creature.CreatureType, creature.SizeClass);
        return [creature.Seed, DiscoveryKeys.TypeHash(scene), species, genus, DiscoveryKeys.Mix64(creature.Seed)];
    }
}
