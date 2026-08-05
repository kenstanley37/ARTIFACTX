namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helper for this SAVE's fishing-record data - three PARALLEL fixed
/// 256-slot arrays under vLc.6f=.bTf (sibling of the Stats array and the
/// known-technology/language-word arrays in the same vLc.6f= GcPlayerStateData
/// container), matching libMBIN's GcFishingRecord template exactly:
///   5gB = ProductList     (raw "^"-prefixed GcFishTable ProductIds; "^" = empty slot)
///   yv6 = ProductCountList (uint - times that species has been caught)
///   CXv = LargestCatchList (float - largest catch size recorded for that species)
///
/// Confirmed directly (2026-08-05) against the user's real ~100+ hour save:
/// 220 of 256 slots filled, packed CONTIGUOUSLY from index 0 with zero gaps
/// (no slot N+1 filled while slot N is still empty) - the game itself only
/// ever appends new catches at the next open index, never leaves holes. Any
/// edit here must preserve that same packed/contiguous shape across all
/// three arrays together (never just 5gB alone - yv6/CXv would drift out of
/// index-alignment with it otherwise), the same "stage the whole array"
/// discipline NmsCataloguePaths/NmsLanguagePaths already use, just across
/// three co-indexed arrays instead of one.
///
/// The full catalog of possible fish (GcFishTable's own ProductID list, not
/// a hand-guessed naming-pattern regex - an early attempt at inferring the
/// list from the F_<biome>_<quality>_<size> id convention alone undercounted
/// by ~10 against this same save's real 220 entries) is extracted into the
/// catalog DB by DataCataloger's `add-fish-records` command.
/// </summary>
public static class NmsFishingPaths
{
    /// <summary>The fixed slot count both the game's own MBIN template
    /// (GcFishingRecord, Size = 0x100) and every real save observed use -
    /// unlike the variable-length "just append" arrays elsewhere in this app
    /// (4kj/MF2/B1h), all three arrays below must always stay exactly this
    /// length, never grow or shrink.</summary>
    public const int SlotCount = 256;

    /// <summary>Shared prefix for HasStagedEditsUnder/RevertEditsUnder - covers
    /// all three arrays below in one call, since a real edit here always
    /// touches all three together (see FishingRecordsPage.SetRowCaught).</summary>
    public static readonly string[] RecordContainerPath = { "vLc", "6f=", "bTf" };

    public static readonly string[] ProductListPath = { "vLc", "6f=", "bTf", "5gB" };
    public static readonly string[] ProductCountListPath = { "vLc", "6f=", "bTf", "yv6" };
    public static readonly string[] LargestCatchListPath = { "vLc", "6f=", "bTf", "CXv" };
}
