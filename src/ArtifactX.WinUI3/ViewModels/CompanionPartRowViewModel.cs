using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One body-part option: the cleaned-up label and the game's own part code.</summary>
public sealed record CompanionPartOption(string Id, string Label);

/// <summary>
/// One entry of a pet's descriptor array (osl) on the Body Parts card: a dropdown of
/// that slot's sibling options, or - for an entry that matches no catalog option (the
/// per-instance detail seed) - the raw value, read-only. The ARCHETYPE row of a
/// single-root rig decides which other slots exist (CompanionViewModel rebuilds them).
/// </summary>
public sealed partial class CompanionPartRowViewModel : ObservableObject
{
    private readonly Action<CompanionPartRowViewModel> _picked;
    private bool _loading;

    public CompanionPartRowViewModel(int index, string label, IReadOnlyList<CompanionPartOption> options, int selected,
        bool isArchetype, Action<CompanionPartRowViewModel> picked)
    {
        Index = index;
        Label = isArchetype ? label + " - ARCHETYPE" : label;
        Options = options;
        IsArchetype = isArchetype;
        _picked = picked;
        _loading = true;
        SelectedIndex = selected;
        _loading = false;
    }

    /// <summary>A row for an entry that matches no catalog option: shown, not editable.</summary>
    public static CompanionPartRowViewModel ReadOnly(int index, string raw) =>
        new(index, $"osl[{index}]", Array.Empty<CompanionPartOption>(), -1, false, _ => { }) { RawValue = raw };

    public int Index { get; }
    public string Label { get; }
    public IReadOnlyList<CompanionPartOption> Options { get; }
    public bool IsArchetype { get; }
    public string RawValue { get; private init; } = "";

    public bool IsEditable => Options.Count > 0;
    public bool IsReadOnly => !IsEditable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedId))]
    public partial int SelectedIndex { get; set; }

    /// <summary>The selected option's part code, shown on hover.</summary>
    public string SelectedId => SelectedIndex >= 0 && SelectedIndex < Options.Count ? Options[SelectedIndex].Id : RawValue;

    partial void OnSelectedIndexChanged(int value)
    {
        if (!_loading && value >= 0) _picked(this);
    }
}
