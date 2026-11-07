namespace ArtifactX.Core.Universe;

/// <summary>
/// Initial crashed-ship technology damage, separate from empty broken inventory cells.
/// Steam build 26295378, inspected 2026-10-30: 0x1404DB840 writes damage 1 at
/// installed entry +0x1c. SHIPJUMP1 and HYPERDRIVE are always damaged and consume
/// no draws; each other installed entry consumes one site-seeded MWC draw.
/// The chance comes from GcAISpaceshipGlobals.CrashedShipBrokenTechChance (zero
/// for the Sentinel caller at 0x141747908). This does not evaluate adjacency.
/// Checked against 120 native runs / 840 flags, including zero/one probabilities,
/// zero/all-bits-set seeds and the extracted chance; no unmapped emulator reads.
/// </summary>
public static class StarshipTechnologyDamageGeneration
{
    /// <summary>
    /// Returns damage flags in the supplied installation order, not coordinate order.
    /// Pass the extracted chance explicitly; missing or invalid rules yield no result.
    /// The two forced IDs are executable comparisons, not a catalog technology list.
    /// </summary>
    public static bool[]? InitialDamage(ulong siteKey, IReadOnlyList<string> installedIds,
        float brokenTechnologyChance)
    {
        if (!float.IsFinite(brokenTechnologyChance) || brokenTechnologyChance is < 0f or > 1f
            || installedIds.Any(string.IsNullOrWhiteSpace)) return null;

        var rng = SolarLayoutGeneration.RandomFor(siteKey);
        var result = new bool[installedIds.Count];
        for (int i = 0; i < installedIds.Count; i++)
        {
            if (installedIds[i] is "SHIPJUMP1" or "HYPERDRIVE")
                result[i] = true;
            else
            {
                float roll = (float)(rng.NextWord() * 2.3283064370807974e-10);
                result[i] = roll < brokenTechnologyChance;
            }
        }
        return result;
    }
}
