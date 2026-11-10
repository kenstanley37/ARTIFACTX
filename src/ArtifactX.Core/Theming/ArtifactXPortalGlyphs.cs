namespace ArtifactX.Core.Theming;

/// <summary>
/// ArtifactX's own cartoon artwork for the 16 portal glyphs (0..F): filled silhouettes on a 48x48 grid,
/// drawn by hand to be recognisable next to the game's symbols without copying them. No game texture,
/// icon or vector data is used. Render with a fill (even-odd holes), not a stroke. Names are the ones
/// players commonly use for each glyph; the game itself shows only the symbols.
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
        new(0x0, '0', "Sunset",    "M4,32 H44 V36 H4 Z M10,39 H38 V42 H10 Z M12,30 A12,12 0 0 1 36,30 Z M22.5,8 H25.5 V14 H22.5 Z M9.6,13.4 L11.7,11.3 L15.9,15.5 L13.8,17.6 Z M38.4,13.4 L36.3,11.3 L32.1,15.5 L34.2,17.6 Z"),
        new(0x1, '1', "Bird",      "M4,18 C10,13 17,14 22,22 L24,25 L26,22 C31,14 38,13 44,18 C37,18 31,21 27,28 L27,34 L24,38 L21,34 L21,28 C17,21 11,18 4,18 Z"),
        new(0x2, '2', "Face",      "M12,8 H36 Q40,8 40,12 V26 Q40,38 24,42 Q8,38 8,26 V12 Q8,8 12,8 Z M14.5,20 A3.5,3 0 1 0 21.5,20 A3.5,3 0 1 0 14.5,20 Z M26.5,20 A3.5,3 0 1 0 33.5,20 A3.5,3 0 1 0 26.5,20 Z M18,30 H30 V32.5 H18 Z"),
        new(0x3, '3', "Diplo",     "M6,10 C6,7 12,6 13,9 L14,12 C15,18 17,21 20,22 C26,18 34,19 37,25 C40,28 43,31 45,35 C41,33 39,32 36,32 L36,40 L32,40 L32,33 L24,33 L24,40 L20,40 L20,32 C16,30 13,24 11,13 L7,12 C6,12 6,11 6,10 Z"),
        new(0x4, '4', "Eclipse",   "M8,24 A16,16 0 1 0 40,24 A16,16 0 1 0 8,24 Z M12,24 A12,12 0 1 0 36,24 A12,12 0 1 0 12,24 Z M20,24 A8,8 0 1 0 36,24 A8,8 0 1 0 20,24 Z"),
        new(0x5, '5', "Balloon",   "M24,4 C34,4 39,11 39,18 C39,26 30,31 27,34 L21,34 C18,31 9,26 9,18 C9,11 14,4 24,4 Z M15,17 C20,19 28,19 33,17 L33,19.5 C28,21.5 20,21.5 15,19.5 Z M21,34 H22.5 V39 H21 Z M25.5,34 H27 V39 H25.5 Z M19,39 H29 V45 H19 Z"),
        new(0x6, '6', "Boat",      "M4,32 H44 L38,42 H10 Z M23,6 H25.5 V30 H23 Z M27.5,8 L40,28 H27.5 Z M21,12 L10,28 H21 Z"),
        new(0x7, '7', "Bug",       "M15,27 A9,12 0 1 0 33,27 A9,12 0 1 0 15,27 Z M20,10 A4,4 0 1 0 28,10 A4,4 0 1 0 20,10 Z M23.3,17 H24.7 V37 H23.3 Z M13,19 L7,15 L6,16.5 L12.2,20.8 Z M12.5,27 L5,27 L5,28.8 L12.5,28.8 Z M13,35 L7,40 L8.2,41.2 L14,36.5 Z M35,19 L41,15 L42,16.5 L35.8,20.8 Z M35.5,27 L43,27 L43,28.8 L35.5,28.8 Z M35,35 L41,40 L39.8,41.2 L34,36.5 Z M20.6,7 L17,3 L18.2,2 L21.5,6 Z M27.4,7 L31,3 L29.8,2 L26.5,6 Z"),
        new(0x8, '8', "Dragonfly", "M22.8,9 H25.2 V42 H22.8 Z M21,6 A3,3 0 1 0 27,6 A3,3 0 1 0 21,6 Z M21.5,13 C14,8 5,9 4,13 C5,17 14,17 21.5,15 Z M26.5,13 C34,8 43,9 44,13 C43,17 34,17 26.5,15 Z M21.5,18 C14,17 7,20 7,23 C9,26 15,23 21.5,20 Z M26.5,18 C34,17 41,20 41,23 C39,26 33,23 26.5,20 Z"),
        new(0x9, '9', "Galaxy",    "M19,24 A5,5 0 1 0 29,24 A5,5 0 1 0 19,24 Z M29,17.5 C24,5 6,6 4,22 C8,12 19,11 21.5,18.5 Z M19,30.5 C24,43 42,42 44,26 C40,36 29,37 26.5,29.5 Z M8,36 A2,2 0 1 0 12,36 A2,2 0 1 0 8,36 Z M36,10 A2,2 0 1 0 40,10 A2,2 0 1 0 36,10 Z"),
        new(0xA, 'A', "Voxel",     "M24,6 L39.5,14.5 L24,23 L8.5,14.5 Z M7,17 L22.6,25.5 L22.6,42 L7,33.5 Z M41,17 L25.4,25.5 L25.4,42 L41,33.5 Z"),
        new(0xB, 'B', "Fish",      "M4,24 C10,14 24,12 33,20 L42,13 L40,24 L42,35 L33,28 C24,36 10,34 4,24 Z M11,22 A2,2 0 1 0 15,22 A2,2 0 1 0 11,22 Z M18,18 C20,22 20,26 18,30 L19.5,30 C21.5,26 21.5,22 19.5,18 Z"),
        new(0xC, 'C', "Tent",      "M24,6 L44,40 H4 Z M24,24 L30,40 H18 Z M20.5,3 L22.6,6.5 L21.6,7.1 L19.5,3.6 Z M27.5,3 L25.4,6.5 L26.4,7.1 L28.5,3.6 Z"),
        new(0xD, 'D', "Rocket",    "M24,3 C31,9 32,18 31,32 H17 C16,18 17,9 24,3 Z M20.5,16 A3.5,3.5 0 1 0 27.5,16 A3.5,3.5 0 1 0 20.5,16 Z M15.2,25 L9,34 V38 L15.2,34.5 Z M32.8,25 L39,34 V38 L32.8,34.5 Z M20,34 H28 L24,45 Z"),
        new(0xE, 'E', "Tree",      "M24,4 C31,4 35,9 34,14 C40,15 42,22 38,27 C36,32 29,33 24,31 C19,33 12,32 10,27 C6,22 8,15 14,14 C13,9 17,4 24,4 Z M21.8,33 H26.2 V42 H21.8 Z M13,43.5 H35 V45.5 H13 Z"),
        new(0xF, 'F', "Atlas",     "M24,4 L44,40 H4 Z M24,16 L35,35 H13 Z M21,29 A3,3 0 1 0 27,29 A3,3 0 1 0 21,29 Z"),
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
