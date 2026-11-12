using ArtifactX.Core.Companions;

internal static class PetBattlerClassChecks
{
    /// <summary>Two creatures the owner scanned on 2026-11-11 (analysis visor "Trait Potential" panels): their species
    /// and genus seeds give the classes the visor showed. Weights: GcGameTableGlobals CoreStatClassRollTable
    /// (30/30/25/15 per stat in build 185034).</summary>
    public static void Check()
    {
        int[][] rolls = [[30, 30, 25, 15], [30, 30, 25, 15], [30, 30, 25, 15]];
        // H. Scrofbuyae: Combat S, Agility B, Health S, Trait Potential S. N. Dasiythrodia: B, A, S, potential A.
        Expect(0x414E3B61267F7B53, 0x8A948FB7E427F927, rolls, [3, 1, 3], 3, "H. Scrofbuyae");
        Expect(0x364A6A3FEE6C4EFA, 0x59B3DA4E39C624DC, rolls, [3, 2, 1], 2, "N. Dasiythrodia");
        Console.WriteLine("PASS: pet-battle classes (XXH3 rolls) match two creatures' analysis visor panels.");
    }

    // Classes in GcPetBattlerCoreStat order: MaxHealth (Health), Speed (Agility), CombatPotential (Combat).
    private static void Expect(ulong species, ulong genus, int[][] rolls, int[] classes, int potential, string name)
    {
        var (got, gotPotential) = PetBattlerClasses.Roll(species, genus, rolls);
        if (!got.SequenceEqual(classes) || gotPotential != potential)
            throw new InvalidOperationException($"{name}: classes {string.Join(",", got)} potential {gotPotential}, expected {string.Join(",", classes)} / {potential}.");
    }
}
