using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>
/// Dedicated drill-down page displaying itemized building parts for a base,
/// capital freighter, or claimed space station, or settlement administration.
/// Extracted from BasesPage to prevent 21,000 global parts from overloading a single page.
/// </summary>
public sealed partial class BaseDetailsPage : Page
{
    private BaseRowViewModel? _row;
    private List<IGrouping<string, string>> _groups = new();

    public BaseDetailsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not BaseRowViewModel row) return;
        _row = row;

        BaseTitleTxt.Text = row.Name;
        BaseSubTxt.Text = row.Detail;
        BasePartsTxt.Text = row.PartsLabel;
        BasePartsTxt.Foreground = row.PartsBrush;
        BaseFactsTxt.Text = row.Facts;
        BaseAreaTxt.Text = row.AreaLabel;
        BaseAreaTxt.Opacity = row.AreaOpacity;
        BaseDistanceTxt.Text = row.DistanceLabel;
        BaseGlyphStrip.Code = row.PortalCode;
        BaseGlyphs.ItemsSource = row.Glyphs;
        BaseCopyBtn.Tag = row.PortalCode;
        ToolTipService.SetToolTip(BaseCopyBtn, row.PortalCode);

        if (row.Base is JObject b)
        {
            var objects = (b["@ZJ"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => id != "^BASE_FLAG").ToList();
            _groups = objects.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            await CatalogService.WarmCacheAsync(_groups.Select(g => g.Key));
            BaseKindsTxt.Text = $"{_groups.Count:N0} kinds of part";
            RenderParts();
        }
        else
        {
            _groups = new();
            BaseKindsTxt.Text = "Settlement Administration";
            RenderSettlementCard();
        }
    }

    private void RenderParts(string filter = "")
    {
        PartsPanel.Children.Clear();
        string NameOf(string id) => CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } n ? n
            : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id.TrimStart('^').Replace('_', ' ').ToLowerInvariant());

        var filtered = _groups.Where(g =>
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            string name = NameOf(g.Key);
            return name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || g.Key.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        }).ToList();

        PartsSummaryCountTxt.Text = string.IsNullOrWhiteSpace(filter)
            ? $"{_groups.Count:N0} part types"
            : $"{filtered.Count:N0} of {_groups.Count:N0} part types matching \"{filter}\"";

        var shared = filtered.GroupBy(g => NameOf(g.Key)).Where(n => n.Count() > 1).Select(n => n.Key).ToHashSet();
        foreach (var g in filtered)
        {
            var entry = CatalogService.TryGet(g.Key);
            string name = NameOf(g.Key);
            if (shared.Contains(name) && Regex.Match(g.Key, @"\d+$") is { Success: true } number)
                name = $"{name} {number.Value}";

            var line = new Grid { ColumnSpacing = 12, Padding = new Thickness(12, 8, 14, 8), Width = 360 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (entry?.Icon is { } icon)
            {
                line.Children.Add(new Image { Source = icon, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center });
            }

            var label = new TextBlock
            {
                Text = name,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources["ThemeTextBrush"]
            };
            ToolTipService.SetToolTip(label, $"{name} ({g.Key})");
            Grid.SetColumn(label, 1);
            line.Children.Add(label);

            var count = new TextBlock
            {
                Text = g.Count().ToString("N0", CultureInfo.CurrentCulture),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["ThemeAccentBrush"]
            };
            Grid.SetColumn(count, 2);
            line.Children.Add(count);

            PartsPanel.Children.Add(new Border
            {
                Child = line,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
            });
        }
    }

    private void RenderSettlementCard()
    {
        PartsPanel.Children.Clear();
        PartSearchBox.Visibility = Visibility.Collapsed;
        PartsSummaryCountTxt.Text = "Settlement Overview";

        var card = new Border
        {
            Padding = new Thickness(20, 18, 20, 18),
            Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = "SETTLEMENT OVERVIEW", Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] });
        stack.Children.Add(new TextBlock
        {
            Text = "This planetary settlement is governed from your Overseer's Office. Colony construction, citizen decisions, debt settlement, and daily production are managed through dedicated colony records.",
            Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });

        var btn = new Button
        {
            Content = "Open Settlements Page ›",
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(14, 6, 14, 6)
        };
        btn.Click += (_, _) => Frame.Navigate(typeof(SettlementPage));
        stack.Children.Add(btn);

        card.Child = stack;
        PartsPanel.Children.Add(card);
    }

    private void PartSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RenderParts(PartSearchBox.Text.Trim());
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
        else
            Frame.Navigate(typeof(BasesPage));
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }
}
