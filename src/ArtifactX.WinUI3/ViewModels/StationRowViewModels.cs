using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One directorship requirement (race, guild, salvage contracts) as "value / required"
/// with a bar, gold once met.</summary>
public sealed class StationRequirementViewModel
{
    public StationRequirementViewModel(string label, int value, int required)
    {
        Label = label;
        Value = value;
        Required = required;
        double ratio = required <= 0 ? 0 : Math.Clamp((double)value / required, 0, 1);
        FillWidth = new GridLength(ratio, GridUnitType.Star);
        RestWidth = new GridLength(1 - ratio, GridUnitType.Star);
    }

    public string Label { get; }
    public int Value { get; }
    public int Required { get; }
    public bool Done => Value >= Required;
    public string ValueText => $"{Value} / {Required}";
    public string AutomationName => $"{Label}: {Value} of {Required}";

    public GridLength FillWidth { get; }
    public GridLength RestWidth { get; }

    public Brush BarBrush => ThemeResources.Brush(Done ? "ThemeDoneBrush" : "ThemeAccentBrush");
    public Brush ValueBrush => ThemeResources.Brush(Done ? "ThemeDoneBrush" : "ThemeTextBrush");
}

/// <summary>A small tag on a claim card: "YOU ARE HERE" (accent) or a system fact.</summary>
public sealed class StationBadgeViewModel(string text, bool accent, string? tip = null)
{
    public string Text { get; } = text;
    public string? Tip { get; } = tip;
    public Brush? Background { get; } = accent ? ThemeResources.Brush("ThemeAccentBrush") : null;
    public Brush? Border { get; } = accent ? null : ThemeResources.Brush("ThemeLineBrush");
    public Thickness BorderThickness { get; } = new(accent ? 0 : 1);
    public Brush Foreground { get; } = ThemeResources.Brush(accent ? "ThemeOnAccentBrush" : "ThemeTextBrush");
}

/// <summary>Local standing in a station's details: a bar scaled so the claim requirement
/// sits at ~60%, with a tick there. Gold once met.</summary>
public sealed class StationStandingViewModel
{
    public StationStandingViewModel(string label, int value, int required)
    {
        Label = label;
        ValueText = value.ToString("N0");
        double max = Math.Max(required * 1.6, value);
        FillWidth = new GridLength(value / max, GridUnitType.Star);
        RestWidth = new GridLength(1 - value / max, GridUnitType.Star);
        TickWidth = new GridLength(required / max, GridUnitType.Star);
        TickRestWidth = new GridLength(1 - required / max, GridUnitType.Star);
        BarBrush = ThemeResources.Brush(value >= required ? "ThemeDoneBrush" : "ThemeAccentAltBrush");
    }

    public string Label { get; }
    public string ValueText { get; }
    public GridLength FillWidth { get; }
    public GridLength RestWidth { get; }
    public GridLength TickWidth { get; }
    public GridLength TickRestWidth { get; }
    public Brush BarBrush { get; }
}

/// <summary>"12 fauna": a count of the player's discoveries in a station's system.</summary>
public sealed class StationChipViewModel(int count, string label)
{
    public string Count { get; } = count.ToString("N0");
    public string Label { get; } = label;
}

/// <summary>One step of "How to claim a station".</summary>
public sealed class StationHowToStep(int number, string text)
{
    public string Number { get; } = number.ToString();
    public string Text { get; } = text;
}

/// <summary>A station the player directs, as a screenshot tile.</summary>
public sealed partial class StationTileViewModel : ObservableObject
{
    public StationTileViewModel(OwnedStation station, bool isCurrent)
    {
        Station = station;
        IsCurrent = isCurrent;
    }

    public OwnedStation Station { get; }
    public ulong Address => Station.Address;
    public string Name => Station.Name;

    /// <summary>The station is in the system the player is in.</summary>
    public bool IsCurrent { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Border), nameof(BorderThickness))]
    public partial bool IsSelected { get; set; }

    public Brush Border => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Thickness BorderThickness => new(IsSelected ? 2 : 1);

    [ObservableProperty] public partial ImageSource? Picture { get; set; }
}
