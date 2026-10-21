using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// A station the player has earned standing at but doesn't direct yet: its three
/// requirements, what's known about the system (the player's notes, else the game's
/// generator), how far it is and its glyphs. Hidden ones (HiddenStationsService) show faded.
/// </summary>
public sealed class StationClaimViewModel
{
    public StationClaimViewModel(StationSystemStats stats, string name, ulong? here)
    {
        ulong address = stats.Address;
        Address = address;
        Name = name;
        IsHidden = HiddenStationsService.IsHidden(address);
        IsHere = address == here;
        Progress = SpaceStationService.ClaimProgress(stats.Group);
        Distance = here is ulong from ? NmsPlanets.DistanceLightYears(from, address) : null;
        Wealth = UniverseInfo.WealthStars(address);

        int galaxy = S.GalaxyNumber(address);
        SystemLine = $"{UniverseInfo.SystemName(address)} · {NmsGalaxyNames.ForNumber(galaxy) ?? "Galaxy"} ({galaxy})";
        PortalCode = S.PortalAddress(address, 0);

        var note = SystemNotesService.Get(address);
        static string Source(string? noted) => noted is { Length: > 0 } ? $"{noted} (your notes)" : "from the game's generator";
        if (IsHere) Badges.Add(new StationBadgeViewModel("YOU ARE HERE", accent: true));
        Badges.Add(new StationBadgeViewModel(UniverseInfo.Wealth(address), false, $"Wealth - {Source(note?.Wealth)}"));
        Badges.Add(new StationBadgeViewModel(UniverseInfo.Economy(address), false, $"Economy - {Source(note?.Economy)}"));
        // A "Pirate Controlled" note already says it all: no "conflict" suffix, no second Pirate badge.
        string conflict = UniverseInfo.Conflict(address);
        bool pirateNoted = conflict.Contains("Pirate", System.StringComparison.OrdinalIgnoreCase);
        Badges.Add(new StationBadgeViewModel(pirateNoted ? conflict : $"{conflict} conflict", false, $"Conflict - {Source(note?.Conflict)}"));
        if (UniverseInfo.For(address).Pirate && !pirateNoted)
            Badges.Add(new StationBadgeViewModel("Pirate", false, "Pirate-controlled system (the game's generator)"));

        var race = SpaceStationService.Best(S.RaceStats, stats.Group);
        var guild = SpaceStationService.Best(S.GuildStats, stats.Group);
        Requirements.Add(new StationRequirementViewModel($"{race.Name ?? "Race"} standing", race.Value, S.RaceStandingRequired));
        Requirements.Add(new StationRequirementViewModel($"{guild.Name ?? "Guild"} standing", guild.Value, S.GuildStandingRequired));
        Requirements.Add(new StationRequirementViewModel("Salvage contracts", S.ReadStat(stats.Group, S.SalvageContractsStatId), S.SalvageContractsRequired));

        DistanceText = IsHere ? "In the system you're in"
            : Distance switch
            {
                null => "In a different galaxy from you",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };
    }

    public ulong Address { get; }
    public string Name { get; }
    public string SystemLine { get; }
    public string PortalCode { get; }
    public bool IsHidden { get; }
    public bool IsHere { get; }

    /// <summary>0 to 3: how far along the three requirements are.</summary>
    public double Progress { get; }
    public double? Distance { get; }
    public int Wealth { get; }

    public List<StationBadgeViewModel> Badges { get; } = new();
    public List<StationRequirementViewModel> Requirements { get; } = new();
    public string DistanceText { get; }

    public string HideLabel => IsHidden ? "Show" : "Hide";
    public string HideTip => IsHidden ? "Show this station in the list again" : "Not interested in this station? Hide it from the list (Show hidden brings it back)";
    public string HideAutomationName => $"{HideLabel} {Name}";

    public Brush Border => ThemeResources.Brush(IsHere ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Thickness BorderThickness => new(IsHere ? 2 : 1);
    public double Opacity => IsHidden ? 0.55 : 1;
}
