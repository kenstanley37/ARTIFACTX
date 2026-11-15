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
/// Living ships: <see cref="GenerateLiving"/>. Sentinel ships: <see cref="GenerateSentinel"/>.
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

    /// <summary>
    /// The name the game gives an unnamed Living Ship (NAMEGEN_WEIRDSHIP_*): "Yulin KV4". Fitted on
    /// 2026-11-14 to Slot 8's Living Ship, seed 0x2966405E8B675213, named "Yulin KV4" in game: on the
    /// same Mix64-seeded stream as <see cref="Generate"/>, draws 1-2 are the code's letters in printed
    /// order and draw 3 its digit (1-9), then the made-up name (letter set 3, as multi-tool names; 5 to
    /// 8 letters assumed, any maximum fits this sample), then the prefix ("The Lonely"...) and the
    /// pattern. Code and name fit exactly; the prefix-before-pattern order is the one that picks this
    /// ship's pattern (the reverse picks the prefix pattern) - one sample, provisional.
    /// </summary>
    public static string? GenerateLiving(ulong seed, Func<string, IReadOnlyList<string>?> words)
    {
        if (words("WEIRDSHIP_FORMAT") is not { Count: > 0 } formats
            || words("WEIRDSHIP_PREFIX") is not { Count: > 0 } prefixes) return null;
        var rng = new UniverseRandom(StartState(Mix64(seed)));
        char first = (char)('A' + rng.Next(26)), second = (char)('A' + rng.Next(26));
        uint digit = rng.Next(9) + 1;
        string procName = NameGenerator.Capitalize(NameGenerator.Generate(rng, 3, 5, 8));
        string prefix = Pick(rng, prefixes);
        string format = Pick(rng, formats);
        return format.Replace("%PREFIX%", prefix).Replace("%PROCNAME%", procName)
            .Replace("%SHIPCODE%", $"{first}{second}{digit}").Trim();
    }

    /// <summary>The 24 symbols of Sentinel ship names, NMS.exe table 0x144B7CC90 (build 184271): α to ω, no final ς.</summary>
    private const string Greek = "αβγδεζηθικλμνξοπρστυφχψω";

    /// <summary>
    /// The name the game gives an unnamed Sentinel ship (NAMEGEN_ROBOTSHIP_*): "μ-Flameborn", "Crimson-σ",
    /// "Flameborn-9-MH3". Read from NMS.exe 0x140EAC670 (build 184271, Ghidra, 2026-11-14), on the same
    /// Mix64-seeded stream as <see cref="Generate"/>: pattern Next(4), two symbols Next(24), name Next(20),
    /// then %NUM%: 2 pieces, 3 when the next word's top bit is set; each piece draws its kind Next(5) and, after
    /// the first, a separator ('/' when the following word is even, '-' when odd), then: letter + 1-99 ("%c%i"),
    /// letter letter digit, 1-99 + letter ("%i%c"), one letter, or one digit. Checked against the game:
    /// owned ship seed 0x1203E23BB8CB0BF9 = μ-Flameborn (Slot 8); crashed Sentinel site keys on Nionne Sigma
    /// 0xB118F1E99ED0D4CA = Crimson-σ, 0x8CAAD428FF569903 = Flameborn-9-MH3, 0x2089D793883477F0 = Hope-1-D.
    /// </summary>
    public static string? GenerateSentinel(ulong seed, Func<string, IReadOnlyList<string>?> words)
    {
        if (words("ROBOTSHIP_FORMAT") is not { Count: > 0 } formats
            || words("ROBOTSHIP_NAME") is not { Count: > 0 } names) return null;
        var rng = new UniverseRandom(StartState(Mix64(seed)));
        string format = formats[(int)Math.Min(rng.Next(4), (uint)formats.Count - 1)];
        char sym1 = Greek[(int)rng.Next(24)], sym2 = Greek[(int)rng.Next(24)];
        string name = names[(int)Math.Min(rng.Next(20), (uint)names.Count - 1)];
        char Letter() => (char)('A' + rng.Next(26));
        var num = new StringBuilder();
        int pieces = rng.NextWord() >= 0x80000000 ? 3 : 2;
        for (int i = 0; i < pieces; i++)
        {
            uint kind = rng.Next(5);
            if (i > 0) num.Append((rng.NextWord() & 1) == 0 ? '/' : '-');
            switch (kind)
            {
                case 0: { char l = Letter(); num.Append(l).Append(rng.Next(99) + 1); break; }
                case 1: { char a = Letter(), b = Letter(); num.Append(a).Append(b).Append(rng.Next(10)); break; }
                case 2: { char l = Letter(); num.Append(rng.Next(99) + 1).Append(l); break; }
                case 3: num.Append(Letter()); break;
                default: num.Append(rng.Next(10)); break;
            }
        }
        return format.Replace("%NAME%", name).Replace("%SYM1%", sym1.ToString()).Replace("%SYM2%", sym2.ToString())
            .Replace("%NUM%", num.ToString()).Trim();
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
