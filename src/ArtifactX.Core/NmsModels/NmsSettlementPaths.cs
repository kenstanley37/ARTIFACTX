using System.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helpers for owned Settlements. Unlike Pets (a small flat 30-slot
/// array under the same vLc/6f= container), settlements live in a much
/// bigger 100-slot array (GQA) that is NOT exclusively "mine" - most of its
/// entries belong to OTHER real players (confirmed 2026-07-29: of 100
/// entries in a real save, 96 had a name, but only the 2 the player
/// actually owns - "Settlement1"/"Settlement2" - had an Owner matching the
/// local account; the other 94 named ones belonged to strangers like
/// "robshawre342"/"Chocogury_13KH", almost certainly settlements
/// encountered/synced from other players during normal play). A sibling
/// array, nlG (72 items in the same sample), also references settlements
/// by name but is a much smaller, generic-looking discovery/waypoint-marker
/// list (position + orientation + a type tag "iAF" that can be "Settlement"
/// among presumably other values, + 3 bools) - NOT the rich settlement
/// record, and not used here.
///
/// Occupancy/ownership: a slot is "mine" when its Name (NKm) is non-empty
/// AND its Owner's OnlineID (3?K/K7E) matches the local account's own
/// OnlineID, read from LocalPlayerOnlineIdPath (&lt;h0/F=J[0]/K7E - the
/// save's own single-entry "local platform account" record, confirmed
/// present in every top-level save and exactly matching both owned
/// settlements' Owner.K7E in the one real save checked so far). An empty
/// slot has NKm == "" (not the "^" bare-caret convention Pets/other pages
/// use) and every other field zeroed/defaulted (hiD == -1, x3&lt; == 0,
/// :Qn/SS2/HMQ wrapper objects all their "None" member, abj's 2 production
/// slots present but with CZw == "^" and zeroed amounts).
///
/// Field map CONFIRMED via exact positional cross-reference (2026-07-29)
/// against libMBIN's own GcSettlementState - a real settlement record's 32
/// JSON keys appear in EXACTLY the same order as GcSettlementState's 32
/// fields by NMS Index (0-31), the same technique that helped confirm
/// several CreatureSave fields for Pets. This is strong static evidence
/// (same field count, same order, and every sampled value is plausible for
/// its matched field's type/name - e.g. Population=22 for a real
/// settlement vs 0 for empty, Race="Warriors" is a real AlienRaceEnum
/// member, Stats has exactly 8 entries matching SettlementStatTypeEnum's 8
/// members) but is NOT the same as in-game test confirmation the way
/// Pets fields eventually got.
///
/// FIRST REAL IN-GAME COMPARISON (2026-07-29): Name and Population (x3&lt;)
/// both matched exactly. Two of the 8 Stats (Debt, Happiness) were close to
/// the in-game display (within normal simulation drift over the time
/// between decrypt and screenshot). The other 4 displayed values in-game
/// (Max Population, Productivity, Maintenance Cost, Sentinel Alert Level)
/// did NOT match their assumed Stats slots (MaxPopulation/Production/
/// Upkeep/Sentinels all read back near 0 or a value unrelated to what was
/// shown) - those are most likely computed live from Buildings/Perks
/// rather than stored as a simple absolute number in Stats, so editing
/// those 4 specific slots may do nothing visible even though the array
/// position mapping itself is probably still correct. Race and the Perks
/// array (see PerksPath below) were independently confirmed correct by
/// name via NomNom (an established third-party NMS save editor) showing
/// matching values for the same real settlement.
///
/// Full field map, in Index/JSON-key order:
///  0 20I  = UniqueId (NMSString0x40, hex-looking id, e.g. "12c32f3ab8745642")
///  1 yhJ  = UniverseAddress (ulong)
///  2 wMC  = Position ([x,y,z] floats)
///  3 qK9  = SeedValue (ulong, serialized as a "0x..."-prefixed hex string -
///           same underlying value as UniqueId/20I in the one sample seen,
///           just formatted differently; not confirmed whether that's
///           always true or coincidental for this settlement)
///  4 d3x  = BuildingStates (int[48], Size 0x30 - per-plot building
///           type/state, matches GcSettlementState.BuildingStates exactly
///           by array size)
///  5 3@T  = LastBuildingUpgradesTimestamps (ulong[48] - mostly 0, one
///           real timestamp-shaped value at whichever plot was last
///           upgraded, matching array size 0x30 exactly)
///  6 NKm  = Name (NMSString0x40) - see NamePath below
///  7 3?K  = Owner (GcDiscoveryOwner-shaped: f5Q=LocalID, K7E=OnlineID,
///           V?:=Username, D6b=Platform e.g. "ST" for Steam, 3I1=Timestamp -
///           positional match against GcDiscoveryOwner's 5 fields, though
///           libMBIN's own copy of that class only declares 4 - the 5th
///           JSON key still matches an int/Timestamp shape) - see
///           OwnerOnlineIdPath/OwnerUsernamePath/OwnerPlatformPath below
///  8 HMQ  = PendingJudgementType (wrapped enum, field "?SU", "None" when
///           no settlement judgement/decision is currently pending)
///  9 Xs1  = PendingCustomJudgementID (NMSString0x10, "^" when none)
/// 10 gUR  = Stats (int[8] - see StatsPath below, matches
///           GcSettlementStatType.SettlementStatTypeEnum's exact 8 members
///           in order: MaxPopulation/Happiness/Production/Upkeep/
///           Sentinels/Debt/Alert/BugAttack)
/// 11 OEf  = Perks (string[8], "^" for an empty slot else a perk id like
///           "^PROC_FUN#09286" - NOT exposed for editing yet, since valid
///           perk ids aren't cataloged anywhere yet, unlike Pets' osl)
/// 12 0Qr  = LastJudgementTime (ulong timestamp)
/// 13 OI3  = LastUpkeepDebtCheckTime (ulong timestamp)
/// 14 g&lt;v  = LastDebtChangeTime (ulong timestamp)
/// 15 HWh  = LastAlertChangeTime (ulong timestamp)
/// 16 Air  = LastBugAttackChangeTime (ulong timestamp)
/// 17 Kvr  = DbResourceId (NMSString0x40, empty on the one sample seen)
/// 18 hMW  = DbTimestamp (ulong)
/// 19 gEp  = DbVersion (int)
/// 20 abj  = ProductionState (2 GcSettlementProductionSlotData-shaped
///           entries: CZw=item id e.g. "^POWERCELL"/"^GAS1", 6X1=timestamp,
///           1o9=?, T=R=capacity, U2&gt;=current amount?, N6t=wrapped enum
///           "None", 5a3=fill fraction 0-1, :aM=a second float always 1.0
///           in the one sample seen) - NOT exposed yet, needs more samples
///           to pin down U2&gt;/1o9's exact meaning before editing safely
/// 21 i4g  = IsReported (bool)
/// 22 hiD  = NextBuildingUpgradeIndex (int, -1 when none pending - NOT
///           "population" despite being a prominent-looking number; that
///           was an early misread corrected once the libMBIN cross-check
///           lined up NextBuildingUpgradeIndex at this exact position)
/// 23 :Qn  = NextBuildingUpgradeClass (wrapped enum, field "iqv", e.g.
///           "Settlement_LandingZone" when a specific building class is
///           queued, "None" otherwise)
/// 24 xru  = NextBuildingUpgradeSeedValue (ulong, hex-string formatted)
/// 25 SS2  = Race (wrapped GcAlienRace enum, field "0Hi" - despite the
///           name, this is the settlement's CULTURE/archetype, not a
///           player species: Traders/Warriors/Explorers/Robots/Atlas/
///           Diplomats/Exotics/None/Builders) - see RacePath below
/// 26 @rg  = MiniMissionStartTime (ulong timestamp)
/// 27 Ak8  = MiniMissionSeed (ulong, 0 when no mini-mission generated yet)
/// 28 rr0  = LastJudgementPerkID (NMSString0x10 - references one of the
///           Perks/OEf entries, e.g. "^PROC_FUN#09286" matched OEf[5]
///           exactly in the one sample seen)
/// 29 x3&lt;  = Population (ushort) - see PopulationPath below
/// 30 WGY  = LastPopulationChangeTime (ulong timestamp)
/// 31 PZh  = LastWeaponRefreshTime (list, empty when no defensive weapons
///           have needed a respawn timer)
/// </summary>
public static class NmsSettlementPaths
{
    public static readonly string[] SettlementArrayPath = { "vLc", "6f=", "GQA" };

    public static string[] SettlementPath(int settlementIndex) =>
        new[] { "vLc", "6f=", "GQA", settlementIndex.ToString() };

    /// <summary>The save's own single "local platform account" record -
    /// confirmed present as a 1-entry list at every top-level save checked
    /// so far, shaped like a GcDiscoveryOwner (see this class's doc comment).
    /// Used to tell "my settlements" apart from the many other real
    /// players' settlements that also live in the GQA array - compare a
    /// candidate settlement's OwnerOnlineIdPath against this same path's
    /// value.</summary>
    public static readonly string[] LocalPlayerOnlineIdPath = { "<h0", "F=J", "0", "K7E" };

    public static string[] NamePath(int settlementIndex) => SettlementPath(settlementIndex).Append("NKm").ToArray();

    public static string[] OwnerOnlineIdPath(int settlementIndex) => SettlementPath(settlementIndex).Append("3?K").Append("K7E").ToArray();
    public static string[] OwnerUsernamePath(int settlementIndex) => SettlementPath(settlementIndex).Append("3?K").Append("V?:").ToArray();
    public static string[] OwnerPlatformPath(int settlementIndex) => SettlementPath(settlementIndex).Append("3?K").Append("D6b").ToArray();

    /// <summary>Race (settlement Culture/archetype, e.g. "Warriors") - the
    /// complete, authoritative value set is libMBIN's own GcAlienRace.
    /// AlienRaceEnum. ArtifactX.Core deliberately doesn't reference libMBIN
    /// (a WinUI3-side concern), so the UI pulls the enum's values directly
    /// via Enum.GetNames rather than a hand-copied duplicate living here -
    /// same pattern as Pets' CreatureTypePath.</summary>
    public static string[] RacePath(int settlementIndex) => SettlementPath(settlementIndex).Append("SS2").Append("0Hi").ToArray();

    public static string[] PopulationPath(int settlementIndex) => SettlementPath(settlementIndex).Append("x3<").ToArray();

    /// <summary>The whole 8-entry Perks array ("buffs" shown in-game under
    /// "Settlement Features") - each entry is either "^" (empty) or a
    /// caret-prefixed perk id, e.g. "^PROC_FUN#09286" (IsProc-flagged
    /// perks carry a "#NNNNN" per-instance roll suffix; non-Proc perks like
    /// "^SENT_QUAR"/"^POSITIVE_EXTRA2" don't, confirmed in a real sample).
    /// Real perk ids and their resolved names/descriptions/stat effects are
    /// catalogued separately - see CatalogService.GetSettlementPerksAsync
    /// and CatalogBuildService's Phase 1.8. Staged as a whole array, same
    /// reasoning as StatsPath below.</summary>
    public static string[] PerksPath(int settlementIndex) => SettlementPath(settlementIndex).Append("OEf").ToArray();

    /// <summary>The whole 8-entry Stats array (MaxPopulation/Happiness/
    /// Production/Upkeep/Sentinels/Debt/Alert/BugAttack, in that exact
    /// order - matches libMBIN's GcSettlementStatType.SettlementStatTypeEnum).
    /// Staged as a whole, same reasoning as Pets' TraitsPath/
    /// MutationPointsPath (a deeper leaf-only stage isn't seen by
    /// SaveSessionManager's staged-edit lookup).</summary>
    public static string[] StatsPath(int settlementIndex) => SettlementPath(settlementIndex).Append("gUR").ToArray();
}
