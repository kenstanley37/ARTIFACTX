using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A building site of one class: its key (the marker's BuildingSeed), point in system space at the planet
/// radius, the coordinates the Analysis Visor shows there and how far it is from where the search started.</summary>
public sealed record BuildingSiteLocation(ulong PlanetAddress, ulong Key, double X, double Y, double Z,
    double Latitude, double Longitude, double DistanceKm)
{
    public string Coordinates => NmsPlanetCoordinates.Format(Latitude, Longitude);
}

/// <summary>
/// Where a planet's buildings of one class stand, from the game's building grid
/// (<see cref="PlanetBuildingSites.SitesNear"/>) with the ground test crashed ships use (CrashedShipService's planet
/// context). Checked 2026-11-05 on Kaim 30/19 (Slot 7, Ihweyf): the two nearest predicted Shop sites (-6.79, +64.54
/// and -7.77, +62.52) each held a Minor Settlement shop (visor -6.78, +64.54 and -7.76, +62.51), and the game's own
/// SHOP scan event written for them led straight there. An earlier list without the ground test put one under the sea.
/// </summary>
public static class BuildingSiteService
{
    /// <summary>The <paramref name="count"/> sites of <paramref name="buildingClass"/> nearest a point on the planet;
    /// empty when the planet can't be worked out yet or the system has no race (no shops are built there).</summary>
    public static async Task<IReadOnlyList<BuildingSiteLocation>> NearAsync(ulong planetAddress, int buildingClass,
        double latitude, double longitude, int count)
    {
        if (await CrashedShipService.ContextAsync(planetAddress) is not { } context || context.Uncharted) return [];
        return await Task.Run(() =>
        {
            var dir = CrashedShipService.Direction(latitude, longitude);
            var c = context.Centre;
            return (IReadOnlyList<BuildingSiteLocation>)PlanetBuildingSites.SitesNear(dir, context.Divisions, context.Radius,
                    context.SectorSeed, context.Slots, cls => cls == buildingClass, count * 4)
                .Where(context.Builds).Take(count)
                .Select(site =>
                {
                    var (lat, lon) = site.Coordinates;
                    double angle = Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(site.Point), dir), -1f, 1f));
                    return new BuildingSiteLocation(planetAddress, site.Key, c.X + site.Point.X, c.Y + site.Point.Y,
                        c.Z + site.Point.Z, lat, lon, angle * context.Radius / 1000);
                }).ToList();
        });
    }
}
