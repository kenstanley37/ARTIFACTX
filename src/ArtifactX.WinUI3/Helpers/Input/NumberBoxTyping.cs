using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Globalization.NumberFormatting;

namespace ArtifactX.WinUI3.Helpers.Input;

/// <summary>
/// NumberBox only commits its Value on Enter or when focus leaves it, so an edit
/// wasn't staged (and Save/Reset didn't appear) while the player was still in the
/// box (Bug 46, 2026-11-03). <c>NumberBoxTyping.CommitWhileTyping="True"</c>
/// commits as soon as the typed text is a complete in-range number - text the box
/// would format back exactly as typed, so setting Value never rewrites what's being
/// typed ("5." or a cleared box wait for Enter/focus loss, as before). A box left
/// empty goes back to the last value it holds, since backspacing "45" has already
/// staged 4 and an empty box would hide that.
/// </summary>
public static class NumberBoxTyping
{
    public static readonly DependencyProperty CommitWhileTypingProperty = DependencyProperty.RegisterAttached(
        "CommitWhileTyping", typeof(bool), typeof(NumberBoxTyping), new PropertyMetadata(false, OnCommitWhileTypingChanged));

    // The value the box holds (when focused, then as committed while typing).
    private static readonly DependencyProperty LastValueProperty = DependencyProperty.RegisterAttached(
        "LastValue", typeof(double), typeof(NumberBoxTyping), new PropertyMetadata(double.NaN));

    public static bool GetCommitWhileTyping(NumberBox box) => (bool)box.GetValue(CommitWhileTypingProperty);
    public static void SetCommitWhileTyping(NumberBox box, bool value) => box.SetValue(CommitWhileTypingProperty, value);

    private static void OnCommitWhileTypingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NumberBox box) return;
        box.Loaded -= Box_Loaded;
        if (e.NewValue is true) box.Loaded += Box_Loaded;
    }

    /// <summary>The inner text box only exists once the template is applied; Loaded
    /// can run again after the page is revisited, so the handler is swapped, not added.</summary>
    private static void Box_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not NumberBox box || FindInputBox(box) is not TextBox input) return;
        input.TextChanged -= Input_TextChanged;
        input.TextChanged += Input_TextChanged;
        input.GotFocus -= Input_GotFocus;
        input.GotFocus += Input_GotFocus;
        input.LostFocus -= Input_LostFocus;
        input.LostFocus += Input_LostFocus;
    }

    private static void Input_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject input && FindOwner(input) is NumberBox box) box.SetValue(LastValueProperty, box.Value);
    }

    /// <summary>Runs after the box's own commit on focus loss, which leaves Value NaN for empty text.</summary>
    private static void Input_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject input || FindOwner(input) is not NumberBox box || !GetCommitWhileTyping(box)) return;
        box.DispatcherQueue.TryEnqueue(() =>
        {
            if (double.IsNaN(box.Value) && box.GetValue(LastValueProperty) is double last && !double.IsNaN(last)) box.Value = last;
        });
    }

    private static void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox input || FindOwner(input) is not NumberBox box
            || !GetCommitWhileTyping(box) || box.NumberFormatter is not INumberParser parser) return;
        string text = input.Text.Trim();
        if (parser.ParseDouble(text) is not double value || value < box.Minimum || value > box.Maximum) return;
        if (box.NumberFormatter.FormatDouble(value) != text || value == box.Value) return;
        box.SetValue(LastValueProperty, value);
        box.Value = value;
    }

    private static TextBox? FindInputBox(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox { Name: "InputBox" } input) return input;
            if (FindInputBox(child) is TextBox found) return found;
        }
        return null;
    }

    private static NumberBox? FindOwner(DependencyObject child)
    {
        for (var parent = VisualTreeHelper.GetParent(child); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is NumberBox box) return box;
        return null;
    }
}
