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

    /// <summary>The underlying catalog row's own id, exposed ONLY as a
    /// relative ordering hint (not a stable identity - it can and will
    /// change across catalog rebuilds) - since extraction walks each rig's
    /// source data depth-first in its original left-to-right order, the
    /// relative order of SortOrder values within one query result still
    /// reconstructs which sibling category/option came first in the game's
    /// own data, every time the catalog is rebuilt. Needed to build a
    /// correctly-ordered default osl array when swapping the top-level
    /// archetype entry - see CompanionsPage.BuildDefaultDescriptorArray.</summary>
    public required int SortOrder { get; init; }
}
