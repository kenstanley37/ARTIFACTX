using System;
using System.Globalization;

namespace ArtifactX.Core.Universe;

/// <summary>Address-based travel requirements, independent of a loaded save.
/// Owner's controlled portal tests on 2026-10-28: 64275B6DA013 reached the
/// purple system with access, but redirected to 102E5B6DA013 without access.
/// The unlock field is unverified; this does not infer player access.</summary>
public static class PortalTravel
{
    public static bool RequiresPurpleStarAccess(string? code)
    {
        if (code is null || code.Length != 12) return false;
        foreach (char symbol in code)
            if (!Uri.IsHexDigit(symbol)) return false;
        return int.TryParse(code.AsSpan(1, 3), NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture, out int system) && SystemGenerator.IsPurpleSystemId(system);
    }
}
