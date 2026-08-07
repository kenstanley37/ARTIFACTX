using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using System;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// One page, in-page tab strip for the 7 fixed exocraft types - same "one
/// page, many switchable sections" shape as MilestonesPage, not a nav parent
/// with a separate page per type (2026-08-06 user correction after an
/// initial pass built 7 separate pages: "We just need the tech area and the
/// cargo area for each one on one page").
/// </summary>
public sealed partial class ExocraftPage : Page
{
    private static readonly ExocraftType[] AllTypes =
    {
        ExocraftType.Roamer,
        ExocraftType.Nomad,
        ExocraftType.Colossus,
        ExocraftType.Pilgrim,
        ExocraftType.Dragonfly,
        ExocraftType.Nautilon,
        ExocraftType.Minotaur,
    };

    private ExocraftType _selectedType = ExocraftType.Roamer;

    public ExocraftPage()
    {
        InitializeComponent();
        BuildTabStrip();
        ShowTab(_selectedType);
    }

    private void BuildTabStrip()
    {
        TabPanel.Children.Clear();

        foreach (var type in AllTypes)
        {
            bool isCurrent = type == _selectedType;

            var button = new Button
            {
                Tag = type,
                Content = new TextBlock
                {
                    Text = ExocraftCapacity.DisplayName(type),
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal
                },
                Padding = new Thickness(16, 6, 16, 6),
                BorderThickness = new Thickness(isCurrent ? 2 : 1),
                BorderBrush = new SolidColorBrush(isCurrent
                    ? Color.FromArgb(255, 255, 157, 0)
                    : Color.FromArgb(255, 90, 98, 112)),
                Background = new SolidColorBrush(isCurrent
                    ? Color.FromArgb(60, 255, 157, 0)
                    : Color.FromArgb(20, 255, 255, 255))
            };

            button.Click += TabButton_Click;
            TabPanel.Children.Add(button);
        }
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ExocraftType type } || type == _selectedType) return;

        _selectedType = type;
        BuildTabStrip();
        ShowTab(type);
    }

    /// <summary>Swaps in a fresh ExocraftDetailControl for the given type -
    /// removing the previous one from ContentHost.Children fires its
    /// Unloaded event, which unsubscribes its SaveSessionManager handlers
    /// (see ExocraftDetailControl.Control_Unloaded) before the new one is
    /// added, so only the currently-visible tab's grids are ever loaded or
    /// subscribed at once.</summary>
    private void ShowTab(ExocraftType type)
    {
        ContentHost.Children.Clear();

        var detail = new ExocraftDetailControl();
        ContentHost.Children.Add(detail);
        detail.Initialize(type);
    }
}
