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
/// between decrypt and screenshot). The other 4 (Max Population,
/// Production/Productivity, Upkeep/Maintenance Cost, Sentinels/Sentinel
/// Alert Level) did NOT match with the field left at its original value -
/// first read as "probably computed independently, ignoring Stats
/// entirely." A SECOND round (2026-07-29, same day) corrected that: setting
/// Production to a large, distinctive value (2,000,000,000) produced an
/// in-game Productivity display of 2,000,301,113 - a 0.015% difference,
/// i.e. essentially the edited value flowing straight through. Setting Max
/// Population from 0 to 100 moved the displayed max from ~53-56 to 159, a
/// big correlated jump but not a 1:1 overwrite. Best-supported model now:
/// the display is ADDITIVE - edited Stats value + a baseline contribution
/// from Buildings/Perks/other settlement state - not "Stats is ignored."
/// Confirmed editing these 4 slots DOES have a real, visible in-game
/// effect; it's just relative to a baseline, not an absolute override.
/// Upkeep/Sentinels haven't specifically been pushed to a large distinctive
/// value the way Production was, so their exact additive-vs-ignored status
/// is still one notch less certain than Production/MaxPopulation's. The
/// Perks array (see PerksPath below) was independently confirmed correct by
/// name via NomNom (an established third-party NMS save editor) showing
/// matching values for the same real settlement.
///
/// Perks array size CONFIRMED as exactly 8, not more (2026-07-29): the
/// in-game "Settlement Features" panel paginates 6 per page, which briefly
/// looked like it might imply a 12-slot cap when 2 pages were seen - a real
/// test settled it: with exactly 7 of the 8 OEf slots populated, the panel
/// showed 6 on page 1 and exactly 1 on page 2, matching an 8-slot array
/// perfectly (2 pages is just what 7-8 populated items looks like at
/// 6-per-page, regardless of whether the true cap is 8 or 12 - this
/// specific count settles it at 8).
///
/// Race (SS2/0Hi) is CONFIRMED WORKING (2026-07-30), via a clean
/// one-variable-at-a-time test round after an earlier scattered/uncontrolled
/// round wrongly looked inconsistent. It does NOT write the in-game "X
/// Planetary Settlement" subtitle text literally - the game TRANSLATES the
/// culture archetype into one of the real playable species for that
/// subtitle, matching known NMS lore associations exactly:
///   Traders  -> "Gek"      (Gek are canonically merchants/traders)
///   Warriors -> "Vy'keen"  (Vy'keen are canonically a warrior culture)
///   Explorers -> "Korvax"  (Korvax are canonically explorers/scientists)
/// Those 3 were directly confirmed via save-edit-reload round trips, each
/// showing the exact predicted subtitle. The other 6 enum members (Robots/
/// Atlas/Diplomats/Exotics/None/Builders) haven't been individually tested,
/// though "Robots" -> "Autophage" is a strong untested guess (Autophage are
/// the mechanical NMS race, seen in an earlier uncontrolled test round).
///
/// IMPORTANT CAVEAT this field does NOT affect: a settlement's "Dominant
/// Lifeform" (visible on the deeper stats/Overseer-tenure screen, reached
/// via a second interaction, not the main overview) stayed "Vy'keen"
/// across all three race changes including when the subtitle correctly
/// showed "Korvax" - so Dominant Lifeform is a genuinely separate,
/// still-unmapped stat, not driven by SS2. Don't assume changing Race
/// affects anything beyond the cosmetic subtitle text until Dominant
/// Lifeform's real backing field is found.
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
/// 25 SS2  = Race (wrapped GcAlienRace enum, field "0Hi" - the type-level
///           name suggests the settlement's CULTURE/archetype, not a
///           player species: Traders/Warriors/Explorers/Robots/Atlas/
///           Diplomats/Exotics/None/Builders - but UNCONFIRMED/likely
///           inconsistent, see RacePath below for why)
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
    /// complete value set libMBIN declares is GcAlienRace.AlienRaceEnum.
    /// ArtifactX.Core deliberately doesn't reference libMBIN (a WinUI3-side
    /// concern), so the UI pulls the enum's values directly via
    /// Enum.GetNames rather than a hand-copied duplicate living here - same
    /// pattern as Pets' CreatureTypePath. CONFIRMED WORKING (2026-07-30) via
    /// a clean one-variable-at-a-time save+reload test: this field controls
    /// the in-game "X Planetary Settlement" subtitle, but the game
    /// TRANSLATES it to a real species name rather than writing it
    /// literally - Traders-&gt;"Gek", Warriors-&gt;"Vy'keen", Explorers-&gt;
    /// "Korvax" were each individually confirmed (matches known NMS lore
    /// associations for those species). The other 6 enum members are
    /// untested. Confirmed NOT to affect a separate "Dominant Lifeform"
    /// stat shown on the settlement's deeper Overseer-tenure screen, which
    /// stayed fixed across all three race changes - that's a genuinely
    /// different, still-unmapped field.</summary>
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
    /// reasoning as StatsPath below.
    ///
    /// CONFIRMED (2026-07-30): writing an IsProc perk id with NO roll
    /// suffix works fine - the game generates fresh, readable, thematically
    /// appropriate flavor text on its own rather than needing one supplied.
    /// Real examples from a save+reload test (GameId -&gt; in-game text):
    /// "^PROC_FARM" (catalog Name template "%FOOD% %FARM%") rendered as
    /// "Ever-burning Jam cannery"; "^POL_MOOD" ("%HAPPY_ADJ%
    /// %HAPPY_FACILITY%") rendered as "Generous festivals"; "^PROC_BUI"
    /// ("%GREEK%") rendered as literal fake-Greek gibberish
    /// ("11&#947;&#955;110&#956;&#953;") - apparently the intended joke for
    /// that specific template. Safe to pick any cataloged perk id, Proc or
    /// not, without worrying about reconstructing a suffix.</summary>
    public static string[] PerksPath(int settlementIndex) => SettlementPath(settlementIndex).Append("OEf").ToArray();

    /// <summary>The whole 8-entry Stats array (MaxPopulation/Happiness/
    /// Production/Upkeep/Sentinels/Debt/Alert/BugAttack, in that exact
    /// order - matches libMBIN's GcSettlementStatType.SettlementStatTypeEnum).
    /// Staged as a whole, same reasoning as Pets' TraitsPath/
    /// MutationPointsPath (a deeper leaf-only stage isn't seen by
    /// SaveSessionManager's staged-edit lookup).</summary>
    public static string[] StatsPath(int settlementIndex) => SettlementPath(settlementIndex).Append("gUR").ToArray();
}
