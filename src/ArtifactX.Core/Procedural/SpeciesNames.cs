using System.Text;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Procedural;

/// <summary>
/// The Latin-style species names the game gives creatures and plants ("J.
/// Mustlimii", "N. Molarurfium"), shown on the Fauna and Flora records and a
/// companion's Genetic Profile. Read off NMS.exe build 184271 on 2026-11-04
/// (static disassembly, nothing run) and checked against the owner's Slot 8:
/// all 14 Fauna records, all 8 Flora records, all 8 Mineral records and 4 companions (Witchilly "Z.
/// Wingeebus", Ana-Ana "A. Rainrobeus", Beary-Beary "F. Lobcineus", Bihi-Bihi
/// "L. Leafuivateum") - 34 of 34, letter for letter.
///
/// Minerals (0x140eb48a0, "Omkovaite"): seed = Mix64(seed), StartState; max =
/// (w &gt;&gt; 30) + 5; the word builder (alphaset 1, min 5, max) plus MINERAL_SUFFIX
/// ("ite"), the rude-word filter once, first letter capitalised. A Mineral
/// record's (bYs) UqY[1] is the seed.
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
///
/// Where a wild creature's two come from (<see cref="FaunaSeeds"/>, 184271, Ghidra 2026-11-11): the Animal
/// discovery key builder 0x14044e1e0 stores [creature seed, djb2 hash of a resource name, species, genus,
/// optional 5th] and gets species and genus from 0x14123e740:
///   species = Hash128to64(low = creature seed, high = Hash128to64(low = CreatureID bytes 8-15, high = bytes 0-7));
///   genus   = Hash128to64(low = size class, high = Hash128to64(low = creature type, high = planet address)),
/// the type as the role data has it (PredictedCreature.CreatureType: right for all 1,494 discoveries in the owner's
/// saves whose creature seed the generator predicts) and the live creature's size class (GcCreatureSizeClasses
/// 0-3, PredictedCreature.SizeClass from PlanetProfileGenerator.SpeciesScale). Of 1,496 such discoveries: species
/// 1,306, genus 1,261, both 1,198 (80%); most misses spawn with other creature data than the role picked (ANTELOPE as
/// TRICERATOPS...). A pet keeps the pair as
/// SpeciesSeed (0x23a8 at run time) and GenusSeed (0x23b0), copied by 0x141230ad0.
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

    /// <summary>A wild creature's species and genus seeds (its discovery's bEr[2] and bEr[3], the seedA and seedB
    /// of <see cref="Fauna"/>) from its role seed, CreatureID ("BIRD", "^BIRD"), the planet's packed universe address,
    /// its GcCreatureTypes value and its GcCreatureSizeClasses value (Small 0 to Huge 3).</summary>
    public static (ulong Species, ulong Genus) FaunaSeeds(ulong creatureSeed, string creatureId, ulong planetAddress,
        int creatureType, int sizeClass)
    {
        Span<byte> id = stackalloc byte[16];
        string text = creatureId.TrimStart('^').ToUpperInvariant();
        for (int i = 0; i < 16 && i < text.Length; i++) id[i] = (byte)text[i];
        ulong idHash = Hash128(BitConverter.ToUInt64(id[8..]), BitConverter.ToUInt64(id[..8]));
        ulong species = Hash128(creatureSeed, idHash);
        ulong genus = Hash128((ulong)(long)sizeClass, Hash128((ulong)(long)creatureType, planetAddress));
        return (species, genus);
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

    /// <summary>A mineral's name from its seed, or null without the suffix.</summary>
    public static string? Mineral(ulong seed, string? suffix)
    {
        if (string.IsNullOrEmpty(suffix)) return null;
        var rng = new UniverseRandom(StartState(Mix64(seed)));
        int max = (int)(rng.NextWord() >> 30) + 5;
        string word = NameGenerator.Clean(rng, NameGenerator.Generate(rng, 1, 5, max) + suffix);
        return char.ToUpperInvariant(word[0]) + word[1..];
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
