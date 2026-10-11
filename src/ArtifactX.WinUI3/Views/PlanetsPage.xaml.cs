using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Read-only planet browser: the system you're saved in (drawn as a small
/// animated orbit view) and a finder over every planet in the save's
/// discovery list, each with its portal address. All data comes from
/// NmsPlanets - see its doc comment for what the save does and doesn't store.
/// </summary>
public sealed partial class PlanetsPage : Page
{
    private static Task? _glyphWarmup;

    private List<KnownPlanet> _allPlanets = new();
    private readonly List<Storyboard> _orbitAnimations = new();
    private bool _suppressFilterEvents;

    public PlanetsPage()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        Unloaded += Page_Unloaded;
        _ = LoadPageAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadPageAsync());

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        StopOrbitAnimations();
    }

    private static Brush ThemeBrush(string key) => ThemeResources.Brush(key);

    private async Task LoadPageAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        _glyphWarmup ??= CatalogService.WarmCacheAsync(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));
        await _glyphWarmup;

        var discoveries = SaveSessionManager.GetValue(S.DiscoveriesPath) as JArray;
        _allPlanets = NmsPlanets.ReadAll(discoveries);
        var counts = NmsPlanets.CountDiscoveries(discoveries);

        BuildCurrentSystem(counts);
        BuildFilterChoices();
        ApplyFilters();
    }

    // ---------------------------------------------------------------- current system

    private void BuildCurrentSystem(Dictionary<ulong, PlanetDiscoveryCounts> counts)
    {
        var universeAddress = SaveSessionManager.GetValue(S.CurrentUniverseAddressPath);
        ulong? systemAddress = S.ReadSystemAddress(universeAddress);
        int currentPlanet = universeAddress?["oZw"]?["jsv"]?.Value<int>() ?? 0;

        CurrentPlanetsPanel.Children.Clear();
        StopOrbitAnimations();
        SystemCanvas.Children.Clear();

        if (systemAddress is not ulong system)
        {
            SystemHeadingTxt.Text = "Unknown location";
            SystemSubTxt.Text = "";
            NoSystemPlanetsTxt.Visibility = Visibility.Visible;
            return;
        }

        int galaxy = S.GalaxyNumber(system);
        SystemHeadingTxt.Text = $"{NmsGalaxyNames.ForNumber(galaxy) ?? "Galaxy"} · Galaxy {galaxy}";

        var planets = _allPlanets.Where(p => p.SystemAddress == system).OrderBy(p => p.PlanetIndex).ToList();
        SystemSubTxt.Text = (planets.Count, currentPlanet) switch
        {
            (0, _) => "No planets discovered here yet",
            (_, 0) => $"{planets.Count} known planet{(planets.Count == 1 ? "" : "s")} · you're in space",
            _ => $"{planets.Count} known planet{(planets.Count == 1 ? "" : "s")} · you're on planet {currentPlanet}",
        };

        string systemCode = S.PortalAddress(system, 0);
        SystemCodeTxt.Text = systemCode;
        CopySystemCodeBtn.Tag = systemCode;
        SystemGlyphsPanel.Children.Clear();
        foreach (var glyph in GlyphsFor(systemCode))
            SystemGlyphsPanel.Children.Add(GlyphTile(glyph, 28));

        DrawSystem(planets, currentPlanet);

        NoSystemPlanetsTxt.Visibility = planets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var planet in planets)
        {
            counts.TryGetValue(planet.Address, out var tally);
            CurrentPlanetsPanel.Children.Add(BuildPlanetTile(planet, tally, ReadPlanetStats(planet.Address), planet.PlanetIndex == currentPlanet));
        }
    }

    /// <summary>The star plus one orbit per known planet. Orbits turn slowly
    /// (outer planets slower); nothing animates when Windows' "Animation
    /// effects" setting is off.</summary>
    private void DrawSystem(List<KnownPlanet> planets, int currentPlanet)
    {
        const double centre = 180;
        bool animate = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;

        var glow = new Ellipse
        {
            Width = 90, Height = 90,
            Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop { Color = ColorHelper.FromArgb(150, 255, 214, 140), Offset = 0 },
                    new GradientStop { Color = ColorHelper.FromArgb(0, 255, 180, 90), Offset = 1 },
                },
            },
        };
        Place(glow, centre, centre);
        var star = new Ellipse { Width = 30, Height = 30, Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 236, 200)) };
        Place(star, centre, centre);

        for (int i = 0; i < planets.Count; i++)
        {
            var planet = planets[i];
            double radius = 42 + (i + 1) * (130.0 / Math.Max(planets.Count, 1));
            bool here = planet.PlanetIndex == currentPlanet;

            var orbit = new Ellipse
            {
                Width = radius * 2, Height = radius * 2,
                Stroke = ThemeBrush(here ? "ThemeAccentBrush" : "ThemeLineBrush"),
                StrokeThickness = here ? 1.4 : 1,
                Opacity = here ? 0.9 : 0.6,
            };
            Place(orbit, centre, centre);

            double size = here ? 22 : 16;
            var body = PlanetDrawing(planet, size, here);

            // The arm spins around the star; the body counter-spins so its ring keeps its tilt.
            var arm = new Grid { Width = radius * 2, Height = radius * 2 };
            body.HorizontalAlignment = HorizontalAlignment.Center;
            body.VerticalAlignment = VerticalAlignment.Top;
            body.Margin = new Thickness(0, -body.Height / 2, 0, 0);
            arm.Children.Add(body);
            Place(arm, centre, centre);

            double startAngle = (planet.PlanetIndex * 67 + (int)(planet.Address & 0x3F)) % 360;
            var armTurn = new RotateTransform { Angle = startAngle };
            arm.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            arm.RenderTransform = armTurn;
            var bodyTurn = new RotateTransform { Angle = -startAngle };
            body.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            body.RenderTransform = bodyTurn;

            if (!animate) continue;
            double seconds = 40 + i * 22;
            var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            storyboard.Children.Add(Spin(armTurn, startAngle, startAngle + 360, seconds));
            storyboard.Children.Add(Spin(bodyTurn, -startAngle, -startAngle - 360, seconds));
            storyboard.Begin();
            _orbitAnimations.Add(storyboard);
        }
    }

    private static DoubleAnimation Spin(RotateTransform target, double from, double to, double seconds)
    {
        var animation = new DoubleAnimation { From = from, To = to, Duration = TimeSpan.FromSeconds(seconds) };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Angle");
        return animation;
    }

    private void StopOrbitAnimations()
    {
        foreach (var storyboard in _orbitAnimations) storyboard.Stop();
        _orbitAnimations.Clear();
    }

    private void Place(FrameworkElement element, double centreX, double centreY)
    {
        Canvas.SetLeft(element, centreX - element.Width / 2);
        Canvas.SetTop(element, centreY - element.Height / 2);
        SystemCanvas.Children.Add(element);
    }

    /// <summary>A planet circle in its type colour, with a tilted ring when it
    /// has one and an accent outline on the planet you're on.</summary>
    private static Grid PlanetDrawing(KnownPlanet planet, double size, bool highlight)
    {
        var grid = new Grid { Width = size * 2, Height = size };
        grid.Children.Add(new Ellipse
        {
            Width = size, Height = size,
            Fill = new SolidColorBrush(ParseColour(NmsPlanets.TypeColour(planet.BiomeIndex))),
            Stroke = highlight ? ThemeBrush("ThemeAccentBrush") : null,
            StrokeThickness = highlight ? 2 : 0,
        });
        if (planet.HasRings)
        {
            grid.Children.Add(new Ellipse
            {
                Width = size * 1.9, Height = size * 0.45,
                Stroke = ThemeBrush("ThemeTextDimBrush"), StrokeThickness = 1.5,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new RotateTransform { Angle = -18 },
            });
        }
        return grid;
    }

    private Border BuildPlanetTile(KnownPlanet planet, PlanetDiscoveryCounts? tally, Dictionary<string, double> stats, bool here)
    {
        var stack = new StackPanel { Spacing = 8 };

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var drawing = PlanetDrawing(planet, 30, here);
        drawing.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(drawing);
        var names = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock
        {
            Text = $"PLANET {planet.PlanetIndex}",
            Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"],
        });
        names.Children.Add(new TextBlock
        {
            Text = planet.HasRings ? $"{planet.TypeName} · rings" : planet.TypeName,
            Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
            FontWeight = FontWeights.SemiBold,
        });
        Grid.SetColumn(names, 1);
        header.Children.Add(names);
        stack.Children.Add(header);

        if (here)
        {
            stack.Children.Add(new Border
            {
                Background = ThemeBrush("ThemeAccentBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = "YOU ARE HERE", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = ThemeBrush("ThemeOnAccentBrush") },
            });
        }

        var t = tally ?? new PlanetDiscoveryCounts(0, 0, 0);
        stack.Children.Add(DimLine($"Discovered: {t.Fauna} fauna · {t.Flora} flora · {t.Minerals} minerals"));

        // ^TIME / ^DIST_WALKED units assumed seconds / metres (Cholton: 199 and 85
        // after a few minutes on foot).
        var visit = new List<string>();
        if (stats.TryGetValue("^TIME", out double secondsHere) && secondsHere >= 1)
            visit.Add($"{FormatDuration(secondsHere)} here");
        if (stats.TryGetValue("^DIST_WALKED", out double walked) && walked >= 1)
            visit.Add($"{walked:N0} m walked");
        if (visit.Count > 0)
            stack.Children.Add(DimLine(string.Join(" · ", visit)));

        stack.Children.Add(DimLine($"Found by {(planet.DiscoveredHere ? "you" : planet.Discoverer)}"));

        var glyphRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var glyph in GlyphsFor(planet.PortalCode))
            glyphRow.Children.Add(GlyphTile(glyph, 20));
        var copy = new Button { Content = "Copy", Tag = planet.PortalCode, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        copy.Click += CopyCode_Click;
        ToolTipService.SetToolTip(copy, $"Copy portal address {planet.PortalCode}");
        glyphRow.Children.Add(copy);
        stack.Children.Add(glyphRow);

        return new Border
        {
            Width = 372,
            Padding = new Thickness(14, 12, 14, 12),
            Background = ThemeBrush("ThemePanelAltBrush"),
            BorderBrush = ThemeBrush(here ? "ThemeAccentBrush" : "ThemeLineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = ThemeResources.CardCornerRadius,
            Child = stack,
        };
    }

    private static TextBlock DimLine(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
        FontSize = 12,
    };

    /// <summary>Numeric ^PLANET_STATS values for one planet (int ">vs" or float "eoL").</summary>
    private static Dictionary<string, double> ReadPlanetStats(ulong planetAddress)
    {
        var result = new Dictionary<string, double>();
        if (SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) is not JArray groups) return result;

        var group = groups.OfType<JObject>().FirstOrDefault(g =>
            g[":rc"]?.Value<string>() == "^PLANET_STATS" && S.ParsePackedAddress(g["2Ak"]) == planetAddress);
        if (group?["gUR"] is not JArray stats) return result;

        foreach (var stat in stats.OfType<JObject>())
        {
            string? id = stat["b2n"]?.Value<string>();
            var value = stat[">MX"]?[">vs"] ?? stat[">MX"]?["eoL"];
            if (id is not null && value is not null && value.Type is JTokenType.Integer or JTokenType.Float)
                result[id] = value.Value<double>();
        }
        return result;
    }

    private static string FormatDuration(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
             : span.TotalMinutes >= 1 ? $"{span.Minutes}m {span.Seconds}s"
             : $"{span.Seconds}s";
    }

    // ---------------------------------------------------------------- finder

    private void BuildFilterChoices()
    {
        _suppressFilterEvents = true;

        TypeFilterBox.Items.Clear();
        TypeFilterBox.Items.Add(new ComboBoxItem { Content = $"All types ({_allPlanets.Count})", Tag = -1 });
        foreach (int type in NmsPlanets.SelectableTypes)
        {
            int count = _allPlanets.Count(p => p.BiomeIndex == type);
            if (count > 0)
                TypeFilterBox.Items.Add(new ComboBoxItem { Content = $"{NmsPlanets.TypeName(type)} ({count})", Tag = type });
        }
        TypeFilterBox.SelectedIndex = 0;

        GalaxyFilterBox.Items.Clear();
        GalaxyFilterBox.Items.Add(new ComboBoxItem { Content = "All galaxies", Tag = -1 });
        foreach (var galaxy in _allPlanets.GroupBy(p => p.Galaxy).OrderBy(g => g.Key))
            GalaxyFilterBox.Items.Add(new ComboBoxItem { Content = $"{GalaxyLabel(galaxy.Key)} ({galaxy.Count()})", Tag = galaxy.Key });
        GalaxyFilterBox.SelectedIndex = 0;

        _suppressFilterEvents = false;
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressFilterEvents) ApplyFilters();
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        int type = (TypeFilterBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? -1;
        int galaxy = (GalaxyFilterBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? -1;
        bool ringsOnly = RingsOnlyBox.IsChecked == true;
        bool mineOnly = MineOnlyBox.IsChecked == true;

        var matches = _allPlanets
            .Where(p => type < 0 || p.BiomeIndex == type)
            .Where(p => galaxy < 0 || p.Galaxy == galaxy)
            .Where(p => !ringsOnly || p.HasRings)
            .Where(p => !mineOnly || p.DiscoveredHere)
            .OrderByDescending(p => p.DiscoveredUnixSeconds)
            .Select(ToRow)
            .ToList();

        ResultsList.ItemsSource = matches;
        ResultCountTxt.Text = $"{matches.Count:N0} of {_allPlanets.Count:N0} planets";
    }

    private static PlanetRowViewModel ToRow(KnownPlanet planet) => new()
    {
        TypeLabel = planet.TypeName,
        Swatch = new SolidColorBrush(ParseColour(NmsPlanets.TypeColour(planet.BiomeIndex))),
        RingVisibility = planet.HasRings ? Visibility.Visible : Visibility.Collapsed,
        LocationLabel = $"{GalaxyLabel(planet.Galaxy)} · planet {planet.PlanetIndex}",
        PortalCode = planet.PortalCode,
        Glyphs = GlyphsFor(planet.PortalCode),
        DiscoveredLabel = $"{(planet.DiscoveredHere ? "You" : planet.Discoverer)} · {FormatDate(planet.DiscoveredUnixSeconds)}",
    };

    // ---------------------------------------------------------------- shared helpers

    private static string GalaxyLabel(int galaxy) =>
        NmsGalaxyNames.ForNumber(galaxy) is string name ? $"{name} ({galaxy})" : $"Galaxy {galaxy}";

    private static string FormatDate(long unixSeconds) =>
        unixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.ToString("d", CultureInfo.CurrentCulture) : "unknown date";

    /// <summary>Glyph images for a 12-digit portal code; empty if the catalog
    /// predates add-station-assets (the code text is always shown too).</summary>
    private static List<ImageSource> GlyphsFor(string code)
    {
        var images = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }

    private static Border GlyphTile(ImageSource glyph, double size) => new()
    {
        Width = size,
        Height = size,
        Background = ThemeBrush("ThemeVoidBrush"),
        Child = new Image { Source = glyph, Width = size - 4, Height = size - 4 },
    };

    private static Windows.UI.Color ParseColour(string hex)
    {
        uint rgb = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Windows.UI.Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    // ---------------------------------------------------------------- glyph decoder

    private void DecoderInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool ok = NmsPortalCode.TryParse(DecoderInputBox.Text, out string code, out string error);

        DecoderErrorTxt.Text = error;
        DecoderErrorTxt.Visibility = error.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        DecoderResultPanel.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        DecoderGlyphsPanel.Children.Clear();
        if (!ok) return;

        foreach (var glyph in GlyphsFor(code))
            DecoderGlyphsPanel.Children.Add(GlyphTile(glyph, 40));

        bool fromCoordinates = DecoderInputBox.Text.Contains(':');
        DecoderDetailTxt.Text = fromCoordinates
            ? $"Portal code {code} (planet 1 - coordinates don't include a planet)"
            : $"Portal code {code} · galactic coordinates {NmsPortalCode.ToCoordinates(code)} · planet {code[0]}";
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }
}
