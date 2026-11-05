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
/// Each entry is libMBIN's GcWordGroupKnowledge: MYl = the word GROUP id, D;o =
/// one flag per GcAlienRace value (9), set for the race whose language the word
/// is known in. Saves the game wrote (Slots 1, 4, 5, checked 2026-11-04) set
/// only the word's own race: TRA_ index 0 (Traders), WAR_ 1, EXP_ 2, ATLAS_ 4,
/// BUI_ 8 (Builders). Until 2026-11-04 ArtifactX wrote [true + 7 false] for
/// every race, so Slot 7 holds over 2,000 Vy'keen/Korvax/Autophage/Atlas
/// entries flagged as Gek only; whether the game counts those is unchecked.
/// Confirmed in game 2026-11-04 (Slot 8): the Korvax milestone read 89 words,
/// matching the 89 EXP_ groups in the save; six Korvax words written by
/// ArtifactX with the Korvax flag, then one more learned in game, made it 96.
/// The milestone number lagged until that next word was learned - it is the
/// separate WORDS_LEARNT stat, which the game recounts only on a new word.
///
/// The catalog's GcAlienLanguageWords category (catalog format 71) lists the
/// learnable groups from the game's speech table, with their English forms:
/// Gek 928, Vy'keen 936, Korvax 864, Autophage 889, Atlas 226 in build 184271.
/// Slot 1, which the game reports as knowing every Gek word, holds all 928 Gek
/// groups. Several word forms share one group ("traveller", "travellers"), so
/// the older list of every form left ~236 Gek words "unknown" forever (#31).
///
/// That 5-word diff also matched the account's own Words Learned count
/// precisely, corroborating this is really the same list that stat tracks.
/// WRITING through ArtifactX itself hasn't been round-trip-checked in-game
/// the way the Catalogue page's known-technology write was - worth a quick
/// check after a real edit here.
/// </summary>
public static class NmsLanguagePaths
{
    public static readonly string[] KnownWordsArrayPath = { "vLc", "6f=", "MF2" };
}
