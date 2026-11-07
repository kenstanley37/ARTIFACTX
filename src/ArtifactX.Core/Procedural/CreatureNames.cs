using System.Text;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Procedural;

/// <summary>
/// The everyday name the game gives a creature ("Witchilly", "Ana-Ana",
/// "Bigava-TO0/K42"), shown for a companion the player never renamed. Read off
/// NMS.exe build 184271 on 2026-11-04 (static disassembly of 0x140eaa420) and
/// checked two ways: the routine run in an emulator (Unicorn) gives the owner's
/// four Slot 8 companions exactly - Witchilly, Ana-Ana, Beary-Beary, Bihi-Bihi -
/// and this port matches the emulator on 560 random seeds over 14 creature types.
///
/// A companion's seed (0x1412304cc, building the Companions screen):
/// Hash128to64(low = Hash128to64(low = CreatureID bytes 8-15, high = bytes 0-7),
/// high = BirthTime), CreatureID as 16 bytes ("QUAD" reads as "QUAD_PET").
/// The name routine then mixes it (Mix64), StartState, and:
///   1. A = NAMEGEN_CRE_((w * 300) &gt;&gt; 32 + 1); B = the word builder (alphaset 5,
///      min 2, max (w * 3) &gt;&gt; 32 + 3), first letter capitalised; C = A, D = B.
///   2. A, C, B, D in turn each get an ending, the rude-word filter, a vowel
///      after a run of consonants, and the filter again. "Harsh" kinds (FIEND,
///      BUGFIEND, SCUTTLER_PET, QUAD_PET, HORROR_PET, SPIDERQUAD_PET) end -st/-sh,
///      -ss or a table of 28 (ak, ush...); the rest -zi, -enia, -sta, -tto, -y/-i
///      or a table of 31 (illy, etta...).
///   3. One draw f in [0, 1): robots (QUAD_PET, ROBO_PET, HOVER_PET,
///      ROBO_RODENT, SPIDERQUAD_PET) are "A-code"; otherwise A of 5 letters or
///      fewer gives "A-A" or "A-C", else B of 5 or fewer "B-B" or "B-D" (FIEND,
///      SCUTTLER_PET, BUGFIEND and LANDSQUID_PET skip both); A of 8+ with B of
///      6+ gives A; then f &lt; 0.35 A, &lt; 0.7 B, &lt; 0.9 "A-B", else "B-E" with a
///      fresh word E made like B.
/// </summary>
public static class CreatureNames
{
    private const ulong K = 0x9DDFEA08EB382D69;
    private const double TinyDouble = 2.3283064370807974e-10;

    private static readonly string[] HarshEndings = ["ak", "akk", "ash", "ax", "az", "ekk", "esh", "ex", "ez", "ic", "ik", "ikk", "iss", "ix", "iz", "ok", "okk", "oss", "osh", "ox", "oxis", "oz", "uk", "ukk", "ush", "uss", "ux", "uz"];
    private static readonly string[] SoftEndings = ["a", "i", "ee", "ia", "ie", "iva", "eve", "elle", "ella", "itso", "ito", "ico", "ina", "etta", "inny", "eeny", "etto", "itta", "ita", "inna", "innia", "oz", "y", "pito", "il", "ila", "ava", "oop", "ippo", "ipti", "illy"];
    private static readonly HashSet<string> Harsh = ["FIEND", "SCUTTLER_PET", "QUAD_PET", "BUGFIEND", "HORROR_PET", "SPIDERQUAD_PET"];
    private static readonly HashSet<string> NoRepeats = ["FIEND", "SCUTTLER_PET", "BUGFIEND", "LANDSQUID_PET"];
    private static readonly HashSet<string> Robots = ["QUAD_PET", "ROBO_PET", "HOVER_PET", "ROBO_RODENT", "SPIDERQUAD_PET"];

    /// <summary>A companion's name seed from its CreatureID (XID, "^CAT") and BirthTime.</summary>
    public static ulong CompanionSeed(string creatureId, ulong birthTime)
    {
        Span<byte> id = stackalloc byte[16];
        string text = CreatureKey(creatureId);
        for (int i = 0; i < 16 && i < text.Length; i++) id[i] = (byte)text[i];
        ulong first = BitConverter.ToUInt64(id[..8]), second = BitConverter.ToUInt64(id[8..]);
        return Hash128(Hash128(second, first), birthTime);
    }

    /// <summary>The name for a seed and creature kind, or null without the
    /// NAMEGEN_CRE words.</summary>
    public static string? Generate(ulong seed, string creatureId, IReadOnlyList<string> creatureRoots)
    {
        if (creatureRoots.Count < 300) return null;
        string kind = CreatureKey(creatureId);
        bool harsh = Harsh.Contains(kind);
        var rng = new UniverseRandom(StartState(Mix64(seed)));

        string a = creatureRoots[(int)rng.Next(300)];
        string b = NewWord(rng);
        string c = a, d = b;
        a = Finish(rng, a, harsh);
        c = Finish(rng, c, harsh);
        b = Finish(rng, b, harsh);
        d = Finish(rng, d, harsh);

        float f = (float)(rng.NextWord() * TinyDouble);
        bool repeats = !NoRepeats.Contains(kind);
        if (Robots.Contains(kind)) return $"{a}-{RobotCode(rng)}";
        if (repeats && a.Length <= 5) return $"{a}-{((rng.NextWord() & 1) != 0 ? c : a)}";
        if (repeats && b.Length <= 5) return $"{b}-{((rng.NextWord() & 1) != 0 ? d : b)}";
        if (a.Length >= 8 && b.Length >= 6) return a;
        if (f < 0.35f) return a;
        if (f < 0.7f) return b;
        if (f < 0.9f) return $"{a}-{b}";
        return $"{b}-{Finish(rng, NewWord(rng), harsh)}";
    }

    private static string CreatureKey(string creatureId)
    {
        string id = creatureId.TrimStart('^').ToUpperInvariant();
        return id == "QUAD" ? "QUAD_PET" : id;
    }

    private static string NewWord(UniverseRandom rng)
    {
        string word = NameGenerator.Generate(rng, 5, 2, (int)rng.Next(3) + 3);
        return word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
    }

    /// <summary>Ending, rude-word filter, consonant-run vowel, filter (0x140eabcd0).</summary>
    private static string Finish(UniverseRandom rng, string word, bool harsh)
    {
        if (word.Length >= 2)
        {
            string ending = harsh ? HarshEnding(rng, word[^1]) : SoftEnding(rng, word[^1]);
            word = NameGenerator.Clean(rng, word + ending);
        }
        int run = ConsonantRun(word);
        if (run >= 0) word = word.Insert(run + 1, "aeiou"[(int)rng.Next(5)].ToString());
        return NameGenerator.Clean(rng, word);
    }

    private static string HarshEnding(UniverseRandom rng, char last) => last switch
    {
        'a' => (rng.NextWord() & 1) != 0 ? "st" : "sh",
        'e' => "sh",
        'i' or 'o' or 'u' => "ss",
        'x' or 'k' => "",
        _ => HarshEndings[rng.Next((uint)HarshEndings.Length)],
    };

    private static string SoftEnding(UniverseRandom rng, char last) => last switch
    {
        'a' => (rng.NextWord() & 1) != 0 ? "" : "zi",
        'e' => (rng.NextWord() & 1) != 0 ? "e" : "enia",
        'i' => (rng.NextWord() & 1) != 0 ? "" : "sta",
        'o' => (rng.NextWord() & 1) != 0 ? "" : "tto",
        'u' => (rng.NextWord() & 1) != 0 ? "y" : "i",
        _ => SoftEndings[rng.Next((uint)SoftEndings.Length)],
    };

    /// <summary>Three consonants then a letter that isn't a vowel or 'y': the
    /// run's start minus one, as 0x140ea0570 returns it; -1 when none.</summary>
    private static int ConsonantRun(string word)
    {
        int consonants = 0;
        for (int i = 0; i < word.Length; i++)
        {
            if (consonants == 3)
            {
                if (!"aeiouy".Contains(word[i])) return i - 3;
                consonants = 0;
            }
            consonants = "aeiou".Contains(word[i]) ? 0 : consonants + 1;
        }
        return -1;
    }

    /// <summary>2 or 3 parts joined by '-' or '/': "K42", "TO7", "42K", "K", "4".</summary>
    private static string RobotCode(UniverseRandom rng)
    {
        var code = new StringBuilder();
        int parts = (int)(rng.NextWord() >> 31) + 2;
        for (int i = 0; i < parts; i++)
        {
            uint kind = rng.Next(5);
            if (i != 0) code.Append((rng.NextWord() & 1) != 0 ? '-' : '/');
            switch (kind)
            {
                case 0:
                {
                    char letter = Letter(rng);
                    code.Append(letter).Append(rng.Next(99) + 1);
                    break;
                }
                case 1:
                    code.Append(Letter(rng)).Append(Letter(rng)).Append((char)('0' + rng.Next(10)));
                    break;
                case 2:
                {
                    char letter = Letter(rng);
                    code.Append(rng.Next(99) + 1).Append(letter);
                    break;
                }
                case 3:
                    code.Append(Letter(rng));
                    break;
                default:
                    code.Append((char)('0' + rng.Next(10)));
                    break;
            }
        }
        return code.ToString();
    }

    private static char Letter(UniverseRandom rng) => (char)('A' + rng.Next(26));

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
