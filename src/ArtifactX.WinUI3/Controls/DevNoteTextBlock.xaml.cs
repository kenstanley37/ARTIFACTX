using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>Development-only confidence/verification-status note (e.g. "CONFIRMED
/// against 29 real frigates", "writing hasn't been tested in-game yet") - useful
/// to contributors, meaningless to an alpha tester. Collapsed outright in Release
/// builds rather than just dimmed further, since the whole point is alpha users
/// never see it. Genuine user-facing help text (field usage instructions, warnings
/// about save-wide side effects) must NOT use this control - only actual dev notes.</summary>
public sealed partial class DevNoteTextBlock : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(DevNoteTextBlock), new PropertyMetadata(string.Empty));
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public DevNoteTextBlock()
    {
        InitializeComponent();
#if !DEBUG
        Visibility = Visibility.Collapsed;
#endif
    }
}
