using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class StatBonusBoxes : UserControl
{
    private string[]? _bonusesPath;

    public StatBonusBoxes() => InitializeComponent();

    public ObservableCollection<StatBoxViewModel> Stats { get; } = new();

    /// <summary>True while a stat edit is being staged. Staging raises
    /// SaveSessionManager.PendingEditsChanged; the page skips its reload for its own
    /// stat edits (rebuilding every grid on each one made switching boxes lag).</summary>
    public bool IsStaging { get; private set; }

    /// <summary>A stat was changed and staged.</summary>
    public event EventHandler? Edited;

    /// <summary>Shows the stats of the bonus array at bonusesPath, labelled.</summary>
    public void Show(string[] bonusesPath, IReadOnlyList<(string Label, string Key)> stats)
    {
        _bonusesPath = bonusesPath;
        if (Stats.Count == stats.Count && Stats.Select(s => s.Key).SequenceEqual(stats.Select(s => s.Key)))
        {
            // Same boxes: just new values, so a box being typed in keeps its focus.
            foreach (var stat in Stats) stat.Load(StatBonusService.Read(bonusesPath, stat.Key) ?? double.NaN);
            return;
        }
        Stats.Clear();
        foreach (var (label, key) in stats)
            Stats.Add(new StatBoxViewModel(label, key, StatBonusService.Read(bonusesPath, key) ?? double.NaN, OnEdited));
    }

    /// <summary>Nothing selected: the boxes stay, blank.</summary>
    public void Clear()
    {
        _bonusesPath = null;
        foreach (var stat in Stats) stat.Load(double.NaN);
    }

    private void OnEdited(StatBoxViewModel stat)
    {
        if (_bonusesPath is null) return;
        IsStaging = true;
        try
        {
            StatBonusService.Write(_bonusesPath, stat.Key, stat.Value);
        }
        finally
        {
            IsStaging = false;
        }
        Edited?.Invoke(this, EventArgs.Empty);
    }
}
