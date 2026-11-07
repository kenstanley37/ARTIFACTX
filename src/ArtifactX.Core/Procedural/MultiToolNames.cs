using System.Text;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Procedural;

/// <summary>
/// The name the game gives a multi-tool the player never renamed, from its
/// seed (SuJ[i].@EL[1]) - "Pillar of Ooruz-IX", "Reflex Beam 1/L20",
/// "Dozhgor-κ". Read off NMS.exe's own weapon-name routine (2026-10-16,
/// NMS build 180836, the code around the NAMEGEN_WEAP_FORMAT_%i lookup) and
/// checked against 13 tools named in game, every model kind included. The
/// order of the random picks is the whole trick - don't "tidy" it:
///
///   1. five words: noun, property, adjective (the staff's own list for a
///      staff), part 1, part 2;
///   2. a serial number of 2 or 3 pieces, each a letter, a digit, a letter and
///      a number either way round, or two letters and a digit, with a '-' or
///      '/' between them - its picks vary, which is why the rest moves about;
///   3. a numeral, II to XIX;
///   4. a made-up name (the same name builder as planets, letter set 3, 5 to
///      8 letters long);
///   5. the pattern: one of 10 for a tool, one of 4 for a staff;
///   6. a Greek letter (used by staff patterns).
///
/// Every piece is drawn whether the pattern uses it or not. The words come
/// from the player's game files (CatalogSteps.AddMultiToolNameWords).
/// </summary>
public static class MultiToolNames
{
    private const ulong M32 = 0xFFFFFFFF;

    /// <summary>The name for <paramref name="seed"/>, or null when a word list
    /// is missing. <paramref name="words"/> gives a list by name ("WEAP_NOUN",
    /// "STAFF_FORMAT"...), words in the game's order.</summary>
    public static string? Generate(ulong seed, bool isStaff, Func<string, IReadOnlyList<string>?> words)
    {
        if (words("WEAP_NOUN") is not { Count: > 0 } nouns || words("WEAP_PROPERTY") is not { Count: > 0 } properties
            || words(isStaff ? "STAFF_ADJ" : "WEAP_ADJ") is not { Count: > 0 } adjectives
            || words("WEAP_COMP1") is not { Count: > 0 } parts1 || words("WEAP_COMP2") is not { Count: > 0 } parts2
            || words(isStaff ? "STAFF_FORMAT" : "WEAP_FORMAT") is not { Count: > 0 } formats)
            return null;

        var rng = new UniverseRandom(StartState(Mix64(seed)));
        string noun = Pick(rng, nouns), property = Pick(rng, properties), adjective = Pick(rng, adjectives);
        string part1 = Pick(rng, parts1), part2 = Pick(rng, parts2);

        string serial = Serial(rng);
        string numeral = Roman((int)rng.Next(18) + 2);
        string procName = NameGenerator.Capitalize(NameGenerator.Generate(rng, 3, 5, 8));
        // A staff takes the top two bits (one of 4) - the same as Next(4).
        string format = isStaff ? formats[(int)Math.Min(rng.NextWord() >> 30, (uint)formats.Count - 1)] : Pick(rng, formats);
        string symbol = Greek[rng.Next((uint)Greek.Length)];

        return format.Replace("%NOUN%", noun).Replace("%PROPERTY%", property).Replace("%ADJECTIVE%", adjective)
            .Replace("%WEAP1%", part1).Replace("%WEAP2%", part2).Replace("%SERIALNUM%", serial)
            .Replace("%NUMERAL%", numeral).Replace("%PROCNAME%", procName).Replace("%SYM%", symbol);
    }

    private static string Pick(UniverseRandom rng, IReadOnlyList<string> list) => list[(int)rng.Next((uint)list.Count)];

    private static string Serial(UniverseRandom rng)
    {
        var serial = new StringBuilder();
        int pieces = (int)(rng.NextWord() >> 31) + 2;
        for (int i = 0; i < pieces; i++)
        {
            uint kind = rng.Next(5);
            if (i > 0) serial.Append((rng.NextWord() & 1) != 0 ? '-' : '/');
            switch (kind)
            {
                case 0: { char letter = Letter(rng); serial.Append(letter).Append(rng.Next(99) + 1); break; } // "%c%i"
                case 1: { char a = Letter(rng), b = Letter(rng); serial.Append(a).Append(b).Append(rng.Next(10)); break; }
                case 2: { char letter = Letter(rng); serial.Append(rng.Next(99) + 1).Append(letter); break; } // "%i%c"
                case 3: serial.Append(Letter(rng)); break;
                case 4: serial.Append(rng.Next(10)); break;
            }
        }
        return serial.ToString();
    }

    private static char Letter(UniverseRandom rng) => (char)('A' + rng.Next(26));

    private static readonly string[] Greek =
        "α β γ δ ε ζ η θ ι κ λ μ ν ξ ο π ρ σ τ υ φ χ ψ ω".Split(' ');

    private static string Roman(int n)
    {
        var sb = new StringBuilder();
        foreach (var (value, text) in new[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
            for (; n >= value; n -= value) sb.Append(text);
        return sb.ToString();
    }

    /// <summary>The game's 64-bit mixer, run on the seed first.</summary>
    private static ulong Mix64(ulong v)
    {
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }

    /// <summary>The game's multiply-with-carry seeding (as ShipParts), as a
    /// UniverseRandom state: carry in the high half, value in the low.</summary>
    private static ulong StartState(ulong seed)
    {
        ulong lo = seed & M32, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo;
        ulong s1 = (((lo << 16) | (lo >> 16)) & M32) ^ hi ^ lo;
        return (s1 << 32) | s0;
    }
}
