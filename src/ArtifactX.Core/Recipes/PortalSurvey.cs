using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.Core.Recipes;

/// <summary>One hotspot on a base spot card: the resource and class, its amount (power kP or yield %), and how far it is
/// from the base computer and from the portal.</summary>
public sealed record BaseSpotHotspotLine(PlanetHotspot Hotspot, string Amount, string FromComputer, string FromPortal, string Description);

/// <summary>
/// A planet's portal and what can be built there (#63): the base computer spots near the portal that reach it and
/// deep-level hotspots, and the hotspots around it. Everything a console player can use from coordinates - the
/// Analysis Visor shows the same latitude and longitude. Pure, from the address and the game's tables, so the desktop
/// and the mobile app (its rules file) word it the same.
/// </summary>
public sealed record PortalSurvey(PortalPosition Portal, PlanetHotspotData Plan)
{
    /// <summary>Less room than this between the farthest site and the base boundary reads as tight (warning colour). A
    /// display choice, not game data: about the error of placing a base computer by eye at a beacon.</summary>
    public const double TightMargin = 50;

    /// <summary>The planet's portal and plan; null when the portal can't be placed (gas-giant systems, a system the
    /// solar-layout port doesn't cover) or the tables are missing.</summary>
    public static PortalSurvey? ForPlanet(ulong planetAddress, BiomeTables biomes, PlanetProfileTables profiles, ExtractionTables extraction,
        StationEquipmentTables stations, BuildingOverrideRule[] rules, Func<string, string?> nameOf)
    {
        int planet = (int)((planetAddress >> 52) & 0xF);
        ulong system = planetAddress & ~(0xFUL << 52);
        if (planet < 1 || PortalPositions.ForSystem(system, biomes, profiles, stations, rules).GetValueOrDefault(planet) is not { } portal) return null;
        var plan = HotspotPlanner.Plan(planetAddress, portal.Latitude, portal.Longitude, new ExtractionWorldTables(biomes, profiles, extraction),
            nameOf, expandSearch: true, portal.Latitude, portal.Longitude);
        return new PortalSurvey(portal, plan);
    }

    /// <summary>The hotspots near the portal, nearest first.</summary>
    public IReadOnlyList<PlanetHotspot> NearPortal(int count) =>
        Plan.Hotspots.OrderBy(h => h.PortalDistance ?? double.MaxValue).Take(count).ToList();

    public static IReadOnlyList<BaseSpotHotspotLine> Lines(PlanetHotspotCluster spot)
    {
        float planetRadius = spot.BaseLocalPosition.Length();
        return spot.Hotspots.Select(h => new BaseSpotHotspotLine(h,
            h.Kind == "Power" ? $"{Math.Round(h.Strength)} kP" : $"{h.YieldPercent}%",
            HotspotPlanner.FormatDistance(PlanetHotspotCluster.GroundDistance(spot.BaseLocalPosition, h.LocalPosition, planetRadius)),
            h.PortalDistance is { } fromPortal ? HotspotPlanner.FormatDistance(fromPortal) : "",
            $"{h.ResourceName}, class {h.Class}")).ToList();
    }

    /// <summary>"Reaches the portal and both hotspots - the farthest is 256u of 300u (44u to spare)", and whether that's
    /// tight.</summary>
    public static (string Text, bool Tight) Spare(PlanetHotspotCluster spot, float baseReach)
    {
        if (baseReach <= 0) return (spot.ReachDescription, false);
        string what = spot.IncludesPortal
            ? (spot.Hotspots.Count == 1 ? "the portal and the hotspot" : $"the portal and all {spot.Hotspots.Count} hotspots")
            : $"all {spot.Hotspots.Count} hotspots";
        double spare = baseReach - spot.MaxRadius;
        return ($"Reaches {what} - the farthest is {Math.Round(spot.MaxRadius)}u of {Math.Round(baseReach)}u ({Math.Max(0, Math.Round(spare))}u to spare)",
            spare < TightMargin);
    }
}
