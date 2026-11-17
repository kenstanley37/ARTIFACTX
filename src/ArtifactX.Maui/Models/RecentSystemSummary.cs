namespace ArtifactX.Maui.Models;

/// <summary>
/// Summary row for a recently decoded star system shown on the Home page.
/// </summary>
public sealed record RecentSystemSummary(
    string Name,
    string Subtitle,
    string PortalCode,
    string GalaxyName,
    int GalaxyNumber = 1);
