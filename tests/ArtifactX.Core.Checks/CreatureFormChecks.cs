using ArtifactX.Core.Procedural;

/// <summary>A creature's two forms (ShipParts.Generate = female, GenerateMale = male). The real vectors - five species
/// read from the running game's spawn table - need the player's part trees: GameData.Checks --creature-form-vectors.
/// Here only what the game's two-stream selector guarantees on any tree: the top level matches the female's (its odds
/// are 0), and with odds capped at 1 below it, nested groups follow the second stream (Mix64 of the seed).</summary>
internal static class CreatureFormChecks
{
    public static void Check()
    {
        // A body with five heads; each head carries an ear group (six options) and a horn group (four).
        static PartOption Leaf(string id) => new(id, id, [], []);
        PartOption Head(int h) => new($"_HEAD_{h}", $"_Head_{h}", [],
            [[new PartGroup("_EARS_", Enumerable.Range(0, 6).Select(e => Leaf($"_EARS_{h}{e}")).ToList()),
              new PartGroup("_HORNS_", Enumerable.Range(0, 4).Select(e => Leaf($"_HORNS_{h}{e}")).ToList())]]);
        IReadOnlyList<PartGroup> tree = [new PartGroup("_HEAD_", Enumerable.Range(0, 5).Select(Head).ToList())];
        IReadOnlyList<PartGroup>? Load(string path) => path == "creature.descriptor.mbin" ? tree : null;

        int differ = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var female = ShipParts.Generate("creature.scene.mbin", seed * 0x9E3779B97F4A7C15UL, Load);
            var male = ShipParts.GenerateMale("creature.scene.mbin", seed * 0x9E3779B97F4A7C15UL, Load);
            if (female.Count != 3 || male.Count != 3)
                throw new InvalidOperationException($"seed {seed}: expected head, ears and horns in both forms");
            if (female[0].Id != male[0].Id)
                throw new InvalidOperationException($"seed {seed}: the male head {male[0].Id} differs from the female {female[0].Id}");
            if (female[1].Id != male[1].Id || female[2].Id != male[2].Id) differ++;
        }
        // Six ears times four horns: the two forms share both nested parts about 1 time in 24.
        if (differ < 150)
            throw new InvalidOperationException($"only {differ}/200 seeds gave the male different nested parts");
        Console.WriteLine($"Creature forms: top level shared, nested parts differ for {differ}/200 seeds OK");
    }
}
