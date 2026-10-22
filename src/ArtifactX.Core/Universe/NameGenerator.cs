using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ArtifactX.Core.Universe;

// Ported to C# from nms_namegen (MIT) by Stuart Coyle & had.sh & GoodGuysFree:
// https://github.com/hadsh/nms_namegen (generator.py) - see THIRD-PARTY-NOTICES.md.

/// <summary>
/// The game's procedural word builder, used for region, system and planet
/// names. Letter data (8 alphasets + per-3-letter weights) is the reference's,
/// shipped gzipped as an embedded resource. The reference walks a search tree
/// for the weights; it's stored here as a flat lookup, checked identical for
/// every possible 3-letter key in all 8 alphasets.
/// </summary>
public static class NameGenerator
{
    private const double TinyDouble = 2.3283064370807974e-10;
    private const string Vowels = "aeiou";

    private static readonly Lazy<(string[] Alphasets, Dictionary<string, (string Letter, double Weight)[]>[] Weights)> Data = new(Load);

    private static (string[], Dictionary<string, (string, double)[]>[]) Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArtifactX.Core.namegen-data.json.gz")
            ?? throw new InvalidOperationException("namegen-data resource missing");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gzip);
        var root = doc.RootElement;
        var alphasets = root.GetProperty("alphasets").EnumerateArray().Select(a => a.GetString()!).ToArray();
        var maps = new Dictionary<string, (string, double)[]>[8];
        var letterMap = root.GetProperty("letterMap");
        for (int i = 0; i < 8; i++)
        {
            var map = new Dictionary<string, (string, double)[]>(StringComparer.Ordinal);
            foreach (var entry in letterMap.GetProperty(i.ToString()).EnumerateObject())
                map[entry.Name] = entry.Value.EnumerateArray().Select(w => (w[0].GetString()!, w[1].GetDouble())).ToArray();
            maps[i] = map;
        }
        return (alphasets, maps);
    }

    private static string CharactersFromAlphaset(UniverseRandom rng, int alphasetIndex)
    {
        string alphaset = Data.Value.Alphasets[alphasetIndex & 7];
        int r = (int)rng.Next((uint)(alphaset.Length / 3)) * 3;
        return alphaset.Substring(r, 3);
    }

    private static string InsertVowel(string name, UniverseRandom rng, int index) =>
        name.Insert(index, Vowels[(int)rng.Next(5)].ToString());

    private static int ConsecutiveConsonants(string name)
    {
        int consonance = 0;
        for (int i = 0; i < name.Length; i++)
        {
            if (consonance < 3)
            {
                if (!Vowels.Contains(name[i])) consonance++;
                else consonance = 0;
            }
            else
            {
                if (!"aeiouy".Contains(name[i])) return i - 3;
                consonance = 0;
            }
        }
        return -1;
    }

    public static string Generate(UniverseRandom rng, int alphasetIndex, int minLength, int maxLength)
    {
        var weights = Data.Value.Weights;
        bool alternate = false;
        var name = new StringBuilder(CharactersFromAlphaset(rng, alphasetIndex));
        if ((rng.NextWord() & 1) != 0) alternate = true;

        int register = (int)rng.Next((uint)(maxLength - minLength + 1)) + minLength - 3;
        int add = register;

        // The loop below follows NMS.exe's own routine (build 180836, 0x140e81780, read
        // 2026-10-22), which differs from the reference port on the give-up path:
        // the next letter is looked up from the name's LAST three letters; a fresh chunk
        // comes from the ORIGINAL alphaset; the vowel test looks at name[i + 3] (i already
        // stepped back); and a joining vowel goes in only between two consonants.
        if (register > 0)
        {
            int startAlphaset = alphasetIndex;
            int tries = 8;
            int i = 0;
            while (i < register)
            {
                string key = name.ToString(Math.Max(0, name.Length - 3), Math.Min(3, name.Length));
                weights[alphasetIndex & 7].TryGetValue(key, out var charWeights);
                double target = (float)(rng.NextWord() * TinyDouble);
                if (charWeights is null)
                {
                    tries--;
                    i--;
                    alphasetIndex = (alphasetIndex + 1) & 7;

                    if (tries == 0)
                    {
                        if (add < 3) break;
                        string temp = CharactersFromAlphaset(rng, startAlphaset);
                        bool before = i + 3 >= 0 && i + 3 < name.Length && Vowels.Contains(name[i + 3]);
                        bool after = Vowels.Contains(temp[0]);
                        i += 3;
                        if (before && after) name.Append('\'');
                        else if (!before && !after) name.Append(Vowels[(int)rng.Next(5)]);
                        name.Append(temp);
                        tries = 1;
                        add -= 3;
                    }
                }
                else
                {
                    int index;
                    if (alternate)
                    {
                        target *= charWeights.Length - 1;
                        index = (int)(Math.Sign(target) * 0.5 + target);
                    }
                    else
                    {
                        double weight = 0.0;
                        int j = 0;
                        for (; j < charWeights.Length; j++)
                        {
                            weight += charWeights[j].Weight;
                            if (weight >= target) break;
                        }
                        index = Math.Min(j, charWeights.Length - 1);
                    }
                    name.Append(charWeights[index].Letter);
                    add--;
                }

                if (name.Length > 63) name.Length = 63;
                i++;
            }
        }

        string result = name.ToString();
        char first = result[0], second = result[1];

        // Insert a vowel at the start where needed.
        if (!Vowels.Contains(first) && !Vowels.Contains(second))
        {
            if (first != 's' || !"hklmnprtwy".Contains(second))
            {
                bool allowedPair = (second == 'h' && "ctw".Contains(first))
                    || (second == 'l' && "bcfgps".Contains(first))
                    || (second == 'r' && "bcdfgkpt".Contains(first))
                    || (second == 'w' && "dgt".Contains(first))
                    || (second == 'y' && "hmr".Contains(first));
                if (!allowedPair) result = InsertVowel(result, rng, 1);
            }
        }

        // Insert a vowel at the end where needed.
        if (result.Length > 1)
        {
            char ult = result[^1], penult = result[^2];
            if (penult != 'g' || Vowels.Contains(ult))
            {
                if ((ult == 'b' && "gn".Contains(penult))
                    || (ult == 'd' && "bdfghkmpst".Contains(penult))
                    || (ult == 'g' && penult == 'l')
                    || (ult == 'p' && "bdhkt".Contains(penult))
                    || (ult == 'r' && "bfg".Contains(penult))
                    || (ult == 't' && penult == 'g')
                    || (ult == 'w' && !Vowels.Contains(penult)))
                    result = InsertVowel(result, rng, result.Length - 1);
            }
        }

        // Insert vowels in the middle.
        int consonance = ConsecutiveConsonants(result);
        if (consonance != -1)
        {
            consonance += (int)rng.Next(3) + 1;
            result = InsertVowel(result, rng, consonance);
        }
        return Clean(rng, result);
    }

    /// <summary>The game's rude-word filter, the last step of every generated word (NMS.exe
    /// build 180836, 0x140e83680 calling 0x140e82390 per letter; the reference port lacks it).
    /// At each position, the first word found starting there has one letter swapped for a
    /// random pick - a draw that also shifts every later pick, which is why names such as
    /// "Rigie IV" (not "Nigie Major") came out wrong before (2026-10-22). Rules read
    /// straight from the routine: word, letter to swap, replacements.</summary>
    private static readonly (string Word, int Swap, string With)[] RudeWords =
    {
        ("anal", 1, "tdwzx"),
        ("anus", 1, "tdwzx"),
        ("ass", 0, "eo"),
        ("bitch", 3, "ao"),
        ("blow", 0, "caog"),
        ("boner", 0, "lvsz"),
        ("clit", 3, "cpfn"),
        ("cock", 0, "smlth"),
        ("cuck", 3, "aeiou"),
        ("cum", 0, "rgsn"),
        ("cunt", 1, "eio"),
        ("dick", 2, "aeol"),
        ("dong", 3, "teo"),
        ("fag", 0, "nmtz"),
        ("felch", 0, "njkmp"),
        ("fuck", 0, "zlbw"),
        ("gook", 3, "lnrb"),
        ("hitler", 0, "vmpl"),
        ("homo", 1, "eiu"),
        ("jizz", 0, "zrwl"),
        ("kike", 2, "rbl"),
        ("kunt", 3, "aeo"),
        ("love", 2, "mpbt"),
        ("minge", 3, "amlz"),
        ("mong", 3, "aoik"),
        ("nazi", 0, "ryvn"),
        ("negr", 0, "rvlt"),
        ("nig", 0, "rvxb"),
        ("orgy", 2, "vlos"),
        ("paedo", 0, "tmvs"),
        ("paki", 2, "ntr"),
        ("pedo", 0, "tmvs"),
        ("penis", 0, "klsr"),
        ("piss", 2, "cokl"),
        ("porn", 0, "tvbr"),
        ("prick", 0, "stbc"),
        ("puss", 0, "brsl"),
        ("quee", 2, "nlr"),
        ("rape", 2, "cnys"),
        ("semen", 2, "tzrv"),
        ("sex", 0, "tdrp"),
        ("shag", 3, "nml"),
        ("shit", 3, "nvsl"),
        ("slag", 3, "dnvy"),
        ("slut", 2, "aeo"),
        ("spaz", 3, "nlr"),
        ("spic", 3, "nlr"),
        ("spunk", 1, "lh"),
        ("tard", 0, "jcgwz"),
        ("tit", 2, "nmbc"),
        ("turd", 3, "nbtp"),
        ("twat", 1, "ehi"),
        ("wank", 3, "aei"),
        ("whore", 0, "csa"),
        ("wog", 0, "cslv"),
        ("wop", 0, "csld"),
    };

    private static string Clean(UniverseRandom rng, string word)
    {
        var letters = word.ToCharArray();
        for (int i = 0; i < letters.Length; i++)
            foreach (var (bad, swap, with) in RudeWords)
            {
                if (i + bad.Length > letters.Length) continue;
                int k = 0;
                while (k < bad.Length && letters[i + k] == bad[k]) k++;
                if (k < bad.Length) continue;
                letters[i + swap] = with[(int)rng.Next((uint)with.Length)];
                break;
            }
        return new string(letters);
    }

    /// <summary>Python's str.capitalize(): first letter upper, the rest lower.</summary>
    public static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
}
