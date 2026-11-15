using ArtifactX.Core.Procedural;

internal static class ShipNameChecks
{
    /// <summary>Slot 8's unnamed Living Ship (seed 0x2966405E8B675213) is "Yulin KV4" in game (2026-11-14).
    /// The two patterns are the game's; the 25 prefixes are placeholders (only their count matters here).</summary>
    public static void CheckLivingShipName()
    {
        var lists = new Dictionary<string, IReadOnlyList<string>>
        {
            ["WEIRDSHIP_FORMAT"] = ["%PROCNAME% %SHIPCODE%", "%PREFIX% %PROCNAME%"],
            ["WEIRDSHIP_PREFIX"] = Enumerable.Range(0, 25).Select(i => $"Prefix {i}").ToArray(),
        };
        string? name = ShipNames.GenerateLiving(0x2966405E8B675213UL, l => lists.GetValueOrDefault(l));
        if (name != "Yulin KV4") throw new InvalidOperationException($"Living Ship name: expected Yulin KV4, got {name}");
        Console.WriteLine("PASS: Living Ship name from its seed matches the game (Yulin KV4).");
    }
}
