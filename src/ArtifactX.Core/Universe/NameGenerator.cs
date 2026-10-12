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

        if (register > 0)
        {
            int tries = 8;
            int i = 0;
            while (i < register)
            {
                string key = name.ToString(i, Math.Min(3, name.Length - i));
                weights[alphasetIndex & 7].TryGetValue(key, out var charWeights);
                double target = rng.NextWord() * TinyDouble;
                if (charWeights is null)
                {
                    tries--;
                    i--;
                    alphasetIndex = (alphasetIndex + 1) & 0x08000007;
                    if (alphasetIndex < 0) alphasetIndex = -alphasetIndex;

                    if (tries == 0)
                    {
                        if (add < 3) break;
                        string temp = CharactersFromAlphaset(rng, alphasetIndex);
                        if (Vowels.Contains(name[i + 2]) && Vowels.Contains(temp[0]))
                            name.Append('\'');
                        else if (!Vowels.Contains(temp[0]))
                            name.Append(Vowels[(int)(rng.Next(5) & 0xFF)]);
                        name.Append(temp);
                        tries = 1;
                        i += 2;
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

                if (name.Length > 63) name.Length = 64;
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
        return result;
    }

    /// <summary>Python's str.capitalize(): first letter upper, the rest lower.</summary>
    public static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
}
