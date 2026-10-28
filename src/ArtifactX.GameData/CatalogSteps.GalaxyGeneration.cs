using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// Region star-generation settings for <see cref="RegionStarGeneration"/>: the galaxy map's
    /// <c>DefaultGeneration</c> block and the goal-gap radius. The generator blends
    /// <c>DefaultGeneration</c> toward <c>FinalAnimationGeneration</c> by
    /// <c>FinalTransitionInterpolationValue</c> (NMS build 180836 <c>0x1421C2430</c>); that value
    /// is 0 in the shipped globals, so the default block applies unchanged. A non-zero value
    /// yields null rather than a guessed blend.
    /// </summary>
    private static GalaxyGenerationSettings? GalaxyGeneration(Func<string, NMSTemplate?> decode)
    {
        if (decode("GCGALAXYGLOBALS.GLOBAL.MBIN") is not GcGalaxyGlobals galaxy
            || decode("GCGAMEPLAYGLOBALS.GLOBAL.MBIN") is not GcGameplayGlobals gameplay
            || galaxy.FinalTransitionInterpolationValue != 0f || galaxy.DefaultGeneration is not { } g) return null;
        return new GalaxyGenerationSettings(g.BaseGenerationThreshold, g.StarGenerationThreshold, g.BaseTurbulenceGain,
            g.BaseTurbulenceLac, g.BaseTurbulenceScale, g.SpiralFormChance, gameplay.GoalGapVoxelDist);
    }

    /// <summary>Star-placement fields of the same <c>DefaultGeneration</c> block, for <see cref="RegionStarPositions"/>.</summary>
    private static GalaxyPlacementSettings? GalaxyPlacement(Func<string, NMSTemplate?> decode)
    {
        if (decode("GCGALAXYGLOBALS.GLOBAL.MBIN") is not GcGalaxyGlobals galaxy
            || galaxy.FinalTransitionInterpolationValue != 0f || galaxy.DefaultGeneration is not { } g) return null;
        return new GalaxyPlacementSettings(g.ConnectionAttractorMax.X, g.ConnectionAttractorMax.Y, g.ConnectionAttractorMin.X,
            g.ConnectionAttractorMin.Y, g.ConnectionDistortion.X, g.ConnectionDistortion.Y, g.ConnectionDistortionTMult, g.InnerFieldScales.Z);
    }
}
