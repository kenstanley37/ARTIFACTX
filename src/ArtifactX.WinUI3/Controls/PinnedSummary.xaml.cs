using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// A page's compact summary for its pinned top bar (owner idea, 2026-11-11: on a long page you lose sight of
/// what you're looking at). Title (bold), Accent (accent colour, e.g. the planet type) and a dimmer Details
/// line, each one line and trimmed on a narrow window. Fades in when <see cref="IsShown"/> is true; while
/// hidden it takes no clicks and screen readers skip it, so the page isn't announced twice.
/// </summary>
public sealed partial class PinnedSummary : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PinnedSummary), new PropertyMetadata("", OnTextChanged));
    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(string), typeof(PinnedSummary), new PropertyMetadata("", OnTextChanged));
    public static readonly DependencyProperty DetailsProperty =
        DependencyProperty.Register(nameof(Details), typeof(string), typeof(PinnedSummary), new PropertyMetadata("", OnTextChanged));
    public static readonly DependencyProperty IsShownProperty =
        DependencyProperty.Register(nameof(IsShown), typeof(bool), typeof(PinnedSummary), new PropertyMetadata(false, OnIsShownChanged));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Accent { get => (string)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public string Details { get => (string)GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }
    public bool IsShown { get => (bool)GetValue(IsShownProperty); set => SetValue(IsShownProperty, value); }

    /// <summary>" · " between title and accent, only when both have text.</summary>
    public string AccentSeparator => string.IsNullOrEmpty(Title) || string.IsNullOrEmpty(Accent) ? "" : "  ·  ";

    public PinnedSummary()
    {
        InitializeComponent();
        ApplyShown();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PinnedSummary)d).Bindings.Update();
    }

    private static void OnIsShownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PinnedSummary)d).ApplyShown();

    private void ApplyShown()
    {
        Root.Opacity = IsShown ? 1 : 0;
        Root.IsHitTestVisible = IsShown;
        // Panels and UserControls have no automation peer, so the two text lines themselves are shown to or
        // hidden from screen readers.
        var view = IsShown ? AccessibilityView.Content : AccessibilityView.Raw;
        AutomationProperties.SetAccessibilityView(TitleLine, view);
        AutomationProperties.SetAccessibilityView(DetailsLine, view);
    }
}
