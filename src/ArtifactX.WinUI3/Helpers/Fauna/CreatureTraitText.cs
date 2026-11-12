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

    /// <summary>The species' two forms, each gender with its weight and height ("Male 121.5kg / 3.0m or Female
    /// 126.2kg / 3.1m"); the game's Discoveries page shows the one you scan. A form without a known gender (robots)
    /// shows its size alone.</summary>
    public static string Forms(PredictedCreature creature, CreatureTables? tables)
    {
        if (creature.Info is not { } info) return "";
        string Gender(int n) => tables?.GenderWords is { } words && n >= 1 && n <= words.Length ? words[n - 1] + " " : "";
        var forms = new List<string> { Gender(info.Gender1) + info.Size, Gender(info.Gender2) + info.Size2 };
        return string.Join(" or ", forms.Distinct());
    }
}
