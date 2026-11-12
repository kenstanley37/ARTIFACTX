using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One creature on a planet's page: 3D model, species name and traits.</summary>
public sealed partial class CreatureCardViewModel : ObservableObject
{
    public CreatureCardViewModel(Creature creature, ulong system, int planet, string? rigId = null, bool isDiscovered = true, string about = "",
        CreatureBattleViewModel? battle = null, bool? canTame = null)
    {
        About = about;
        Creature = creature;
        RigId = rigId ?? "antelope";
        IsDiscovered = isDiscovered;
        Traits = string.Join(" · ", new[] { creature.Rarity, creature.Habitat, creature.Activity }.Where(s => s.Length > 0));
        Body = string.Join(" · ", new[] { creature.Gender, creature.Size }.Where(s => s.Length > 0));
        // The game's own flag (CanBeUsedInPetBattler) when the game data has it; otherwise a guess from the habitat.
        // Some hovering kinds (flying lizards, snakes, beetles) can be pets, so the guess only applies without the flag.
        string? reason = creature.Habitat.Contains("Underwater", StringComparison.OrdinalIgnoreCase) ? "lives underwater"
            : Regex.IsMatch(creature.Habitat, "Fly|Flight|Air|Sky", RegexOptions.IgnoreCase) ? "flies"
            : null;
        string? noPet = IsExtinct ? "extinct"
            : canTame == true ? null
            : canTame == false ? reason ?? ""
            : reason;
        CanTame = noPet is null;
        CompanionNote = noPet is null ? "" : noPet.Length > 0 ? $"Can't be a companion - {noPet}" : "Can't be a companion";
        // Battle stats only matter for creatures a player can adopt.
        Battle = CanTame ? battle : null;
    }

    /// <summary>Whether a player can adopt this creature (and so take it into pet battles); untameable cards go last.</summary>
    public bool CanTame { get; }

    public Creature Creature { get; }
    public string RigId { get; }

    /// <summary>The creature's own scene and seed, which pick its body parts (CreatureModelService.PartsForSeedAsync);
    /// empty / 0 when not known, and the rig's default parts are drawn.</summary>
    public string ModelScene { get; init; } = "";
    public ulong ModelSeed { get; init; }
    public bool IsDiscovered { get; }
    public bool IsSilhouette => !IsDiscovered;
    public string Name => Creature.Name;
    public string Traits { get; }
    public string Body { get; }
    public bool HasBody => Body.Length > 0;
    /// <summary>Behaviour, diet and notes of a predicted species ("Hasty · eats Solar Vine · Photosensitive").</summary>
    public string About { get; }
    public bool HasAbout => About.Length > 0;
    /// <summary>The visor's "Trait Potential" panel: overall class, Combat Effectiveness / Agility / Health and element.</summary>
    public CreatureBattleViewModel? Battle { get; }
    public string CompanionNote { get; }
    public bool IsExtinct => Creature.Rarity == "Extinct";
    public string BadgeText => IsDiscovered ? "DISCOVERED" : "UNDISCOVERED";
}
