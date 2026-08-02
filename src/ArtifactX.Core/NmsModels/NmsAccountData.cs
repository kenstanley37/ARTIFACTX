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
}
