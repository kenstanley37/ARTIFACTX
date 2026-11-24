namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Whether each body in a system is a planet or a moon, and which planet a moon orbits.
/// The system generator places moons body by body: a large planet takes up to two moons
/// right after it (SystemBodies.Hosts) - exact on Amogat-Aizu XIV and Muraya-Risti against
/// the game's own Discoveries views (2026-10-17). When the moon count the player recorded
/// disagrees with the generator's, the old guess (moons are the bodies right after planet 1).
/// (Until 2026-11-24 a MARK MOON / MARK PLANET pop-up read off the game screen came first;
/// screen reading was removed and its body_kinds table dropped in user.db step 22.)
/// </summary>
public static class BodyKindsService
{
    public static bool IsMoon(ulong system, int body) => Host(system, body) > 0;

    /// <summary>The planet a moon orbits (1-based), 0 for a planet.</summary>
    public static int Host(ulong system, int body)
    {
        var bodies = UniverseInfo.Bodies(system);
        int? recordedMoons = SystemNotesService.Get(system)?.Moons;
        if (recordedMoons is null || recordedMoons == bodies.MoonCount)
            return body >= 1 && body <= bodies.Hosts.Length ? bodies.Hosts[body - 1] : 0;
        // The recorded count disagrees with the generator: the old guess.
        return body >= 2 && body <= 1 + recordedMoons ? 1 : 0;
    }
}
