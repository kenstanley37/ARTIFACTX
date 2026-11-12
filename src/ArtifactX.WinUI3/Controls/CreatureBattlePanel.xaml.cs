using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A creature's Trait Potential panel (<see cref="CreatureBattleViewModel"/>); hidden while Battle is null.</summary>
public sealed partial class CreatureBattlePanel : UserControl
{
    public static readonly DependencyProperty BattleProperty =
        DependencyProperty.Register(nameof(Battle), typeof(CreatureBattleViewModel), typeof(CreatureBattlePanel),
            new PropertyMetadata(null, OnBattleChanged));

    public CreatureBattleViewModel? Battle { get => (CreatureBattleViewModel?)GetValue(BattleProperty); set => SetValue(BattleProperty, value); }

    public CreatureBattlePanel()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
    }

    private static void OnBattleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (CreatureBattlePanel)d;
        panel.Visibility = e.NewValue is null ? Visibility.Collapsed : Visibility.Visible;
        panel.Bindings.Update();
    }
}
