namespace ArtifactX.Almanac.MultiTool;

/// <summary>
/// Multi-Tool "Type" (base model) options, sourced directly from the real list
/// of .SCENE.MBIN files found under models/common/weapons/multitool/ (see
/// `DataCataloger multitool`), not from any in-game data table - none exists
/// mapping a Type name to a model path (confirmed by searching the classified
/// catalog, grepping raw MBIN field values, and dumping the one plausible
/// candidate's actual structure - see project history). NomNom itself
/// maintains its own hand-curated, version-pinned mapping for the same reason.
///
/// Path is the only field that matters: an in-game test confirmed the visual
/// swap works correctly writing ONLY the model path (NTx.93M), with no other
/// field required.
///
/// Excluded from the discovered file list, and why:
///   - SWITCHMULTITOOL.SCENE.MBIN: Nintendo Switch crossover exclusive, not
///     normal equippable content - unpredictable outside that context.
///   - STAFFNPCMULTITOOL.SCENE.MBIN: explicitly NPC-designated, likely missing
///     player-specific rig/animation hookups.
///   - Muzzle flashes, projectiles, sub-assembly parts (atlasmtparts/*), and
///     effects - not base models at all, just referenced by the real ones.
///
/// TO UPDATE AFTER A GAME PATCH: re-run `DataCataloger multitool` against the
/// updated install, diff its output against this list, add any new entries
/// (display name is just a cleaned-up version of the filename), and drop any
/// that no longer exist. No new testing needed - the visual-swap mechanism
/// itself is confirmed generic, not per-file.
/// </summary>
public static class MultiToolTypes
{
    public static readonly IReadOnlyList<MultiToolTypeInfo> All = new[]
    {
        new MultiToolTypeInfo("Rifle", "MODELS/COMMON/WEAPONS/MULTITOOL/MULTITOOL.SCENE.MBIN"),
        new MultiToolTypeInfo("Voltaic Staff", "MODELS/COMMON/WEAPONS/MULTITOOL/STAFFMULTITOOL.SCENE.MBIN"),
        new MultiToolTypeInfo("Sentinel", "MODELS/COMMON/WEAPONS/MULTITOOL/SENTINELMULTITOOL.SCENE.MBIN"),
        new MultiToolTypeInfo("Sentinel (Alt)", "MODELS/COMMON/WEAPONS/MULTITOOL/SENTINELMULTITOOLB.SCENE.MBIN"),
        new MultiToolTypeInfo("Atlas", "MODELS/COMMON/WEAPONS/MULTITOOL/ATLASMULTITOOL.SCENE.MBIN"),
        new MultiToolTypeInfo("Voltaic Staff (Atlas)", "MODELS/COMMON/WEAPONS/MULTITOOL/STAFFMULTITOOLATLAS.SCENE.MBIN"),
        new MultiToolTypeInfo("Voltaic Staff (Bone)", "MODELS/COMMON/WEAPONS/MULTITOOL/STAFFMULTITOOLBONE.SCENE.MBIN"),
        new MultiToolTypeInfo("Voltaic Staff (Ruin)", "MODELS/COMMON/WEAPONS/MULTITOOL/STAFFMULTITOOLRUIN.SCENE.MBIN"),
        new MultiToolTypeInfo("Rod", "MODELS/COMMON/WEAPONS/MULTITOOL/RODMULTITOOL.SCENE.MBIN"),
        new MultiToolTypeInfo("Royal", "MODELS/COMMON/WEAPONS/MULTITOOL/ROYALMULTITOOL.SCENE.MBIN"),
        new MultiToolTypeInfo("Swarm", "MODELS/COMMON/WEAPONS/MULTITOOL/SWARMMULTITOOL.SCENE.MBIN"),
    };
}