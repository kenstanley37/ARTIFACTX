using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One creature in the Creature Finder: name, where it lives, how far away, and its battle panel.</summary>
public sealed class CreatureFinderRowViewModel
{
    public CreatureFinderRowViewModel(FoundCreature found, ulong? here, double? distance, ulong? herePlanet = null)
    {
        Found = found;
        PinKey = CreaturePinService.KeyOf(found.Planet.Address, found.Creature.Seed);
        IsPinned = CreaturePinService.IsPinned(PinKey);
        IsHere = herePlanet is ulong p && p == found.Planet.Address;
        var planet = found.Planet;
        Name = found.Name;
        PlanetLabel = $"{UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex)} · {UniverseInfo.SystemName(planet.SystemAddress)}";
        Traits = string.Join(" · ", new[] { found.Rarity, found.Habitat, found.Activity }.Where(s => s.Length > 0));
        DistanceLabel = PlanetText.Distance(planet, here, distance);
        // The galaxy as its own badge (owner, 2026-11-12: "I have to scan the text to find it"); another galaxy than
        // the one the save is in is marked, since getting there means a galaxy jump.
        IsOtherGalaxy = here is ulong h && NmsSpaceStationPaths.GalaxyNumber(h) != planet.Galaxy;
        GalaxyLabel = IsOtherGalaxy ? $"{PlanetText.GalaxyLabel(planet.Galaxy)} · not your galaxy" : PlanetText.GalaxyLabel(planet.Galaxy);
        StatusLabel = found.Discovered ? "Discovered by you" : "Not discovered yet";
        // The size class (GcCreatureSizeClasses), then both forms with gender, weight and height.
        string sizeClass = found.Creature.Scale > 0 ? SizeWord(found.Creature.SizeClass) : "";
        string forms = Helpers.Fauna.CreatureTraitText.Forms(found.Creature, PlanetTypesService.CreatureTables);
        SizeLabel = string.Join(" · ", new[] { sizeClass, forms }.Where(s => s.Length > 0));
        Battle = new CreatureBattleViewModel(found.Battle);
        AutomationName = (IsPinned ? "Pinned. " : "") + $"{Name}, {StatusLabel}, on {PlanetLabel}" + (IsHere ? " (you're on this planet)" : "")
            + $", {GalaxyLabel}. {Battle.AutomationName}";
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
    public string GalaxyLabel { get; }

    /// <summary>The save is on this creature's planet right now.</summary>
    public bool IsHere { get; }

    /// <summary>Pinned cards stay at the top of the list, whatever the filters (CreaturePinService).</summary>
    public string PinKey { get; }
    public bool IsPinned { get; }
    public string PinGlyph => IsPinned ? "\uE77A" : "\uE718";
    public string PinName => IsPinned ? $"Unpin {Name}" : $"Pin {Name} to the top of the list";
    public bool IsOtherGalaxy { get; }
    public bool IsThisGalaxy => !IsOtherGalaxy;
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
