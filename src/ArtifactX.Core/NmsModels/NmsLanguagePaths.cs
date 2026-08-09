namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helper for this SAVE's known alien-vocabulary word list - a flat
/// array of raw ("^"-prefixed) word ids at vLc.6f=.MF2, sibling of the Stats
/// array (NmsPlayerStatsPaths) and the known-technology array
/// (NmsCataloguePaths) in the same vLc.6f= GcPlayerStateData container.
///
/// Confirmed directly (2026-08-04): a controlled before/after diff (5 Gek
/// words marked known via a reference tool, then re-extracted) showed this array grow
/// by exactly 5 entries - ^TRA_ATTACK, ^TRA_BEWARE, ^TRA_BLOOD, ^TRA_COMBAT,
/// ^TRA_DANGER - with every entry having exactly one `True` flag in its own
/// small metadata array (semantics of that flag not fully decoded, but
/// membership in this list alone is what "known" means; no entry was found
/// with all-False flags).
///
/// SHARED across all 5 groups in ONE array, not one array per group - each
/// entry's own id prefix identifies which group's word it is:
///   TRA_ = Gek ("Traders"), WAR_ = Vy'keen ("Warriors"),
///   EXP_ = Korvax ("Explorers"), BUI_ = Autophage,
///   ATLAS_ = a smaller (~262-word) special vocabulary pool that isn't tied
///   to any race and has no matching WORDS_LEARNT stat in Milestones -
///   added 2026-08-04 after noticing it mixed into the same array during the
///   original investigation.
/// A per-group page must read/write this SAME path, filtering only its own
/// prefix for display while preserving every other group's entries untouched
/// when staging an edit (see LanguageWordsControl).
///
/// The catalog DB's GcAlienLanguageWords category (added via DataCataloger's
/// `add-language-words` command, decoding language/nms_loc1_english.mbin -
/// the exact same loc-string system used for every other item name) supplies
/// real English names, keyed by these exact ids: TRA_ATTACK -> "attack",
/// confirmed against the save's own entries exactly. ~1,100-1,170 possible
/// words per race exist in the full vocabulary (Atlas's own pool is smaller,
/// ~262); a given save's actual known subset is normally a small fraction of
/// that (e.g. 21-140 in a real mid-progress save).
/// </summary>
public static class NmsLanguagePaths
{
    public static readonly string[] KnownWordsArrayPath = { "vLc", "6f=", "MF2" };
}
