namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One star on the region map: its generated position (region units, about ±50) and what it is to you.
/// <paramref name="lightYears"/> is the galaxy map's distance from your system, when you're in this region.</summary>
public sealed class RegionMapStar(int system, float x, float y, float z, bool visited, bool here, string name, string detail, string portalCode,
    int? lightYears = null)
{
    public int System { get; } = system;
    public float X { get; } = x;
    public float Y { get; } = y;
    public float Z { get; } = z;
    public bool Visited { get; } = visited;
    public bool IsHere { get; } = here;
    public bool IsPurple => System > 1000;
    public string Name { get; } = name;
    public string Detail { get; } = detail;
    public string PortalCode { get; } = portalCode;
    public int? LightYears { get; } = lightYears;
    public string Status => IsHere ? "You are here"
        : (Visited ? "Visited" : "Not visited yet") + (LightYears is int ly ? $" · {ly:N0} ly from you" : "");
}
