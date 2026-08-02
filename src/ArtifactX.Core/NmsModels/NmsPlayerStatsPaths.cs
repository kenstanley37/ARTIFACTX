using System.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helpers for this SAVE's lifetime stats (GcPlayerStateData.Stats,
/// obfuscated key "gUR" - the SAME key as every other field literally named
/// "Stats" elsewhere in this app, e.g. Settlement/Frigate Stats arrays, since
/// this obfuscation scheme hashes by field NAME text, not by which class the
/// field lives in). Confirmed at vLc/6f=/gUR by structurally searching a real
/// save's JSON for the GcPlayerStatsGroup shape (a GroupId + Address + nested
/// Stats list) rather than guessing by position.
///
/// SCOPED TO THIS SAVE FILE, not the player's account or their other save
/// slots - this lives inside the same top-level vLc/6f= (GcPlayerStateData)
/// block as everything else on this save, and each of the app's other save
/// slots is a fully independent file with its own separate copy of this
/// whole structure. "Not tied to any single pet" (the actual point worth
/// making, since CompanionsPage also has a genuinely different PER-PET
/// "Holo-Arena Victories" field) is not the same claim as "shared across the
/// account" - an earlier version of this doc comment conflated the two.
///
/// Unlike every other array this app edits, this ISN'T a flat list of simple
/// values or id strings - it's a TWO-LEVEL structure: an outer list of STAT
/// GROUPS (confirmed via a real save: 72 groups total - one "GLOBAL_STATS"
/// group holding 457 general lifetime counters, one "TELEMETRY" group, and
/// 70 per-visited-planet groups keyed by Address). Each group:
///   ":rc" = GroupId (short string, e.g. "^GLOBAL_STATS" - NOTE: an earlier
///           pass here misread this as literally "GLOBAL", from truncating a
///           debug print at 6 characters and mistaking the cut-off text for
///           the complete value - caught once the actual field read back 0
///           for everything in the app, not from re-reading the source data
///           more carefully first)
///   "2Ak" = Address (ulong, 0 for GLOBAL_STATS/TELEMETRY, a real planet
///           address for the per-planet ones)
///   "gUR" = Stats again (yes, the SAME obfuscated key as the OUTER array -
///           GcPlayerStatsGroup's own field is ALSO literally named "Stats")
///           - a list of GcPlayerStat entries
/// Each stat entry:
///   "b2n" = Id (short string, e.g. "^PB_WINS")
///   "&gt;MX" = Value (a GcStatValueData wrapper: "&gt;vs" = IntValue,
///           "eoL" = FloatValue, a 3rd Denominator field not seen populated
///           in the sample - only IntValue is exposed here, every Arena
///           League stat found so far is a plain int counter)
///
/// Because this is a "find by id" structure like Settlement Perks/Frigate
/// Traits, NOT a fixed-index array, callers must locate the GLOBAL_STATS
/// group's index and a stat's index within it at RUNTIME by scanning the
/// live JSON (see CompanionsPage.xaml.cs's ResolveGlobalStatGroupIndex/
/// ResolveStatIndex) - group order/position is not assumed stable across
/// different saves.
///
/// ARENA LEAGUE STAT IDS (2026-07-30) - cross-referenced against a real
/// in-game Catalog &amp; Guide/Milestones screenshot of the "Arena League"
/// faction panel, CONFIRMED EXACT for all 4 against a fresh save reload:
///   PB_PETS_MAXED -&gt; "Apex Companions" (4 == 4)
///   EGGS_HATCHED -&gt; "Hatch Eggs" (11 == 11)
///   PB_BOSS_WINS -&gt; "Champions Defeated" (40 == 40)
///   PB_WINS -&gt; "Holo-Arena Victories" (49 == 49) - NOT the same thing as
///     the existing PER-PET "Holo-Arena Victories" field already on
///     CompanionsPage (NmsCompanionPaths.HoloArenaVictoriesPath) - that's each
///     individual creature's own win count; this is this save's total
///     across every pet combined, a genuinely different number in a
///     completely different part of the save.
/// An earlier check against an older decrypted snapshot had these last two
/// off by 3 (37/46 vs. 40/49) - re-checking against a freshly reloaded save
/// resolved that as just real gameplay progress between snapshots, not a
/// mapping error; all 4 are now confirmed exact.
///
/// PB_CHALL_WINS (6 in the sample) and PB_LOSSES (4) are clearly
/// Arena-adjacent by naming but weren't matched to anything specific visible
/// on the Milestones screen - not exposed yet.
///
/// PB_D_NEXUS -&gt; "Iteration: Oceanus" (2026-07-31, resolved after the id
/// search above missed it - "OCEAN"/"OCEANUS" doesn't appear anywhere in the
/// stat's own id text, it's an unrelated internal codename like the other
/// Arena stats' abbreviations). Confirmed via value progression across two
/// real saves rather than a single-snapshot text match: an older save/dump
/// from before this milestone was ever earned has NO PB_D_NEXUS entry at all
/// (matching the screenshot's dim/unlit "0/1" medal from that time), while
/// this SAME account's current save has PB_D_NEXUS = 1, exactly matching a
/// fresh screenshot's "Iteration: Oceanus - Total: 1" after the player
/// earned that first win. It's also the only still-unmapped PB_-prefixed
/// stat whose value is a plausible "victories" count (PB_CHALL_WINS/
/// PB_LOSSES are both non-zero and track something else). Exposed as the
/// 5th Arena League box.
///
/// EXPLORATION/SURVIVAL MILESTONES + THE GEK/VY'KEEN/KORVAX (2026-08-01) -
/// confirmed exact by dumping all 457 GLOBAL_STATS entries from a real save
/// and cross-checking every candidate against 6 real Catalog &amp; Guide
/// screenshots (one per category). All matched exactly except where noted:
///   Exploration Milestones: JM-&gt;"Overall Journey" (71==71),
///     ALIENS_MET-&gt;"Alien Encounters" (90==90), WORDS_LEARNT-&gt;"Words
///     Collected" (272==272), DIST_WARP-&gt;"Space Exploration" (122==122).
///     "Planetary Zoology" (0/1 ecosystems) has no confident candidate - too
///     many other 0-valued stats to disambiguate from a single screenshot;
///     not exposed.
///   Survival Milestones: SENTINEL_KILLS-&gt;"Sentinels Destroyed" (151==151),
///     MONEY-&gt;"Units Accrued" (1992707379==1992707379, NOT MONEY_EVER,
///     which is a smaller/different counter), ENEMIES_KILLED-&gt;"Ships
///     Destroyed" (80==80, naming mismatch between the raw id and the
///     in-game label - confirmed by value, not by name). DIST_WALKED
///     (float, "eoL" not "&gt;vs") -&gt;"On-foot Exploration" was 119828.64 in
///     the dump vs. 120,158 in the screenshot - not an exact match, but the
///     ~0.3% gap is consistent with real gameplay between the dump and the
///     screenshot being taken, not a wrong mapping (no other candidate is
///     remotely close in magnitude for a walking-distance-shaped stat).
///     LONGEST_LIFE_EX (float) -&gt;"Extreme Survival" matched exactly
///     (1252.54 == 1252.5, the screenshot's own display already truncates
///     to 1 decimal).
///   The Gek/Vy'keen/Korvax share one underlying scheme: a per-race letter
///   prefix (T=Gek, W=Vy'keen, E=Korvax) on STANDING/DONE_MISSIONS/
///   WORDS_LEARNT/SEEN_SYSTEMS, e.g. TRA_STANDING/TDONE_MISSIONS/
///   TWORDS_LEARNT/TSEEN_SYSTEMS for the Gek - all 12 (4 fields x 3 races)
///   confirmed exact against their respective screenshots. Each race's 5th,
///   unique field also confirmed: WALKERS_KILLED-&gt;Vy'keen "Walkers
///   Destroyed" (2==2), NANITES_EVER-&gt;Korvax "Nanite Clusters"
///   (26183==26183, NOT plain NANITES, which is current on-hand currency).
///   Gek's own unique 5th field ("Smuggling Run", 0 in the screenshot) has
///   no confident candidate for the same reason as Planetary Zoology above
///   - not exposed. NOTE: EXP_STANDING is Korvax's standing, not "Explorers
///   Guild" as an earlier pass here guessed from the id text alone before
///   any screenshot existed to check against.
///
/// OUTLAWS + THE AUTOPHAGE + PARTIAL MERCHANTS/EXPLORERS GUILD (2026-08-01,
/// same day) - a 2nd screenshot batch resolved the BUI_* mystery from just
/// above: it's not a coincidence or something unrelated, it's **The
/// Autophage's** own letter prefix (a 4th race-style scheme alongside Gek/
/// Vy'keen/Korvax's T/W/E, thematically fitting since Autophage are lore-wise
/// "divergent Korvax" - a related-but-separate lineage). Confirmed exact:
///   Outlaws: PIRATE_STAND-&gt;"Standing" (9==9), PIRATE_MISSIONS-&gt;"Missions
///     Completed" (4==4), BOUNTIES-&gt;"Bounties Completed" (25==25).
///     "Smuggling Run" and "Freighters Plundered" both read 0 in the
///     screenshot with no confident candidate to disambiguate - not exposed
///     (same reasoning as Planetary Zoology/Gek's Smuggling Run above).
///   The Autophage: BUI_STANDING-&gt;"Standing" (15==15, the same value that
///     made this look like a Korvax/EXP_STANDING coincidence before this
///     screenshot existed - it wasn't), BDONE_MISSIONS-&gt;"Missions
///     Completed" (3==3), BWORDS_LEARNT-&gt;"Words Learned" (140==140,
///     confirms the B-prefix independently of Standing),
///     DRONE_SHARDS-&gt;"Radiant Shards" (12==12, thematic fit - Autophage are
///     robotic). "Autophage Salvage" (3 in the screenshot) ties with
///     BDONE_MISSIONS and several unrelated stats - not exposed.
///   Merchants Guild (partial): MONEY-&gt;"Units Earned" (1992707379==
///     1992707379, the SAME field as Survival Milestones' "Units Accrued" -
///     confirmed dual-purpose, not a conflict), PLANTS_PLANTED-&gt;"Plants
///     Farmed" (18==18), PROC_PRODS-&gt;"Rare Treasures" (103==103, only
///     candidate at that value, name doesn't obviously match but same
///     ENEMIES_KILLED-style mismatch precedent already seen once).
///   Explorers Guild (partial): RARE_SCANNED-&gt;"Rare Fauna Scanned"
///     (37==37), DIST_WARP-&gt;"Systems Mapped" (123==123, the SAME field as
///     Exploration Milestones' "Space Exploration" - confirmed dual-purpose
///     again, off by 1 from the earlier Exploration Milestones screenshot's
///     122, consistent with real gameplay between the two screenshots),
///     DISC_FLORA-&gt;"Plants Cataloged" (66==66).
///   "Standing" and "Missions Completed" for Merchants Guild and Explorers
///   Guild both read 0 in their screenshots - TGUILD_STAND/WGUILD_STAND/
///   EGUILD_STAND and the matching *GDONE_MISSIONS fields are the obvious
///   structural candidates (same "T/W/E + GUILD" naming pattern, one per
///   guild) but ALL read None/0 in this save, so there's no way to verify
///   which maps to which guild by value alone - not exposed until someone's
///   save has non-zero guild standing to check against.
///
/// STILL UNMAPPED: Mercenaries Guild (no screenshot provided yet) and
/// Previous Expeditions - the latter does not look like it lives in
/// GLOBAL_STATS at all (it's a list of named/numbered expedition completion
/// badges in game, not a lifetime counter), so it likely needs an entirely
/// different save structure and probably different UI than a NumberBox grid
/// once someone tracks it down.
/// </summary>
public static class NmsPlayerStatsPaths
{
    public static readonly string[] StatGroupsArrayPath = { "vLc", "6f=", "gUR" };

    public static string[] GroupPath(int groupIndex) => new[] { "vLc", "6f=", "gUR", groupIndex.ToString() };
    public static string[] GroupIdPath(int groupIndex) => GroupPath(groupIndex).Append(":rc").ToArray();
    public static string[] GroupStatsArrayPath(int groupIndex) => GroupPath(groupIndex).Append("gUR").ToArray();

    public static string[] StatPath(int groupIndex, int statIndex) =>
        GroupPath(groupIndex).Append("gUR").Append(statIndex.ToString()).ToArray();
    public static string[] StatIdPath(int groupIndex, int statIndex) => StatPath(groupIndex, statIndex).Append("b2n").ToArray();
    public static string[] StatIntValuePath(int groupIndex, int statIndex) => StatPath(groupIndex, statIndex).Append(">MX").Append(">vs").ToArray();

    /// <summary>Some stats (e.g. DIST_WALKED, LONGEST_LIFE_EX) store their
    /// value as a float ("eoL") instead of an int ("&gt;vs") - the two are
    /// mutually exclusive on any given entry, never both populated.</summary>
    public static string[] StatFloatValuePath(int groupIndex, int statIndex) => StatPath(groupIndex, statIndex).Append(">MX").Append("eoL").ToArray();
}
