namespace ArtifactX.Core.Universe;

/// <summary>
/// A regular building site's cluster layout and facing (NMS.exe build 184271, ported from
/// scripts/research/region-generator/clusterport.py and shopcrate.py; docs/research/PLANETARY_MULTITOOL_LOCATIONS.md):
/// - member choice (0x141635570): the cluster layout's entries (<see cref="BuildingClusterEntry"/>) on the site key's
///   MWC after the slot and chance draws; Shelter entries (class 2) are gated by GcBuildingGlobals.ChanceOfAddingShelter
///   on a side stream (not written back);
/// - placement (0x1416360A0 first loop, members 1..3, no random draws): member i stands at A + D_i * dir(theta_i) on
///   the tangent plane, D_i = r0 + a0 + ri + ai, theta advanced by asin((ri + ai) / D_i) degrees before and after each
///   member; only cube face +X has been checked against live member coordinates;
/// - re-centring (0x1412B6BA0): every building in the cluster (main building included) shifts by
///   -sum(r_i * (p_i - A)) / sum(r_i);
/// - facing (0x141635570): the 0x140576380 cube-face basis axis projected onto the tangent plane, turned about the up
///   vector by (first MWC word of the site key &gt;&gt; 29) * 45 - an angle the game passes to sin/cos as radians.
/// Checked 2026-11-05 against the live pre-shift site records of Kaim 30/19's three surveyed shops: members 3/3,
/// pre-shift positions 5/5 within 3 mm, shop 1 root-node shift (0.59, 9.87, 1.50) vs live (0.60, 9.89, 1.50), and
/// all three stored forwards to 4 decimals.
/// </summary>
public static class BuildingClusterPlacement
{
    private const int ShelterClass = 2;
    private static readonly double Inv2Pow32 = Math.Pow(2, -32);

    /// <summary>The cluster layout entries for a building class from the catalog tables, or null when the class has no cluster.</summary>
    public static BuildingClusterEntry[]? ClusterForClass(BuildingLayoutTables tables, int buildingClass) =>
        tables.ClassClusterLayouts is not null && tables.ClusterLayouts is not null
        && tables.ClassClusterLayouts.TryGetValue(buildingClass, out var id)
        && tables.ClusterLayouts.TryGetValue(id, out var entries)
            ? entries : null;

    /// <summary>0x141635570: the record indices of the cluster members chosen for a site key, given the cluster layout,
    /// the planet's building-record classes in order and GcBuildingGlobals.ChanceOfAddingShelter from the catalog.</summary>
    public static int[] ChooseMembers(
        ulong siteKey,
        IReadOnlyList<BuildingClusterEntry> layout,
        IReadOnlyList<int> recordClasses,
        float chanceOfAddingShelter)
    {
        var rng = PlanetBuildingSites.Mwc.Start(siteKey);
        rng.Word();     // initial step in 0x1412B6BA0 before passing to 0x141635570
        rng.Word();     // slot draw
        rng.Word();     // chance draw; rng is now *param_2
        var side = rng;
        double shelterRoll = side.Word() * Inv2Pow32;
        var chosen = new List<int>();
        foreach (var entry in layout)
        {
            if (entry.BuildingClass == ShelterClass)
            {
                if (!(shelterRoll < chanceOfAddingShelter)) continue;
            }
            else if (entry.Probability < 1f)
            {
                if (!(rng.Word() * Inv2Pow32 < entry.Probability)) continue;
            }
            int count = (int)rng.Next((uint)(entry.Max - entry.Min + 1)) + entry.Min;
            for (int k = 0; k < count; k++)
            {
                int first = 0;
                for (int i = 0; i < recordClasses.Count; i++)
                    if (recordClasses[i] == entry.BuildingClass) { first = i; break; }
                int same = 0;
                for (int i = first + 1; i < recordClasses.Count && recordClasses[i] == entry.BuildingClass; i++)
                    same++;
                chosen.Add(first + (int)rng.Next((uint)(same + 1)));
            }
        }
        return chosen.ToArray();
    }

    /// <summary>0x1416360A0 first loop: pre-shift positions of members 1..3 around planet-relative site point A.
    /// <paramref name="radii"/>[0] is the main building's radius.</summary>
    public static (double X, double Y, double Z)[] PlaceMembers(
        (double X, double Y, double Z) sitePoint,
        IReadOnlyList<double> radii,
        IReadOnlyList<double>? margins = null)
    {
        var n = Normalize(sitePoint);
        int ax = DominantAxis(sitePoint);
        var v = ax == 2 ? (0.0, 1.0, 0.0) : (0.0, 0.0, 1.0);   // face table 0x145A01AB0 via the dominant axis
        var t1 = Cross(n, v);
        var t2 = Cross(n, t1);
        double m0 = margins is { Count: > 0 } ? margins[0] : 0.0;
        float acc = 0f;
        int limit = Math.Min(4, radii.Count);
        var result = new (double X, double Y, double Z)[Math.Max(0, limit - 1)];
        for (int i = 1; i < limit; i++)
        {
            double r = radii[i], a = margins is not null && i < margins.Count ? margins[i] : 0.0;
            float d = (float)(radii[0] + m0) + (float)(r + a);
            double t = Math.Clamp((r + a) / d, -1.0, 1.0);
            float stepDeg = (float)((Math.PI / 2.0 - Math.Acos(t)) * 57.29578);
            acc += stepDeg;
            double th = (float)(acc * 0.017453292f);
            acc += stepDeg;                                     // 0x14163720C adds the step again
            double cos = Math.Cos(th), sin = Math.Sin(th);
            var dir = Normalize((cos * t1.X + sin * t2.X, cos * t1.Y + sin * t2.Y, cos * t1.Z + sin * t2.Z));
            result[i - 1] = (sitePoint.X + d * dir.X, sitePoint.Y + d * dir.Y, sitePoint.Z + d * dir.Z);
        }
        return result;
    }

    /// <summary>0x1412B6BA0: shifts every building in the cluster (main building at index 0 included) by minus the
    /// radius-weighted mean offset from site point A.</summary>
    public static (double X, double Y, double Z)[] Recentre(
        (double X, double Y, double Z) sitePoint,
        IReadOnlyList<(double X, double Y, double Z)> positions,
        IReadOnlyList<double> radii)
    {
        double sx = 0, sy = 0, sz = 0, totalRadius = 0;
        for (int i = 0; i < positions.Count && i < radii.Count; i++)
        {
            double r = radii[i];
            sx += r * (positions[i].X - sitePoint.X);
            sy += r * (positions[i].Y - sitePoint.Y);
            sz += r * (positions[i].Z - sitePoint.Z);
            totalRadius += r;
        }
        double dx = -sx / totalRadius, dy = -sy / totalRadius, dz = -sz / totalRadius;
        return positions.Select(p => (p.X + dx, p.Y + dy, p.Z + dz)).ToArray();
    }

    /// <summary>0x141635570: a building's unit up, forward and side (up x forward) vectors from its site point A and key.
    /// The yaw angle (first MWC word &gt;&gt; 29) * 45 is passed to sin/cos as radians, matching the game's machine code.</summary>
    public static ((double X, double Y, double Z) Up, (double X, double Y, double Z) Forward, (double X, double Y, double Z) Side) Facing(
        (double X, double Y, double Z) sitePoint,
        ulong siteKey)
    {
        var n = Normalize(sitePoint);
        var (a, b) = CubeFaceBasis(sitePoint);
        var xAxis = Normalize(Cross(a, b));
        var f0 = Normalize(Cross(Cross(n, Cross(xAxis, a)), n));
        var rng = PlanetBuildingSites.Mwc.Start(siteKey);
        double ang = (rng.Word() >> 29) * 45.0;
        double cos = Math.Cos(ang), sin = Math.Sin(ang);
        var nCrossF0 = Cross(n, f0);
        var forward = (f0.X * cos + nCrossF0.X * sin, f0.Y * cos + nCrossF0.Y * sin, f0.Z * cos + nCrossF0.Z * sin);
        return (n, forward, Cross(n, forward));
    }

    /// <summary>The 0x140576380 cube-face basis (normal axis a, reference axis b) from the dominant component of p.</summary>
    internal static ((double X, double Y, double Z) A, (double X, double Y, double Z) B) CubeFaceBasis((double X, double Y, double Z) p)
    {
        int ax = DominantAxis(p);
        bool pos = (ax == 0 ? p.X : ax == 1 ? p.Y : p.Z) > 0;
        return (ax, pos) switch
        {
            (0, true) => ((1, 0, 0), (0, 1, 0)),
            (0, false) => ((-1, 0, 0), (0, 0, 1)),
            (1, true) => ((0, 1, 0), (0, 0, 1)),
            (1, false) => ((0, -1, 0), (-1, 0, 0)),
            (2, true) => ((0, 0, 1), (1, 0, 0)),
            _ => ((0, 0, -1), (0, 1, 0)),
        };
    }

    internal static int DominantAxis((double X, double Y, double Z) p)
    {
        double ax = Math.Abs(p.X), ay = Math.Abs(p.Y), az = Math.Abs(p.Z);
        return ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;
    }

    internal static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    internal static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    internal static (double X, double Y, double Z) Normalize((double X, double Y, double Z) v)
    {
        double len = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return (v.X / len, v.Y / len, v.Z / len);
    }
}

