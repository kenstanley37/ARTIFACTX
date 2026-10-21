using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One choice in a picker: what it says, and the value it stands for ("" = any / not recorded).</summary>
public sealed record ChoiceItem(string Label, string Value)
{
    public override string ToString() => Label;
}

/// <summary>
/// A ComboBox's choices and selection. Refilling keeps what was picked when it's still
/// there, and doesn't count as the player changing it. The first choice is "any" (or
/// "not recorded"); a filter with nothing else to pick hides (IsShown).
/// </summary>
public sealed partial class ChoiceFilter : ObservableObject
{
    private bool _refilling;

    public ChoiceFilter(string header, double width = 200)
    {
        Header = header;
        Width = width;
    }

    public string Header { get; }
    public double Width { get; }

    [ObservableProperty] public partial IReadOnlyList<ChoiceItem> Choices { get; private set; } = [];
    [ObservableProperty] public partial ChoiceItem? Selected { get; set; }
    [ObservableProperty] public partial bool IsShown { get; private set; } = true;

    /// <summary>The picked value, "" for the first choice.</summary>
    public string Value => Selected?.Value ?? "";
    public bool IsSet => Value.Length > 0;

    /// <summary>The player picked something (not raised by Refill or Select).</summary>
    public event Action? Changed;

    partial void OnSelectedChanged(ChoiceItem? value)
    {
        if (_refilling) return;
        if (value is null) { if (Choices.Count > 0) Selected = Choices[0]; return; } // a ComboBox clears it when its list changes
        Changed?.Invoke();
    }

    public void Refill(string first, IEnumerable<ChoiceItem> options)
    {
        string previous = Value;
        _refilling = true;
        var list = new List<ChoiceItem> { new(first, "") };
        list.AddRange(options);
        Choices = list;
        Selected = list.FirstOrDefault(c => c.Value == previous) ?? list[0];
        OnPropertyChanged(nameof(Selected)); // the new list may hold an equal item the box hasn't selected
        _refilling = false;
        IsShown = list.Count > 1;
    }

    /// <summary>Picks a value without raising Changed (loading a record).</summary>
    public void Select(string value)
    {
        _refilling = true;
        Selected = Choices.FirstOrDefault(c => c.Value == value) ?? Choices.FirstOrDefault();
        _refilling = false;
    }

    public void Reset() => Select("");
}
