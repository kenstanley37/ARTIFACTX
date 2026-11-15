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

    /// <summary>Sentinel ship names (NMS.exe 0x140EAC670) for one owned ship seed and three crashed-Sentinel site keys,
    /// as the game showed them (2026-10-29 / 2026-11-14). The four patterns are the game's; the 20 names are
    /// placeholders ("N2" stands for Crimson, "N16" Hope, "N17" Flameborn).</summary>
    public static void CheckSentinelShipNames()
    {
        var lists = new Dictionary<string, IReadOnlyList<string>>
        {
            ["ROBOTSHIP_FORMAT"] = ["%NAME%-%SYM1%", "%SYM1%-%NAME%", "%NAME%-%SYM1%%SYM2%", "%NAME%-%NUM%"],
            ["ROBOTSHIP_NAME"] = Enumerable.Range(0, 20).Select(i => $"N{i}").ToArray(),
        };
        foreach (var (seed, expected) in new[] { (0x1203E23BB8CB0BF9UL, "μ-N17"), (0xB118F1E99ED0D4CAUL, "N2-σ"),
                     (0x8CAAD428FF569903UL, "N17-9-MH3"), (0x2089D793883477F0UL, "N16-1-D") })
            if (ShipNames.GenerateSentinel(seed, l => lists.GetValueOrDefault(l)) is var name && name != expected)
                throw new InvalidOperationException($"Sentinel name for {seed:X16}: expected {expected}, got {name}");
        Console.WriteLine("PASS: Sentinel ship names match the game for 1 owned ship and 3 crashed Sentinels.");
    }
}
