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
}
