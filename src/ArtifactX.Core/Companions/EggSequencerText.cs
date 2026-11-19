namespace ArtifactX.Core.Companions;

/// <summary>An item the Egg Sequencer takes: its id, display name, whether it is a substance (counted at
/// <see cref="EggSequencerRules.SubstanceModifier"/> times its value), its base value and what it always does.</summary>
public sealed record EggCatalyst(string Id, string Name, bool IsSubstance, long Value, EggCatalystEffect Effect);

/// <summary>How one item reads in a slot's item list.</summary>
/// <param name="TraitWord">The personality word the item moves toward (Neural Calibrator).</param>
/// <param name="OppositeTraitWord">The word it moves away from.</param>
/// <param name="EffectText">What it does in this slot, in a few words ("▲ Bigger", "Toward Aggression").</param>
/// <param name="AmountText">How many make the full effect, or why it never does.</param>
/// <param name="Needed">How many give the slot's full effect (a full dose, or a certain change), for sorting.</param>
/// <param name="HasEffect">False for an item with no value, which does nothing.</param>
/// <param name="ReachesFull">A slot can hold enough of it for the full effect.</param>
public sealed record EggCatalystLabel(string TraitWord, string OppositeTraitWord, string EffectText, string AmountText,
    int Needed, bool HasEffect, bool ReachesFull);

/// <summary>What a filled (or empty) slot shows: the game's dose line and status, and in plain words what happens.</summary>
/// <param name="Result">What the slot does, or null when it does nothing (empty, too little, or the species can't
/// change there).</param>
/// <param name="SizeAfter">Growth Hormone with a known egg: its size afterwards.</param>
public sealed record EggSlotReport(string DoseText, bool IsOverdosed, string StatusLabel, string StatusText,
    string Explanation, EggSlotOutcome? Result, float? SizeAfter);

/// <summary>
/// The Egg Sequencer screen's words, shared by the desktop and phone pages: slot names and subtitles from the game's
/// own text (<see cref="EggSequencerRules.Texts"/>), each item's label in a slot's list, and a slot's report - the
/// dose the game shows ("Dose: 100%", "Overdosed! (102%)") and a plain-words explanation, because most players don't
/// follow the raw mechanics (owner, 2026-11-04). The numbers come from <see cref="EggSequencer"/>.
/// </summary>
/// <summary>A choice in a slot's "what it does" filter.</summary>
public sealed record EggCatalystFilter(string Label, Func<EggCatalyst, EggCatalystLabel, bool> Matches)
{
    public override string ToString() => Label;
}

public static class EggSequencerText
{
    /// <summary>A slot's filter choices, "Any effect" first: bigger / smaller for the Growth Hormone, one per
    /// personality word the items move toward for the Neural Calibrator, a cheap certain change otherwise.</summary>
    public static IReadOnlyList<EggCatalystFilter> Filters(EggCatalystSlot slot, IEnumerable<EggCatalystLabel> labels)
    {
        var any = new EggCatalystFilter("Any effect", (_, _) => true);
        return slot switch
        {
            EggCatalystSlot.GrowthHormone =>
                [any, new("Makes it bigger", (i, _) => i.Effect.Grows), new("Makes it smaller", (i, _) => !i.Effect.Grows)],
            EggCatalystSlot.NeuralCalibrator =>
                [any, .. labels.Select(l => l.TraitWord).Distinct().Order()
                    .Select(word => new EggCatalystFilter($"Toward {word}", (_, l) => l.TraitWord == word))],
            _ => [any, new("Certain with 10 or fewer", (_, l) => l.Needed <= 10)],
        };
    }

    /// <summary>The words the game shows for a trait above / below 0 (0x1407875f4), in <see cref="EggTrait"/> order.</summary>
    public static readonly IReadOnlyList<string> PositiveKeys = ["UI_PET_HELPFUL_RATING", "UI_PET_AGGRESSION_RATING", "UI_PET_INDEPENDENCE_RATING"];
    public static readonly IReadOnlyList<string> NegativeKeys = ["UI_PET_PLAYFUL_RATING", "UI_PET_GENTLENESS_RATING", "UI_PET_ATTACHMENT_RATING"];

    /// <summary>How doses are counted, in plain words, under the slots.</summary>
    public static string DoseNote(EggSequencerRules rules, long fullDose, string? fullDoseName) =>
        $"A full dose is {fullDose:N0} units of value, the worth of one {fullDoseName ?? rules.FullDoseProductId}. "
        + $"Raw materials count {rules.SubstanceModifier:0.#} times their value. A slot holds up to {rules.ItemLimit:N0}.";

    /// <summary>The game's English text for a key, or the key when the rules lack it.</summary>
    public static string Text(EggSequencerRules rules, string key) =>
        rules.Texts.TryGetValue(key, out var text) && text.Length > 0 ? text : key;

    /// <summary>The slot's letter in the game's text keys (S, A, C, P).</summary>
    private static string Code(EggCatalystSlot slot) => slot switch
    {
        EggCatalystSlot.GrowthHormone => "S",
        EggCatalystSlot.GeneSplitter => "A",
        EggCatalystSlot.DyeInjector => "C",
        _ => "P",
    };

    public static string SlotName(EggCatalystSlot slot, EggSequencerRules rules) => Text(rules, $"UI_EGG_MODIFIER_INPUT_{Code(slot)}");

    public static string SlotSubtitle(EggCatalystSlot slot, EggSequencerRules rules) => Text(rules, $"UI_EGG_MODIFIER_INPUT_{Code(slot)}_SUB");

    /// <summary>The Embryo Status label the slot changes ("Weight / Height", "Anatomy"...).</summary>
    public static string SlotStatusLabel(EggCatalystSlot slot, EggSequencerRules rules) =>
        slot == EggCatalystSlot.GrowthHormone ? Text(rules, "WEIGHT_HEIGHT") : Text(rules, $"UI_EGG_MODIFIER_OUT_{Code(slot)}");

    /// <summary>The word for a trait on the side its value is (above 0 the positive word).</summary>
    public static string TraitWord(EggTrait trait, float value, EggSequencerRules rules) =>
        Text(rules, (value > 0 ? PositiveKeys : NegativeKeys)[(int)trait]);

    public static EggCatalystLabel Label(EggCatalyst item, EggCatalystSlot slot, EggSequencerRules rules)
    {
        var effect = item.Effect;
        string toward = Text(rules, (effect.TraitIncreases ? PositiveKeys : NegativeKeys)[(int)effect.Trait]);
        string away = Text(rules, (effect.TraitIncreases ? NegativeKeys : PositiveKeys)[(int)effect.Trait]);
        bool certainSlot = slot is EggCatalystSlot.GeneSplitter or EggCatalystSlot.DyeInjector;
        int needed = certainSlot ? effect.CertainChangeCount : effect.FullDoseCount;
        bool hasEffect = effect.FullDoseCount > 0;
        string effectText = slot switch
        {
            EggCatalystSlot.GrowthHormone => effect.Grows ? "▲ Bigger" : "▼ Smaller",
            EggCatalystSlot.GeneSplitter => "Remixes anatomy",
            EggCatalystSlot.DyeInjector => "Its own colours",
            _ => $"Toward {toward}",
        };
        string amount = !hasEffect ? "No value, so no effect"
            : needed > rules.ItemLimit ? $"Never a full effect: a slot holds {rules.ItemLimit:N0}"
            : $"{(certainSlot ? "Certain with" : "Full dose:")} {needed:N0}";
        return new(toward, away, effectText, amount, needed, hasEffect, hasEffect && needed <= rules.ItemLimit);
    }

    /// <summary>The amount a slot starts at when an item goes in: what the full effect needs (at least 1), and no more
    /// than <paramref name="owned"/> when the player carries some (the game only takes what you carry).</summary>
    public static int StartCount(EggCatalystSlot slot, EggCatalyst item, EggSequencerRules rules, long owned = 0)
    {
        int suggested = Math.Max(1, EggSequencer.SuggestedCount(slot, item.Effect, rules));
        return owned > 0 ? (int)Math.Min(suggested, owned) : suggested;
    }

    /// <summary>
    /// What a slot shows with <paramref name="count"/> of <paramref name="item"/> in it (null for empty). With a known
    /// <paramref name="egg"/> the numbers are exact (its size and personality before and after); without one, the
    /// change is described for any egg, and <paramref name="anyEggHint"/> (a sentence such as "Choose your egg above
    /// for exact numbers.") follows the personality explanation.
    /// </summary>
    public static EggSlotReport Describe(EggCatalystSlot slot, EggCatalyst? item, int count, EggCreature? egg, long fullDose,
        EggSequencerRules rules, string anyEggHint = "")
    {
        string baseLabel = SlotStatusLabel(slot, rules);
        string name = SlotName(slot, rules);
        string T(string key) => Text(rules, key);
        if (item is null)
            return new("Dose: -", false, baseLabel, T("UI_EGG_MODIFIER_OUT_INHERITED"),
                $"Empty, so the new companion keeps its parent's {baseLabel.ToLowerInvariant()}.", null, null);

        var o = EggSequencer.Outcome(slot, item.IsSubstance, item.Value, count, fullDose, rules);
        // The game rounds the dose down ("Dose: 0%" for one Ferrite Dust, "102%" for 102.5).
        bool overdosed = o.Overdose > 0;
        string dose = overdosed ? $"Overdosed! ({Math.Floor((1 + o.Overdose) * 100):0}%)" : $"Dose: {Math.Floor(o.Dose * 100):0}%";
        var e = item.Effect;
        var species = egg is null ? null : rules.Species.GetValueOrDefault(egg.Species);
        bool locked = species is not null && slot switch
        {
            EggCatalystSlot.GrowthHormone => !species.CanChangeGrowth,
            EggCatalystSlot.GeneSplitter => !species.CanChangeAnatomy,
            EggCatalystSlot.DyeInjector => !species.CanChangeColour,
            _ => !species.CanChangeTraits,
        };
        var result = locked || o.Dose <= 0 ? null : o;
        float? sizeAfter = slot == EggCatalystSlot.GrowthHormone && result is not null && egg is not null && species is not null
            ? EggSequencer.ScaleAfter(species, egg.Scale, e.Grows, o.Dose, rules) : null;

        string More(int needed, string what) =>
            needed > rules.ItemLimit ? $" Even a full slot ({rules.ItemLimit:N0}) is not {what}; a more valuable item works better."
            : needed > o.Count ? $" {needed:N0} would make {what}." : "";

        if (locked)
            return new(dose, overdosed, baseLabel, T("UI_EGG_MODIFIER_OUT_INHERITED"),
                $"{egg!.Name} can't have its {baseLabel.ToLowerInvariant()} changed, so the {name} does nothing for this egg.", null, null);
        if (o.Dose <= 0)
            return new(dose, overdosed, baseLabel, T("UI_EGG_MODIFIER_OUT_INHERITED"),
                $"{o.Count:N0} {item.Name} is too little to make a difference. Add more.", null, null);

        string label = baseLabel, status, explanation;
        switch (slot)
        {
            case EggCatalystSlot.GrowthHormone:
                status = T(e.Grows ? "UI_EGG_MODIFIER_OUT_INCREASING" : "UI_EGG_MODIFIER_OUT_DECREASING");
                // Exact for a saved egg: its unY moves, then is held to the species' range (EggSequencer.ScaleAfter).
                explanation = egg is not null && species is not null && sizeAfter is float size
                    ? $"{egg.Name}: size {egg.Scale:0.##} becomes {size:0.##} (its species can be {species.MinScale:0.##} to {species.MaxScale:0.##})."
                    : $"The new companion will be {(e.Grows ? "bigger" : "smaller")}: up to {o.SizeShare:P0} of the size range its species can have.";
                explanation += More(e.FullDoseCount, "a full dose");
                break;
            case EggCatalystSlot.GeneSplitter:
                status = T("UI_EGG_MODIFIER_OUT_UNSTABLE");
                explanation = (o.ChangeChance >= 1 ? "Its anatomy will be remixed: new body parts and accessories."
                        : $"{o.ChangeChance:P0} chance its anatomy is remixed (new body parts and accessories).")
                    + (o.BodyTypeChance > 0 ? $" Overdosed, so there is also a {o.BodyTypeChance:P0} chance its whole body type changes." : "")
                    + More(e.CertainChangeCount, "a certain change");
                break;
            case EggCatalystSlot.DyeInjector:
                status = T("UI_EGG_MODIFIER_OUT_UNSTABLE");
                explanation = (o.ChangeChance >= 1 ? "Its colours will change." : $"{o.ChangeChance:P0} chance its colours change.")
                    + $" {item.Name} always gives the same colours; use a different item for different colours."
                    + More(e.CertainChangeCount, "a certain change");
                break;
            default:
                var words = Label(item, slot, rules);
                if (egg is not null)
                {
                    // The game names the trait by the sign of the result, or of the current value at 0.
                    float before = egg.Traits[(int)e.Trait], after = EggSequencer.TraitAfter(before, e, o);
                    float shown = Math.Abs(after) < 1.5e-5f ? before : after;
                    label = TraitWord(e.Trait, shown, rules);
                    bool flipped = Math.Abs(after) >= 1.5e-5f && Math.Sign(after) != Math.Sign(before);
                    status = T(flipped || Math.Abs(after) > Math.Abs(before) ? "UI_EGG_MODIFIER_OUT_INCREASING" : "UI_EGG_MODIFIER_OUT_DECREASING");
                    explanation = $"{egg.Name}: {TraitWord(e.Trait, before, rules)} {Math.Abs(before) * 100:0}% becomes {TraitWord(e.Trait, shown, rules)} {Math.Abs(after) * 100:0}% "
                        + $"(moves {o.TraitChange:0} points toward {words.TraitWord}).";
                }
                else
                {
                    label = words.TraitWord;
                    status = T("UI_EGG_MODIFIER_OUT_INCREASING");
                    explanation = $"Its personality moves {o.TraitChange:0} points (out of 100) toward {words.TraitWord}, away from {words.OppositeTraitWord}. "
                        + $"If the egg leans toward {words.OppositeTraitWord}, the game shows this as {words.OppositeTraitWord} decreasing."
                        + (anyEggHint.Length > 0 ? " " + anyEggHint : "");
                }
                explanation += More(e.FullDoseCount, "a full dose");
                break;
        }
        return new(dose, overdosed, label, status, explanation, result, sizeAfter);
    }
}
