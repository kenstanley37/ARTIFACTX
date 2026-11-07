using Microsoft.UI.Text;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using System;
using System.Linq;
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

    /// <summary>A picture for each vehicle button: the game's own Geobay icon,
    /// which shows the vehicle on its pad (user request 2026-10-13). The
    /// Dragonfly has no Geobay, so it gets the Exocraft Summoning Station.</summary>
    private static string IconIdFor(ExocraftType type) => type switch
    {
        ExocraftType.Roamer => "GARAGE_M",
        ExocraftType.Nomad => "GARAGE_S",
        ExocraftType.Colossus => "GARAGE_L",
        ExocraftType.Pilgrim => "GARAGE_B",
        ExocraftType.Nautilon => "GARAGE_SUB",
        ExocraftType.Minotaur => "GARAGE_MECH",
        _ => "SUMMON_GARAGE",
    };

    public ExocraftPage()
    {
        InitializeComponent();
        BuildTabStrip();
        ShowTab(_selectedType);
        _ = LoadIconsAsync();
    }

    private async System.Threading.Tasks.Task LoadIconsAsync()
    {
        await CatalogService.WarmCacheAsync(AllTypes.Select(IconIdFor));
        BuildTabStrip();
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
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new Image
                        {
                            Source = CatalogService.TryGet(IconIdFor(type))?.Icon,
                            Width = 72,
                            Height = 72,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                        new TextBlock
                        {
                            Text = ExocraftCapacity.DisplayName(type),
                            FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                    },
                },
                MinWidth = 104,
                Padding = new Thickness(10, 6, 10, 8)
            };
            ThemeResources.StyleChoiceButton(button, isCurrent);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, ExocraftCapacity.DisplayName(type));

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
