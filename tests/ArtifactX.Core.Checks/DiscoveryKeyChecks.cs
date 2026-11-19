using ArtifactX.Core.Universe;

internal static class DiscoveryKeyChecks
{
    /// <summary>DiscoveryKeys.TypeHash against type hashes in the owner's Slot 8 discovery records (2026-11-19): a
    /// creature, a plant and a mineral, plus the spellings the game reduces to the same name (backslashes, lower case,
    /// a path without ".SCENE.MBIN").</summary>
    public static void Check()
    {
        (string Scene, ulong Hash)[] known =
        [
            ("MODELS/PLANETS/CREATURES/SMALLBIRD/BIRD.SCENE.MBIN", 0xE7CAD69E2F26FF55),
            ("MODELS/PLANETS/CREATURES/SPIDERRIG/SPIDER.SCENE.MBIN", 0x66153F23968A29EA),
            ("MODELS/PLANETS/BIOMES/SCORCHED/LARGE/LARGESPIRE.SCENE.MBIN", 0x9E250DEC48A267DE),
            ("MODELS/PLANETS/BIOMES/WEIRD/HYDROGARDEN/HYDROPODFLOAT.SCENE.MBIN", 0x3602A73F65F9374B),
            (@"models\planets\biomes\weird\hydrogarden\hydropodfloat.scene.mbin", 0x3602A73F65F9374B),
            ("MODELS/PLANETS/BIOMES/WEIRD/HYDROGARDEN/HYDROPODFLOAT", 0x3602A73F65F9374B),
        ];
        foreach (var (scene, hash) in known)
            if (DiscoveryKeys.TypeHash(scene) != hash)
                throw new InvalidOperationException($"Type hash of {scene}: {DiscoveryKeys.TypeHash(scene):X16}, expected {hash:X16}.");
        Console.WriteLine($"PASS: discovery type hashes ({known.Length} scene names).");
    }
}
