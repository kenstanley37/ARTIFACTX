using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One owned frigate as a card: rating badge, role, race, the stat its role uses and
/// how many expeditions it has flown. The game's generated names aren't in the save, so a
/// frigate without a custom name is shown by its role.</summary>
public sealed partial class FrigateCardViewModel : ObservableObject
{
    public FrigateCardViewModel(FrigateSummary f)
    {
        Index = f.Index;
        Title = f.CustomName.Length > 0 ? f.CustomName : $"{RoleName(f.Role)} frigate";
        HasCustomName = f.CustomName.Length > 0;
        Rating = f.Rating.Length > 0 ? f.Rating : "?";
        RatingBrush = ThemeResources.ClassBrush(f.Rating);
        int statIndex = f.Role switch { "Combat" => 0, "Exploration" => 1, "Mining" => 2, "Diplomacy" => 3, _ => -1 };
        // The role's in the title unless the frigate has its own name; no race shows as nothing
        // (the Normandy and the Leviathan have "None").
        var parts = new List<string>();
        if (HasCustomName) parts.Add(RoleName(f.Role));
        if (f.Race is { Length: > 0 } race && race != "None") parts.Add(RaceNames.Display(race));
        if (statIndex >= 0 && statIndex < f.Stats.Length) parts.Add($"{RoleName(f.Role)} {f.Stats[statIndex]}");
        Detail = string.Join(" · ", parts);
        Expeditions = f.Expeditions == 1 ? "1 expedition" : $"{f.Expeditions.ToString("N0", CultureInfo.CurrentCulture)} expeditions";
    }

    public int Index { get; }
    public string Title { get; }
    public bool HasCustomName { get; }
    public string Rating { get; }
    public Brush RatingBrush { get; }
    public string Detail { get; }
    public string Expeditions { get; }
    public string AccessibleName => $"{Title}, class {Rating}, {Detail}, {Expeditions}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Border), nameof(BorderThickness))]
    public partial bool IsSelected { get; set; }

    public Brush Border => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Thickness BorderThickness => new(IsSelected ? 2 : 1);

    /// <summary>The game's word for a role ("Industrial" for Mining, as the frigate list shows it).</summary>
    public static string RoleName(string role) => role switch
    {
        "Mining" => "Industrial",
        "Diplomacy" => "Trade",
        "DeepSpace" or "DeepSpaceCommon" => "Deep space",
        "GhostShip" => "Ghost ship",
        "" => "Unknown",
        _ => role,
    };
}

/// <summary>A trait a slot can hold, coloured by whether it helps (green) or hurts (red).</summary>
public sealed record FrigateTraitChoice(string Id, string Label, Brush Brush)
{
    public override string ToString() => Label;
}

/// <summary>One of a frigate's trait slots.</summary>
public sealed partial class FrigateTraitSlotViewModel : ObservableObject
{
    private readonly Action<FrigateTraitSlotViewModel> _changed;
    private bool _loading;

    public FrigateTraitSlotViewModel(int slot, IReadOnlyList<FrigateTraitChoice> choices, string currentId, Action<FrigateTraitSlotViewModel> changed)
    {
        Slot = slot;
        Choices = choices;
        _changed = changed;
        _loading = true;
        Selected = choices.FirstOrDefault(c => string.Equals(c.Id, currentId, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
        _loading = false;
    }

    public int Slot { get; }
    public string Label => $"Trait {Slot + 1}";
    public IReadOnlyList<FrigateTraitChoice> Choices { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Swatch))]
    public partial FrigateTraitChoice? Selected { get; set; }

    public Brush Swatch => Selected?.Brush ?? ThemeResources.Brush("ThemeTextDimBrush");

    partial void OnSelectedChanged(FrigateTraitChoice? value)
    {
        if (!_loading && value is not null) _changed(this);
    }
}
