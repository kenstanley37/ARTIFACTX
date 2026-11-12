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

    /// <summary>Behaviour, diet and notes as one line ("Hasty · eats Solar Vine · Photosensitive"), the analysis visor's
    /// Behavior, Diet and Notes; empty on game data before format 80.</summary>
    public static string About(PredictedCreature creature)
    {
        if (creature.Info is not { } info) return "";
        var parts = new List<string>();
        if (info.Behaviour.Length > 0) parts.Add(info.Behaviour);
        if (info.Diet.Length > 0) parts.Add($"eats {info.Diet}");
        if (info.Notes.Length > 0) parts.Add(info.Notes);
        return string.Join(" · ", parts);
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
