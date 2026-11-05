using System.Text;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Procedural;

/// <summary>
/// The Latin-style species names the game gives creatures and plants ("J.
/// Mustlimii", "N. Molarurfium"), shown on the Fauna and Flora records and a
/// companion's Genetic Profile. Read off NMS.exe build 184271 on 2026-11-04
/// (static disassembly, nothing run) and checked against the owner's Slot 8:
/// all 14 Fauna records, all 8 Flora records and 4 companions (Witchilly "Z.
/// Wingeebus", Ana-Ana "A. Rainrobeus", Beary-Beary "F. Lobcineus", Bihi-Bihi
/// "L. Leafuivateum") - 26 of 26, letter for letter.
///
/// Fauna (0x140eb39f0): seed = CityHash Hash128to64(low = seedB, high = seedA),
/// StartState. One draw picks NAMEGEN_CRE_((w * 300) &gt;&gt; 32 + 1); the next sets
/// max = (w * 3) &gt;&gt; 32 + 4, and the planet-name word builder (NameGenerator,
/// alphaset 6, min 3) appends a word. Flora (0x140eb4190): seed = Mix64(seed),
/// NAMEGEN_FLORA_ROOT_((w * 250) &gt;&gt; 32 + 1), max = (w &gt;&gt; 31) + 4. Both then:
///   1. end by the last letter: a/o add "e"; e/i one draw, odd "a" else "um";
///      u one draw, odd "m" else "s"; otherwise one draw into the Latin endings
///      at 0x144b7cda0, (w * 14) &gt;&gt; 32;
///   2. run the rude-word filter twice (NameGenerator.Clean);
///   3. at the first letter after three consonants that isn't a vowel or 'y',
///      insert one drawn vowel two places back (none when that is index 2);
///   4. one draw picks the genus letter 'A' + (w * 26) &gt;&gt; 32: "%c. " + word.
///
/// Which seeds: a creature's are the 3rd and 4th seeds of its discovery
/// (fDu/ETO/OsQ/?fB entry, 8P3/bEr[2] and [3]; the discovery namer at
/// 0x140524d3b reads +0x18/+0x20). Its Fauna record (MZ1 UqY) keeps only the
/// planet address and bEr[0], so the record is matched to its discovery. A
/// Flora record's UqY[1] is the plant's seed itself. A companion's are m9o and
/// JrL (the Companions screen at 0x14075733b).
/// </summary>
public static class SpeciesNames
{
    private const ulong K = 0x9DDFEA08EB382D69;
    private const string Vowels = "aeiou";

    private static readonly string[] LatinEndings = ["ium", "ima", "ica", "osa", "eus", "era", "eum", "ae", "ea", "ia", "is", "ii", "us", "um"];

    /// <summary>A creature's species name from its two seeds, or null without
    /// the NAMEGEN_CRE words.</summary>
    public static string? Fauna(ulong seedA, ulong seedB, IReadOnlyList<string> creatureRoots)
    {
        if (creatureRoots.Count < 300) return null;
        var rng = new UniverseRandom(StartState(Hash128(seedB, seedA)));
        string root = creatureRoots[(int)rng.Next(300)];
        int max = (int)rng.Next(3) + 4;
        return Finish(rng, root + NameGenerator.Generate(rng, 6, 3, max));
    }

    /// <summary>A plant's species name from its seed, or null without the
    /// NAMEGEN_FLORA_ROOT words.</summary>
    public static string? Flora(ulong seed, IReadOnlyList<string> floraRoots)
    {
        if (floraRoots.Count < 250) return null;
        var rng = new UniverseRandom(StartState(Mix64(seed)));
        string root = floraRoots[(int)rng.Next(250)];
        int max = (int)(rng.NextWord() >> 31) + 4;
        return Finish(rng, root + NameGenerator.Generate(rng, 6, 3, max));
    }

    private static string Finish(UniverseRandom rng, string word)
    {
        word += word[^1] switch
        {
            'a' or 'o' => "e",
            'e' or 'i' => (rng.NextWord() & 1) != 0 ? "a" : "um",
            'u' => (rng.NextWord() & 1) != 0 ? "m" : "s",
            _ => LatinEndings[rng.Next((uint)LatinEndings.Length)],
        };
        word = NameGenerator.Clean(rng, NameGenerator.Clean(rng, word));

        var name = new StringBuilder(word);
        int consonants = 0;
        for (int i = 0; i < name.Length; i++)
        {
            if (consonants == 3)
            {
                if (!"aeiouy".Contains(name[i]))
                {
                    if (i != 2) name.Insert(i - 2, Vowels[(int)rng.Next(5)]);
                    break;
                }
                consonants = 0;
            }
            consonants = Vowels.Contains(name[i]) ? 0 : consonants + 1;
        }
        char genus = (char)('A' + rng.Next(26));
        return $"{genus}. {name}";
    }

    private static ulong Hash128(ulong low, ulong high)
    {
        ulong a = (low ^ high) * K;
        a ^= a >> 47;
        ulong b = (high ^ a) * K;
        b ^= b >> 47;
        return b * K;
    }

    private static ulong Mix64(ulong v)
    {
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }

    private static ulong StartState(ulong seed)
    {
        ulong lo = seed & 0xFFFFFFFF, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo;
        ulong s1 = (((lo << 16) | (lo >> 16)) & 0xFFFFFFFF) ^ hi ^ lo;
        return (s1 << 32) | s0;
    }
}
