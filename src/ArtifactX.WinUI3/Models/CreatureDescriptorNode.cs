namespace ArtifactX.WinUI3.Models;

/// <summary>One node from CatalogService.GetCreatureDescriptorTreeAsync -
/// see ArtifactX.Core.NmsModels.NmsPetPaths' class doc comment (the "osl"
/// bullet) for how a pet's own save data references these nodes by
/// OptionId, and CatalogBuildService's Phase 1.7 for how the tree is
/// extracted. ParentOptionId is the PARENT NODE'S OWN OptionId (not a raw
/// database row id), so a returned list is self-contained and stable across
/// catalog rebuilds - null for a rig's top-level options.</summary>
public sealed class CreatureDescriptorNode
{
    public required string Category { get; init; }
    public required string OptionId { get; init; }
    public required string Name { get; init; }
    public string? ParentOptionId { get; init; }
}
