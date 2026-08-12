namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helpers for the account-wide accountdata.hg file - NOT a per-save-
/// slot save file. Confirmed via direct inspection (2026-08-02): unlike
/// save*.hg (LZ4-block-compressed, see SaveStreamProcessor), accountdata.hg
/// is plain, uncompressed JSON starting at byte 0 - it can be read/written
/// as plain UTF-8 text directly, with no container framing to strip or
/// rewrap. It lives in the same folder as every save slot for a given
/// platform account (one accountdata.hg per account folder), so its path is
/// derived from whichever save is currently active rather than selected
/// independently - see AccountSessionManager.ResolveAccountDataPath.
///
/// Top-level shape: {"F2P": &lt;int&gt;, "B89": {...}}. B89 holds ~15 lists of
/// raw item/content ids representing different "unlocked/seen" categories,
/// confirmed via direct inspection against a real account file. These DO
/// carry the same leading "^" prefix per-slot save arrays use (an earlier
/// note here claimed otherwise and was wrong - confirmed 2026-08-02 after
/// that assumption made every account item read as locked in the UI; always
/// go through CatalogService.NormalizeId when matching these against the
/// catalog DB):
///   B1h (~3681 entries) - the master "everything unlocked" list; ~96% of
///     sampled ids cross-reference successfully against ArtifactX's own
///     item catalog DB (crafting/building/cosmetic items). Used here as the
///     authoritative catalog-unlock list for Phase 1.
///   bLB/&gt;Qn/fyX/d4U - smaller, narrower category-specific unlock lists
///     (blueprints, banners, base parts) that mostly overlap with B1h's
///     contents - NOT edited yet; it's unconfirmed whether the game
///     requires all of them kept in sync or whether B1h alone drives the
///     in-game Catalog screen, so it's safer to leave them untouched until
///     that's confirmed.
///   Bgb (339)/&lt;5B (405) - lore text unlocks / Twitch drop rewards - 0%
///     catalog-DB coverage (not craftable items), need a different name
///     source entirely - not exposed yet (Phase 2).
/// </summary>
public static class NmsAccountData
{
    /// <summary>The master "everything unlocked" list - every crafting recipe,
    /// blueprint, base part, and cosmetic the account has ever picked up or
    /// been granted, referenced by raw (non-caret-prefixed) item id.</summary>
    public static readonly string[] UnlockedItemsPath = { "B89", "B1h" };

    /// <summary>The in-game Guide screen's per-topic Unlocked/Seen state -
    /// found 2026-08-12 via a real controlled test, same methodology as
    /// everything else discovered this way this session: user unchecked
    /// several Guide topics via a reference tool's own Guide tab (which
    /// splits Unlocked and Seen into two separate checkbox columns), and
    /// diffing accountdata.hg before/after showed two ~57-entry arrays
    /// (aZ4/b1D) each losing exactly the same 9 entries - flat lists of raw
    /// "^UI_GUIDE_TOPIC_*" ids (59 known total across the game's own loc
    /// data, see ArtifactX.Almanac.Guide.GuideTopics). A second, isolated
    /// test (unchecking ONLY "Seen" for one topic, leaving "Unlocked"
    /// checked) showed b1D alone lose that one entry - confirming b1D=Seen,
    /// aZ4=Unlocked, not just "these two arrays are related somehow".</summary>
    public static readonly string[] GuideUnlockedPath = { "B89", "aZ4" };
    public static readonly string[] GuideSeenPath = { "B89", "b1D" };
}
