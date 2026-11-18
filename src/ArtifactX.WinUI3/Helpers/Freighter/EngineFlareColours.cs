namespace ArtifactX.WinUI3.Helpers.Freighter;

/// <summary>Each engine flare's glow (NmsInventoryContainer.FreighterEngineEffects) for the 3D preview, picked by eye from
/// the game's Customisation screen (2026-10-22). Display colours, not game data.</summary>
public static class EngineFlareColours
{
    public static uint For(string? value) => value?.ToUpperInvariant() switch
    {
        "^GREEN" => 0x3DFF7A,
        "^BLACKHOLE" => 0xC58BFF,
        "^YELLOW" => 0xFFE44A,
        "^PURPLE" => 0xF04AE0,
        "^TURQUOISE" => 0x3FEFF0,
        "^RED" => 0xFF4A35,
        _ => 0x4A8CFF, // ^DEFAULT: Standard blue
    };
}
