using System.Windows.Input;

namespace ArtifactX.Maui.Controls;

/// <summary>
/// A card the whole of which is one button: its content is drawn as usual and a see-through Button over it takes the
/// tap and is what screen readers find (named by <see cref="AccessibleName"/>). A TapGestureRecognizer alone leaves the
/// card unclickable to TalkBack, and many players with poor eyesight use it. Don't put other tap targets inside.
/// </summary>
public partial class TapCard : ContentView
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(TapCard));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(TapCard));

    public static readonly BindableProperty AccessibleNameProperty =
        BindableProperty.Create(nameof(AccessibleName), typeof(string), typeof(TapCard), "");

    public static readonly BindableProperty AccessibleHintProperty =
        BindableProperty.Create(nameof(AccessibleHint), typeof(string), typeof(TapCard), "");

    public TapCard() => InitializeComponent();

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>What a screen reader says for the whole card.</summary>
    public string AccessibleName
    {
        get => (string)GetValue(AccessibleNameProperty);
        set => SetValue(AccessibleNameProperty, value);
    }

    /// <summary>What tapping does, when the name doesn't say ("Opens the recipe").</summary>
    public string AccessibleHint
    {
        get => (string)GetValue(AccessibleHintProperty);
        set => SetValue(AccessibleHintProperty, value);
    }
}
