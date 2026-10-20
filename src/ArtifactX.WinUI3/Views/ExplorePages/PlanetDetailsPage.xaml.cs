using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArtifactX.Core.NmsModels;
using ArtifactX.GameData;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views.ExplorePages;

public sealed partial class PlanetDetailsPage : Page
{
    private KnownPlanet? _planet;
    private PlanetSnapshot? _snapshot;
    private ulong _currentSystem;
    private int _currentPlanet;
    private List<KnownPlanet> _siblings = new();
    private Dictionary<ulong, PlanetDiscoveryCounts> _lastCounts = new();

    private List<(string Word, string Group, string Tier)> _planetWords = new();
    private List<string> _substances = new();
    private List<FishInfo> _fish = new();
    private Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, (uint Count, float Largest)> _caught = new(StringComparer.OrdinalIgnoreCase);

    private List<string> _editingResources = new();
    private List<string> _editingNotes = new();

    public PlanetDetailsPage()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        CreatureListService.Changed += OnCreaturesChanged;
        ScreenCaptureLogService.SystemPictureSaved += OnSystemPictureSaved;
        FishCatchRecorder.SnapshotSeen += OnSnapshotSeen;
        PlanetDetailsService.Captured += OnPlanetDetailsCaptured;
        PlanetDetailsService.Changed += OnPlanetDetailsChanged;
        PlanetWaterService.Changed += OnWaterChanged;
        SystemNotesService.Changed += OnSystemNotesChanged;
        SystemPageReader.Captured += OnSystemNotesChanged;
        BodyKindsService.Captured += OnSystemNotesChanged;
        FindsService.Changed += OnFindsChanged;

        if (e.Parameter is KnownPlanet planet)
        {
            await LoadPlanetAsync(planet);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        CreatureListService.Changed -= OnCreaturesChanged;
        ScreenCaptureLogService.SystemPictureSaved -= OnSystemPictureSaved;
        FishCatchRecorder.SnapshotSeen -= OnSnapshotSeen;
        PlanetDetailsService.Captured -= OnPlanetDetailsCaptured;
        PlanetDetailsService.Changed -= OnPlanetDetailsChanged;
        PlanetWaterService.Changed -= OnWaterChanged;
        SystemNotesService.Changed -= OnSystemNotesChanged;
        SystemPageReader.Captured -= OnSystemNotesChanged;
        BodyKindsService.Captured -= OnSystemNotesChanged;
        FindsService.Changed -= OnFindsChanged;
    }

    private void OnPlanetDetailsCaptured(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_planet is KnownPlanet open && (system == open.SystemAddress || system == 0) && (planet == open.PlanetIndex || planet == -1))
        {
            UpdateDetails(open);
        }
    });

    private void OnPlanetDetailsChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_planet is KnownPlanet open && (system == open.SystemAddress || system == 0) && (planet == open.PlanetIndex || planet == -1))
        {
            UpdateDetails(open);
        }
    });

    private void OnWaterChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_planet is KnownPlanet open && (system == open.SystemAddress || system == 0) && (planet == open.PlanetIndex || planet == -1))
        {
            UpdateDetails(open);
        }
    });

    private void OnSystemNotesChanged(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_planet is KnownPlanet open && (system == open.SystemAddress || system == 0))
        {
            UpdateHeaderTitles(open);
        }
    });

    private void OnFindsChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        if (_planet is KnownPlanet open)
        {
            RenderFinds(open);
        }
    });

    private void OnCreaturesChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_planet is KnownPlanet open && open.SystemAddress == system && open.PlanetIndex == planet)
        {
            RenderCreatures(open);
        }
    });

    private void OnSystemPictureSaved(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_planet is KnownPlanet open && open.SystemAddress == system)
        {
            LoadHeaderBackdrop(open.SystemAddress);
        }
    });

    private void OnSnapshotSeen(PlanetSnapshot snapshot) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_planet is null) return;
        _snapshot = snapshot;
        _currentSystem = S.ReadSystemAddress(snapshot.Universe) ?? 0;
        _currentPlanet = snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        _lastCounts = NmsPlanets.CountDiscoveries(snapshot.Discoveries);
        _caught = FishCatchService.ReadCaught(snapshot.Fishing);

        RenderSaveFacts(_planet);
        RenderFish(_planet);
        RenderBases(_planet);
        UpdateHereStatus(_planet);
    });

    private async Task LoadPlanetAsync(KnownPlanet planet)
    {
        _planet = planet;

        // Ensure catalog vocabulary and fish data
        try
        {
            var wordsTask = CatalogService.GetPlanetDescriptorsAsync();
            var substancesTask = CatalogService.GetSubstanceNamesAsync();
            var fishTask = CatalogService.GetFishAsync();
            var namesTask = CatalogService.GetItemNamesAsync();

            await Task.WhenAll(wordsTask, substancesTask, fishTask, namesTask);

            _planetWords = await wordsTask ?? new();
            _substances = ((await substancesTask) ?? new())
                .Concat(_planetWords.Where(w => w.Group == "Resource").Select(w => w.Word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

            _fish = ((await fishTask) ?? new()).Where(f => CatalogService.IsFishSpecies(f.Id) && !f.NeedsMission).ToList();
            _itemNames = await namesTask ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Catalog fallback
        }

        // Snapshot of current save
        _snapshot = LiveSaveWatcher.FromSession();
        _currentSystem = S.ReadSystemAddress(_snapshot.Universe) ?? 0;
        _currentPlanet = _snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        _lastCounts = NmsPlanets.CountDiscoveries(_snapshot.Discoveries);
        _caught = FishCatchService.ReadCaught(_snapshot.Fishing);

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

        // 2. Header Container: Titles & Drawing
        LoadHeaderBackdrop(planet.SystemAddress);
        PlanetDrawingFrame.Child = PlanetDrawingHelper.CreatePlanetDrawing(planet, 76, isHere);

        BiomeTxt.Text = planet.HasRings ? $"{planet.TypeName} with rings" : planet.TypeName;
        GalaxyTxt.Text = GalaxyLabel(planet.Galaxy);
        UpdateHeaderTitles(planet);

        UpdateHereStatus(planet);

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
                    Background = ThemeBrush("ThemeVoidBrush"),
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

        // 3. Top Container & Details Summary
        UpdateDetails(planet);

        // 5. Left Column: Save Facts
        RenderSaveFacts(planet);

        // 6. Left Column: Registered Bases
        RenderBases(planet);

        // 7. Left Column: Recorded Finds
        RenderFinds(planet);

        // 8. Right Column: Fishing Catches
        RenderFish(planet);

        // 9. Full Width Bottom: Creatures Discovery Catalog
        RenderCreatures(planet);
    }

    private void UpdateHeaderTitles(KnownPlanet planet)
    {
        bool isMoon = BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex);
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
        CoordsTxt.Text = UniverseInfo.Compact(planet.SystemAddress);
    }

    private void UpdateDetails(KnownPlanet planet)
    {
        var details = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex);
        bool? water = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex);

        WeatherValTxt.Text = !string.IsNullOrEmpty(details?.Weather) ? details.Weather : $"{planet.TypeName} (Not Recorded)";
        SentinelsValTxt.Text = !string.IsNullOrEmpty(details?.Sentinels) ? details.Sentinels : "Standard Activity";
        FloraValTxt.Text = !string.IsNullOrEmpty(details?.Flora) ? details.Flora : "Uncharted";
        FaunaValTxt.Text = !string.IsNullOrEmpty(details?.Fauna) ? details.Fauna : (details?.FaunaTotal is int ft ? $"{ft} Species" : "Uncharted");
        WaterValTxt.Text = water switch { true => "Yes", false => "No", _ => "Not Recorded" };

        TopResourcesWrapPanel.Children.Clear();
        var resList = details?.Resources ?? new List<string>();
        if (resList.Count == 0)
        {
            TopResourcesWrapPanel.Children.Add(DimLine("None recorded yet — edit details below or survey in-game"));
        }
        else
        {
            foreach (var r in resList)
            {
                var chip = new Border
                {
                    Background = ThemeBrush("ThemePanelBrush"),
                    BorderBrush = ThemeBrush("ThemeLineBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = ThemeResources.CardCornerRadius,
                    Padding = new Thickness(8, 3, 8, 3),
                    Child = new TextBlock
                    {
                        Text = r,
                        FontSize = 12,
                        Foreground = ThemeBrush("ThemeTextBrush"),
                        FontFamily = (FontFamily)Application.Current.Resources["ThemeDisplayFont"],
                        FontWeight = FontWeights.SemiBold
                    }
                };
                TopResourcesWrapPanel.Children.Add(chip);
            }
        }

        RenderDetailsSummary(planet, details);
        if (DetailsEditorPanel.Visibility != Visibility.Visible)
        {
            PrepareDetailsEditor(planet, details);
        }
        RenderFish(planet);
    }

    private void UpdateHereStatus(KnownPlanet planet)
    {
        bool isHere = _currentSystem == planet.SystemAddress && _currentPlanet == planet.PlanetIndex;
        if (isHere)
        {
            HereBadge.Visibility = Visibility.Visible;
            HereBadgeTxt.Text = _snapshot?.Coordinates is { } at
                ? $"◆  YOU ARE HERE · {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}"
                : "◆  YOU ARE HERE";
        }
        else
        {
            HereBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadHeaderBackdrop(ulong system)
    {
        if (ScreenCaptureLogService.SystemPicture(system) is string picturePath && File.Exists(picturePath))
        {
            var brush = new ImageBrush { Stretch = Stretch.UniformToFill, Opacity = 0.25 };
            HeaderBackdrop.Background = brush;
            _ = GameScreenshotCache.LoadFadedFileAsync(picturePath, 3.0).ContinueWith(t =>
            {
                if (t.Result is { } img) DispatcherQueue.TryEnqueue(() => brush.ImageSource = img);
            });
        }
        else
        {
            HeaderBackdrop.Background = null;
        }
    }

    // ---------------------------------------------------------------- Manual Details Editor & Summary

    private void RenderDetailsSummary(KnownPlanet planet, PlanetDetails? details)
    {
        DetailsSummaryPanel.Children.Clear();
        bool? water = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex);

        void Line(string label, string val)
        {
            if (string.IsNullOrEmpty(val)) return;
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = ThemeBrush("ThemeTextDimBrush")
            });
            var valTxt = new TextBlock
            {
                Text = val,
                FontSize = 13,
                Foreground = ThemeBrush("ThemeTextBrush"),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            Grid.SetColumn(valTxt, 1);
            row.Children.Add(valTxt);
            DetailsSummaryPanel.Children.Add(row);
        }

        Line("Weather", details?.Weather ?? "");
        Line("Sentinels", details?.Sentinels ?? "");
        Line("Flora", details?.Flora ?? "");
        Line("Fauna", details?.Fauna ?? "");
        Line("Water", water switch { true => "Yes", false => "No", _ => "" });
        Line("Resources", details is not null && details.Resources.Count > 0 ? string.Join(", ", details.Resources) : "");
        Line("Notes", details is not null && details.Notes.Count > 0 ? string.Join(" · ", details.Notes) : "");

        if (DetailsSummaryPanel.Children.Count == 0)
        {
            DetailsSummaryPanel.Children.Add(DimLine("Nothing recorded yet — click 'Edit details' to record weather, sentinels, resources or notes."));
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

    // ---------------------------------------------------------------- From Your Save

    private void RenderSaveFacts(KnownPlanet planet)
    {
        SaveFactsContainer.Children.Clear();
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;
        void AddFact(string label, string val)
        {
            if (string.IsNullOrEmpty(val)) return;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, FontSize = 12, Foreground = ThemeBrush("ThemeTextDimBrush") };
            var v = new TextBlock
            {
                Text = val,
                FontSize = 13,
                Foreground = ThemeBrush("ThemeTextBrush"),
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

        _lastCounts.TryGetValue(planet.Address, out var tally);
        bool inThisSystem = planet.SystemAddress == _currentSystem;
        AddFact("You found", inThisSystem || tally is not null ? DiscoveredText(tally, planet.Address) : "");
        AddFact("Visited", VisitText(ReadPlanetStats(planet.Address)));

        string At(JObject b) => NmsPlanetCoordinates.OfBase(b) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "";
        var mine = (_snapshot?.Bases ?? new JArray()).OfType<JObject>()
            .Where(b => b["peI"]?["DPp"]?.Value<string>() == "HomePlanetBase" && S.ParsePackedAddress(b["oZw"]) == planet.Address)
            .Select(b => $"{(b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base")}{At(b)}")
            .ToList();
        if (mine.Count > 0) AddFact(mine.Count == 1 ? "Your base" : "Your bases", string.Join(", ", mine));

        var others = (_snapshot?.Bases ?? new JArray()).OfType<JObject>()
            .Where(b => b["peI"]?["DPp"]?.Value<string>() == "ExternalPlanetBase" && S.ParsePackedAddress(b["oZw"]) == planet.Address)
            .Select(b => $"{(b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed")} ({b["3?K"]?["V?:"]?.Value<string>() ?? "another player"}){At(b)}")
            .ToList();
        if (others.Count > 0) AddFact("Others' bases", string.Join(", ", others));

        if (tally is { Waypoints: > 0 } w) AddFact("Waypoints", w.Waypoints.ToString(CultureInfo.CurrentCulture));
        AddFact("Found by", planet.DiscoveredHere ? "You" : (string.IsNullOrEmpty(planet.Discoverer) ? "Unrecorded" : planet.Discoverer));
        AddFact("Found on", planet.DiscoveredUnixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(planet.DiscoveredUnixSeconds).LocalDateTime.ToString("d MMMM yyyy", CultureInfo.CurrentCulture) : "");
        AddFact("Portal code", planet.PortalCode);
        AddFact("System address", $"0x{planet.SystemAddress:X16}");

        SaveFactsContainer.Children.Add(grid);
    }

    private static string DiscoveredText(PlanetDiscoveryCounts? tally, ulong address)
    {
        var t = tally ?? new PlanetDiscoveryCounts(0, 0, 0);
        int? total = PlanetDetailsService.Get(S.ToSystemAddress(address), (int)((address >> 52) & 0xF))?.FaunaTotal;
        string fauna = total switch { 0 => "no fauna", int n => $"{t.Fauna} / {n} fauna", _ => $"{t.Fauna} fauna" };
        return $"{fauna} · {t.Flora} flora · {t.Minerals} minerals";
    }

    private static string VisitText(Dictionary<string, double> stats)
    {
        var visit = new List<string>();
        if (stats.TryGetValue("^TIME", out double secondsHere) && secondsHere >= 1)
            visit.Add(FormatDuration(secondsHere));
        if (stats.TryGetValue("^DIST_WALKED", out double walked) && walked >= 1)
            visit.Add($"{walked:N0} m walked");
        return string.Join(" · ", visit);
    }

    private static string FormatDuration(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
             : span.TotalMinutes >= 1 ? $"{span.Minutes}m {span.Seconds}s"
             : $"{span.Seconds}s";
    }

    private Dictionary<string, double> ReadPlanetStats(ulong planetAddress)
    {
        var result = new Dictionary<string, double>();
        if (_snapshot?.StatGroups is not JArray groups) return result;

        var group = groups.OfType<JObject>().FirstOrDefault(g =>
            g[":rc"]?.Value<string>() == "^PLANET_STATS" && S.ParsePackedAddress(g["2Ak"]) == planetAddress);
        if (group is null) return result;

        if (group["j6C"] is JArray ints)
            foreach (var stat in ints.OfType<JObject>())
                if (stat["H1>"]?.Value<string>() is { } k && stat[">vs"]?.Value<double>() is { } v)
                    result[k] = v;

        if (group["1;0"] is JArray floats)
            foreach (var stat in floats.OfType<JObject>())
                if (stat["H1>"]?.Value<string>() is { } k && stat["eoL"]?.Value<double>() is { } v)
                    result[k] = v;

        return result;
    }

    // ---------------------------------------------------------------- Registered Bases

    private void RenderBases(KnownPlanet planet)
    {
        BasesListPanel.Children.Clear();
        string At(JObject b) => NmsPlanetCoordinates.OfBase(b) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "";

        var bases = (_snapshot?.Bases ?? new JArray()).OfType<JObject>()
            .Where(b => S.ParsePackedAddress(b["oZw"]) == planet.Address)
            .ToList();

        if (bases.Count == 0)
        {
            BasesListPanel.Children.Add(DimLine("No bases or outposts registered on this planet."));
            return;
        }

        foreach (var b in bases)
        {
            string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base";
            string type = b["peI"]?["DPp"]?.Value<string>() == "HomePlanetBase" ? "Your Base" : "Other Player Base";

            var card = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Background = ThemeBrush("ThemePanelAltBrush"),
                BorderBrush = ThemeBrush("ThemeLineBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = ThemeResources.CardCornerRadius
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new Border
            {
                Width = 32,
                Height = 32,
                Background = ThemeBrush("ThemeVoidBrush"),
                BorderBrush = ThemeBrush("ThemeLineBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = ThemeResources.CardCornerRadius,
                Margin = new Thickness(0, 0, 10, 0),
                Child = new FontIcon { Glyph = "\uEB49", FontSize = 14, Foreground = ThemeBrush("ThemeAccentBrush") }
            };
            var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            st.Children.Add(new TextBlock
            {
                Text = $"{name}{At(b)}",
                FontFamily = (FontFamily)Application.Current.Resources["ThemeDisplayFont"],
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = ThemeBrush("ThemeTextBrush")
            });
            st.Children.Add(new TextBlock { Text = type, Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 11 });

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(st, 1);
            grid.Children.Add(icon);
            grid.Children.Add(st);
            card.Child = grid;
            BasesListPanel.Children.Add(card);
        }
    }

    // ---------------------------------------------------------------- Recorded Finds

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

    // ---------------------------------------------------------------- Fishing Catches (Far Right Column)

    private void RenderFish(KnownPlanet planet)
    {
        FishContainerPanel.Children.Clear();
        if (_fish.Count == 0)
        {
            FishCard.Visibility = Visibility.Collapsed;
            return;
        }
        var caughtHere = FishCatchService.CaughtOn(planet.SystemAddress, planet.PlanetIndex);
        bool? water = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex);
        bool hasWater = water == true || caughtHere.Count > 0 || (water is null && NmsPlanets.AlwaysHasWater(planet.BiomeIndex));
        if (!hasWater)
        {
            FishCard.Visibility = Visibility.Collapsed;
            return;
        }

        FishCard.Visibility = Visibility.Visible;

        var own = _fish.Where(f => f.LivesOn(planet.BiomeIndex) && !f.Biomes.Contains(FishInfo.AnyPlanet)).ToList();
        var anywhere = _fish.Where(f => f.Biomes.Contains(FishInfo.AnyPlanet)).ToList();
        int Caught(List<FishInfo> fish) => fish.Count(f => _caught.ContainsKey(f.Id));

        if (own.Count > 0)
        {
            if (Caught(own) >= own.Count)
                FishContainerPanel.Children.Add(DimLine($"✓ All {own.Count} {planet.TypeName} fish caught (on any {planet.TypeName} planet)"));
            else
                FishContainerPanel.Children.Add(ProgressRow($"{planet.TypeName} fish - on any {planet.TypeName} planet", Caught(own), own.Count));
        }

        FishContainerPanel.Children.Add(DimLine(Caught(anywhere) >= anywhere.Count
            ? $"✓ All {anywhere.Count} fish found on any planet with water caught"
            : $"Plus {anywhere.Count} fish found on any planet with water - you've caught {Caught(anywhere)}."));

        int totalHere = caughtHere.Values.Sum();
        FishContainerPanel.Children.Add(new TextBlock
        {
            Text = totalHere == 0 ? "Caught here: none yet"
                : $"Caught here: {totalHere:N0} fish · {caughtHere.Count} kind{(caughtHere.Count == 1 ? "" : "s")}",
            FontSize = 13,
            Foreground = ThemeBrush("ThemeTextBrush"),
            FontWeight = FontWeights.SemiBold
        });

        var missingOwn = own.Where(f => !_caught.ContainsKey(f.Id)).Select(f => FishName(f.Id)).OrderBy(x => x).ToList();
        var missingAny = anywhere.Where(f => !_caught.ContainsKey(f.Id)).Select(f => FishName(f.Id)).OrderBy(x => x).ToList();
        int missing = missingOwn.Count + missingAny.Count;

        if (missing == 0 && caughtHere.Count == 0)
        {
            FishContainerPanel.Children.Add(DimLine("You've caught every fish you can find here."));
            return;
        }

        void Heading(string text) => FishContainerPanel.Children.Add(new TextBlock { Text = text, FontSize = 11, Foreground = ThemeBrush("ThemeTextDimBrush"), Margin = new Thickness(0, 6, 0, 0) });

        if (missingOwn.Count > 0)
        {
            Heading($"STILL TO CATCH - ONLY ON {planet.TypeName.ToUpperInvariant()} PLANETS ({missingOwn.Count})");
            FishContainerPanel.Children.Add(NameList(missingOwn));
        }
        if (missingAny.Count > 0)
        {
            Heading($"STILL TO CATCH - ANY PLANET WITH WATER ({missingAny.Count})");
            FishContainerPanel.Children.Add(NameList(missingAny));
        }
        if (caughtHere.Count > 0)
        {
            Heading($"CAUGHT ON THIS PLANET ({caughtHere.Count} KIND{(caughtHere.Count == 1 ? "" : "S")})");
            var spots = FishCatchService.SpotsOn(planet.SystemAddress, planet.PlanetIndex);
            string Name(KeyValuePair<string, int> c) => c.Value > 1 ? $"{FishName(c.Key)} ×{c.Value}" : FishName(c.Key);
            var ordered = caughtHere.OrderByDescending(c => c.Value).ThenBy(c => FishName(c.Key)).ToList();
            var located = ordered.Where(c => spots.ContainsKey(c.Key)).Select(c =>
            {
                var shown = spots[c.Key].Select(a => NmsPlanetCoordinates.Format(a.Latitude, a.Longitude)).Distinct().ToList();
                string where = string.Join("  ·  ", shown.Take(3)) + (shown.Count > 3 ? $"  (+{shown.Count - 3} more)" : "");
                return $"{Name(c)}  —  at {where}";
            }).ToList();
            var unlocated = ordered.Where(c => !spots.ContainsKey(c.Key)).Select(Name).ToList();

            const string Recorded = "Recorded by ArtifactX while it followed your game. Catches from before, or while it was closed, aren't known - the save doesn't record where a fish was caught.";
            if (located.Count > 0)
            {
                var list = NameList(Array.Empty<string>());
                list.Text = string.Join(Environment.NewLine, located);
                ToolTipService.SetToolTip(list, Recorded + " Coordinates are where you were when the game next saved - the numbers your ship's dashboard and scanner show.");
                FishContainerPanel.Children.Add(list);
            }
            if (unlocated.Count > 0)
            {
                if (located.Count > 0) Heading("CAUGHT HERE BEFORE LOCATIONS WERE KEPT");
                var list = NameList(unlocated);
                ToolTipService.SetToolTip(list, Recorded);
                FishContainerPanel.Children.Add(list);
            }
        }
    }

    private string FishName(string id)
    {
        string name = _itemNames.TryGetValue(id, out var n) ? n
            : CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } shown ? shown : id;
        return name == name.ToUpperInvariant() ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant()) : name;
    }

    private static StackPanel ProgressRow(string label, int done, int total)
    {
        var block = new StackPanel { Spacing = 2 };
        block.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = ThemeBrush("ThemeTextDimBrush") });
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new ProgressBar { Maximum = Math.Max(1, total), Value = done, VerticalAlignment = VerticalAlignment.Center });
        var count = new TextBlock { Text = $"{done} / {total} caught", FontSize = 13, Foreground = ThemeBrush("ThemeTextBrush") };
        Grid.SetColumn(count, 1);
        row.Children.Add(count);
        block.Children.Add(row);
        return block;
    }

    // ---------------------------------------------------------------- Creatures (Fauna Scans with images)

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
            var tile = new StackPanel { Width = 250, Spacing = 2 };
            string path = CreatureListService.PicturePath(planet.SystemAddress, planet.PlanetIndex, c.Name);
            if (c.Picture && File.Exists(path))
            {
                var image = new BitmapImage();
                using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                    image.SetSource(stream.AsRandomAccessStream());
                tile.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Height = 140,
                    Margin = new Thickness(0, 0, 0, 4),
                    Child = new Image { Source = image, Stretch = Stretch.UniformToFill }
                });
            }
            else
            {
                tile.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Height = 140,
                    Margin = new Thickness(0, 0, 0, 4),
                    Background = ThemeBrush("ThemePanelAltBrush"),
                    BorderBrush = ThemeBrush("ThemeLineBrush"),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = c.Rarity == "Extinct" ? "Extinct - once lived here" : "No picture yet - select it in the game's fauna list",
                        FontSize = 12,
                        Foreground = ThemeBrush("ThemeTextFaintBrush"),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(12, 0, 12, 0)
                    }
                });
            }

            var nameHeaderGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            nameHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameTxt = new TextBlock
            {
                Text = c.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeBrush("ThemeTextBrush"),
                IsTextSelectionEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameTxt, 0);
            nameHeaderGrid.Children.Add(nameTxt);

            var deleteBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE74D", FontSize = 12 },
                Padding = new Thickness(6, 4, 6, 4),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(deleteBtn, "Remove this creature record");
            string cName = c.Name;
            deleteBtn.Click += (s, args) =>
            {
                CreatureListService.Delete(planet.SystemAddress, planet.PlanetIndex, cName);
            };
            Grid.SetColumn(deleteBtn, 1);
            nameHeaderGrid.Children.Add(deleteBtn);

            tile.Children.Add(nameHeaderGrid);

            string traits = string.Join(" · ", new[] { c.Rarity, c.Habitat, c.Activity }.Where(s => s.Length > 0));
            if (traits.Length > 0) tile.Children.Add(DimLine(traits));

            string body = string.Join(" · ", new[] { c.Gender, c.Size }.Where(s => s.Length > 0));
            if (body.Length > 0) tile.Children.Add(DimLine(body));

            string? noPet = c.Rarity == "Extinct" ? "extinct"
                : c.Habitat == "Underwater" ? "lives underwater"
                : Regex.IsMatch(c.Habitat, @"Fly|Flight|Air|Sky", RegexOptions.IgnoreCase) ? "flies"
                : null;
            if (noPet is not null)
            {
                tile.Children.Add(new TextBlock
                {
                    Text = $"Can't be a companion - {noPet}",
                    FontSize = 11,
                    Foreground = ThemeBrush("ThemeTextFaintBrush"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            CreaturesWrapPanel.Children.Add(new Border
            {
                Child = tile,
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(6),
                Background = ThemeBrush("ThemeVoidBrush"),
                BorderBrush = ThemeBrush("ThemeLineBrush"),
                BorderThickness = new Thickness(1)
            });
        }
    }

    // ---------------------------------------------------------------- Navigation & Action Helpers

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

    private static Brush ThemeBrush(string resourceKey) => (Brush)Application.Current.Resources[resourceKey];

    private static TextBlock DimLine(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
        FontSize = 12
    };

    private static TextBlock NameList(IEnumerable<string> names) => new()
    {
        Text = string.Join(", ", names),
        FontSize = 12,
        Foreground = ThemeBrush("ThemeTextBrush"),
        TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true
    };

    private static string GalaxyLabel(int galaxy) =>
        NmsGalaxyNames.ForNumber(galaxy) is string name ? $"{name} ({galaxy})" : $"Galaxy {galaxy}";
}
