namespace ArtifactX.Core.Universe;

/// <summary>
/// Pairs a planet's predicted creatures with the player's creature discoveries for it. A discovery's first seed
/// (bEr[0]) is the creature's role seed, which <see cref="PredictedCreature.Seed"/> predicts (96% of 2026's
/// discoveries in the owner's saves, fauna-check 2026-11-11), so those pair exactly. Discoveries the generator doesn't
/// predict (mostly made by older game versions) then fill the remaining predicted slots in order, so the number of
/// species shown still matches the planet's total; any left over after that are returned without a slot.
/// </summary>
public static class PlanetFaunaPairing
{
    /// <summary>One creature card: a predicted creature (null for a discovery beyond the predicted list) and the
    /// index of its discovery in the list passed in (-1 when not discovered yet).</summary>
    public readonly record struct Slot(PredictedCreature? Predicted, int Discovery);

    public static IReadOnlyList<Slot> Pair(IReadOnlyList<PredictedCreature> predicted, IReadOnlyList<ulong> discoverySeeds)
    {
        var used = new bool[discoverySeeds.Count];
        var assigned = new int[predicted.Count];
        Array.Fill(assigned, -1);

        for (int p = 0; p < predicted.Count; p++)
        {
            if (predicted[p].Seed == 0) continue;
            for (int d = 0; d < discoverySeeds.Count; d++)
                if (!used[d] && discoverySeeds[d] == predicted[p].Seed)
                {
                    used[d] = true;
                    assigned[p] = d;
                    break;
                }
        }

        int next = 0;
        for (int p = 0; p < predicted.Count; p++)
        {
            if (assigned[p] >= 0) continue;
            while (next < used.Length && used[next]) next++;
            if (next >= used.Length) break;
            used[next] = true;
            assigned[p] = next;
        }

        var slots = new List<Slot>(predicted.Count);
        for (int p = 0; p < predicted.Count; p++) slots.Add(new Slot(predicted[p], assigned[p]));
        for (int d = 0; d < used.Length; d++)
            if (!used[d]) slots.Add(new Slot(null, d));
        return slots;
    }
}
