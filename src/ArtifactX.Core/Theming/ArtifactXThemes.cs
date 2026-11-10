namespace ArtifactX.Core.Theming;

/// <summary>
/// Canonical theme palette tokens shared between ArtifactX.WinUI3 (desktop) and ArtifactX.Maui (mobile).
/// Every theme defines the exact same semantic colour slots and corner radii.
/// </summary>
public sealed record ArtifactXThemePalette(
    string Id,
    string DisplayName,
    string VoidHex,
    string ChromeHex,
    string PanelHex,
    string PanelAltHex,
    string LineHex,
    string AccentHex,
    string AccentAltHex,
    string DoneHex,
    string TextHex,
    string TextDimHex,
    string TextFaintHex,
    string OnAccentHex,
    double CardCornerRadius);

public static class ArtifactXThemes
{
    public static readonly ArtifactXThemePalette GalaxyMap = new(
        Id: "GalaxyMap",
        DisplayName: "Galaxy Map",
        VoidHex: "#04050B",
        ChromeHex: "#08090F",
        PanelHex: "#14182C",
        PanelAltHex: "#1E223A",
        LineHex: "#2A3150",
        AccentHex: "#FFC62E",
        AccentAltHex: "#FF9D3D",
        DoneHex: "#7EF0C2",
        TextHex: "#ECEAF2",
        TextDimHex: "#BDB8CC",
        TextFaintHex: "#938DA8",
        OnAccentHex: "#04050B",
        CardCornerRadius: 10);

    public static readonly ArtifactXThemePalette StationCore = new(
        Id: "StationCore",
        DisplayName: "Station Core",
        VoidHex: "#070912",
        ChromeHex: "#0C1024",
        PanelHex: "#10142A",
        PanelAltHex: "#161B38",
        LineHex: "#2A3160",
        AccentHex: "#39D0FF",
        AccentAltHex: "#7C4DFF",
        DoneHex: "#FFC62E",
        TextHex: "#E9ECF6",
        TextDimHex: "#B4BCD6",
        TextFaintHex: "#8E97B4",
        OnAccentHex: "#070912",
        CardCornerRadius: 2);

    public static IReadOnlyList<ArtifactXThemePalette> All { get; } = [GalaxyMap, StationCore];
}
