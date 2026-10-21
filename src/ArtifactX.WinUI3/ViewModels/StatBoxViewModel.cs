using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One stat-bonus box (StatBonusBoxes): "Damage" for ^SHIP_DAMAGE and its value.
/// Blank (NaN) when the save has no such entry.</summary>
public sealed partial class StatBoxViewModel : ObservableObject
{
    private readonly Action<StatBoxViewModel> _edited;
    private bool _loading;
    private double _shown;

    public StatBoxViewModel(string label, string key, double value, Action<StatBoxViewModel> edited)
    {
        Label = label;
        Key = key;
        _edited = edited;
        Load(value);
    }

    public string Label { get; }
    public string Key { get; }

    [ObservableProperty] public partial double Value { get; set; }

    /// <summary>Shows a value read from the save, without counting it as an edit.</summary>
    public void Load(double value)
    {
        _loading = true;
        _shown = value;
        Value = value;
        _loading = false;
    }

    /// <summary>The NumberBox shows 8 decimals and hands back its rounded value
    /// (77.0675582885742 -> 77.06755829) when it reformats, e.g. after switching
    /// items - that's not an edit, so it's never staged (found 2026-10-21).</summary>
    partial void OnValueChanged(double oldValue, double newValue)
    {
        if (_loading || double.IsNaN(newValue) || IsSameAsShown(newValue)) return;
        _shown = newValue;
        _edited(this);
    }

    private bool IsSameAsShown(double value) =>
        !double.IsNaN(_shown) && Math.Abs(value - _shown) <= 1e-7 * Math.Max(1, Math.Abs(_shown));
}
