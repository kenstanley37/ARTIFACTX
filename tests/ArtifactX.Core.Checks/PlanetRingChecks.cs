using ArtifactX.Core.Universe;

/// <summary>Ringed-planet pick (PlanetRings) against systems checked in game.</summary>
internal static class PlanetRingChecks
{
    public static void Check()
    {
        // Apolok-Sats (Steam slot 1, packed 0xB016F9FF9007): the game's Discoveries page draws rings
        // on Seburydes (body 1) only, 2026-11-08.
        const ulong apolok = 0xB016F9FF9007UL;
        ulong portal = (((apolok >> 40) & 0xFFF) << 32) | (apolok & 0xFFFFFFFF);
        int galaxy = (int)((apolok >> 32) & 0xFF);
        int ringed = PlanetRings.RingedBody(portal, galaxy);
        if (ringed != 1)
            throw new InvalidOperationException($"Apolok-Sats ringed body: expected 1, got {ringed}");
        Console.WriteLine("Planet rings: Apolok-Sats ringed body 1 OK");
    }
}

