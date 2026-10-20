using ArtifactX.Core;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>
/// Dedicated drill-down page for viewing and editing full planet details,
/// save telemetry, recorded resources, creature photos, fish catches, and notes.
/// </summary>
public sealed partial class PlanetDetailsPage : Page
{
    private KnownPlanet? _planet;
    private List<KnownPlanet> _siblings = new();
    private PlanetSnapshot? _snapshot;
    private ulong? _currentSystem;
    private int _currentPlanet;
    private List<string> _editingResources = new();
    private List<string> _editingNotes = new();
    private List<(string Word, string Group, string Tier)> _planetWords = new();
    private List<string> _substances = new();

    public PlanetDetailsViewModel ViewModel { get; } = new();

    public PlanetDetailsPage()
    {
        InitializeComponent();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is KnownPlanet planet)
        {
            await LoadPlanetAsync(planet);
        }
        else if (e.Parameter is string planetName && !string.IsNullOrEmpty(planetName))
        {
            PlanetTitleTxt.Text = planetName;
        }
    }

    private async Task LoadPlanetAsync(KnownPlanet planet)
    {
        _planet = planet;

        // Load vocabulary descriptors asynchronously if not cached
        try
        {
            var words = await CatalogService.GetPlanetDescriptorsAsync();
            _planetWords = words ?? new();
            var substances = await CatalogService.GetSubstanceNamesAsync();
            _substances = (substances ?? new())
                .Concat(_planetWords.Where(w => w.Group == "Resource").Select(w => w.Word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch
        {
            // Fallback gracefully if catalog isn't loaded
        }

        // Snapshot of current save
        _snapshot = LiveSaveWatcher.FromSession();
        _currentSystem = S.ReadSystemAddress(_snapshot.Universe);
        _currentPlanet = _snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        bool isHere = _currentSystem == planet.SystemAddress && _currentPlanet == planet.PlanetIndex;
        bool isMoon = BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex);

        // 1. Sibling navigation
        var allPlanets = NmsPlanets.ReadAll(_snapshot.Discoveries);
        _siblings = allPlanets.Where(p => p.SystemAddress == planet.SystemAddress)
            .GroupBy(p => p.PlanetIndex).Select(g => g.First())
            .OrderBy(p => p.PlanetIndex).ToList();

        if (_siblings.Count > 1)
        {
            SiblingNavPanel.Visibility = Visibility.Visible;
            int at = _siblings.FindIndex(p => p.PlanetIndex == planet.PlanetIndex);
            PrevPlanetBtn.IsEnabled = at > 0;
            NextPlanetBtn.IsEnabled = at >= 0 && at < _siblings.Count - 1;
        }
        else
        {
            SiblingNavPanel.Visibility = Visibility.Collapsed;
        }

        // 2. Hero Header
        PlanetDrawingFrame.Child = PlanetDrawingHelper.CreatePlanetDrawing(planet, 76, isHere);
        string systemName = UniverseInfo.SystemName(planet.SystemAddress);
        PlanetEyebrowTxt.Text = $"{(isMoon ? "MOON" : $"PLANET {planet.PlanetIndex}")} OF {systemName.ToUpperInvariant()}";
        string planetName = UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex);
        PlanetTitleTxt.Text = planetName;

        string originalName = UniverseInfo.GeneratedPlanetName(planet.SystemAddress, planet.PlanetIndex);
        if (!string.Equals(planetName, originalName, StringComparison.OrdinalIgnoreCase))
        {
            OriginalNameTxt.Text = $"Originally named: {originalName}";
            OriginalNameTxt.Visibility = Visibility.Visible;
        }
        else
        {
            OriginalNameTxt.Visibility = Visibility.Collapsed;
        }

        SystemNameTxt.Text = systemName;
        BiomeTxt.Text = planet.HasRings ? $"{planet.TypeName} with rings" : planet.TypeName;
        HereBadge.Visibility = isHere ? Visibility.Visible : Visibility.Collapsed;

        // Portal Glyphs
        GlyphContainer.Children.Clear();
        var glyphTiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var c in planet.PortalCode)
        {
            var icon = CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon;
            if (icon is not null)
            {
                glyphTiles.Children.Add(new Border
                {
                    Width = 32,
                    Height = 32,
                    Background = (Brush)Application.Current.Resources["ThemeVoidBrush"],
                    CornerRadius = ThemeResources.CardCornerRadius,
                    Child = new Image { Source = icon, Width = 28, Height = 28 }
                });
            }
        }
        var glyphStrip = new ArtifactX.WinUI3.Controls.GlyphStrip { Content = glyphTiles, Code = planet.PortalCode, VerticalAlignment = VerticalAlignment.Center };
        var copyBtn = new Button { Content = "Copy", Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        copyBtn.Click += (_, _) =>
        {
            var dp = new DataPackage();
            dp.SetText(planet.PortalCode);
            Clipboard.SetContent(dp);
        };
        GlyphContainer.Children.Add(glyphStrip);
        GlyphContainer.Children.Add(copyBtn);

        // 3. Environmental Stats
        var details = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex);
        WeatherTypeTxt.Text = !string.IsNullOrEmpty(details?.Weather) ? details.Weather : "Not Recorded";
        WeatherDescTxt.Text = planet.TypeName;
        SentinelActivityTxt.Text = !string.IsNullOrEmpty(details?.Sentinels) ? details.Sentinels : "Standard Activity";
        FloraDensityTxt.Text = !string.IsNullOrEmpty(details?.Flora) ? details.Flora : "Uncharted";
        FaunaAbundanceTxt.Text = !string.IsNullOrEmpty(details?.Fauna) ? details.Fauna : (details?.FaunaTotal is int ft ? $"{ft} Species" : "Uncharted");

        // 4. Detected Resources
        ResourcesWrapPanel.Children.Clear();
        var resList = details?.Resources ?? new List<string>();
        if (resList.Count == 0)
        {
            ResourcesWrapPanel.Children.Add(new TextBlock
            {
                Text = "No resources recorded yet. Use 'Edit details' below or survey the planet in-game.",
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                FontSize = 13
            });
        }
        else
        {
            foreach (var r in resList)
            {
                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = ThemeResources.CardCornerRadius,
                    Padding = new Thickness(14, 10, 14, 10),
                    Width = 260
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var iconBox = new Border
                {
                    Width = 36,
                    Height = 36,
                    Background = (Brush)Application.Current.Resources["ThemeVoidBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = ThemeResources.CardCornerRadius,
                    Margin = new Thickness(0, 0, 12, 0),
                    Child = new FontIcon
                    {
                        Glyph = "\uE8EC",
                        FontSize = 16,
                        Foreground = (Brush)Application.Current.Resources["ThemeAccentBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textStack.Children.Add(new TextBlock
                {
                    Text = r,
                    FontFamily = (FontFamily)Application.Current.Resources["ThemeDisplayFont"],
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14,
                    Foreground = (Brush)Application.Current.Resources["ThemeTextBrush"]
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = "Planet Resource",
                    Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                    FontSize = 11
                });
                Grid.SetColumn(iconBox, 0);
                Grid.SetColumn(textStack, 1);
                grid.Children.Add(iconBox);
                grid.Children.Add(textStack);
                card.Child = grid;
                ResourcesWrapPanel.Children.Add(card);
            }
        }

        // 5. Recorded Details Summary & Editor
        RenderDetailsSummary(planet, details);
        PrepareDetailsEditor(planet, details);

        // 6. Save Facts
        RenderSaveFacts(planet);

        // 7. Creatures
        RenderCreatures(planet);

        // 8. Fishing
        RenderFish(planet);

        // 9. Bases
        RenderBases(planet);

        // 10. Finds
        RenderFinds(planet);
    }

    private void RenderDetailsSummary(KnownPlanet planet, PlanetDetails? details)
    {
        DetailsSummaryPanel.Children.Clear();
        bool? water = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex);

        void AddRow(string label, string val)
        {
            if (string.IsNullOrEmpty(val)) return;
            var row = new Grid { ColumnSpacing = 16 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["ThemeTextDimBrush"]
            });
            var valTxt = new TextBlock
            {
                Text = val,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["ThemeTextBrush"],
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            Grid.SetColumn(valTxt, 1);
            row.Children.Add(valTxt);
            DetailsSummaryPanel.Children.Add(row);
        }

        AddRow("Weather", details?.Weather ?? "");
        AddRow("Sentinels", details?.Sentinels ?? "");
        AddRow("Flora", details?.Flora ?? "");
        AddRow("Fauna", details?.Fauna ?? "");
        AddRow("Water", water switch { true => "Yes", false => "No", _ => "" });
        AddRow("Resources", details is not null && details.Resources.Count > 0 ? string.Join(", ", details.Resources) : "");
        AddRow("Notes", details is not null && details.Notes.Count > 0 ? string.Join(" · ", details.Notes) : "");

        if (DetailsSummaryPanel.Children.Count == 0)
        {
            DetailsSummaryPanel.Children.Add(new TextBlock
            {
                Text = "Nothing recorded yet — click 'Edit details' to record weather, sentinels, resources or notes.",
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                FontSize = 13
            });
        }
    }

    private void PrepareDetailsEditor(KnownPlanet planet, PlanetDetails? details)
    {
        var d = details?.Copy() ?? new PlanetDetails();
        _editingResources = d.Resources.ToList();
        _editingNotes = d.Notes.ToList();

        // Custom name
        string indexKey = planet.PlanetIndex.ToString(CultureInfo.InvariantCulture);
        string currentCustom = SystemNotesService.Get(planet.SystemAddress)?.PlanetNames.TryGetValue(indexKey, out var cn) == true ? cn : "";
        string orig = UniverseInfo.GeneratedPlanetName(planet.SystemAddress, planet.PlanetIndex);
        NameInGameBox.Text = currentCustom == orig ? "" : currentCustom;
        NameInGameBox.PlaceholderText = orig;

        // ComboBoxes
        List<string> Words(string group) => _planetWords.Where(w => w.Group == group)
            .GroupBy(w => w.Word, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(w => w.Word, StringComparer.CurrentCultureIgnoreCase).Select(w => w.Word).ToList();

        SentinelsPicker.ItemsSource = Words("Sentinels");
        SentinelsPicker.Text = d.Sentinels;

        FloraPicker.ItemsSource = Words("Life");
        FloraPicker.Text = d.Flora;

        FaunaPicker.ItemsSource = Words("Life");
        FaunaPicker.Text = d.Fauna;

        WaterPicker.ItemsSource = new[] { "Not recorded", "Yes", "No" };
        WaterPicker.SelectedIndex = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex) switch { true => 1, false => 2, _ => 0 };

        var weatherWords = _planetWords.Where(w => w.Group == "Weather").Select(w => w.Word)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(w => w, StringComparer.CurrentCultureIgnoreCase).ToList();
        WeatherSuggestBox.Text = d.Weather;
        WeatherSuggestBox.TextChanged += (s, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                s.ItemsSource = string.IsNullOrWhiteSpace(s.Text) ? null
                    : weatherWords.Where(w => w.Contains(s.Text, StringComparison.OrdinalIgnoreCase)).Take(15).ToList();
            }
        };

        // Resources chips
        RenderEditorResourceChips();
        ResourceAddBox.TextChanged += (s, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                s.ItemsSource = string.IsNullOrWhiteSpace(s.Text) ? null
                    : _substances.Where(sub => sub.Contains(s.Text, StringComparison.OrdinalIgnoreCase)).Take(15).ToList();
            }
        };
        ResourceAddBox.QuerySubmitted += (s, args) =>
        {
            string chosen = args.ChosenSuggestion as string ?? args.QueryText;
            if (!string.IsNullOrWhiteSpace(chosen))
            {
                chosen = chosen.Trim();
                if (!_editingResources.Contains(chosen, StringComparer.OrdinalIgnoreCase))
                {
                    _editingResources.Add(chosen);
                    RenderEditorResourceChips();
                }
                s.Text = "";
                s.ItemsSource = null;
            }
        };

        // Notes
        RenderEditorNotes();
    }

    private void RenderEditorResourceChips()
    {
        EditorResourceChips.Children.Clear();
        foreach (var r in _editingResources.ToList())
        {
            var chip = new Button
            {
                Content = $"{r}  ✕",
                Padding = new Thickness(8, 2, 8, 2),
                CornerRadius = ThemeResources.CardCornerRadius,
                FontSize = 12
            };
            chip.Click += (_, _) =>
            {
                _editingResources.Remove(r);
                RenderEditorResourceChips();
            };
            EditorResourceChips.Children.Add(chip);
        }
    }

    private void RenderEditorNotes()
    {
        EditorNotesList.Children.Clear();
        foreach (var note in _editingNotes.ToList())
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var noteTxt = new TextBlock
            {
                Text = $"• {note}",
                Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
                VerticalAlignment = VerticalAlignment.Center
            };
            var removeBtn = new Button
            {
                Content = "✕",
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = ThemeResources.CardCornerRadius,
                FontSize = 11
            };
            removeBtn.Click += (_, _) =>
            {
                _editingNotes.Remove(note);
                RenderEditorNotes();
            };
            Grid.SetColumn(noteTxt, 0);
            Grid.SetColumn(removeBtn, 1);
            row.Children.Add(noteTxt);
            row.Children.Add(removeBtn);
            EditorNotesList.Children.Add(row);
        }
    }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        string text = NewNoteInput.Text.Trim();
        if (text.Length > 0)
        {
            _editingNotes.Add(text);
            NewNoteInput.Text = "";
            RenderEditorNotes();
        }
    }

    private void EditDetailsToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        bool open = DetailsEditorPanel.Visibility == Visibility.Collapsed;
        DetailsEditorPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        DetailsSummaryPanel.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        EditDetailsToggleBtn.Content = open ? "Close editor" : "Edit details";
    }

    private void SaveDetails_Click(object sender, RoutedEventArgs e)
    {
        if (_planet is not KnownPlanet planet) return;

        var d = new PlanetDetails
        {
            Weather = WeatherSuggestBox.Text?.Trim() ?? "",
            Sentinels = SentinelsPicker.Text?.Trim() ?? "",
            Flora = FloraPicker.Text?.Trim() ?? "",
            Fauna = FaunaPicker.Text?.Trim() ?? "",
            Resources = _editingResources.ToList(),
            Notes = _editingNotes.ToList()
        };

        PlanetDetailsService.Save(planet.SystemAddress, planet.PlanetIndex, d);

        bool? water = WaterPicker.SelectedIndex switch { 1 => true, 2 => false, _ => null };
        PlanetWaterService.Set(planet.SystemAddress, planet.PlanetIndex, water);

        // Rename in system notes
        string typedName = NameInGameBox.Text?.Trim() ?? "";
        string orig = UniverseInfo.GeneratedPlanetName(planet.SystemAddress, planet.PlanetIndex);
        var note = SystemNotesService.Get(planet.SystemAddress) ?? new SystemNote();
        string key = planet.PlanetIndex.ToString(CultureInfo.InvariantCulture);
        if (typedName.Length > 0 && !string.Equals(typedName, orig, StringComparison.OrdinalIgnoreCase))
            note.PlanetNames[key] = typedName;
        else
            note.PlanetNames.Remove(key);
        SystemNotesService.Save(planet.SystemAddress, note);

        SaveStatusTxt.Text = $"Saved at {DateTime.Now:t}";

        // Refresh UI
        RenderDetailsSummary(planet, d);
        _ = LoadPlanetAsync(planet);
    }

    private void RenderSaveFacts(KnownPlanet planet)
    {
        SaveFactsContainer.Children.Clear();
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;
        void AddFact(string label, string val)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, FontSize = 12, Foreground = (Brush)Application.Current.Resources["ThemeTextDimBrush"] };
            var v = new TextBlock
            {
                Text = !string.IsNullOrEmpty(val) ? val : "—",
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["ThemeTextBrush"],
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            Grid.SetRow(l, row);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            grid.Children.Add(l);
            grid.Children.Add(v);
            row++;
        }

        AddFact("Discovered by", planet.DiscoveredHere ? "You" : (string.IsNullOrEmpty(planet.Discoverer) ? "Unrecorded" : planet.Discoverer));
        DateTime date = DateTimeOffset.FromUnixTimeSeconds(planet.DiscoveredUnixSeconds).LocalDateTime;
        AddFact("Discovered on", planet.DiscoveredUnixSeconds > 0 ? date.ToString("d MMMM yyyy", CultureInfo.CurrentCulture) : "Not recorded");
        AddFact("Portal code", planet.PortalCode);
        AddFact("System Address", $"0x{planet.SystemAddress:X16}");

        SaveFactsContainer.Children.Add(grid);
    }

    private void RenderCreatures(KnownPlanet planet)
    {
        var creatures = CreatureListService.On(planet.SystemAddress, planet.PlanetIndex);
        if (creatures.Count == 0)
        {
            CreaturesCard.Visibility = Visibility.Collapsed;
            return;
        }

        CreaturesCard.Visibility = Visibility.Visible;
        CreaturesWrapPanel.Children.Clear();

        foreach (var c in creatures)
        {
            var tile = new StackPanel { Width = 240, Spacing = 3 };
            string path = CreatureListService.PicturePath(planet.SystemAddress, planet.PlanetIndex, c.Name);
            if (c.Picture && File.Exists(path))
            {
                var image = new BitmapImage();
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                image.SetSource(stream.AsRandomAccessStream());
                tile.Children.Add(new Border
                {
                    CornerRadius = ThemeResources.CardCornerRadius,
                    Height = 130,
                    Margin = new Thickness(0, 0, 0, 4),
                    Child = new Image { Source = image, Stretch = Stretch.UniformToFill }
                });
            }
            else
            {
                tile.Children.Add(new Border
                {
                    CornerRadius = ThemeResources.CardCornerRadius,
                    Height = 130,
                    Margin = new Thickness(0, 0, 0, 4),
                    Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = c.Rarity == "Extinct" ? "Extinct — once lived here" : "No picture captured",
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["ThemeTextFaintBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });
            }

            tile.Children.Add(new TextBlock
            {
                Text = c.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["ThemeTextBrush"],
                IsTextSelectionEnabled = true
            });

            string traits = string.Join(" · ", new[] { c.Rarity, c.Habitat, c.Activity }.Where(s => s.Length > 0));
            if (traits.Length > 0)
                tile.Children.Add(new TextBlock { Text = traits, Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 11 });

            string bio = string.Join(" · ", new[] { c.Gender, c.Size }.Where(s => s.Length > 0));
            if (bio.Length > 0)
                tile.Children.Add(new TextBlock { Text = bio, Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 11 });

            var borderWrapper = new Border
            {
                Child = tile,
                Padding = new Thickness(10),
                CornerRadius = ThemeResources.CardCornerRadius,
                Background = (Brush)Application.Current.Resources["ThemeVoidBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                BorderThickness = new Thickness(1)
            };
            CreaturesWrapPanel.Children.Add(borderWrapper);
        }
    }

    private void RenderFish(KnownPlanet planet)
    {
        var caughtHere = FishCatchService.CaughtOn(planet.SystemAddress, planet.PlanetIndex);
        if (caughtHere.Count == 0)
        {
            FishCard.Visibility = Visibility.Collapsed;
            return;
        }

        FishCard.Visibility = Visibility.Visible;
        FishContainerPanel.Children.Clear();

        var spots = FishCatchService.SpotsOn(planet.SystemAddress, planet.PlanetIndex);
        string FormatFishName(string id)
        {
            string name = CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } shown ? shown : id;
            return name == name.ToUpperInvariant() ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant()) : name;
        }

        var ordered = caughtHere.OrderByDescending(c => c.Value).ThenBy(c => FormatFishName(c.Key)).ToList();

        foreach (var c in ordered)
        {
            var row = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string displayName = FormatFishName(c.Key);
            string countText = c.Value > 1 ? $" ×{c.Value}" : "";

            var textStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
            textStack.Children.Add(new TextBlock
            {
                Text = $"{displayName}{countText}",
                Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
                FontWeight = FontWeights.SemiBold
            });

            if (spots.TryGetValue(c.Key, out var coords) && coords.Count > 0)
            {
                var shown = coords.Select(a => NmsPlanetCoordinates.Format(a.Latitude, a.Longitude)).Distinct().ToList();
                string where = string.Join("  ·  ", shown.Take(3)) + (shown.Count > 3 ? $"  (+{shown.Count - 3} more)" : "");
                textStack.Children.Add(new TextBlock
                {
                    Text = $"Caught at {where}",
                    Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                    FontSize = 11
                });
            }

            Grid.SetColumn(textStack, 0);
            row.Children.Add(textStack);

            FishContainerPanel.Children.Add(row);
        }
    }

    private void RenderBases(KnownPlanet planet)
    {
        BasesListPanel.Children.Clear();
        string At(JObject b) => NmsPlanetCoordinates.OfBase(b) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "";

        var bases = (_snapshot?.Bases ?? new JArray()).OfType<JObject>()
            .Where(b => S.ParsePackedAddress(b["oZw"]) == planet.Address)
            .ToList();

        if (bases.Count == 0)
        {
            BasesListPanel.Children.Add(new TextBlock
            {
                Text = "No bases or outposts registered on this planet.",
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                FontSize = 13
            });
            return;
        }

        foreach (var b in bases)
        {
            string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base";
            string type = b["peI"]?["DPp"]?.Value<string>() == "HomePlanetBase" ? "Your Base" : "Other Player Base";

            var card = new Border
            {
                Padding = new Thickness(14, 10, 14, 10),
                Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = ThemeResources.CardCornerRadius
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new Border
            {
                Width = 34,
                Height = 34,
                Background = (Brush)Application.Current.Resources["ThemeVoidBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = ThemeResources.CardCornerRadius,
                Margin = new Thickness(0, 0, 12, 0),
                Child = new FontIcon { Glyph = "\uEB49", FontSize = 16, Foreground = (Brush)Application.Current.Resources["ThemeAccentBrush"] }
            };
            var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            st.Children.Add(new TextBlock
            {
                Text = $"{name}{At(b)}",
                FontFamily = (FontFamily)Application.Current.Resources["ThemeDisplayFont"],
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)Application.Current.Resources["ThemeTextBrush"]
            });
            st.Children.Add(new TextBlock { Text = type, Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 12 });

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(st, 1);
            grid.Children.Add(icon);
            grid.Children.Add(st);
            card.Child = grid;
            BasesListPanel.Children.Add(card);
        }
    }

    private void RenderFinds(KnownPlanet planet)
    {
        var finds = FindsService.All.Where(f => f.PortalCode == planet.PortalCode && f.Galaxy == planet.Galaxy).ToList();
        if (finds.Count == 0)
        {
            FindsCard.Visibility = Visibility.Collapsed;
            return;
        }

        FindsCard.Visibility = Visibility.Visible;
        FindsListPanel.Children.Clear();

        foreach (var f in finds)
        {
            string desc = string.Join(" · ", new[] { f.Type, f.Title, f.Class, f.Location }.Where(s => s.Length > 0));
            FindsListPanel.Children.Add(new TextBlock
            {
                Text = desc,
                Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
                FontSize = 13,
                IsTextSelectionEnabled = true
            });
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.Frame.CanGoBack)
        {
            this.Frame.GoBack();
        }
        else
        {
            this.Frame.Navigate(typeof(PlanetsPage));
        }
    }

    private void PrevPlanetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_planet is null) return;
        int at = _siblings.FindIndex(p => p.PlanetIndex == _planet.PlanetIndex);
        if (at > 0)
        {
            _ = LoadPlanetAsync(_siblings[at - 1]);
        }
    }

    private void NextPlanetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_planet is null) return;
        int at = _siblings.FindIndex(p => p.PlanetIndex == _planet.PlanetIndex);
        if (at >= 0 && at < _siblings.Count - 1)
        {
            _ = LoadPlanetAsync(_siblings[at + 1]);
        }
    }

    private async void ExportDataButton_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog exportDialog = new ContentDialog
        {
            Title = "Telemetry Exported",
            Content = $"Successfully saved survey data for {PlanetTitleTxt.Text} to your workspace exports.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };

        await exportDialog.ShowAsync();
    }
}
