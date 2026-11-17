using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>
/// The planet's finest terrain regions and their seeds. A region is a 32 m cell of the planet's cube: the point is
/// projected onto the cube face at the planet radius, each face axis is cut into 32 m cells, and the face's own axis
/// sits just outside the face (radius + 16). The region is named by its cell centre in whole metres, and its seed is
/// CityHash Hash128to64 over the three centre coordinates (NMS.exe build 184271 0x1403DB300; 0x1403DB250 on 185034:
/// it finds the level-0 node of the planet's region tree and hashes the node's ints at +0x74, +0x78, +0x7C).
/// Saved base-building objects outside bases (GcPersistentBBObjectData in vLc.6f=.c5s) store this as RegionSeed
/// ("ofi"); the game builds such an object only in the region whose seed it carries.
/// Checked 2026-11-10: the seeds of five game-built Save Beacons (Steam slot 8, two planet sizes, four systems,
/// positive and negative faces) reproduce exactly; the code path was found by a hardware write watch on the beacon
/// object's seed (+0x110 of cGcBaseBuildingObject) while the owner placed beacons in game.
/// </summary>
public static class PlanetRegions
{
    /// <summary>Edge of a level-0 region cell, metres.</summary>
    public const int CellSize = 32;

    private const ulong HashMul = 0x9DDFEA08EB382D69;

    /// <summary>The centre of the level-0 region holding a planet-relative point, in whole metres.</summary>
    public static (int X, int Y, int Z) Cell(Vector3 planetRelative, float radius)
    {
        double x = planetRelative.X, y = planetRelative.Y, z = planetRelative.Z;
        double m = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
        if (m <= 0) return (0, 0, 0);
        int r = (int)Math.Round(radius);
        int face = Math.Abs(x) >= Math.Abs(y) && Math.Abs(x) >= Math.Abs(z) ? 0 : Math.Abs(y) >= Math.Abs(z) ? 1 : 2;
        int Axis(double v, int axis, double component) =>
            axis == face ? (component > 0 ? r + CellSize / 2 : -(r + CellSize / 2))
                         : (int)Math.Floor(v / m * r / CellSize) * CellSize + CellSize / 2;
        return (Axis(x, 0, x), Axis(y, 1, y), Axis(z, 2, z));
    }

    /// <summary>The RegionSeed the game gives a saved object at this planet-relative point.</summary>
    public static ulong Seed(Vector3 planetRelative, float radius)
    {
        var (x, y, z) = Cell(planetRelative, radius);
        return Seed(x, y, z);
    }

    /// <summary>The seed of the region whose cell centre is (x, y, z): Hash128to64(Hash128to64(x, y), z).</summary>
    public static ulong Seed(int x, int y, int z)
    {
        unchecked
        {
            ulong sx = (ulong)(long)x, sz = (ulong)(long)z;
            ulong u = (ulong)(long)(y ^ x) * HashMul;
            u = (u >> 47 ^ u ^ sx) * HashMul;
            u = (u >> 47 ^ u) * HashMul;
            ulong v = (sz ^ u) * HashMul;
            v = (v >> 47 ^ v ^ u) * HashMul;
            return (v >> 47 ^ v) * HashMul;
        }
    }
}
