namespace ArtifactX.WinUI3.Models;

/// <summary>One creature as a planet's page shows it: from the save's discoveries and the game's creature
/// generator. <paramref name="SeenAt"/> says which ("Discovered" or "Unscanned").</summary>
public sealed record Creature(string Name, string Rarity, string Habitat, string Activity, string Gender, string Size, string SeenAt);
