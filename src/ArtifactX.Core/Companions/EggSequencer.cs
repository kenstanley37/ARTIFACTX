using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Companions;

/// <summary>A companion personality trait, in the game's GcCreaturePetTraits order
/// (the order of a companion's three trait values in the save, NmsCompanionPaths).</summary>
public enum EggTrait { Helpfulness, Aggression, Independence }

/// <summary>A PetEggTraitModifierOverrideTable row: an item the game gives a fixed
/// personality effect instead of the hashed one.</summary>
public sealed record EggTraitOverride(string ItemId, EggTrait Trait, bool Increases);

/// <summary>
/// The Egg Sequencer's tuning, read from the player's game files (catalog row
/// EGGSEQUENCER, CatalogSteps.AddEggSequencer): GcCreatureGlobals' PetEgg* values,
/// the trait override table and the product whose value is a full dose
/// (PetEggMaxChangeProduct, SACVENOM in build 184271). ItemLimit
/// (PetEggModificationItemLimit, 999) caps how many of an item a slot takes
/// (the sequencer screen clamps the count to it at 0x140784c59).
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
    int ItemLimit);

/// <summary>What one catalyst item does in the Egg Sequencer.</summary>
/// <param name="Trait">The trait the Neural Calibrator changes.</param>
/// <param name="TraitIncreases">Whether it raises (true) or lowers that trait.</param>
/// <param name="TraitFromTable">The game lists this item in its override table
/// rather than hashing its id.</param>
/// <param name="Grows">Whether the Growth Hormone makes the creature bigger.</param>
/// <param name="FullDoseCount">How many of the item make a full dose (the
/// largest change); 0 when the item has no value and does nothing.</param>
/// <param name="CertainChangeCount">How many make the Gene Splitter's and Dye
/// Injector's change certain; 0 when the item does nothing.</param>
/// <param name="MaxDose">The largest dose one slot of the item reaches (0..1);
/// below 1 when a full dose takes more than the slot holds.</param>
/// <param name="MaxChangeChance">The best Gene Splitter or Dye Injector chance
/// one slot of the item gives (0..1).</param>
public sealed record EggCatalystEffect(EggTrait Trait, bool TraitIncreases, bool TraitFromTable, bool Grows,
    int FullDoseCount, int CertainChangeCount, double MaxDose, double MaxChangeChance);

/// <summary>
/// The Egg Sequencer's rules, read off NMS.exe build 184271 on 2026-11-04
/// (static disassembly, nothing run; not yet compared with the sequencer in game).
/// The sequencer object holds one item per catalyst slot (0x1410d6400 returns
/// it: slot 1 Growth Hormone, 2 Gene Splitter, 3 Dye Injector, 4 Neural
/// Calibrator).
///
///   Dose (0x1410d6fa0 for the Neural Calibrator; the other slots' doses are
///   computed the same way at 0x1410d6e99, 0x1410d7579, 0x1410d7959): the
///   item's value per unit is its BaseValue override from the trait table when
///   above 0 (every row is -1 in 184271), otherwise its BaseValue, times
///   PetEggSubstanceModifier for a substance (0x1410d7a60). Dose =
///   min(count * value, full) / full, where full is the BaseValue of
///   PetEggMaxChangeProduct (read at 0x1410d5874).
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
///   TraitRangeMax), -1, 1).
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

    /// <summary>The effect of one catalyst item. Value is the item's BaseValue
    /// (catalog ItemValues); substances are multiplied by the substance modifier.</summary>
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

        double unit = baseValue * (isSubstance ? rules.SubstanceModifier : 1f);
        int Count(double share) => unit <= 0 || fullDoseValue <= 0 ? 0 : (int)Math.Ceiling(share * fullDoseValue / unit);
        // Gene Splitter and Dye Injector are certain once dose * max * modifier reaches 1.
        double perDose = rules.MaxColourChangeChance * rules.ColourChanceModifier;
        double certain = Math.Min(1, 1 / Math.Max(perDose, 1e-6));
        double maxDose = unit <= 0 || fullDoseValue <= 0 ? 0 : Math.Min(1, rules.ItemLimit * unit / fullDoseValue);
        return new EggCatalystEffect(trait, increases, listed is not null, grows, Count(1), Count(certain), maxDose,
            Math.Min(1, maxDose * perDose));
    }

    /// <summary>The largest trait change a full dose makes, on the save's -1..1
    /// scale (the game shows a trait as its size times 100).</summary>
    public static float FullDoseTraitChange(EggSequencerRules rules) =>
        Math.Min(rules.TraitRangeModifier * 2, rules.TraitRangeMax);

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
