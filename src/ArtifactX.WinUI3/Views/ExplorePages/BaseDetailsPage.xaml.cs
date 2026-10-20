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

    private static readonly Dictionary<string, (string Name, string Glyph)> KnownPartOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["STACONNECT"] = ("Station Connector", "\uE74C"),
        ["BRIDGECONNECTOR"] = ("Bridge Connector", "\uE74C"),
        ["STATION_CORE"] = ("Station Core Terminal", "\uE916"),
        ["U_PARAGON"] = ("Station Paragon Reactor", "\uE950"),
        ["STATION_DESK"] = ("Station Directorate Desk", "\uE80F"),
        ["STATION_CHAIR"] = ("Station Concourse Chair", "\uE80F"),
        ["STATION_BENCH"] = ("Station Concourse Bench", "\uE80F"),
        ["STATION_CRATE"] = ("Station Cargo Crate", "\uE80F"),
        ["STATION_CARGO"] = ("Station Cargo Container", "\uE80F"),
        ["STATION_DOOR"] = ("Station Bulkhead Door", "\uE80F"),
        ["STATION_WINDOW"] = ("Station Observation Window", "\uE80F"),
        ["STATION_WALL"] = ("Station Interior Wall", "\uE80F"),
        ["STATION_LIGHT"] = ("Station Lighting", "\uE838"),
        ["STATION_SIGN"] = ("Station Concession Sign", "\uE80F"),
        ["BUILDFLAG"] = ("Base Computer Flag", "\uE909"),
    };

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

        if (row.Category.Contains("Settlement", StringComparison.OrdinalIgnoreCase))
        {
            if (row.Base is JObject sb && sb["@ZJ"] is JArray partsArr && partsArr.Count > 0)
            {
                var objects = partsArr.OfType<JObject>()
                    .Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => id != "^BASE_FLAG").ToList();
                _groups = objects.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
                await CatalogService.WarmCacheAsync(_groups.Select(g => g.Key));
                BaseKindsTxt.Text = $"{_groups.Count:N0} kinds of placed part";
                PartSearchBox.Visibility = Visibility.Visible;
                PartSearchBox.Header = "Filter parts placed in settlement";
                RenderSettlementWithParts();
            }
            else
            {
                _groups = new();
                BaseKindsTxt.Text = "Settlement Administration";
                PartSearchBox.Visibility = Visibility.Collapsed;
                RenderSettlementCard();
            }
        }
        else if (row.Base is JObject b)
        {
            var objects = (b["@ZJ"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => id != "^BASE_FLAG").ToList();
            _groups = objects.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            await CatalogService.WarmCacheAsync(_groups.Select(g => g.Key));
            BaseKindsTxt.Text = $"{_groups.Count:N0} kinds of part";
            PartSearchBox.Visibility = Visibility.Visible;
            PartSearchBox.Header = "Filter parts in base";
            RenderParts();
        }
        else
        {
            _groups = new();
            BaseKindsTxt.Text = "Standalone Placed Equipment";
            PartSearchBox.Visibility = Visibility.Collapsed;
            RenderPlacedEquipmentCard();
        }
    }

    private static string ResolvePartName(string rawId)
    {
        string norm = CatalogService.NormalizeId(rawId);
        if (KnownPartOverrides.TryGetValue(norm, out var known))
            return known.Name;

        if (CatalogService.TryGet(norm)?.DisplayName is { Length: > 0 } catalogName)
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(catalogName.ToLowerInvariant());

        string cleaned = norm.Replace('_', ' ');
        cleaned = Regex.Replace(cleaned, @"([a-z])([A-Z])", "$1 $2");
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLowerInvariant());
    }

    private static string ResolvePartGlyph(string rawId)
    {
        string norm = CatalogService.NormalizeId(rawId);
        if (KnownPartOverrides.TryGetValue(norm, out var known))
            return known.Glyph;

        string upper = norm.ToUpperInvariant();
        if (upper.Contains("LIGHT") || upper.Contains("LAMP")) return "\uE838"; // Light
        if (upper.Contains("CONNECT") || upper.Contains("BRIDGE") || upper.Contains("LINK") || upper.Contains("PIPE") || upper.Contains("WIRE")) return "\uE74C"; // Link
        if (upper.Contains("CORE") || upper.Contains("REACTOR") || upper.Contains("TERMINAL")) return "\uE916"; // Terminal / Facility
        if (upper.Contains("BEACON") || upper.Contains("SCANNER") || upper.Contains("REFINER") || upper.Contains("FLAG")) return "\uE909"; // Marker / Base
        return "\uE80F"; // Building / Structure
    }

    private void RenderParts(string filter = "")
    {
        PartsPanel.Children.Clear();
        RenderPartsInternal(filter);
    }

    private void RenderSettlementWithParts(string filter = "")
    {
        PartsPanel.Children.Clear();

        var card = new Border
        {
            Padding = new Thickness(18, 14, 18, 14),
            Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 14)
        };

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = "SETTLEMENT OVERVIEW & CITIZEN DIRECTIVE", Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] });
        stack.Children.Add(new TextBlock
        {
            Text = "This planetary settlement is governed from your Overseer's Office. Colony construction, citizen decisions, debt settlement, and daily production are managed through dedicated colony records.",
            Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        });

        var btn = new Button
        {
            Content = "Open Settlements Page ›",
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(12, 5, 12, 5)
        };
        btn.Click += (_, _) => Frame.Navigate(typeof(SettlementPage));
        stack.Children.Add(btn);

        card.Child = stack;
        PartsPanel.Children.Add(card);

        RenderPartsInternal(filter);
    }

    private void RenderPartsInternal(string filter)
    {
        var filtered = _groups.Where(g =>
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            string name = ResolvePartName(g.Key);
            return name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || g.Key.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        }).ToList();

        PartsSummaryCountTxt.Text = string.IsNullOrWhiteSpace(filter)
            ? $"{_groups.Count:N0} part types"
            : $"{filtered.Count:N0} of {_groups.Count:N0} part types matching \"{filter}\"";

        var shared = filtered.GroupBy(g => ResolvePartName(g.Key)).Where(n => n.Count() > 1).Select(n => n.Key).ToHashSet();
        foreach (var g in filtered)
        {
            var entry = CatalogService.TryGet(g.Key);
            string name = ResolvePartName(g.Key);
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
            else
            {
                // Fallback icon badge ensures every part has a visually balanced, high-contrast glyph
                var iconBadge = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(6),
                    Background = (Brush)Application.Current.Resources["ThemeVoidBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                    BorderThickness = new Thickness(1),
                    Child = new FontIcon
                    {
                        Glyph = ResolvePartGlyph(g.Key),
                        FontSize = 14,
                        Foreground = (Brush)Application.Current.Resources["ThemeAccentBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                line.Children.Add(iconBadge);
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

    private void RenderPlacedEquipmentCard()
    {
        PartsPanel.Children.Clear();
        PartSearchBox.Visibility = Visibility.Collapsed;
        PartsSummaryCountTxt.Text = "Placed Equipment Overview";

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
        stack.Children.Add(new TextBlock { Text = "EQUIPMENT PLACED OUTSIDE BASES", Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] });
        stack.Children.Add(new TextBlock
        {
            Text = "This equipment (portable refiner, save beacon, save point, signal booster, or geobay) was placed directly on a planet surface outside of a registered base perimeter. Each item placed across the galaxy counts towards your global 21,000 part construction limit.",
            Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = "To reclaim building budget, dial the portal address shown above and fly to the recorded coordinates to pick up or dismantle the equipment in-game.",
            Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        });

        card.Child = stack;
        PartsPanel.Children.Add(card);
    }

    private void PartSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string filter = PartSearchBox.Text.Trim();
        if (_row?.Category.Contains("Settlement", StringComparison.OrdinalIgnoreCase) == true)
        {
            RenderSettlementWithParts(filter);
        }
        else
        {
            RenderParts(filter);
        }
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
