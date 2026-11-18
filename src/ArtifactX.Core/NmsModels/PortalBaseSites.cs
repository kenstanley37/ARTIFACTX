using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Base computer spots whose boundary reaches the planet's portal and one to three deep-level hotspots (#63): most
/// players build at the portal, and a power hotspot inside the base saves a field of solar panels. The spot is the
/// centre of the smallest circle around the portal and the hotspots, on the ground; it qualifies when every one of
/// them lies within <c>reach</c> of it along the surface. Reach is a new base's boundary from the catalog
/// (GcBuildingGlobals.MinRadiusForBases). Ranked: power first, then more hotspots, better grades, and a base computer
/// closer to the portal.
/// </summary>
public static class PortalBaseSites
{
    /// <summary>The ranked spots. <paramref name="portalDirection"/> is the portal's unit direction from the planet centre;
    /// hotspot positions are planet-local (direction x ground radius).</summary>
    public static IReadOnlyList<PlanetHotspotCluster> Find(IReadOnlyList<PlanetHotspot> hotspots, Vector3 portalDirection,
        float reach, float planetRadius, PlanetTerrainData? terrain = null)
    {
        if (hotspots.Count == 0 || reach <= 0f || planetRadius <= 0f) return Array.Empty<PlanetHotspotCluster>();
        Vector3 portal = Vector3.Normalize(portalDirection) * planetRadius;
        var near = hotspots.Where(h => Surface(Vector3.Normalize(portal), h.LocalPosition, planetRadius) <= 2 * reach).ToList();
        var sites = new List<PlanetHotspotCluster>();
        for (int i = 0; i < near.Count; i++)
        {
            Add([near[i]]);
            for (int j = i + 1; j < near.Count; j++)
            {
                Add([near[i], near[j]]);
                for (int k = j + 1; k < near.Count; k++) Add([near[i], near[j], near[k]]);
            }
        }
        return sites
            .OrderByDescending(s => s.Hotspots.Any(h => h.Kind == "Power"))
            .ThenByDescending(s => s.Hotspots.Count)
            .ThenByDescending(s => s.Hotspots.Sum(h => h.ClassGrade))
            .ThenBy(s => s.PortalDistance)
            .ToList();

        void Add(PlanetHotspot[] set)
        {
            var points = set.Select(h => h.LocalPosition).Append(portal).ToArray();
            // Circle maths relative to the portal: at ~100 km from the planet centre a float resolves only ~1 cm, so points
            // on the circle's edge failed its containment test and a worse circle won.
            var (centre, _) = SmallestCircle(points.Select(p => p - portal).ToArray());
            Vector3 dir = Vector3.Normalize(centre + portal);
            double farthest = points.Max(p => Surface(dir, p, planetRadius));
            if (farthest > reach) return;
            float ground = planetRadius;
            if (terrain is not null)
                ground += PlanetTerrain.SurfaceHeight(terrain, dir.X * planetRadius, dir.Y * planetRadius, dir.Z * planetRadius, planetRadius);
            double lat = Math.Asin(Math.Clamp(dir.Y, -1f, 1f)) * (180.0 / Math.PI);
            double lon = Math.Atan2(dir.X, dir.Z) * (180.0 / Math.PI);
            double toPortal = Surface(dir, portal, planetRadius);
            double span = set.Length < 2 ? 0 : set.SelectMany(a => set, (a, b) => (double)Vector3.Distance(a.LocalPosition, b.LocalPosition)).Max();
            ulong key = 0xB0A7000000000000UL;
            for (int n = 0; n < set.Length; n++) key ^= set[n].CandidateKey << n;
            sites.Add(new PlanetHotspotCluster(set[0].PlanetAddress,
                set.Length == 1 ? HotspotComboType.Single : PlanetHotspotCluster.Classify(set),
                "PORTAL + " + string.Join(" + ", set.Select(h => h.Kind.ToUpperInvariant())),
                set, farthest, span, lat, lon, NmsPlanetCoordinates.Format(lat, lon), dir * ground,
                toPortal, toPortal < 1000 ? $"{Math.Round(toPortal)}u from portal" : $"{toPortal / 1000.0:F1} km from portal", key,
                IncludesPortal: true));
        }
    }

    /// <summary>The power hotspot closest to the portal along the ground, for "too far for one base" advice.</summary>
    public static (PlanetHotspot Hotspot, double Distance)? NearestPower(IReadOnlyList<PlanetHotspot> hotspots, Vector3 portalDirection, float planetRadius)
    {
        Vector3 dir = Vector3.Normalize(portalDirection);
        return hotspots.Where(h => h.Kind == "Power")
            .Select(h => (Hotspot: h, Distance: Surface(dir, h.LocalPosition, planetRadius)))
            .OrderBy(p => p.Distance)
            .Select(p => ((PlanetHotspot, double)?)p)
            .FirstOrDefault();
    }

    private static double Surface(Vector3 direction, Vector3 position, float radius) =>
        PlanetHotspotCluster.GroundDistance(direction, position, radius);

    /// <summary>Smallest circle around up to four nearby points (a few hundred metres apart, so the ground is flat
    /// enough): the best of every pair's and every triple's circle that holds them all.</summary>
    internal static (Vector3 Centre, float Radius) SmallestCircle(IReadOnlyList<Vector3> points)
    {
        (Vector3 Centre, float Radius) best = (points[0], points.Count == 1 ? 0f : float.MaxValue);
        bool Holds((Vector3 C, float R) c) => points.All(p => Vector3.Distance(c.C, p) <= c.R + 0.01f);
        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
            {
                var pair = ((points[i] + points[j]) * 0.5f, Vector3.Distance(points[i], points[j]) * 0.5f);
                if (pair.Item2 < best.Radius && Holds(pair)) best = pair;
                for (int k = j + 1; k < points.Count; k++)
                    if (Circumcircle(points[i], points[j], points[k]) is { } tri && tri.Radius < best.Radius && Holds(tri)) best = tri;
            }
        return best;
    }

    private static (Vector3 Centre, float Radius)? Circumcircle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, n = Vector3.Cross(ab, ac);
        float n2 = n.LengthSquared();
        if (n2 < 1e-6f) return null;
        Vector3 offset = (Vector3.Cross(n, ab) * ac.LengthSquared() + Vector3.Cross(ac, n) * ab.LengthSquared()) * (1f / (2f * n2));
        return (a + offset, offset.Length());
    }
}
