namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Base Storage Container ("Chest" - WA4.rri) grid dimensions. CONFIRMED, not
/// a guess: all 10 of a real player's containers showed unlocked positions
/// spanning exactly X 0-9, Y 0-4, and every one had hl?.Count == 50 - unlike
/// ships/multi-tools/freighter, storage containers have no Class-based
/// capacity scaling (B@N.1o6 was uniformly "C" across every container
/// sampled, with no in-game way to change it), so this is a single fixed
/// constant, not a catalog lookup.
/// </summary>
public static class BaseStorageCapacity
{
    public const int Columns = 10;
    public const int Rows = 5;
}
