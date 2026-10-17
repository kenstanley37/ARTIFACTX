using System.Text;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Procedural;

/// <summary>
/// The name the game gives a starship the player never renamed, from its seed
/// (NTx.@EL[1]) - "Ashes of the Suitaka". Read off NMS.exe's own ship-name
/// routine (2026-10-16, NMS build 180836, the code around the
/// NAMEGEN_SHIP_FORMAT_%i lookup, 0x140e8ed50). The same generator as
/// multi-tool names (MultiToolNames); every piece is drawn whether the pattern
/// uses it or not, in this order:
///
///   1. five words: noun, verb, possession, property, adjective;
///   2. a numeral, II to XIX;
///   3. a ship code: a digit 1-9, then two letters, printed letter, letter, digit
///      ("%c%c%d" with the third draw's letter first);
///   4. a made-up name (the planet name builder, letter set 5, 5 to 8 letters);
///   5. the pattern: one of the first 15 (the 16th is never picked).
///
/// Living ships and Sentinel ships have their own routines (NAMEGEN_WEIRDSHIP_*,
/// NAMEGEN_ROBOTSHIP_*) and aren't covered here.
/// </summary>
public static class ShipNames
{
    private const ulong M32 = 0xFFFFFFFF;

    /// <summary>The name for <paramref name="seed"/>, or null when a word list
    /// is missing. <paramref name="words"/> gives a list by name ("SHIP_NOUN",
    /// "SHIP_FORMAT"...), words in the game's order.</summary>
    public static string? Generate(ulong seed, Func<string, IReadOnlyList<string>?> words)
    {
        if (words("SHIP_NOUN") is not { Count: > 0 } nouns || words("SHIP_VERB") is not { Count: > 0 } verbs
            || words("SHIP_POSESSION") is not { Count: > 0 } possessions || words("SHIP_PROPERTY") is not { Count: > 0 } properties
            || words("SHIP_ADJ") is not { Count: > 0 } adjectives || words("SHIP_FORMAT") is not { Count: > 0 } formats)
            return null;

        var rng = new UniverseRandom(StartState(Mix64(seed)));
        string noun = Pick(rng, nouns), verb = Pick(rng, verbs), possession = Pick(rng, possessions);
        string property = Pick(rng, properties), adjective = Pick(rng, adjectives);

        string numeral = Roman((int)rng.Next(18) + 2);
        uint digit = rng.Next(9) + 1;
        char second = (char)('A' + rng.Next(26)), first = (char)('A' + rng.Next(26));
        string code = $"{first}{second}{digit}";
        string procName = NameGenerator.Capitalize(NameGenerator.Generate(rng, 5, 5, 8));
        string format = formats[(int)Math.Min(rng.Next(15), (uint)formats.Count - 1)];

        return format.Replace("%NOUN%", noun).Replace("%VERB%", verb).Replace("%POSSESSION%", possession)
            .Replace("%PROPERTY%", property).Replace("%ADJECTIVE%", adjective).Replace("%NUMERAL%", numeral)
            .Replace("%SHIPCODE%", code).Replace("%PROCNAME%", procName).Trim();
    }

    private static string Pick(UniverseRandom rng, IReadOnlyList<string> list) => list[(int)rng.Next((uint)list.Count)];

    private static string Roman(int n)
    {
        var sb = new StringBuilder();
        foreach (var (value, text) in new[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
            for (; n >= value; n -= value) sb.Append(text);
        return sb.ToString();
    }

    private static ulong Mix64(ulong v)
    {
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }

    private static ulong StartState(ulong seed)
    {
        ulong lo = seed & M32, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo;
        ulong s1 = (((lo << 16) | (lo >> 16)) & M32) ^ hi ^ lo;
        return (s1 << 32) | s0;
    }
}
