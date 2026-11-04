using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Companions;

/// <summary>A companion personality trait, in the game's GcCreaturePetTraits order
/// (the order of a companion's three trait values in the save, NmsCompanionPaths).</summary>
public enum EggTrait { Helpfulness, Aggression, Independence }

/// <summary>The sequencer's catalyst slots, in the game's order (0x1410d6400 slots 1-4).</summary>
public enum EggCatalystSlot { GrowthHormone, GeneSplitter, DyeInjector, NeuralCalibrator }

/// <summary>A PetEggTraitModifierOverrideTable row: an item the game gives a fixed
/// personality effect instead of the hashed one.</summary>
public sealed record EggTraitOverride(string ItemId, EggTrait Trait, bool Increases);

/// <summary>
/// The Egg Sequencer's tuning, read from the player's game files (catalog row
/// EGGSEQUENCER, CatalogSteps.AddEggSequencer): GcCreatureGlobals' PetEgg* values,
/// the trait override table and the product whose value is a full dose
/// (PetEggMaxChangeProduct, SACVENOM in build 184271). ItemLimit
/// (PetEggModificationItemLimit, 999) caps how many of an item a slot takes
/// (the sequencer screen clamps the count to it at 0x140784c59). Ingredients
/// are the products and substances flagged EggModifierIngredient, the only
/// items the sequencer takes (it counts them by that flag at 0x1410d5c57).
/// Texts holds the game's English words for the screen (slot names, their
/// subtitles, the Embryo Status labels and the six personality words).
/// </summary>
public sealed record EggSequencerRules(
    IReadOnlyList<EggTraitOverride> Overrides,
    string FullDoseProductId,
    float SubstanceModifier,
    float TraitRangeModifier,
    float TraitRangeMax,
    float ScaleRangeModifier,
    float ScaleRangeMax,
    float AccessoryChanceModifier,
    float MaxAccessoriesChangeChance,
    float ColourChanceModifier,
    float MaxColourChangeChance,
    float OverdosageModifier,
    float MaxOverdosage,
    float MaxTopDescriptorChangeChance,
    int ItemLimit,
    IReadOnlyList<string> Ingredients,
    IReadOnlyDictionary<string, string> Texts);

/// <summary>What one item always does in each slot (whatever the amount).</summary>
/// <param name="Trait">The trait the Neural Calibrator moves.</param>
/// <param name="TraitIncreases">True moves it toward its positive word
/// (Helpfulness, Aggression, Independence), false toward the negative one
/// (Playfulness, Gentleness, Devotion).</param>
/// <param name="TraitFromTable">The game lists this item in its override table
/// rather than hashing its id.</param>
/// <param name="Grows">Whether the Growth Hormone makes the creature bigger.</param>
/// <param name="FullDoseCount">How many make a full dose; 0 when the item has
/// no value and does nothing.</param>
/// <param name="CertainChangeCount">How many make the Gene Splitter's and Dye
/// Injector's change certain; 0 when the item does nothing.</param>
public sealed record EggCatalystEffect(EggTrait Trait, bool TraitIncreases, bool TraitFromTable, bool Grows,
    int FullDoseCount, int CertainChangeCount);

/// <summary>What a given amount of an item does in one slot.</summary>
/// <param name="Count">The amount, after the slot's limit.</param>
/// <param name="Dose">0..1, as the game's "Dose: N%".</param>
/// <param name="Overdose">Gene Splitter only: the share past a full dose the
/// game shows as "Overdosed! (100% + N)"; 0 otherwise.</param>
/// <param name="ChangeChance">Gene Splitter and Dye Injector: the chance of a
/// change (0..1).</param>
/// <param name="BodyTypeChance">Gene Splitter: the chance of changing the main
/// body type, from overdosing.</param>
/// <param name="TraitChange">Neural Calibrator: the trait change in points
/// (the game shows a trait 0-100).</param>
/// <param name="SizeShare">Growth Hormone: the change as a share of the
/// species' size range.</param>
public sealed record EggSlotOutcome(int Count, double Dose, double Overdose, double ChangeChance,
    double BodyTypeChance, double TraitChange, double SizeShare);

/// <summary>
/// The Egg Sequencer's rules, read off NMS.exe build 184271 on 2026-11-04
/// (static disassembly) and checked against the owner's sequencer screens the
/// same day: Ice Cream x2 in the Growth Hormone "Dose: 100%" and Weight /
/// Height "Increasing"; Viscous Custard x2 in the Neural Calibrator 100% and
/// Gentleness "Increasing" (lowers Aggression); Shadow Lure x117 100% and
/// Gentleness "Decreasing" (raises Aggression; 117 is exactly a full dose);
/// Frozen Tubers x100 in the Gene Splitter "Overdosed! (102%)"; Ferrite Dust x1
/// "Dose: 0%". Every value agrees.
///
///   Dose (0x1410d6fa0 for the Neural Calibrator; the other slots at
///   0x1410d6e99, 0x1410d7579, 0x1410d7959): value per unit is the item's
///   BaseValue override from the trait table when above 0 (every row is -1 in
///   184271), otherwise its BaseValue, times PetEggSubstanceModifier for a
///   substance (0x1410d7a60). Dose = min(count * value, full) / full, where
///   full is the BaseValue of PetEggMaxChangeProduct (read at 0x1410d5874).
///
///   Overdose, Gene Splitter only (0x1410d7646): (count * BaseValue -
///   min(count * value, full)) * PetEggOverdosageModifier / full, at most
///   PetEggMaxOverdosage. Its chance of changing the main body type is
///   min(overdose, PetEggMaxTopDescriptorChangeChance) (0x1410d80fd).
///
///   Item hash: the first 8 bytes of the item id (16-byte inline string, so
///   "PLANT_POOP" hashes as "PLANT_PO"), times 0x9ddfea08eb382d69, x ^= x &gt;&gt; 47,
///   times the same constant, seeds the game's generator (StartState).
///
///   Neural Calibrator (0x1410d7d70): an item in the override table (product
///   id, or substance id when the product id is empty) uses its Trait and
///   IncreasesTrait. Any other item: first word w, trait = (w * 3) &gt;&gt; 32;
///   second word as a fraction (w / 2^32, rounded to float) &gt;= 0.5 raises it.
///   The target trait is clamp(current +- min(TraitRangeModifier * 2 * dose,
///   TraitRangeMax), -1, 1). The screen names a trait by its sign (0x1407875f4:
///   Helpfulness/Playfulness, Aggression/Gentleness, Independence/Devotion).
///
///   Growth Hormone (0x1410d7b50): first word's fraction &gt;= 0.5 grows. Target
///   scale = clamp(current +- min((max - min) * ScaleRangeModifier * dose,
///   ScaleRangeMax), min, max) with the species' scale range.
///
///   Gene Splitter (0x1410d7fc0) and Dye Injector (0x1410d6a10): the chance
///   is min(dose * MaxChange * ChanceModifier, 1), rolled from the game's
///   running random state (not predictable). A Dye Injector change takes its
///   colours from the item's hash, so the same item gives the same colours.
/// </summary>
public static class EggSequencer
{
    private const ulong HashMultiplier = 0x9DDFEA08EB382D69;

    /// <summary>What an item always does. Value is the item's BaseValue
    /// (catalog ItemValues); substances count it times the substance modifier.</summary>
    public static EggCatalystEffect Effect(string itemId, bool isSubstance, long baseValue, long fullDoseValue,
        EggSequencerRules rules)
    {
        string id = itemId.TrimStart('^');
        var rng = new UniverseRandom(StartState(ItemHash(id)));
        uint first = rng.NextWord(), second = rng.NextWord();
        bool grows = Fraction(first) >= 0.5f;

        var listed = rules.Overrides.FirstOrDefault(o => string.Equals(o.ItemId, id, StringComparison.OrdinalIgnoreCase));
        EggTrait trait = listed?.Trait ?? (EggTrait)(int)(((ulong)first * 3) >> 32);
        bool increases = listed?.Increases ?? Fraction(second) >= 0.5f;

        double unit = Unit(isSubstance, baseValue, rules);
        int Count(double share) => unit <= 0 || fullDoseValue <= 0 ? 0 : (int)Math.Ceiling(share * fullDoseValue / unit);
        // Gene Splitter and Dye Injector are certain once dose * max * modifier reaches 1.
        double certain = Math.Min(1, 1 / Math.Max(rules.MaxColourChangeChance * rules.ColourChanceModifier, 1e-6));
        return new EggCatalystEffect(trait, increases, listed is not null, grows, Count(1), Count(certain));
    }

    /// <summary>What <paramref name="count"/> of an item does in one slot.</summary>
    public static EggSlotOutcome Outcome(EggCatalystSlot slot, bool isSubstance, long baseValue, int count,
        long fullDoseValue, EggSequencerRules rules)
    {
        count = Math.Clamp(count, 0, rules.ItemLimit);
        double unit = Unit(isSubstance, baseValue, rules);
        if (fullDoseValue <= 0 || unit <= 0 || count == 0) return new(count, 0, 0, 0, 0, 0, 0);
        double used = Math.Min(count * unit, fullDoseValue);
        double dose = used / fullDoseValue;
        double overdose = slot == EggCatalystSlot.GeneSplitter
            ? Math.Clamp((count * (double)baseValue - used) * rules.OverdosageModifier / fullDoseValue, 0, rules.MaxOverdosage)
            : 0;
        double chance = slot switch
        {
            EggCatalystSlot.GeneSplitter => Math.Min(1, dose * rules.MaxAccessoriesChangeChance * rules.AccessoryChanceModifier),
            EggCatalystSlot.DyeInjector => Math.Min(1, dose * rules.MaxColourChangeChance * rules.ColourChanceModifier),
            _ => 0,
        };
        return new(count, dose, overdose, chance, Math.Min(overdose, rules.MaxTopDescriptorChangeChance),
            slot == EggCatalystSlot.NeuralCalibrator ? Math.Min(rules.TraitRangeModifier * 2 * dose, rules.TraitRangeMax) * 100 : 0,
            slot == EggCatalystSlot.GrowthHormone ? rules.ScaleRangeModifier * dose : 0);
    }

    /// <summary>The amount a slot suggests for an item: a full dose for size and
    /// personality, a certain change for anatomy and colouring, within the limit.</summary>
    public static int SuggestedCount(EggCatalystSlot slot, EggCatalystEffect effect, EggSequencerRules rules) =>
        Math.Min(rules.ItemLimit, slot is EggCatalystSlot.GeneSplitter or EggCatalystSlot.DyeInjector
            ? effect.CertainChangeCount : effect.FullDoseCount);

    private static double Unit(bool isSubstance, long baseValue, EggSequencerRules rules) =>
        baseValue * (isSubstance ? rules.SubstanceModifier : 1f);

    private static float Fraction(uint word) => (float)(word * (1.0 / 4294967296.0));

    private static ulong ItemHash(string id)
    {
        Span<byte> bytes = stackalloc byte[8];
        for (int i = 0; i < 8 && i < id.Length; i++) bytes[i] = (byte)id[i];
        ulong x = BitConverter.ToUInt64(bytes) * HashMultiplier;
        x ^= x >> 47;
        return x * HashMultiplier;
    }

    /// <summary>The game's multiply-with-carry seeding (as MultiToolNames).</summary>
    private static ulong StartState(ulong seed)
    {
        ulong lo = seed & 0xFFFFFFFF, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo;
        ulong s1 = (((lo << 16) | (lo >> 16)) & 0xFFFFFFFF) ^ hi ^ lo;
        return (s1 << 32) | s0;
    }
}
