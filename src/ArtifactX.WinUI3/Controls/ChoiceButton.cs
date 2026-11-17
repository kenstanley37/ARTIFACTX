using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// A button that shows whether it is the current choice (accent outline and tinted fill when selected), for
/// pickers bound in XAML: <c>IsSelected="{x:Bind IsSelected, Mode=OneWay}"</c>. The look is
/// <see cref="ThemeResources.StyleChoiceButton"/>, the same as the pickers built in code.
/// </summary>
public sealed partial class ChoiceButton : Button
{
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(ChoiceButton), new PropertyMetadata(false, (d, _) => ((ChoiceButton)d).Restyle()));

    public ChoiceButton()
    {
        Loaded += (_, _) => Restyle();
        ActualThemeChanged += (_, _) => Restyle();
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    private void Restyle() => ThemeResources.StyleChoiceButton(this, IsSelected);
}
