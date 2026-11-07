namespace ArtifactX.Core.Recipes;

/// <summary>Resource-yield definition extracted from GcRegionHotspotSubstance.
/// AmountCost is internal link-grid units per whole SubstanceYeild batch.</summary>
public sealed record ExtractionSubstance(string Id, int AmountCost, int Yield);

/// <summary>Native stock conversion at 0x1412F5400 in the executable inspected
/// 2026-11-01 divides signed internal stock by AmountCost BEFORE multiplying
/// by SubstanceYeild. Fractional batches are not displayed as item counts.
/// Preserved Paraffinium saves resolve to the owner's observed 6750 and 7000.
/// This only converts stock; it does not establish resource identity, network
/// grouping, future production or offline accrual. Unsupported integer-overflow
/// inputs are rejected instead of reproducing negative/wrapped inventory.</summary>
public static class ExtractionStock
{
    public static long DisplayAmount(long storedUnits, ExtractionSubstance resource)
    {
        if (storedUnits is < 0 or > int.MaxValue || resource.AmountCost <= 0 || resource.Yield <= 0)
            throw new ArgumentOutOfRangeException(nameof(storedUnits), "Unsupported extraction stock or yield definition.");
        return checked((int)(storedUnits / resource.AmountCost) * resource.Yield);
    }
}
