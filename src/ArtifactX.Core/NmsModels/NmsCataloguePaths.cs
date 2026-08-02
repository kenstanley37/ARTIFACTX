namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helper for this SAVE's "known/discovered technology" list - a flat
/// array of raw ("^"-prefixed) GcTechnologyTable ids at vLc.6f=.4kj, sibling
/// of the Stats array NmsPlayerStatsPaths already owns (same vLc.6f=
/// GcPlayerStateData container). PER-SLOT, not account-wide - unlike
/// accountdata.hg's B89/B1h list (see NmsAccountData), which was tested
/// directly and found to have no confirmed in-game effect, this field's
/// effect WAS confirmed directly (2026-08-02): appending an id the account
/// had never crafted/owned (HYPERDRIVE_SPEC, "Frameshift Catapult") made it
/// appear named/revealed in the ship's technology-install picker where it
/// was previously absent, after a real save/reload cycle.
///
/// Cross-referenced against the catalog DB: of 219 entries in the user's
/// main 100+ hour save, 218 matched a real GcTechnologyTable row (the lone
/// miss, T_BOBBLE_OCTO, is very likely just missing from the catalog DB,
/// not a different category) - this field is effectively pure
/// GcTechnologyTable, never Product/Substance/ProceduralTechnology.
/// </summary>
public static class NmsCataloguePaths
{
    public static readonly string[] KnownTechnologyArrayPath = { "vLc", "6f=", "4kj" };
}
