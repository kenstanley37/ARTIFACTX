namespace ArtifactX.Almanac.Starship;

/// <summary>
/// One confirmed Starship "Type" (base hull model), same shape as
/// MultiToolTypeInfo - only the model path actually matters for a swap.
/// </summary>
public sealed record StarshipTypeInfo(string DisplayName, string ScenePath);
