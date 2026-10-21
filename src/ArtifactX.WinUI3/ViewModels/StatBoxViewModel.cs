using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One stat-bonus box (StatBonusBoxes): "Damage" for ^SHIP_DAMAGE and its value.
/// Blank (NaN) when the save has no such entry.</summary>
public sealed partial class StatBoxViewModel : ObservableObject
{
    private readonly Action<StatBoxViewModel> _edited;
    private bool _loading;

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
        Value = value;
        _loading = false;
    }

    partial void OnValueChanged(double oldValue, double newValue)
    {
        if (!_loading && !double.IsNaN(newValue) && !newValue.Equals(oldValue)) _edited(this);
    }
}
