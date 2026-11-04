using System.Text.Json;
using ArtifactX.Core.Procedural;

internal static class SpeciesNameChecks
{
    /// <summary>The owner's Slot 8 screens, 2026-11-04: the Fauna and Flora records
    /// and four companions' Genetic Profiles. `path` is a JSON object with the
    /// catalog's word lists "CRE" and "FLORA_ROOT" (MultiToolNameWords rows, format
    /// 69). Fauna seeds are the creature discovery's bEr[2]/bEr[3]; a companion's
    /// m9o/JrL; a plant's its Flora record's UqY[1].</summary>
    public static void Native(string path)
    {
        var words = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("No words in " + path);
        (ulong A, ulong B, string Name)[] fauna =
        [
            (0x821E109CA692CDD8, 0xB0469BED3AA33F7F, "J. Mustlimii"),       // Largest Herbivore
            (0x6C0591227C99D9C4, 0xEBA4AB296E139995, "H. Solarpohonum"),
            (0xD24985E2062EF782, 0xB939A63E5559C883, "C. Pollenkraniae"),
            (0x5EB65682E5AB822F, 0xEC2F079976825B3B, "K. Esoxziae"),
            (0xBE1E13D24290EBD4, 0x8B8962ADDEA70689, "G. Lycoratsiae"),
            (0xD43FAD2C9F2798AD, 0x481D2EB7025D5487, "I. Aippglutera"),     // vowel inserted
            (0xAFD90999006460E7, 0x65958A4278CA3F16, "Q. Tyranteareum"),
            (0x7C94B96039ED7648, 0xB039F6E31BC6118B, "G. Vombathea"),
            (0xEB2BD58B97C5304D, 0x03113BEA51F38E7E, "X. Muskwerum"),
            (0xFECEECA2753A5B41, 0x4130381FA274CF21, "G. Radionunum"),
            (0xD7C1F24DDE4851E8, 0xD5D1A87E996941E3, "U. Tritonlubesium"),
            (0xA3AC2B8B58223445, 0x5C9A6729375A8440, "W. Coldpetusleus"),
            (0x2D02D698B7C5BA14, 0x9884045C338B057D, "K. Gerriibae"),
            (0xA557FC119CC13663, 0x1BEF6BCC3C727C69, "Z. Wingeebus"),       // companion Witchilly
            (0x529DE40B76669644, 0xCE3C073914754A49, "A. Rainrobeus"),      // Ana-Ana
            (0x6A405C6060B3DEB5, 0xC62CB142C11C25FC, "F. Lobcineus"),       // Beary-Beary
            (0xB8FDF94A2C710CD4, 0x5C03D0E18D3116DF, "L. Leafuivateum"),    // Bihi-Bihi
        ];
        (ulong Seed, string Name)[] flora =
        [
            (0xA4A47A025E7E1C08, "N. Molarurfium"),
            (0x9C1065F733A9AFBF, "G. Norfdejorae"),
            (0xC4DA0E375D9EA5F0, "X. Pondbolima"),
            (0xEAB3FDA20E255880, "K. Kyloxarum"),
            (0xA7D20A4605370360, "O. Scornuihabima"),
            (0xF15DD728C17A9FE6, "K. Washurpium"),
            (0xC52E4C825E7A8194, "X. Luresoscea"),
            (0x46F02EFC0332C4EC, "O. Fumelefosa"),
        ];
        foreach (var c in fauna)
        {
            string? name = SpeciesNames.Fauna(c.A, c.B, words["CRE"]);
            if (name != c.Name) throw new InvalidOperationException($"Fauna {c.A:X16}/{c.B:X16}: {name}, expected {c.Name}.");
        }
        foreach (var c in flora)
        {
            string? name = SpeciesNames.Flora(c.Seed, words["FLORA_ROOT"]);
            if (name != c.Name) throw new InvalidOperationException($"Flora {c.Seed:X16}: {name}, expected {c.Name}.");
        }
        Console.WriteLine($"Species names: {fauna.Length + flora.Length} of {fauna.Length + flora.Length} match the game.");
    }
}
