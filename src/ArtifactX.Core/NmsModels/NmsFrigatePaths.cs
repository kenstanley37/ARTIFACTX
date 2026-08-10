using System.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helpers for owned Frigates (the freighter fleet roster shown on the
/// in-game "Frigate List" screen). Unlike Settlements' GQA array, this list
/// (;Du, under the same vLc/6f= container as GQA/nlG) is NOT shared with
/// other players - it's a plain, directly-owned `List&lt;GcFleetFrigateSaveData&gt;`
/// with no ownership filter needed, confirmed at vLc/6f=/;Du by parsing a
/// real save's full JSON and searching for the literal key (not just
/// guessed by position).
///
/// Field map CONFIRMED (2026-07-30) via exact positional cross-reference
/// against libMBIN's GcFleetFrigateSaveData (16 fields, JSON key order
/// matches NMS Index order exactly, same technique used for Settlements/
/// Companions) - AND independently verified against real screenshot data: all 29
/// frigates in the test save (Steam Slot 6) were dumped and cross-checked
/// one-by-one against 4 screenshots of the in-game "Frigate List" panel.
/// Every single one matched exactly (Class badge, Role label, and all 4
/// visible stat numbers), giving much higher upfront confidence than
/// Settlements had at the same stage. What this does NOT yet cover: writing
/// a NEW value back and confirming the game accepts/reflects it after a
/// save+reload - only reading/matching existing values has been verified so
/// far, the same "read confirmed, write untested" gap Settlement Stats had
/// before its own additive-vs-literal surprise. Don't assume editing works
/// exactly as expected (e.g. additive vs. literal, min/max enforcement)
/// until a real edit-reload test happens.
///
/// Full field map, in Index/JSON-key order:
///  0 SLc = ResourceSeed (GcSeed, [hasValue, hexSeed]) - drives the
///          procedurally-generated flavor name/appearance used when
///          CustomName is empty; not edited here (hand-picking a
///          meaningful seed isn't practical)
///  1 @ui = HomeSystemSeed (GcSeed) - not edited here
///  2 =rR = ForcedTraitsSeed (GcSeed) - not edited here
///  3 4kx = TimeOfLastIncomeCollection (ulong timestamp) - not edited here
///  4 fH8 = CustomName (NMSString0x100) - see CustomNamePath below
///  5 uw7 = FrigateClass (wrapped GcFrigateClass.FrigateClassEnum, inner
///          field ALSO named "uw7" - an outer/inner key collision, same
///          pattern occasionally seen elsewhere e.g. Race/InventoryClass
///          below) - see FrigateClassPath below
///  6 SS2 = Race (wrapped GcAlienRace enum, inner field "0Hi") - IDENTICAL
///          key pair to Settlement's Race field (SS2/0Hi), confirming this
///          obfuscation scheme hashes by field NAME text, not by which
///          class the field lives in - see RacePath below
///  7 1o6 = InventoryClass (wrapped GcInventoryClass.InventoryClassEnum,
///          inner field ALSO "1o6") - the S/A/B/C class badge shown at the
///          left of each row in-game - see InventoryClassPath below
///  8 5es = TotalNumberOfExpeditions (int)
///  9 v=L = TotalNumberOfSuccessfulEvents (int)
/// 10 5VG = TotalNumberOfFailedEvents (int)
/// 11 MuL = NumberOfTimesDamaged (int)
/// 12 Mjm = TraitIDs (List&lt;NMSString0x10&gt;, NO Size attribute in
///          libMBIN - genuinely unbounded like Settlement's Perks array,
///          even though every one of the 29 sampled frigates had exactly 5
///          entries, some "^" empty. Given this session's own Perks-array
///          mistake, do NOT read "5" as a real cap without a growth test
///          the way Perks eventually got. Raw ids only, e.g. "^NORMANDY_1"/
///          "^MINING_PRI" - no catalog built yet, shown read-only for now.
/// 13 gUR = Stats (List&lt;int&gt;, also NO Size attribute, but every
///          sampled frigate had exactly 11 entries - matching
///          GcFrigateStatType.FrigateStatTypeEnum's 11 members exactly:
///          Combat/Exploration/Mining/Diplomatic/FuelBurnRate/
///          FuelCapacity/Speed/ExtraLoot/Repair/Invulnerable/Stealth.
///          CONFIRMED against real screenshots: the in-game list view only
///          shows the first 4 (Combat/Exploration/Mining/Diplomatic) as its
///          4 numeric columns - Stats[0..3] matched the screenshot numbers
///          exactly for all 29 frigates (including negative values like
///          -2, and the UI showing "-" for a value of exactly 0, not a
///          missing value). The other 7 stats aren't shown on this panel;
///          presumably used elsewhere (fuel mechanics, expedition odds) -
///          see StatsPath below.
/// 14 yJC = RepairsMade (int)
/// 15 7hK = DamageTaken (int)
///
/// Role label / auto-generated name derivation (informational, not stored
/// separately - CONFIRMED via all 29 real frigates matching their
/// screenshot row exactly): FrigateClass drives the in-game "Role" pill
/// shown next to each frigate, which does NOT read the enum name literally:
///   Combat -&gt; COMBAT             Support -&gt; SUPPORT
///   Exploration -&gt; EXPLORATION   Normandy -&gt; RECON (easter egg: always
///   Mining -&gt; INDUSTRIAL           renders as "SSV Normandy SR1")
///   Diplomacy -&gt; TRADE           DeepSpace -&gt; ORGANIC
///   Pirate -&gt; RAIDER             GhostShip -&gt; DOOMED
/// DeepSpaceCommon and Swarm (the remaining 2 of 11 enum members) weren't
/// present in the 29-frigate sample, so their Role/name mapping is
/// unconfirmed. When CustomName (fH8) is empty, the flashy name shown
/// in-game (e.g. "SS-2 Akeosc's Gamble", "CV The Hawk of Dreams") is
/// generated at render time from ResourceSeed + Race + InventoryClass +
/// FrigateClass and is NOT stored anywhere in the save - ArtifactX can't
/// reproduce or preview it, only show that the slot is auto-named.
/// </summary>
public static class NmsFrigatePaths
{
    public static readonly string[] FrigateArrayPath = { "vLc", "6f=", ";Du" };

    public static string[] FrigatePath(int frigateIndex) =>
        new[] { "vLc", "6f=", ";Du", frigateIndex.ToString() };

    public static string[] CustomNamePath(int frigateIndex) => FrigatePath(frigateIndex).Append("fH8").ToArray();

    /// <summary>FrigateClass (e.g. "Combat", "Mining") - the complete value
    /// set is GcFrigateClass.FrigateClassEnum. Pulled directly via
    /// Enum.GetNames in the UI rather than duplicated here, same pattern as
    /// Settlement's RacePath. See this class's doc comment for the
    /// Role-label translation table.</summary>
    public static string[] FrigateClassPath(int frigateIndex) => FrigatePath(frigateIndex).Append("uw7").Append("uw7").ToArray();

    /// <summary>Same key pair (SS2/0Hi) as Settlement's RacePath - both are
    /// the same GcAlienRace enum type, just embedded in different parent
    /// classes.</summary>
    public static string[] RacePath(int frigateIndex) => FrigatePath(frigateIndex).Append("SS2").Append("0Hi").ToArray();

    /// <summary>InventoryClass (C/B/A/S) - the colored class badge shown at
    /// the left of each row in the in-game Frigate List. Complete value set
    /// is GcInventoryClass.InventoryClassEnum.</summary>
    public static string[] InventoryClassPath(int frigateIndex) => FrigatePath(frigateIndex).Append("1o6").Append("1o6").ToArray();

    /// <summary>The 2nd element of ResourceSeed (SLc), i.e. just the "0x..."
    /// hex string - matches a reference tool's own "Model Seed" (confirmed:
    /// NORMANDY's SLc[1] value 0xE876443F6A0A28A6 exactly matched what that
    /// reference showed for the same field on the same frigate). Same
    /// "append '1' to reach the hex sibling, ignore the leading bool"
    /// pattern already used for Freighter's ModelSeedPath/CrewSeedPath
    /// (NmsInventoryContainer).</summary>
    public static string[] ModelSeedPath(int frigateIndex) => FrigatePath(frigateIndex).Append("SLc").Append("1").ToArray();

    /// <summary>The 2nd element of HomeSystemSeed (@ui) - matches a
    /// reference tool's own "Home Seed" (confirmed: NORMANDY's @ui[1] value
    /// 0x0 exactly matched).</summary>
    public static string[] HomeSeedPath(int frigateIndex) => FrigatePath(frigateIndex).Append("@ui").Append("1").ToArray();

    /// <summary>The Traits array (5 raw ids per frigate in every sample seen,
    /// e.g. "^NORMANDY_1") - now catalog-backed and editable, same pattern as
    /// Settlement's PerksPath. Real names/exact numeric stat deltas come from
    /// CatalogService.GetFrigateTraitsAsync and CatalogBuildService's Phase
    /// 1.9, which decodes frigatetraittable.mbin (name/id/which-stat/which-
    /// tier per trait) AND gcfleetglobals.global.mbin's FrigateTraitStrengths
    /// (the tier->exact-number lookup).
    ///
    /// Checked all 5 traits on the SSV Normandy SR1 (index 0) against a real
    /// reference screenshot of that exact frigate: 3 of 5 matched EXACTLY,
    /// including the number - NORMANDY_1 "Deep Scout Prototype (+15 Combat)",
    /// NORMANDY_2 "Tantalus Drive (+15 Exploration)", NORMANDY_3 "Mass
    /// Accelerator Cannon (+6 Combat)". The other 2 resolved the correct
    /// trait NAME but a DIFFERENT effect description than the reference
    /// shows: NORMANDY_4 ("Internal Emission Sink") computed here as "+1
    /// Stealth" (mathematically correct per the raw FrigateStatType=Stealth/
    /// Strength=TertiaryMedium -> StatAlteration lookup) vs. the reference's
    /// "Silent Running Capability Enabled"; NORMANDY_5 ("Long-Range FTL
    /// Capacity") computed as "+3 Speed" (Speed/TertiaryLarge, also
    /// mathematically correct per the same table) vs. the reference's "+3%
    /// Expedition Duration". Best guess: that reference tool hardcodes
    /// special flavor text for a handful of unique/named traits like these
    /// two rather than always deriving from the generic stat-delta formula -
    /// nothing in the decoded MBIN data suggests a different, more "correct"
    /// number exists to find. Treat the computed description as accurate to
    /// the game's raw data model, but not guaranteed to read exactly like
    /// that reference tool's for every trait.
    ///
    /// Like Perks, this array is declared unbounded (List&lt;NMSString0x10&gt;
    /// in libMBIN, no Size attribute) even though every sampled frigate had
    /// exactly 5 - don't assume that's an enforced cap without a growth test
    /// (see Settlement Perks' own history of getting this wrong). In the UI
    /// each row is one array entry; an empty entry (bare "^") shows as
    /// "(None)" in its dropdown.</summary>
    public static string[] TraitIDsPath(int frigateIndex) => FrigatePath(frigateIndex).Append("Mjm").ToArray();

    /// <summary>The whole 11-entry Stats array (Combat/Exploration/Mining/
    /// Diplomatic/FuelBurnRate/FuelCapacity/Speed/ExtraLoot/Repair/
    /// Invulnerable/Stealth, in that exact order - matches libMBIN's
    /// GcFrigateStatType.FrigateStatTypeEnum). Staged as a whole, same
    /// reasoning as Settlement's StatsPath.</summary>
    public static string[] StatsPath(int frigateIndex) => FrigatePath(frigateIndex).Append("gUR").ToArray();

    public static string[] TotalNumberOfExpeditionsPath(int frigateIndex) => FrigatePath(frigateIndex).Append("5es").ToArray();
    public static string[] TotalNumberOfSuccessfulEventsPath(int frigateIndex) => FrigatePath(frigateIndex).Append("v=L").ToArray();
    public static string[] TotalNumberOfFailedEventsPath(int frigateIndex) => FrigatePath(frigateIndex).Append("5VG").ToArray();
    public static string[] NumberOfTimesDamagedPath(int frigateIndex) => FrigatePath(frigateIndex).Append("MuL").ToArray();
    public static string[] RepairsMadePath(int frigateIndex) => FrigatePath(frigateIndex).Append("yJC").ToArray();
    public static string[] DamageTakenPath(int frigateIndex) => FrigatePath(frigateIndex).Append("7hK").ToArray();
}
