using System;
using System.Numerics;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// A planetary deep-level resource hotspot discovered via the Survey Device.
/// Generated from the planet's procedural seed and coordinate grid (native 0x1412F4AC0 / HotspotNeighborhood).
/// </summary>
public sealed record PlanetHotspot(
    ulong PlanetAddress,
    string Kind,
    string ResourceId,
    string ResourceName,
    string Class,
    int ClassGrade,
    float Strength,
    int YieldPercent,
    float Range,
    double Latitude,
    double Longitude,
    string Coordinates,
    double Distance,
    string DistanceLabel,
    double? PortalDistance,
    string? PortalDistanceLabel,
    ulong CandidateKey,
    Vector3 LocalPosition)
{
    public string ClassBadgeKey => Class switch
    {
        "S" => "ThemeWarningBrush",     // Gold / Amber
        "A" => "ThemeAccentBrush",      // Purple / Violet
        "B" => "ThemeTextBrush",        // Bright Text / Cyan
        _ => "ThemeTextDimBrush"        // Slate / Dim
    };

    public string KindBadgeKey => Kind switch
    {
        "Power" => "ThemeWarningBrush", // Yellow / Energy
        "Mineral" => "ThemeAccentBrush", // Blue / Metal
        "Gas" => "ThemeDoneBrush",      // Green / Atmosphere
        _ => "ThemeTextDimBrush"
    };

    public string KindGlyph => Kind switch
    {
        "Power" => "\uE945",           // Lightning bolt
        "Mineral" => "\uE80F",         // Mining / Ore
        "Gas" => "\uE753",             // Cloud / Atmosphere
        _ => "\uE90A"
    };
}
