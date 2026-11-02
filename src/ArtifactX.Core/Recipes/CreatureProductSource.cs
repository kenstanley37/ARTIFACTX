namespace ArtifactX.Core.Recipes;

/// <summary>Direct creature-global product mappings joined to the creature data
/// table. These describe creature sources, not guaranteed planet spawns.</summary>
public sealed record CreatureProductSource(string ItemId, string CreatureId, string CreatureKind,
    string Habitat, string Method, int MinimumBlobs);
