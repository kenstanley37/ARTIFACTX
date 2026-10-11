namespace ArtifactX.Almanac.Freighter;

/// <summary>
/// "Capital Ship Research" blueprint-gated items - a completely different
/// unlock mechanism from the plain known-technology array CataloguePage
/// edits (vLc/6f=/4kj). Discovered 2026-08-11/12 via a real controlled
/// in-game test (see project_fresh_save_bug_sweep memory): the user
/// researched 4 specific blueprints (Fuel Oxidiser, Mind Control Device,
/// Freighter Glass Corridor, and the Red recolour option) and a before/after
/// save diff showed the SAME array Catalogue edits (4kj) was completely
/// unchanged - the real array is vLc/6f=/eZ<
/// (NmsFreighterUpgradePaths.BlueprintArrayPath), a separate flat list of
/// "^ITEM_ID" strings that gained exactly those 4 new entries:
/// ^FRIG_BOOST_SPD, ^FRIG_BOOST_TRA, ^FRE_CORR_A_GLAS, ^FREIGHT_RED.
///
/// eZ< is NOT Freighter-exclusive - its very first sampled entry
/// ("BUILD_REFINER1") is a base-building unlock unrelated to freighters, so
/// it's NMS's general "unlocked blueprint" tracking array shared across
/// several systems. This list is the curated Freighter/Frigate-relevant
/// subset only, found by cross-referencing the confirmed GameIds' naming
/// prefixes (FRIG_, FRE_, CORRIDOR*_SPACE, FREIGHT_) against the shipped
/// catalog DB's existing GcProductTable rows - real item names, not guessed,
/// confirmed via `DataCataloger grep`/DB query, not copied from any external
/// source. The "Currency" shown alongside these on the research screen
/// ("Salvaged Frigate Module") turned out to not need any of this at all -
/// it's just a stack of a regular Product item (^FRIG_TOKEN) sitting in the
/// Freighter's own Cargo (already fully editable via FreighterPage's
/// existing Cargo grid).
///
/// The 10 "Recolouring" blueprints are NOT listed here: they come from the
/// catalog's "FreighterPaints" category (DataCataloger add-freighter-paints,
/// read from the game's customisationcolourpalettes.mbin) along with each
/// paint's real colour. An earlier hand-typed version here guessed the cyan
/// paint as FREIGHT_TURQUOISE; the real id is FREIGHT_CYAN (2026-10-10).
///
/// Still open: the true FULL universe of possible Construction Module/
/// Frigate Upgrade blueprint IDs couldn't be confirmed from a single
/// authoritative game-data table (the obvious candidate,
/// metadata/gamestate/difficultyconfig.mbin, does reference these IDs per a
/// raw grep hit, but the generic dumpfile inspector's depth/list-length caps
/// truncated before reaching them) - this list was assembled from what the
/// shipped catalog DB already had correctly named under matching prefixes,
/// cross-checked against every item visible across the user's own Capital
/// Ship Research screenshots. If a real save shows a locked item not in this
/// list, that's a real gap to fill, not a sign the mechanism is wrong.
/// </summary>
public static class FreighterUpgrades
{
    public static readonly IReadOnlyList<FreighterUpgradeInfo> All = new[]
    {
        // Frigate Upgrade - consumable, one-expedition frigate boosts. Each
        // is gated behind researching Fuel Oxidiser first (confirmed via the
        // in-game "BLUEPRINT LOCKED - Research Fuel Oxidizer to unlock
        // blueprint" prompt shown on all 4 of the others).
        new FreighterUpgradeInfo("FRIG_BOOST_SPD", "Fuel Oxidiser", "Frigate Upgrade"),
        new FreighterUpgradeInfo("FRIG_BOOST_TRA", "Mind Control Device", "Frigate Upgrade"),
        new FreighterUpgradeInfo("FRIG_BOOST_MIN", "Mineral Compressor", "Frigate Upgrade"),
        new FreighterUpgradeInfo("FRIG_BOOST_COM", "Explosive Drones", "Frigate Upgrade"),
        new FreighterUpgradeInfo("FRIG_BOOST_EXP", "Holographic Analyser", "Frigate Upgrade"),

        // Construction Module - freighter rooms/parts, placed as whole
        // pre-fabricated modules rather than assembled piece by piece.
        new FreighterUpgradeInfo("CORRIDOR_S", "Freighter Corridor", "Construction Module"),
        new FreighterUpgradeInfo("CORRIDOR_SPACE", "Freighter Corridor", "Construction Module"),
        new FreighterUpgradeInfo("CORRIDORL_SPACE", "Curved Freighter Corridor", "Construction Module"),
        new FreighterUpgradeInfo("CORRIDORT_SPACE", "Freighter Junction", "Construction Module"),
        new FreighterUpgradeInfo("CORRIDORX_SPACE", "Freighter Cross Junction", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_A", "Freighter Corridor", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_A_GLAS", "Freighter Glass Corridor", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_A_L", "L-Junction", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_A_STR", "Straight Corridor", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_A_T", "T-Junction", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_GLA_L", "Glass L-Junction", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_GLA_ST", "Straight Glass Corridor", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_GLA_T", "Glass T-Junction", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_G_STA", "Freighter Glass Stairs", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_STA", "Freighter Stairs", "Construction Module"),
        new FreighterUpgradeInfo("FRE_EXT_PLATFOR", "Exterior Platform", "Construction Module"),
        new FreighterUpgradeInfo("FRE_EXT_WALKWAY", "Exterior Catwalk", "Construction Module"),
        new FreighterUpgradeInfo("FRE_EXT_W_STA", "Freighter Exterior Stairs", "Construction Module"),
        new FreighterUpgradeInfo("FRE_FACE_DOOR_A", "Bulkhead Door", "Construction Module"),
        new FreighterUpgradeInfo("FRE_FACE_WALL", "Internal Freighter Wall", "Construction Module"),
        new FreighterUpgradeInfo("FRE_FACE_WINDOW", "Reinforced Window", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_BIO", "Biological Room (Expansion)", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_COOK", "Nutrition Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_DRESS", "Appearance Modifier Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_EXTR", "Stellar Extractor Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_FLEET", "Fleet Command Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_IND", "Industrial Room (Expansion)", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_IND1", "Industrial Room (Expansion Variant)", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_LADDER", "Ladder Module", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_NPCBUI", "Construction Specialist's Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_NPCFAR", "Agricultural Specialist's Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_NPCSCI", "Science Specialist's Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_NPCVEH", "Exocraft Specialist's Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_NPCWEA", "Weapons Specialist Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_PLANT0", "Double Cultivation Chamber", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_PLANT1", "Cultivation Chamber", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_REFINE", "Refiner Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_ROCLOC", "Storage Shuttle Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_SCAN", "Scanner Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_SHOP", "Galactic Trade Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE0", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE1", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE2", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE3", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE4", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE5", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE6", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE7", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE8", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_STORE9", "Storage Room", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_TECH", "Technology Room (Expansion)", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_TELEPO", "Teleport Chamber", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_VEHICL", "Orbital Exocraft Materializer", "Construction Module"),
        // Newer-update parts, found 2026-10-10 in a real save's eZ< list but
        // missing here; names from the catalog's own GcProductTable rows.
        new FreighterUpgradeInfo("FRE_CORR_B", "Freighter Passageway", "Construction Module"),
        new FreighterUpgradeInfo("FRE_ROOM_B", "Commercial Room (Expansion)", "Construction Module"),
        new FreighterUpgradeInfo("FRE_CORR_STA_B", "Steel Staircase", "Construction Module"),
    };
}
