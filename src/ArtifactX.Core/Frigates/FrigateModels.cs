namespace ArtifactX.Core.Frigates;

/// <summary>
/// A frigate class's model (catalog format 109, row FRIGATEMODELS): metadata/simulation/space/aispaceshipmanager.mbin's
/// Frigate-role ShipModels entries, by GcFrigateClass (Combat 0 ... Swarm 10).
/// </summary>
/// <param name="Class">GcFrigateClass value - the save's frigate class (;Du uw7).</param>
/// <param name="Scene">The model the game spawns, upper case.</param>
/// <param name="PreviewScene">The scene the 3D preview draws: the model's own LOD 1 scene (its TkLODComponentData's
/// second LODModel) when it has one - LOD 0 also holds the boardable interior, 270,000 to 1,000,000 triangles in build
/// 185034, LOD 1 is the hull at 60,000 to 160,000 - else <see cref="Scene"/> itself.</param>
public sealed record FrigateModel(int Class, string Scene, string PreviewScene);
