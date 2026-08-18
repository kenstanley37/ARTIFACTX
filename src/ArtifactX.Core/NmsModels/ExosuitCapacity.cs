namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Exosuit Technology/Cargo max grid dimensions. Confirmed NOT class-gated.
/// Grid shape confirmed directly from a real, far-progressed save (10 columns
/// wide for both containers - a save with fewer unlocked columns can look
/// narrower than the true grid, which is exactly what happened during initial
/// investigation). Totals (60 Tech, 120 Cargo) match community-documented
/// current-version numbers, now with a verified column/row shape.
/// If Hello Games changes these values, only this file needs updating.
/// </summary>
public static class ExosuitCapacity
{
    public const int TechnologyColumns = 10;
    public const int TechnologyRows = 6;   // 10 x 6 = 60, confirmed

    public const int CargoColumns = 10;
    public const int CargoRows = 12;       // 10 x 12 = 120, confirmed
}