using ArtifactX.Core.NmsModels;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// One planet or moon on the system map: its centre from the save (thousands of units, <c>6f=.aHM</c>), the
/// planet a moon orbits (0 for a planet, from the system generator), and the discovered planet when the save
/// knows it (for its drawing and to open its page).
/// </summary>
public sealed record SystemMapBody(int Index, string Name, double X, double Y, double Z, int Host, string HostName, bool IsHere,
    KnownPlanet? Planet)
{
    public bool IsMoon => Host > 0;
    public string Kind => IsHere ? (IsMoon ? $"You're on this moon of {HostName}" : "You're on this planet")
        : IsMoon ? $"Moon of {HostName}" : "Planet";
}
