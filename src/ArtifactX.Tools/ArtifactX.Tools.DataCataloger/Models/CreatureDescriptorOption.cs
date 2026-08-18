namespace ArtifactX.Tools.DataCataloger.Models;

/// <summary>
/// One node in a creature body-part "Descriptors" tree
/// (models/planets/creatures/&lt;rig&gt;/&lt;name&gt;.descriptor.mbin,
/// TkModelDescriptorList) - see CatalogBuildService's Phase 1.7 for how this
/// is extracted and ArtifactX.Core.NmsModels.NmsPetPaths' class doc comment
/// (the "osl" bullet) for how a pet's own save data references these nodes
/// by OptionId. Self-referencing via ParentOptionId to preserve the real
/// recursive tree shape - the flat CatalogItem/CatalogCategory schema every
/// other extracted table uses doesn't fit this data.
/// </summary>
public class CreatureDescriptorOption
{
    public int Id { get; set; }

    /// <summary>Which rig this node belongs to, e.g. "trex" - the source
    /// descriptor.mbin's own filename stem, lowercased. Matches a pet's own
    /// XID lowercased for most species; see Phase 1.7's comment for the
    /// confirmed exceptions (word-order mismatches like SWIMCOW/cowswim).</summary>
    public string RigId { get; set; } = "";

    /// <summary>The slot/category this option belongs to, e.g. "_HEAD_" -
    /// sibling options sharing the same Category under the same parent are
    /// mutually exclusive alternatives (a pet picks exactly one per slot).</summary>
    public string Category { get; set; } = "";

    /// <summary>Raw save-file value, e.g. "_HEAD_ALIEN" - matches an osl
    /// array entry once its leading "^" is stripped.</summary>
    public string OptionId { get; set; } = "";

    /// <summary>Display label from the source data, e.g. "_Head_Alien" -
    /// the game's own internal label, not a real player-facing localized
    /// name (no loc key exists anywhere for these).</summary>
    public string Name { get; set; } = "";

    /// <summary>Selection weight from the source data - 0 on every sampled
    /// node so far, kept for completeness rather than confirmed useful.</summary>
    public float Chance { get; set; }

    /// <summary>Null for a rig's top-level options; otherwise the option
    /// this one is nested under (e.g. an EYES option is nested under
    /// whichever BLOB option it belongs to, which is itself nested under a
    /// HEAD option).</summary>
    public int? ParentOptionId { get; set; }
    public CreatureDescriptorOption? ParentOption { get; set; }

    public List<CreatureDescriptorOption> Children { get; set; } = new();
}
