namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One hotspot row on a base-combo or Near Portal card, as table columns: its yield, and how far it is from that
/// card's base computer and from the portal ("" when the portal isn't known). Details holds the rest for a tooltip.</summary>
public sealed record ClusterHotspotRow(PlanetHotspotItemViewModel Hotspot, string Yield, string FromBase, string FromPortal, string Details);
