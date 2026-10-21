using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// Toggles its content between a compact, game-matching preview height and showing every
/// row at once - the ScrollViewer already sizes to its content, so removing the cap (rather
/// than computing an exact "full" height) is all expanding needs. The chevron points the
/// way it will go, so it reads as "click to do this next", not as a state label. Whether it's
/// open is remembered per SettingKey (2026-10-20).
/// </summary>
[ContentProperty(Name = nameof(ScrollContent))]
public sealed partial class ExpandableScroll : UserControl
{
    private const string ChevronDown = "", ChevronUp = "";

    public static readonly DependencyProperty ScrollContentProperty =
        DependencyProperty.Register(nameof(ScrollContent), typeof(object), typeof(ExpandableScroll), new PropertyMetadata(null));

    public static readonly DependencyProperty CollapsedHeightProperty =
        DependencyProperty.Register(nameof(CollapsedHeight), typeof(double), typeof(ExpandableScroll), new PropertyMetadata(368.0, (d, _) => ((ExpandableScroll)d).Apply()));

    public object? ScrollContent
    {
        get => GetValue(ScrollContentProperty);
        set => SetValue(ScrollContentProperty, value);
    }

    /// <summary>The preview height (232 shows a tech grid's first rows, 368 a cargo grid's).</summary>
    public double CollapsedHeight
    {
        get => (double)GetValue(CollapsedHeightProperty);
        set => SetValue(CollapsedHeightProperty, value);
    }

    /// <summary>AppSettingsService flag remembering whether it's open, e.g. "ExosuitCargoExpanded".</summary>
    public string SettingKey { get; set; } = "";

    private bool _expanded;

    public ExpandableScroll()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _expanded = SettingKey.Length > 0 && AppSettingsService.GetFlag(SettingKey, false);
            Apply();
        };
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        _expanded = !_expanded;
        if (SettingKey.Length > 0) AppSettingsService.SetFlag(SettingKey, _expanded);
        Apply();
    }

    private void Apply()
    {
        Scroller.MaxHeight = _expanded ? double.PositiveInfinity : CollapsedHeight;
        Chevron.Glyph = _expanded ? ChevronUp : ChevronDown;
        string tip = _expanded ? "Collapse" : "Expand";
        ToolTipService.SetToolTip(ToggleBtn, tip);
        AutomationProperties.SetName(ToggleBtn, tip);
    }
}
