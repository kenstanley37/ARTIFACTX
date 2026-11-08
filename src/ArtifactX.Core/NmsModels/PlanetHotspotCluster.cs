using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ArtifactX.Core.NmsModels;

public enum HotspotComboType
{
    Trifecta,       // Power + Mineral + Gas (The Holy Grail)
    PowerMineral,   // Power + Mineral (Self-powered mine)
    PowerGas,       // Power + Gas (Self-powered atmospheric extractor)
    MineralGas,     // Mineral + Gas
    DualMineral,    // Two distinct minerals
    DualPower,      // Two power hotspots
    OtherTriple,    // 3 hotspots of other combinations
}

/// <summary>
/// A cluster of 2 or 3 distinct deep-level extraction hotspots within single base perimeter.
/// Provides the mathematically optimal Base Computer placement coordinate (minimum enclosing circle)
/// to simultaneously reach and extract all sites from a single base.
/// </summary>
public sealed record PlanetHotspotCluster(
    ulong PlanetAddress,
    HotspotComboType ComboType,
    string Title,
    IReadOnlyList<PlanetHotspot> Hotspots,
    double MaxRadius,
    double MaxSpan,
    double BaseLatitude,
    double BaseLongitude,
    string BaseCoordinates,
    Vector3 BaseLocalPosition,
    double? PortalDistance,
    string? PortalDistanceLabel,
    ulong ClusterKey)
{
    public string ComboBadgeKey => ComboType switch
    {
        HotspotComboType.Trifecta => "ThemeWarningBrush",     // Gold / Legendary
        HotspotComboType.PowerMineral => "ThemeAccentBrush",  // Violet / Primary
        HotspotComboType.PowerGas => "ThemeDoneBrush",        // Green / Energy+Gas
        _ => "ThemeTextBrush"
    };

    public string ComboGlyph => ComboType switch
    {
        HotspotComboType.Trifecta => "\uE735",       // Star / Crown
        HotspotComboType.PowerMineral => "\uE945",   // Lightning bolt
        HotspotComboType.PowerGas => "\uE753",       // Cloud
        _ => "\uE80F"                                // Mining
    };

    public string ReachDescription =>
        $"Base Computer placed here reaches all {Hotspots.Count} hotspots within {Math.Round(MaxRadius)}u (sites span {Math.Round(MaxSpan)}u apart).";

    /// <summary>
    /// Finds multi-hotspot clusters (pairs and triplets) within base building range.
    /// Calculates the optimal Base Computer coordinate (minimum enclosing circle) for each cluster.
    /// </summary>
    public static IReadOnlyList<PlanetHotspotCluster> Find(
        IReadOnlyList<PlanetHotspot> hotspots,
        double maxBaseRadius = 375.0,
        double maxSpan = 650.0,
        double? portalLat = null,
        double? portalLon = null)
    {
        if (hotspots == null || hotspots.Count < 2) return Array.Empty<PlanetHotspotCluster>();

        float planetRadius = hotspots[0].LocalPosition.Length();
        if (planetRadius < 1000f) planetRadius = 98304f;

        Vector3? portalDir = null;
        if (portalLat.HasValue && portalLon.HasValue)
        {
            double pLatRad = portalLat.Value * Math.PI / 180.0;
            double pLonRad = portalLon.Value * Math.PI / 180.0;
            portalDir = new Vector3(
                (float)(Math.Cos(pLatRad) * Math.Sin(pLonRad)),
                (float)Math.Sin(pLatRad),
                (float)(Math.Cos(pLatRad) * Math.Cos(pLonRad))
            );
        }

        var clusters = new List<PlanetHotspotCluster>();
        int count = hotspots.Count;

        // 1. Triplets (look for 3-way combos, especially Trifectas: Power + Mineral + Gas)
        for (int i = 0; i < count; i++)
        {
            var h1 = hotspots[i];
            for (int j = i + 1; j < count; j++)
            {
                var h2 = hotspots[j];
                float d12 = Vector3.Distance(h1.LocalPosition, h2.LocalPosition);
                if (d12 > maxSpan) continue;

                for (int k = j + 1; k < count; k++)
                {
                    var h3 = hotspots[k];
                    float d23 = Vector3.Distance(h2.LocalPosition, h3.LocalPosition);
                    if (d23 > maxSpan) continue;

                    float d31 = Vector3.Distance(h3.LocalPosition, h1.LocalPosition);
                    if (d31 > maxSpan) continue;

                    // Require at least 2 distinct kinds in a 3-way cluster
                    int distinctKinds = (new[] { h1.Kind, h2.Kind, h3.Kind }).Distinct().Count();
                    if (distinctKinds < 2) continue;

                    var (center, enclosingRadius) = MinEnclosingCircle(h1.LocalPosition, h2.LocalPosition, h3.LocalPosition);
                    if (enclosingRadius > maxBaseRadius) continue;

                    Vector3 baseDir = Vector3.Normalize(center);
                    Vector3 baseLocalPos = baseDir * planetRadius;

                    // Compute true surface distances
                    double sDist1 = SurfaceDistance(baseDir, h1.LocalPosition, planetRadius);
                    double sDist2 = SurfaceDistance(baseDir, h2.LocalPosition, planetRadius);
                    double sDist3 = SurfaceDistance(baseDir, h3.LocalPosition, planetRadius);
                    double trueMaxRadius = Math.Max(sDist1, Math.Max(sDist2, sDist3));
                    if (trueMaxRadius > maxBaseRadius) continue;

                    double maxEdge = Math.Max(d12, Math.Max(d23, d31));
                    double baseLat = Math.Asin(Math.Clamp(baseDir.Y, -1f, 1f)) * (180.0 / Math.PI);
                    double baseLon = Math.Atan2(baseDir.X, baseDir.Z) * (180.0 / Math.PI);

                    double? portalDist = null;
                    string? portalDistLabel = null;
                    if (portalDir.HasValue)
                    {
                        portalDist = SurfaceDistance(portalDir.Value, baseLocalPos, planetRadius);
                        portalDistLabel = portalDist < 1000
                            ? $"{Math.Round(portalDist.Value)}u from portal"
                            : $"{portalDist.Value / 1000.0:F1} km from portal";
                    }

                    var list = new[] { h1, h2, h3 };
                    var comboType = Classify(list);
                    string title = TitleFor(comboType, list);

                    ulong clusterKey = 0xBC00000000000000UL ^ h1.CandidateKey ^ (h2.CandidateKey << 1) ^ (h3.CandidateKey << 2);

                    clusters.Add(new PlanetHotspotCluster(
                        PlanetAddress: h1.PlanetAddress,
                        ComboType: comboType,
                        Title: title,
                        Hotspots: list,
                        MaxRadius: trueMaxRadius,
                        MaxSpan: maxEdge,
                        BaseLatitude: baseLat,
                        BaseLongitude: baseLon,
                        BaseCoordinates: NmsPlanetCoordinates.Format(baseLat, baseLon),
                        BaseLocalPosition: baseLocalPos,
                        PortalDistance: portalDist,
                        PortalDistanceLabel: portalDistLabel,
                        ClusterKey: clusterKey
                    ));
                }
            }
        }

        // 2. Pairs (look for 2-way combos, prioritizing Power + Mineral, Power + Gas)
        for (int i = 0; i < count; i++)
        {
            var h1 = hotspots[i];
            for (int j = i + 1; j < count; j++)
            {
                var h2 = hotspots[j];
                float span = Vector3.Distance(h1.LocalPosition, h2.LocalPosition);
                if (span > maxSpan) continue;

                // Check if already covered by an existing 3-way cluster with the same kinds
                bool coveredByTriple = clusters.Any(c => c.Hotspots.Count == 3
                    && c.Hotspots.Contains(h1) && c.Hotspots.Contains(h2));
                if (coveredByTriple && (h1.Kind == h2.Kind)) continue;

                var (center, enclosingRadius) = MinEnclosingCircle(h1.LocalPosition, h2.LocalPosition);
                if (enclosingRadius > maxBaseRadius) continue;

                Vector3 baseDir = Vector3.Normalize(center);
                Vector3 baseLocalPos = baseDir * planetRadius;

                double sDist1 = SurfaceDistance(baseDir, h1.LocalPosition, planetRadius);
                double sDist2 = SurfaceDistance(baseDir, h2.LocalPosition, planetRadius);
                double trueMaxRadius = Math.Max(sDist1, sDist2);
                if (trueMaxRadius > maxBaseRadius) continue;

                double baseLat = Math.Asin(Math.Clamp(baseDir.Y, -1f, 1f)) * (180.0 / Math.PI);
                double baseLon = Math.Atan2(baseDir.X, baseDir.Z) * (180.0 / Math.PI);

                double? portalDist = null;
                string? portalDistLabel = null;
                if (portalDir.HasValue)
                {
                    portalDist = SurfaceDistance(portalDir.Value, baseLocalPos, planetRadius);
                    portalDistLabel = portalDist < 1000
                        ? $"{Math.Round(portalDist.Value)}u from portal"
                        : $"{portalDist.Value / 1000.0:F1} km from portal";
                }

                var list = new[] { h1, h2 };
                var comboType = Classify(list);
                string title = TitleFor(comboType, list);

                ulong clusterKey = 0xBC00000000000000UL ^ h1.CandidateKey ^ (h2.CandidateKey << 1);

                clusters.Add(new PlanetHotspotCluster(
                    PlanetAddress: h1.PlanetAddress,
                    ComboType: comboType,
                    Title: title,
                    Hotspots: list,
                    MaxRadius: trueMaxRadius,
                    MaxSpan: span,
                    BaseLatitude: baseLat,
                    BaseLongitude: baseLon,
                    BaseCoordinates: NmsPlanetCoordinates.Format(baseLat, baseLon),
                    BaseLocalPosition: baseLocalPos,
                    PortalDistance: portalDist,
                    PortalDistanceLabel: portalDistLabel,
                    ClusterKey: clusterKey
                ));
            }
        }

        // Rank clusters:
        // 1. Trifectas first
        // 2. Power + Mineral or Power + Gas next
        // 3. Highest total class grades (S=3, A=2, B=1, C=0)
        // 4. Tightest base radius first
        return clusters
            .OrderBy(c => c.ComboType switch
            {
                HotspotComboType.Trifecta => 0,
                HotspotComboType.PowerMineral => 1,
                HotspotComboType.PowerGas => 2,
                HotspotComboType.MineralGas => 3,
                _ => 4
            })
            .ThenByDescending(c => c.Hotspots.Sum(h => h.ClassGrade))
            .ThenBy(c => c.MaxRadius)
            .ToList();
    }

    private static double SurfaceDistance(Vector3 dir1, Vector3 pos2, float radius)
    {
        Vector3 dir2 = Vector3.Normalize(pos2);
        float dot = Math.Clamp(Vector3.Dot(dir1, dir2), -1f, 1f);
        return Math.Acos(dot) * radius;
    }

    private static (Vector3 Center, double Radius) MinEnclosingCircle(Vector3 p1, Vector3 p2)
    {
        return ((p1 + p2) * 0.5f, Vector3.Distance(p1, p2) * 0.5f);
    }

    private static (Vector3 Center, double Radius) MinEnclosingCircle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float d12_sq = Vector3.DistanceSquared(p1, p2);
        float d23_sq = Vector3.DistanceSquared(p2, p3);
        float d31_sq = Vector3.DistanceSquared(p3, p1);

        if (d23_sq >= d12_sq + d31_sq)
            return ((p2 + p3) * 0.5f, MathF.Sqrt(d23_sq) * 0.5f);
        if (d31_sq >= d12_sq + d23_sq)
            return ((p3 + p1) * 0.5f, MathF.Sqrt(d31_sq) * 0.5f);
        if (d12_sq >= d23_sq + d31_sq)
            return ((p1 + p2) * 0.5f, MathF.Sqrt(d12_sq) * 0.5f);

        Vector3 v1 = p2 - p1;
        Vector3 v2 = p3 - p1;
        Vector3 n = Vector3.Cross(v1, v2);
        float n_len_sq = n.LengthSquared();
        if (n_len_sq < 1e-6f)
        {
            float longest_sq = MathF.Max(d12_sq, MathF.Max(d23_sq, d31_sq));
            if (longest_sq == d12_sq) return ((p1 + p2) * 0.5f, MathF.Sqrt(d12_sq) * 0.5f);
            if (longest_sq == d23_sq) return ((p2 + p3) * 0.5f, MathF.Sqrt(d23_sq) * 0.5f);
            return ((p3 + p1) * 0.5f, MathF.Sqrt(d31_sq) * 0.5f);
        }

        Vector3 term1 = Vector3.Cross(v2, n);
        Vector3 term2 = Vector3.Cross(n, v1);
        Vector3 offset = (term1 * v1.LengthSquared() + term2 * v2.LengthSquared()) * (1.0f / (2.0f * n_len_sq));
        return (p1 + offset, offset.Length());
    }

    private static HotspotComboType Classify(IReadOnlyList<PlanetHotspot> list)
    {
        bool hasPower = list.Any(h => h.Kind == "Power");
        bool hasMin = list.Any(h => h.Kind == "Mineral");
        bool hasGas = list.Any(h => h.Kind == "Gas");

        if (list.Count == 3)
        {
            if (hasPower && hasMin && hasGas) return HotspotComboType.Trifecta;
            if (hasPower && hasMin) return HotspotComboType.PowerMineral;
            if (hasPower && hasGas) return HotspotComboType.PowerGas;
            return HotspotComboType.OtherTriple;
        }

        if (hasPower && hasMin) return HotspotComboType.PowerMineral;
        if (hasPower && hasGas) return HotspotComboType.PowerGas;
        if (hasMin && hasGas) return HotspotComboType.MineralGas;
        if (list.Count(h => h.Kind == "Mineral") == 2) return HotspotComboType.DualMineral;
        if (list.Count(h => h.Kind == "Power") == 2) return HotspotComboType.DualPower;
        return HotspotComboType.PowerMineral;
    }

    private static string TitleFor(HotspotComboType type, IReadOnlyList<PlanetHotspot> list) => type switch
    {
        HotspotComboType.Trifecta => $"TRIFECTA COMBO ({list.Count} SITES)",
        HotspotComboType.PowerMineral => $"POWER + MINERAL ({list.Count} SITES)",
        HotspotComboType.PowerGas => $"POWER + GAS ({list.Count} SITES)",
        HotspotComboType.MineralGas => $"MINERAL + GAS ({list.Count} SITES)",
        HotspotComboType.DualMineral => $"DUAL MINERAL ({list.Count} SITES)",
        HotspotComboType.DualPower => $"DUAL POWER ({list.Count} SITES)",
        _ => $"RESOURCE CLUSTER ({list.Count} SITES)"
    };
}
