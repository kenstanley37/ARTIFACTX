using ArtifactX.Core.Companions;
using ArtifactX.Core.Universe;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Helpers.Fauna;

/// <summary>
/// A predicted creature's traits in the game's own words (catalog format 80: rarity, activity and gender words), for
/// the creature cards on Planet Details. Empty strings when the game data predates them, so the card simply leaves
/// the line out.
/// </summary>
public static class CreatureTraitText
{
    /// <summary>Rarity and activity, as the game's discovery list shows them ("Rare", "Always Active").</summary>
    public static (string Rarity, string Activity) RarityAndActivity(PredictedCreature creature, CreatureTables? tables)
    {
        string rarity = creature.Info is { } info && tables?.RarityWords is { Length: 3 } rarities && info.Rarity is >= 0 and < 3
            ? rarities[info.Rarity] : "";
        string activity = tables?.ActivityWords is { Length: 5 } activities && creature.ActiveTime is >= 0 and < 5
            ? activities[creature.ActiveTime] : "";
        return (rarity, activity);
    }

    /// <summary>Age, behaviour, diet, notes and produce as one line ("Perpetual (young: Juvenile) · Hasty · eats Solar
    /// Vine · Photosensitive · produces Wild Milk"), the analysis visor's Age, Behavior, Diet, Notes and Produces; empty
    /// on game data before format 80.</summary>
    public static string About(PredictedCreature creature, CreatureTables? tables)
    {
        if (creature.Info is not { } info) return "";
        var parts = new List<string>();
        // Age: adults' word, then the young ones' (the visor shows whichever individual you scan).
        if (tables?.Text?.Ages is { } ages && info.AdultAge >= 1 && info.AdultAge <= ages.Length)
            parts.Add(info.BabyAge >= 1 && info.BabyAge <= ages.Length ? $"{ages[info.AdultAge - 1]} (young: {ages[info.BabyAge - 1]})" : ages[info.AdultAge - 1]);
        if (info.Behaviour.Length > 0) parts.Add(info.Behaviour);
        if (info.Diet.Length > 0) parts.Add($"eats {info.Diet}");
        if (info.Notes.Length > 0) parts.Add(info.Notes);
        if (tables?.ProduceOf(creature) is string item && tables.ProduceNames?.GetValueOrDefault(item) is { Length: > 0 } product)
            parts.Add($"produces {product}");
        return string.Join(" · ", parts);
    }

    /// <summary>The analysis visor's "Trait Potential" panel as one line ("Trait Potential S: Combat Effectiveness S ·
    /// Agility B · Health S · Fire"), from the species' name seeds (the saved ones for a discovered creature); empty on
    /// game data before format 82.</summary>
    public static string Battle(PredictedCreature creature, CreatureTables? tables, ulong speciesSeed, ulong genusSeed)
    {
        if (tables?.PetBattle is not { Labels.Length: 4 } battle || battle.ClassRolls.Length < 3) return "";
        var (classes, potential) = PetBattlerClasses.Roll(speciesSeed, genusSeed, battle.ClassRolls);
        const string Letters = "CBAS";
        // The visor lists Combat Effectiveness, Agility, Health: stats 2, 1, 0 in GcPetBattlerCoreStat order.
        var parts = new[] { 2, 1, 0 }.Select(i => $"{battle.Labels[i]} {Letters[classes[i]]}").ToList();
        if (creature.Info is { Element: >= 0 } info && info.Element < battle.AffinityWords.Length && battle.AffinityWords[info.Element].Length > 0)
            parts.Add(battle.AffinityWords[info.Element]);
        return $"{battle.Labels[3]} {Letters[potential]}: {string.Join(" · ", parts)}";
    }

    /// <summary>The species' two forms, each gender with its weight and height ("Male 121.5kg / 3.0m or Female
    /// 126.2kg / 3.1m"); the game's Discoveries page shows the one you scan. Robots use their own gender words
    /// ("Electronic"); a form without a known gender shows its size alone.</summary>
    public static string Forms(PredictedCreature creature, CreatureTables? tables)
    {
        if (creature.Info is not { } info) return "";
        string[]? words = info.RobotGenders ? tables?.RobotGenderWords : tables?.GenderWords;
        string Gender(int n) => words is not null && n >= 1 && n <= words.Length ? words[n - 1] + " " : "";
        var forms = new List<string> { Gender(info.Gender1) + info.Size, Gender(info.Gender2) + info.Size2 };
        return string.Join(" or ", forms.Distinct());
    }
}
