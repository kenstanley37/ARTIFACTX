namespace ArtifactX.Core.Universe;

/// <summary>
/// Every region's purple systems: indices <see cref="Start"/>..<see cref="Start"/> + <see cref="Count"/> - 1 (1000..1064).
/// Not game-file data: NMS.exe writes them into each region's GcGalaxyVoxelAttributesData unconditionally
/// (PurpleSystemsCount 0x41, PurpleSystemsStart 0x3E8 at 0x14132AEBE..0x14132AEC5, build 180836;
/// docs/research/REGION_STAR_COUNT.md). The one place these numbers live in the app.
/// Open question (issue #1): RegionStarPositions numbers its purple stars 1001..1065 (and the region map treats
/// index > 1000 as purple), while this range and the system generator say 1000..1064. Saves seen so far hold neither
/// 1000 nor 1065, so which end is real is unconfirmed in game.
/// </summary>
public static class PurpleSystems
{
    public const int Start = 1000;
    public const int Count = 65;

    /// <summary>A system index in the purple range.</summary>
    public static bool Contains(long systemIndex) => systemIndex >= Start && systemIndex < Start + Count;
}
