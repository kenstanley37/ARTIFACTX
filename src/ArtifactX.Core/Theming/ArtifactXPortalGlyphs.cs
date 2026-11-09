namespace ArtifactX.Core.Theming;

/// <summary>
/// Original geometric vector line-art definitions for the 16 hexadecimal portal glyphs (0..F).
/// Built on a 24x24 coordinate grid so both .NET MAUI and WinUI 3 can render crisp vector glyphs
/// without shipping any game texture assets.
/// </summary>
public sealed record PortalGlyphVector(
    int Index,
    char HexDigit,
    string Name,
    string PathData);

public static class ArtifactXPortalGlyphs
{
    public static IReadOnlyList<PortalGlyphVector> All { get; } =
    [
        new(0x0, '0', "Horizon", "M3,15 L21,15 M6,19 L18,19 M7,15 A5,5 0 0 1 17,15 M12,6 L12,8 M6.5,8.5 L8,10 M17.5,8.5 L16,10"),
        new(0x1, '1', "Wings",   "M3,9 L12,14 L21,9 M6,13 L12,16 L18,13 M12,16 L12,20 M9,20 L15,20"),
        new(0x2, '2', "Visage",  "M12,3 L20,19 L4,19 Z M8.5,13 L10.5,13 M13.5,13 L15.5,13 M9,16.5 L15,16.5"),
        new(0x3, '3', "Crest",   "M6,20 L6,14 L13,14 L13,20 M13,14 L13,6 L17,4 L17,7 L15,8 M6,14 L3,17"),
        new(0x4, '4', "Eclipse", "M12,5 A6,6 0 1 0 12,17 A6,6 0 1 0 12,5 M14,6 A5,5 0 0 0 14,16 M5,20 L19,20"),
        new(0x5, '5', "Beacon",  "M12,3 L18,9 L12,15 L6,9 Z M12,7 A2,2 0 1 0 12,11 A2,2 0 1 0 12,7 M12,15 L12,21 M9,21 L15,21"),
        new(0x6, '6', "Vessel",  "M12,4 L12,16 M12,5 L18,13 L12,13 M12,7 L7,13 L12,13 M4,16 L7,20 L17,20 L20,16 Z"),
        new(0x7, '7', "Scarab",  "M12,6 L16,9 L16,15 L12,18 L8,15 L8,9 Z M8,9 L4,6 M16,9 L20,6 M8,12 L3,12 M16,12 L21,12 M8,15 L4,18 M16,15 L20,18"),
        new(0x8, '8', "Flutter", "M12,3 L12,21 M12,8 L4,5 L4,9 L12,11 M12,8 L20,5 L20,9 L12,11 M12,13 L6,15 L6,17 L12,16 M12,13 L18,15 L18,17 L12,16"),
        new(0x9, '9', "Vortex",  "M12,10 A2,2 0 1 0 12,14 A2,2 0 1 0 12,10 M12,10 A4,4 0 0 1 16,14 A6,6 0 0 1 10,20 A8,8 0 0 1 4,12 A9,9 0 0 1 13,3 A8,8 0 0 1 21,9"),
        new(0xA, 'A', "Voxel",   "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z M4,7.5 L12,12 L20,7.5 M12,12 L12,21"),
        new(0xB, 'B', "Fin",     "M3,15 L8,10 L17,10 L21,7 L20,12 L15,17 L8,17 Z M14,12 L15,12"),
        new(0xC, 'C', "Spire",   "M3,20 L13,3 M21,20 L11,3 M4,20 L20,20 M12,12 L8,20 L16,20 Z"),
        new(0xD, 'D', "Ascent",  "M12,3 L16,9 L16,16 L8,16 L8,9 Z M8,13 L5,17 L5,19 L8,19 M16,13 L19,17 L19,19 L16,19 M10,19 L10,21 M14,19 L14,21"),
        new(0xE, 'E', "Canopy",  "M12,21 L12,7 M8,21 L16,21 M12,13 L7,9 M12,13 L17,9 M12,9 L8,5 M12,9 L16,5"),
        new(0xF, 'F', "Prism",   "M12,3 L21,19 L3,19 Z M12,19 L7.5,11 L16.5,11 Z M11.5,14 L12.5,14"),
    ];

    public static PortalGlyphVector ForDigit(char hexDigit)
    {
        int idx = char.ToUpperInvariant(hexDigit) switch
        {
            >= '0' and <= '9' => hexDigit - '0',
            >= 'A' and <= 'F' => char.ToUpperInvariant(hexDigit) - 'A' + 10,
            _ => 0,
        };
        return All[idx];
    }
}
