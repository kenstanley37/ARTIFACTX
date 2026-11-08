using ArtifactX.Core.Universe;

/// <summary>Ringed-planet pick (PlanetRings) against systems checked in game.</summary>
internal static class PlanetRingChecks
{
    public static void Check()
    {
        // Apolok-Sats (Steam slot 1, packed 0xB016F9FF9007): body 1 (Seburydes) has rings, 2026-11-08.
        CheckSystem("Apolok-Sats", 0xB016F9FF9007UL, expectedBody: 1);

        // Upusichrot (Steam slot 1, packed 0x0EA0205003FFA): body 2 (Edmo IX) has rings, 2026-11-08.
        CheckSystem("Upusichrot", 0x0EA0205003FFAUL, expectedBody: 2);

        Console.WriteLine("Planet rings: Apolok-Sats (body 1) and Upusichrot (body 2) OK");
    }

    private static void CheckSystem(string name, ulong packed, int expectedBody)
    {
        ulong portal = (((packed >> 40) & 0xFFF) << 32) | (packed & 0xFFFFFFFF);
        int galaxy = (int)((packed >> 32) & 0xFF);
        int ringed = PlanetRings.RingedBody(portal, galaxy);
        if (ringed != expectedBody)
            throw new InvalidOperationException($"{name} ringed body: expected {expectedBody}, got {ringed}");
    }
}
