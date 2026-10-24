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
/// how many expeditions it has flown. The generated game name is calculated from its
/// ResourceSeed; a player-entered custom name takes precedence.</summary>
public sealed partial class FrigateCardViewModel : ObservableObject
{
    public FrigateCardViewModel(FrigateSummary f, string? generatedName)
        => Update(f, generatedName);

    /// <summary>Refresh this card without replacing its visual or selection.</summary>
    public void Update(FrigateSummary f, string? generatedName)
    {
        Index = f.Index;
        Key = f.Key;
        Title = f.CustomName.Length > 0 ? f.CustomName : generatedName ?? $"{RoleName(f.Role)} frigate";
        HasCustomName = f.CustomName.Length > 0;
        string rating = f.Rating.Length > 0 ? f.Rating : "?";
        if (Rating != rating)
        {
            Rating = rating;
            RatingBrush = ThemeResources.ClassBrush(f.Rating);
        }
        int statIndex = f.Role switch { "Combat" => 0, "Exploration" => 1, "Mining" => 2, "Diplomacy" => 3, _ => -1 };
        // Include the role beside a named ship unless its main stat already
        // identifies it. Race None has no label (organic and special frigates).
        var parts = new List<string>();
        if ((HasCustomName || generatedName is not null) && statIndex < 0) parts.Add(RoleName(f.Role));
        if (f.Race is { Length: > 0 } race && race != "None") parts.Add(RaceNames.Display(race));
        if (statIndex >= 0 && statIndex < f.Stats.Length) parts.Add($"{RoleName(f.Role)} {f.Stats[statIndex]}");
        Detail = string.Join(" · ", parts);
        Expeditions = f.Expeditions == 1 ? "1 expedition" : $"{f.Expeditions.ToString("N0", CultureInfo.CurrentCulture)} expeditions";
    }

    public int Index { get; private set; }
    public string Key { get; private set; } = "";
    public bool HasCustomName { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial string Title { get; private set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial string Rating { get; private set; } = "";
    [ObservableProperty]
    public partial Brush RatingBrush { get; private set; } = ThemeResources.ClassBrush("");
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial string Detail { get; private set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial string Expeditions { get; private set; } = "";
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
        "DeepSpace" or "DeepSpaceCommon" => "Organic",
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
