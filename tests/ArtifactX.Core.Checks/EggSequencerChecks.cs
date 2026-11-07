using System.Text.Json;
using ArtifactX.Core.Companions;

internal static class EggSequencerChecks
{
    /// <summary>The owner's in-game sequencer screens, 2026-11-04 (two eggs). `path` is
    /// the catalog's EGGSEQUENCER DescriptionEnglish (format 67). Values are the items'
    /// BaseValue in build 184271; the full dose is Sac Venom's 64,000. A trait is shown
    /// by its sign, so "Gentleness Decreasing" is Aggression up.</summary>
    public static void Native(string path)
    {
        var rules = JsonSerializer.Deserialize<EggSequencerRules>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("No rules in " + path);
        const long full = 64000;
        // Slot, item, substance, value, count, game dose (with overdose), trait (Neural) or grows (Growth).
        (EggCatalystSlot Slot, string Id, bool Substance, long Value, int Count, int Percent, EggTrait? Trait, bool Up)[] cases =
        [
            (EggCatalystSlot.GrowthHormone, "FOOD_ICE", false, 48000, 2, 100, null, true),          // Weight / Height Increasing
            (EggCatalystSlot.GrowthHormone, "COMPOUND2", false, 320000, 1, 100, null, true),        // Semiconductor
            (EggCatalystSlot.GeneSplitter, "FOOD_P_COLDWILD", false, 800, 100, 102, null, false),   // Overdosed! (102%)
            (EggCatalystSlot.GeneSplitter, "REACTION3", false, 50000, 5, 129, null, false),         // Overdosed! (129%)
            (EggCatalystSlot.DyeInjector, "LAND1", true, 14, 1, 0, null, false),                    // Dose: 0%
            (EggCatalystSlot.DyeInjector, "FOOD_R_CUSTARD", false, 32000, 2, 100, null, false),
            (EggCatalystSlot.NeuralCalibrator, "FOOD_R_CUSTARD", false, 32000, 2, 100, EggTrait.Aggression, false), // Gentleness Increasing
            (EggCatalystSlot.NeuralCalibrator, "FISHBAIT_NIGHT", false, 550, 117, 100, EggTrait.Aggression, true),  // Gentleness Decreasing
            (EggCatalystSlot.NeuralCalibrator, "COLD1", true, 62, 413, 100, EggTrait.Helpfulness, true),            // Helpfulness Increasing
            (EggCatalystSlot.NeuralCalibrator, "COMPOUND2", false, 320000, 1, 100, EggTrait.Independence, true),    // Independence Increasing
            (EggCatalystSlot.NeuralCalibrator, "SACVENOM", false, 64000, 1, 100, EggTrait.Aggression, true),        // Gentleness Decreasing
            (EggCatalystSlot.NeuralCalibrator, "ALLOY7", false, 150000, 1, 100, EggTrait.Aggression, false),        // Gentleness Increasing
            (EggCatalystSlot.NeuralCalibrator, "ASTEROID1", true, 186, 138, 100, EggTrait.Independence, false),     // Independence Decreasing
        ];
        foreach (var c in cases)
        {
            var effect = EggSequencer.Effect(c.Id, c.Substance, c.Value, full, rules);
            var outcome = EggSequencer.Outcome(c.Slot, c.Substance, c.Value, c.Count, full, rules);
            int percent = (int)Math.Floor((outcome.Dose + outcome.Overdose) * 100);
            if (percent != c.Percent) throw new InvalidOperationException($"{c.Id} x{c.Count} in {c.Slot}: {percent}%, expected {c.Percent}%.");
            if (c.Trait is EggTrait trait && (effect.Trait != trait || effect.TraitIncreases != c.Up))
                throw new InvalidOperationException($"{c.Id}: {effect.Trait} {(effect.TraitIncreases ? "up" : "down")}, expected {trait} {(c.Up ? "up" : "down")}.");
            if (c.Slot == EggCatalystSlot.GrowthHormone && effect.Grows != c.Up)
                throw new InvalidOperationException($"{c.Id}: grows {effect.Grows}, expected {c.Up}.");
        }
        Console.WriteLine($"PASS: {cases.Length} Egg Sequencer results.");
    }
}
