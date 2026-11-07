namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Generates a new random seed in the same "0x" + 16-uppercase-hex-digit
/// format the game itself stores for a "@EL" second element (Ship NTx,
/// Freighter hull bIR, Freighter crew Sjw) - confirmed real full 64-bit
/// values (at least one sampled seed started with 'A', confirming the high
/// bit genuinely gets used), so this draws from all 8 random bytes rather
/// than a signed 63-bit long.
///
/// This is a plain reroll, not a targeted "give me X wings" generator - the
/// actual seed-to-appearance algorithm lives in the game's own compiled
/// code, not in any MBIN data this app can read, so there's no way to
/// preview or aim for a specific look here.
/// </summary>
public static class NmsSeedGenerator
{
    public static string GenerateRandom()
    {
        var bytes = new byte[8];
        Random.Shared.NextBytes(bytes);
        return "0x" + BitConverter.ToUInt64(bytes).ToString("X16");
    }

    /// <summary>Parses a hex seed (e.g. "0x12AB..." or "12AB...") into a 64-bit unsigned integer.</summary>
    public static bool TryParse(string? text, out ulong seed)
    {
        seed = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        string hex = text.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        return ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out seed);
    }
}
