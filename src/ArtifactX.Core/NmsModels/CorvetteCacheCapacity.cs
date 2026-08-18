namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Corvette Workshop Cache grid dimensions. CONFIRMED against real save data:
/// the container's own =Tb/N9> fields (the same pair that hold Exosuit Tech's
/// 10x6 and Cargo's 10x12) read 10x16, and hl?.Count == 160 - matching the
/// in-game panel's own "CORVETTE WORKSHOP CACHE INVENTORY (160 SLOTS)" label
/// exactly. Single fixed constant, not a catalog lookup - there's only ever
/// one Corvette Workshop, with no Class/Type dimension to scale by.
/// </summary>
public static class CorvetteCacheCapacity
{
    public const int Columns = 10;
    public const int Rows = 16;
}
