using System.Text.Json;
using ArtifactX.Core.Procedural;

internal static class CreatureNameChecks
{
    /// <summary>The names the game gives companions never renamed (CreatureNames).
    /// The four Slot 8 companions are the owner's in-game Genetic Profile screens,
    /// 2026-11-04 (seed from XID + BirthTime XwC); the rest are NMS.exe's own routine
    /// (0x140eaa420) run in an emulator on random seeds, one per creature kind (all
    /// 560 such vectors matched). `path`: JSON with the catalog's "CRE" words.</summary>
    public static void Native(string path)
    {
        var words = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("No words in " + path);
        (string Id, ulong Birth, string Name)[] companions =
        [
            ("^FLYINGBEETLE", 1789662219, "Witchilly"),
            ("^PROTOROLLER", 1790704828, "Ana-Ana"),
            ("^CAT", 1790793453, "Beary-Beary"),
            ("^TRICERATOPS", 1790796728, "Bihi-Bihi"),
        ];
        (ulong Seed, string Id, string Name)[] native =
        [
            (0xD1EA2FA4DD9AF44C, "CAT", "Ikazi-Ikazi"),
            (0x5838B4E6615301CA, "TRICERATOPS", "Spellinnia"),
            (0x2B7E79E410F74398, "FLYINGBEETLE", "Starpito"),
            (0xBCAAB315A0835544, "PROTOROLLER", "Corpsilly"),
            (0x778B5B74047598D2, "FIEND", "Brocoss-Zunoss"),
            (0x1923ABDC7004C20D, "BUGFIEND", "Beetoxis"),
            (0xBD9A34AA7487C580, "SCUTTLER_PET", "Biziss"),
            (0xE63A934C7C335188, "LANDSQUID_PET", "Sai-Kehazi"),
            (0x35CC9FAF70AC4FE1, "QUAD_PET", "Soilox-83F/9/C74"),
            (0x1C312100CFAA60A8, "ROBO_PET", "Bigava-TO0-92L-SG6"),
            (0xBF6645DCB024924F, "HOVER_PET", "Beera-57G-E/8"),
            (0x21ABEA776CDD47E5, "ROBO_RODENT", "Wailella-PN4-N/KS8"),
            (0x7307157C489D7245, "SPIDERQUAD_PET", "Mammast-T52/ON4-44A"),
            (0xD9CB1389F3B0D984, "HORROR_PET", "Pollenaz"),
        ];
        foreach (var c in companions)
        {
            string? name = CreatureNames.Generate(CreatureNames.CompanionSeed(c.Id, c.Birth), c.Id, words["CRE"]);
            if (name != c.Name) throw new InvalidOperationException($"Companion {c.Id} born {c.Birth}: {name}, expected {c.Name}.");
        }
        foreach (var c in native)
        {
            string? name = CreatureNames.Generate(c.Seed, c.Id, words["CRE"]);
            if (name != c.Name) throw new InvalidOperationException($"{c.Id} {c.Seed:X16}: {name}, expected {c.Name}.");
        }
        Console.WriteLine($"Creature names: {companions.Length + native.Length} of {companions.Length + native.Length} match the game.");
    }
}
