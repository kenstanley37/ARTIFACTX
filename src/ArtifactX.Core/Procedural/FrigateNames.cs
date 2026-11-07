using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Procedural;

/// <summary>
/// Generates the game's automatic name for a conventional owned frigate from its
/// ResourceSeed. Recovered from NMS build 180836's full frigate naming path
/// (0x140e90070): five ship words, a code selected from the top three bits,
/// a 1-9 number, a procedural name, then a format selected the same way.
/// Organic (DeepSpaceCommon) frigates use GenerateOrganic instead. Crew race
/// and inventory class do not affect the naming stream.
/// </summary>
public static class FrigateNames
{
    /// <summary>The generated name, or null if one of the game's localized
    /// word lists is unavailable. Lists must retain their game order.</summary>
    public static string? Generate(ulong resourceSeed, Func<string, IReadOnlyList<string>?> words)
    {
        if (words("SHIP_NOUN") is not { Count: > 0 } nouns
            || words("SHIP_VERB") is not { Count: > 0 } verbs
            || words("SHIP_POSESSION") is not { Count: > 0 } possessions
            || words("SHIP_PROPERTY") is not { Count: > 0 } properties
            || words("SHIP_ADJ") is not { Count: > 0 } adjectives
            || words("FRIGATE_CODE") is not { Count: >= 8 } codes
            || words("FRIGATE_NAME_FORMAT") is not { Count: >= 8 } formats)
            return null;

        var rng = new UniverseRandom(StartState(Mix64(resourceSeed)));
        string noun = Pick(rng, nouns);
        string verb = Pick(rng, verbs);
        string possession = Pick(rng, possessions);
        string property = Pick(rng, properties);
        string adjective = Pick(rng, adjectives);

        // The game shifts a newly advanced MWC word by 29 and adds one;
        // this yields indices 1..8, rather than Next(10).
        string code = codes[(int)(rng.NextWord() >> 29)];
        uint number = rng.Next(9) + 1;
        string proceduralName = NameGenerator.Capitalize(NameGenerator.Generate(rng, 5, 5, 8));
        string format = formats[(int)(rng.NextWord() >> 29)];

        return format.Replace("%CODE%", code)
            .Replace("%NUMBER%", number.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("%PROCNAME%", proceduralName)
            .Replace("%NOUN%", noun)
            .Replace("%VERB%", verb)
            .Replace("%POSSESSION%", possession)
            .Replace("%PROPERTY%", property)
            .Replace("%ADJECTIVE%", adjective)
            .Trim();
    }

    private static string Pick(UniverseRandom rng, IReadOnlyList<string> list) => list[(int)rng.Next((uint)list.Count)];

    /// <summary>
    /// Organic frigates use the living-ship naming routine (0x140e8dc30 in
    /// NMS build 180836). The owned-frigate caller at 0x141438aad selects it
    /// for FrigateClass 7 (DeepSpaceCommon). Draw a number, two letters in
    /// reverse display order, a name from alphabet 3, a prefix, then a format.
    /// Prefixes and formats are read from the player's game catalog.
    /// </summary>
    public static string? GenerateOrganic(ulong resourceSeed, Func<string, IReadOnlyList<string>?> words)
    {
        if (words("WEIRDSHIP_PREFIX") is not { Count: >= 25 } prefixes
            || words("WEIRDSHIP_FORMAT") is not { Count: >= 2 } formats)
            return null;

        var rng = new UniverseRandom(StartState(Mix64(resourceSeed)));
        uint number = rng.Next(9) + 1;
        char second = (char)('A' + rng.Next(26));
        char first = (char)('A' + rng.Next(26));
        string code = $"{first}{second}{number}";
        string proceduralName = NameGenerator.Capitalize(NameGenerator.Generate(rng, 3, 5, 10));
        string prefix = prefixes[(int)rng.Next(25)];
        string format = formats[(int)(rng.NextWord() >> 31)];

        return format.Replace("%PROCNAME%", proceduralName)
            .Replace("%SHIPCODE%", code)
            .Replace("%PREFIX%", prefix)
            .Trim();
    }

    private static ulong Mix64(ulong value)
    {
        value = (value ^ (value >> 33)) * 0x64DD81482CBD31D7UL;
        value = (value ^ (value >> 33)) * 0xE36AA5C613612997UL;
        return value ^ (value >> 33);
    }

    private static ulong StartState(ulong seed)
    {
        ulong lo = seed & 0xFFFFFFFF;
        ulong hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo;
        ulong s1 = (((lo << 16) | (lo >> 16)) & 0xFFFFFFFF) ^ hi ^ lo;
        return (s1 << 32) | s0;
    }
}
