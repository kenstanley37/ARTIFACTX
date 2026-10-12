using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>Edits JumpPlanner.Range; pages listen to JumpPlanner.RangeChanged.</summary>
public sealed partial class HyperdriveRangeBox : UserControl
{
    private bool _loading = true;

    public HyperdriveRangeBox()
    {
        InitializeComponent();
        RangeBox.Value = JumpPlanner.Range ?? double.NaN;
        _loading = false;
    }

    private void RangeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading) return;
        JumpPlanner.Range = double.IsNaN(args.NewValue) || args.NewValue < 1 ? null : args.NewValue;
    }
}
