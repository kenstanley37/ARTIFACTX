namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Multi-tool Technology grid max dimensions. UNLIKE ExosuitCapacity, this has
/// NOT been fully confirmed against a maxed-out save - the only real data point
/// so far is a save with 18 unlocked slots, which only proves the true grid is
/// at least that large, not its exact shape. 8x3 (24) matches widely-repeated
/// community numbers for multi-tool tech capacity, but treat this as a
/// best-guess starting point, not a verified constant like ExosuitCapacity -
/// revisit if a save with more than 24 unlocked slots ever turns up.
/// </summary>
public static class MultiToolCapacity
{
    public const int TechnologyColumns = 10;
    public const int TechnologyRows = 6;
}