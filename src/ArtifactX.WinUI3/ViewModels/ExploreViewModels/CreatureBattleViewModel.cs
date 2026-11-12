using ArtifactX.WinUI3.Helpers.Fauna;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One stat row of a creature card's battle panel: the game's label and its class badge.</summary>
public sealed record BattleStatRow(string Label, string Letter, Brush Brush);

/// <summary>
/// A creature card's pet-battle panel, laid out like the analysis visor's: the Trait Potential badge, Combat
/// Effectiveness / Agility / Health with class badges in the game's class colours, and the element. These are the
/// stats players pick arena pets by (owner, 2026-11-12), so the card shows them as a panel, not a line of text.
/// </summary>
public sealed class CreatureBattleViewModel
{
    private const string Letters = "CBAS";

    public CreatureBattleViewModel(CreatureBattle battle)
    {
        Heading = battle.Heading.ToUpperInvariant();
        Potential = Letter(battle.Potential);
        PotentialBrush = ThemeResources.ClassBrush(Potential);
        Stats = battle.Stats.Select(s => new BattleStatRow(s.Label, Letter(s.Class), ThemeResources.ClassBrush(Letter(s.Class)))).ToList();
        Element = battle.Element;
        AutomationName = $"{battle.Heading} {Potential}. " + string.Join(", ", Stats.Select(s => $"{s.Label} {s.Letter}"))
            + (Element.Length > 0 ? $". {Element}." : ".");
    }

    public string Heading { get; }
    public string Potential { get; }
    public Brush PotentialBrush { get; }
    public IReadOnlyList<BattleStatRow> Stats { get; }
    public string Element { get; }
    public bool HasElement => Element.Length > 0;
    public string AutomationName { get; }

    private static string Letter(int cls) => cls >= 0 && cls < Letters.Length ? Letters[cls].ToString() : "?";
}
