namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One hotspot on a base-combo or Near Portal card, with where it sits from that card's base computer
/// (and from the portal) - the hotspot's own card only knows its distance from the reference point.</summary>
public sealed record ClusterHotspotRow(PlanetHotspotItemViewModel Hotspot, string Where);
