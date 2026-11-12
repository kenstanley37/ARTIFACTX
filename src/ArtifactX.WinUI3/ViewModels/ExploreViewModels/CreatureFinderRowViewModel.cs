using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One creature in the Creature Finder: name, where it lives, how far away, and its battle panel.</summary>
public sealed class CreatureFinderRowViewModel
{
    public CreatureFinderRowViewModel(FoundCreature found, ulong? here, double? distance)
    {
        Found = found;
        var planet = found.Planet;
        Name = found.Name;
        PlanetLabel = $"{UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex)} · {UniverseInfo.SystemName(planet.SystemAddress)}";
        Traits = string.Join(" · ", new[] { found.Rarity, found.Habitat, found.Activity }.Where(s => s.Length > 0));
        DistanceLabel = string.Join(" · ", new[] { PlanetText.Distance(planet, here, distance), PlanetText.GalaxyLabel(planet.Galaxy) }
            .Where(s => s.Length > 0));
        StatusLabel = found.Discovered ? "Discovered by you" : "Not discovered yet";
        // The size class (GcCreatureSizeClasses), then both forms with gender, weight and height.
        string sizeClass = found.Creature.Scale > 0 ? SizeWord(found.Creature.SizeClass) : "";
        SizeLabel = string.Join(" · ", new[] { sizeClass, found.Forms }.Where(s => s.Length > 0));
        Battle = new CreatureBattleViewModel(found.Battle);
        AutomationName = $"{Name}, {StatusLabel}, on {PlanetLabel}. {Battle.AutomationName}";
    }

    /// <summary>The search result itself (its planet opens Planet Details).</summary>
    public FoundCreature Found { get; }
    public KnownPlanet Planet => Found.Planet;
    public string Name { get; }
    public string PlanetLabel { get; }
    public string Traits { get; }
    public string DistanceLabel { get; }
    public string StatusLabel { get; }
    public string SizeLabel { get; }
    public bool HasSize => SizeLabel.Length > 0;

    /// <summary>A size class's plain word (GcCreatureSizeClasses: Small, Medium, Large, Huge).</summary>
    public static string SizeWord(int sizeClass) => sizeClass switch { 1 => "Medium", 2 => "Large", 3 => "Huge", _ => "Small" };
    public bool IsDiscovered => Found.Discovered;
    public CreatureBattleViewModel Battle { get; }
    public string PortalCode => Planet.PortalCode;
    public string AutomationName { get; }

    /// <summary>What a screen reader announces for the result (the list item's name).</summary>
    public override string ToString() => AutomationName;
}
