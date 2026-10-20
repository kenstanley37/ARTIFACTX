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
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

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
    private readonly HashSet<string> _selectedPartKeys = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (string Name, string Glyph)> KnownPartOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BASE_FLAG"] = ("Base Computer", "\uE909"),
        ["BUILDFLAG"] = ("Base Computer Flag", "\uE909"),
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
        _selectedPartKeys.Clear();
        UpdatePartsToolbar();

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
                    .Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList();
                _groups = objects.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
                await CatalogService.WarmCacheAsync(_groups.Select(g => g.Key));
                BaseKindsTxt.Text = $"{_groups.Count:N0} kinds of placed part";
                PartSearchBox.Visibility = Visibility.Visible;
                SelectAllPartsBtn.Visibility = Visibility.Visible;
                DeselectAllPartsBtn.Visibility = Visibility.Visible;
                DeletePartsBtn.Visibility = Visibility.Visible;
                PartSearchBox.Header = "Filter parts placed in settlement";
                RenderSettlementWithParts();
            }
            else
            {
                _groups = new();
                BaseKindsTxt.Text = "Settlement Administration";
                PartSearchBox.Visibility = Visibility.Collapsed;
                SelectAllPartsBtn.Visibility = Visibility.Collapsed;
                DeselectAllPartsBtn.Visibility = Visibility.Collapsed;
                DeletePartsBtn.Visibility = Visibility.Collapsed;
                RenderSettlementCard();
            }
        }
        else if (row.Base is JObject b)
        {
            var objects = (b["@ZJ"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList();
            _groups = objects.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            await CatalogService.WarmCacheAsync(_groups.Select(g => g.Key));
            BaseKindsTxt.Text = $"{_groups.Count:N0} kinds of part";
            PartSearchBox.Visibility = Visibility.Visible;
            SelectAllPartsBtn.Visibility = Visibility.Visible;
            DeselectAllPartsBtn.Visibility = Visibility.Visible;
            DeletePartsBtn.Visibility = Visibility.Visible;
            PartSearchBox.Header = "Filter parts in base";
            RenderParts();
        }
        else
        {
            _groups = new();
            BaseKindsTxt.Text = "Standalone Placed Equipment";
            PartSearchBox.Visibility = Visibility.Collapsed;
            SelectAllPartsBtn.Visibility = Visibility.Collapsed;
            DeselectAllPartsBtn.Visibility = Visibility.Collapsed;
            DeletePartsBtn.Visibility = Visibility.Collapsed;
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
            bool isBaseComputer = string.Equals(CatalogService.NormalizeId(g.Key), "BASE_FLAG", StringComparison.OrdinalIgnoreCase);
            if (isBaseComputer)
            {
                name = "Base Computer (Anchor Core)";
            }
            else if (shared.Contains(name) && Regex.Match(g.Key, @"\d+$") is { Success: true } number)
            {
                name = $"{name} {number.Value}";
            }

            var line = new Grid { ColumnSpacing = 10, Padding = new Thickness(10, 8, 14, 8), Width = 380 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkBox = new CheckBox
            {
                IsChecked = _selectedPartKeys.Contains(g.Key),
                MinWidth = 0,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Tag = g.Key,
            };
            ToolTipService.SetToolTip(checkBox, isBaseComputer ? "Select Base Computer (Warning: Deletes entire base)" : "Select part type");
            checkBox.Click += (s, _) =>
            {
                if (s is CheckBox cb && cb.Tag is string key)
                {
                    if (cb.IsChecked == true)
                        _selectedPartKeys.Add(key);
                    else
                        _selectedPartKeys.Remove(key);
                    UpdatePartsToolbar();
                }
            };
            Grid.SetColumn(checkBox, 0);
            line.Children.Add(checkBox);

            if (entry?.Icon is { } icon)
            {
                var img = new Image { Source = icon, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(img, 1);
                line.Children.Add(img);
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
                    BorderBrush = (Brush)Application.Current.Resources[isBaseComputer ? "ThemeAccentBrush" : "ThemeLineBrush"],
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
                Grid.SetColumn(iconBadge, 1);
                line.Children.Add(iconBadge);
            }

            var label = new TextBlock
            {
                Text = name,
                FontSize = 13,
                FontWeight = isBaseComputer ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources[isBaseComputer ? "ThemeAccentBrush" : "ThemeTextBrush"]
            };
            ToolTipService.SetToolTip(label, isBaseComputer ? $"{name} (Essential base anchor core)" : $"{name} ({g.Key})");
            Grid.SetColumn(label, 2);
            line.Children.Add(label);

            var count = new TextBlock
            {
                Text = g.Count().ToString("N0", CultureInfo.CurrentCulture),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["ThemeAccentBrush"]
            };
            Grid.SetColumn(count, 3);
            line.Children.Add(count);

            PartsPanel.Children.Add(new Border
            {
                Child = line,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(isBaseComputer ? 1.5 : 1),
                Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"],
                BorderBrush = (Brush)Application.Current.Resources[isBaseComputer ? "ThemeAccentBrush" : "ThemeLineBrush"],
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

        if (_row?.LooseObject != null)
        {
            var deleteBtn = new Button
            {
                Content = "Delete This Equipment",
                Margin = new Thickness(0, 4, 0, 0),
                Padding = new Thickness(14, 6, 14, 6),
                Style = (Style)Application.Current.Resources["AccentButtonStyle"]
            };
            deleteBtn.Click += async (_, _) =>
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Delete Placed Equipment",
                    Content = $"Are you sure you want to delete this equipment ({_row.Name}) from your save file?",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    if (SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) is JArray currentLoose)
                    {
                        var updatedLoose = new JArray();
                        foreach (var token in currentLoose)
                        {
                            if (token is JObject o && (ReferenceEquals(o, _row.LooseObject) || JToken.DeepEquals(o, _row.LooseObject))) continue;
                            updatedLoose.Add(token);
                        }
                        SaveSessionManager.StageEdit(updatedLoose, NmsBuildCounts.LooseObjectsPath);
                        LiveSaveWatcher.LatestSnapshot = null;
                        if (Frame.CanGoBack) Frame.GoBack();
                        else Frame.Navigate(typeof(BasesPage));
                    }
                }
            };
            stack.Children.Add(deleteBtn);
        }
    }

    private void RefreshPartsDisplay()
    {
        string filter = PartSearchBox?.Text.Trim() ?? "";
        if (_row?.Category.Contains("Settlement", StringComparison.OrdinalIgnoreCase) == true)
        {
            RenderSettlementWithParts(filter);
        }
        else
        {
            RenderParts(filter);
        }
    }

    private void SelectAllParts_Click(object sender, RoutedEventArgs e)
    {
        foreach (var g in _groups)
        {
            _selectedPartKeys.Add(g.Key);
        }
        UpdatePartsToolbar();
        RefreshPartsDisplay();
    }

    private void DeselectAllParts_Click(object sender, RoutedEventArgs e)
    {
        _selectedPartKeys.Clear();
        UpdatePartsToolbar();
        RefreshPartsDisplay();
    }

    private void UpdatePartsToolbar()
    {
        int selectedTypes = _selectedPartKeys.Count;
        int selectedPartsCount = _groups.Where(g => _selectedPartKeys.Contains(g.Key)).Sum(g => g.Count());

        if (DeletePartsBtn != null)
        {
            DeletePartsBtn.IsEnabled = selectedTypes > 0;
            DeletePartsBtn.Content = selectedPartsCount > 0
                ? $"Delete Selected ({selectedPartsCount:N0})"
                : "Delete Selected";
        }
    }

    private async void DeleteParts_Click(object sender, RoutedEventArgs e)
    {
        if (_row?.Base is null || _selectedPartKeys.Count == 0) return;

        bool isBaseComputerSelected = _selectedPartKeys.Contains("^BASE_FLAG") || _selectedPartKeys.Contains("BASE_FLAG");
        int selectedCount = _groups.Where(g => _selectedPartKeys.Contains(g.Key)).Sum(g => g.Count());

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = isBaseComputerSelected ? "⚠️ Warning: Deleting Base Computer" : "Delete Selected Parts",
            PrimaryButtonText = isBaseComputerSelected ? "Delete Base Permanently" : "Delete Parts",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        var content = new StackPanel { Spacing = 12 };

        if (isBaseComputerSelected)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"You have selected the Base Computer (^BASE_FLAG) along with {selectedCount - 1:N0} other part(s) in '{_row.Name}'.",
                Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = "WARNING: Deleting the Base Computer will completely dismantle and remove this base from your save file! The base will cease to exist.",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["ThemeDamagedBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = "Tip: If you only want to clear all construction and reset your base budget, click Cancel, deselect the Base Computer, and click Delete Selected again.",
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            content.Children.Add(new TextBlock
            {
                Text = $"Are you sure you want to delete {selectedCount:N0} selected part(s) across {_selectedPartKeys.Count:N0} part type(s) from '{_row.Name}'?",
                Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = "The Base Computer will remain intact, preserving your claim on this planet/system.",
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        dialog.Content = content;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        if (isBaseComputerSelected)
        {
            if (SaveSessionManager.GetValue(S.BasesPath) is JArray currentBases)
            {
                var updatedBases = new JArray();
                foreach (var token in currentBases)
                {
                    if (token is JObject b && (ReferenceEquals(b, _row.Base) ||
                        (JToken.DeepEquals(b["oZw"], _row.Base["oZw"]) &&
                         b["NKm"]?.Value<string>() == _row.Base["NKm"]?.Value<string>() &&
                         b["peI"]?["DPp"]?.Value<string>() == _row.Base["peI"]?["DPp"]?.Value<string>())))
                    {
                        continue;
                    }
                    updatedBases.Add(token);
                }

                SaveSessionManager.StageEdit(updatedBases, S.BasesPath);
                LiveSaveWatcher.LatestSnapshot = null;
                if (Frame.CanGoBack) Frame.GoBack();
                else Frame.Navigate(typeof(BasesPage));
            }
        }
        else
        {
            if (_row.Base["@ZJ"] is JArray partsArr)
            {
                var updatedParts = new JArray();
                foreach (var token in partsArr)
                {
                    if (token is JObject partObj && _selectedPartKeys.Contains(partObj["r<7"]?.Value<string>() ?? ""))
                    {
                        continue;
                    }
                    updatedParts.Add(token);
                }

                _row.Base["@ZJ"] = updatedParts;

                if (SaveSessionManager.GetValue(S.BasesPath) is JArray currentBases)
                {
                    for (int i = 0; i < currentBases.Count; i++)
                    {
                        if (currentBases[i] is JObject b && (ReferenceEquals(b, _row.Base) ||
                            (JToken.DeepEquals(b["oZw"], _row.Base["oZw"]) &&
                             b["NKm"]?.Value<string>() == _row.Base["NKm"]?.Value<string>() &&
                             b["peI"]?["DPp"]?.Value<string>() == _row.Base["peI"]?["DPp"]?.Value<string>())))
                        {
                            var basePath = S.BasesPath.Append(i.ToString(CultureInfo.InvariantCulture)).Append("@ZJ").ToArray();
                            SaveSessionManager.StageEdit(updatedParts, basePath);
                            break;
                        }
                    }
                }

                LiveSaveWatcher.LatestSnapshot = null;

                _selectedPartKeys.Clear();
                var objects = updatedParts.OfType<JObject>()
                    .Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList();
                _groups = objects.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();

                int remainingParts = objects.Count(id => id != "^BASE_FLAG");
                BasePartsTxt.Text = NmsBuildCounts.UploadLabel(remainingParts);
                BaseKindsTxt.Text = $"{_groups.Count:N0} kinds of part";

                UpdatePartsToolbar();
                RefreshPartsDisplay();
            }
        }
    }

    private void PartSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshPartsDisplay();
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
