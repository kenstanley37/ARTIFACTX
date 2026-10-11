using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Turns what players paste (a 12-character portal code, or galactic
/// coordinates XXXX:YYYY:ZZZZ:SSSS from a signal booster) into a portal code.
///
/// Portal code: P SSS YY ZZZ XXX (planet, system, Y, Z, X - see
/// NmsSpaceStationPaths.PortalAddress). Galactic coordinates offset each axis
/// from the galaxy centre: X = portal X + 0x7FF, Y = portal Y + 0x7F,
/// Z = portal Z + 0x7FF, each wrapped to its width; the 4th group is the
/// system index. Coordinates carry no planet, so planet 1 is used.
/// </summary>
public static partial class NmsPortalCode
{
    public static bool TryParse(string? input, out string code, out string error)
    {
        code = "";
        error = "";
        string text = (input ?? "").Trim();
        if (text.Length == 0) return false;

        if (text.Contains(':'))
            return TryParseCoordinates(text, out code, out error);

        string hex = SeparatorRegex().Replace(text, "").ToUpperInvariant();
        if (!hex.All(Uri.IsHexDigit))
        {
            error = "A portal code only uses 0-9 and A-F.";
            return false;
        }
        if (hex.Length != 12)
        {
            error = $"A portal code is 12 characters; this one has {hex.Length}.";
            return false;
        }

        code = hex;
        return true;
    }

    private static bool TryParseCoordinates(string text, out string code, out string error)
    {
        code = "";
        var parts = text.Split(':').Select(p => p.Trim()).ToArray();
        if (parts.Length != 4 || parts.Any(p => p.Length == 0 || p.Length > 4 || !p.All(Uri.IsHexDigit)))
        {
            error = "Galactic coordinates look like 0F2F:0065:077F:0013 (four groups of up to 4 characters).";
            return false;
        }

        int[] v = parts.Select(p => int.Parse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        int x = (v[0] - 0x7FF) & 0xFFF;
        int y = (v[1] - 0x7F) & 0xFF;
        int z = (v[2] - 0x7FF) & 0xFFF;
        int system = v[3] & 0xFFF;

        code = string.Create(CultureInfo.InvariantCulture, $"1{system:X3}{y:X2}{z:X3}{x:X3}");
        error = "";
        return true;
    }

    /// <summary>Galactic coordinates (XXXX:YYYY:ZZZZ:SSSS) for a portal code.</summary>
    public static string ToCoordinates(string code)
    {
        int Hex(int start, int length) => int.Parse(code.AsSpan(start, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int system = Hex(1, 3), y = Hex(4, 2), z = Hex(6, 3), x = Hex(9, 3);
        return string.Create(CultureInfo.InvariantCulture,
            $"{(x + 0x7FF) & 0xFFF:X4}:{(y + 0x7F) & 0xFF:X4}:{(z + 0x7FF) & 0xFFF:X4}:{system:X4}");
    }

    /// <summary>Packed galactic address for a portal code in a galaxy (1-based,
    /// Euclid = 1) - the inverse of NmsSpaceStationPaths.PortalAddress. A
    /// portal code doesn't say which galaxy it's in, so the galaxy is separate.</summary>
    public static ulong ToAddress(string code, int galaxy)
    {
        ulong Hex(int start, int length) => ulong.Parse(code.AsSpan(start, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (Hex(0, 1) << 52) | (Hex(1, 3) << 40) | ((ulong)((galaxy - 1) & 0xFF) << 32)
             | (Hex(4, 2) << 24) | (Hex(6, 3) << 12) | Hex(9, 3);
    }

    [GeneratedRegex(@"[\s\-_.]")]
    private static partial Regex SeparatorRegex();
}
