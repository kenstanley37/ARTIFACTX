using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One creature on a planet's page: 3D model, species name and traits.</summary>
public sealed partial class CreatureCardViewModel : ObservableObject
{
    public CreatureCardViewModel(Creature creature, ulong system, int planet, string? rigId = null, bool isDiscovered = true)
    {
        Creature = creature;
        RigId = rigId ?? "antelope";
        IsDiscovered = isDiscovered;
        Traits = string.Join(" · ", new[] { creature.Rarity, creature.Habitat, creature.Activity }.Where(s => s.Length > 0));
        Body = string.Join(" · ", new[] { creature.Gender, creature.Size }.Where(s => s.Length > 0));
        string? noPet = IsExtinct ? "extinct"
            : creature.Habitat.Contains("Underwater", StringComparison.OrdinalIgnoreCase) ? "lives underwater"
            : Regex.IsMatch(creature.Habitat, "Fly|Flight|Air|Sky", RegexOptions.IgnoreCase) ? "flies"
            : null;
        CompanionNote = noPet is null ? "" : $"Can't be a companion - {noPet}";
    }

    public Creature Creature { get; }
    public string RigId { get; }
    public bool IsDiscovered { get; }
    public bool IsSilhouette => !IsDiscovered;
    public string Name => Creature.Name;
    public string Traits { get; }
    public string Body { get; }
    public bool HasBody => Body.Length > 0;
    public string CompanionNote { get; }
    public bool IsExtinct => Creature.Rarity == "Extinct";
    public string BadgeText => IsDiscovered ? "DISCOVERED" : "UNDISCOVERED";
}
