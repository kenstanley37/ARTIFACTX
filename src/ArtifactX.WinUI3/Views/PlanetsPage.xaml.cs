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
using System.IO;
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

    // _thisSave: the loaded save's planets. _otherSaves: every other slot in
    // the same folder (KnownPlanetsService). _allPlanets is what the finder
    // searches - the two merged (loaded save wins on duplicates) or just
    // _thisSave, depending on "Include my other save slots".
    private List<KnownPlanet> _thisSave = new();
    private List<KnownPlanet> _otherSaves = new();
    private List<KnownPlanet> _allPlanets = new();
    private ulong? _currentSystem;
    private bool _suppressNoteEvents;
    private const string NotRecorded = "Not recorded";
    private int _loadVersion;
    private readonly List<Storyboard> _orbitAnimations = new();
    private bool _suppressFilterEvents;

    // The save data the page is currently showing: from the loaded session at
    // first, then from the newest save file each time the game writes it.
    private PlanetSnapshot _snapshot = new(null, null, null);
    private Dictionary<ulong, PlanetDiscoveryCounts> _lastCounts = new();
    private Dictionary<int, List<string>> _baseNames = new();
    private List<string> _noteList = new();
    private int _currentPlanet;

    // Per planet tile in the current system, the two text lines that change as
    // you play - updated in place so the page doesn't redraw (no blink).
    private readonly Dictionary<ulong, (TextBlock Discovered, TextBlock Visit)> _tileText = new();

    private FileSystemWatcher? _saveWatcher;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _liveTimer;

    public PlanetsPage()
    {
        InitializeComponent();
        // Set here, not in XAML: a XAML SelectedIndex fires SelectionChanged
        // mid-InitializeComponent, before the checkboxes below it exist.
        _suppressFilterEvents = true;
        SortBox.SelectedIndex = 0;
        BodiesFilterBox.SelectedIndex = 0;
        _suppressFilterEvents = false;
        SetLiveHelpVisible(!AppSettingsService.GetFlag(LiveHelpHiddenKey));
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
        StopWatching();
    }

    // ---------------------------------------------------------------- live follow

    /// <summary>Watches the loaded slot's save files. The game writes a save in
    /// several steps, so changes are batched: the page reads the newest file
    /// 2.5s after the last write. Never touches SaveSessionManager's session, so
    /// unsaved edits on other pages are unaffected.</summary>
    private void StartWatching()
    {
        StopWatching();
        if (SaveSessionManager.ActivePlatformFolder is not string folder || !Directory.Exists(folder)) return;

        _liveTimer = DispatcherQueue.CreateTimer();
        _liveTimer.Interval = TimeSpan.FromSeconds(2.5);
        _liveTimer.IsRepeating = false;
        _liveTimer.Tick += (_, _) => _ = LiveRefreshAsync(retry: true);

        _saveWatcher = new FileSystemWatcher(folder, "save*.hg")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _saveWatcher.Changed += OnSaveFileWritten;
        _saveWatcher.Created += OnSaveFileWritten;
        _saveWatcher.Renamed += OnSaveFileWritten;
        _saveWatcher.EnableRaisingEvents = true;
    }

    private void StopWatching()
    {
        if (_saveWatcher is not null)
        {
            _saveWatcher.EnableRaisingEvents = false;
            _saveWatcher.Changed -= OnSaveFileWritten;
            _saveWatcher.Created -= OnSaveFileWritten;
            _saveWatcher.Renamed -= OnSaveFileWritten;
            _saveWatcher.Dispose();
            _saveWatcher = null;
        }
        _liveTimer?.Stop();
        _liveTimer = null;
    }

    private void OnSaveFileWritten(object sender, FileSystemEventArgs e)
    {
        var slot = SaveSessionManager.ActiveSlot;
        if (slot is null || !slot.Files.Any(f => string.Equals(f.FullPath, e.FullPath, StringComparison.OrdinalIgnoreCase))) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            _liveTimer?.Stop();
            _liveTimer?.Start();
        });
    }

    private async Task LiveRefreshAsync(bool retry)
    {
        if (SaveSessionManager.ActiveSlot is not { } slot) return;
        string newest = slot.Files.Select(f => f.FullPath).Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? "";
        if (newest.Length == 0) return;

        var snapshot = await LiveSaveReader.ReadAsync(newest);
        if (snapshot is null)
        {
            // Probably caught mid-write - try once more a moment later.
            if (retry) { await Task.Delay(3000); await LiveRefreshAsync(retry: false); }
            return;
        }

        ApplySnapshot(snapshot, live: true);
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
    }

    /// <summary>Shows a snapshot, redrawing only what changed: a new system
    /// rebuilds the Current System card (with a short fade), landing on another
    /// planet rebuilds its tiles, and new discoveries just update the numbers.</summary>
    private void ApplySnapshot(PlanetSnapshot snapshot, bool live)
    {
        ulong? newSystem = S.ReadSystemAddress(snapshot.Universe);
        int newPlanet = snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        var newPlanets = NmsPlanets.ReadAll(snapshot.Discoveries);

        bool systemChanged = !live || newSystem != _currentSystem;
        bool planetsChanged = newPlanets.Count != _thisSave.Count;
        bool layoutChanged = systemChanged || newPlanet != _currentPlanet
            || newPlanets.Count(p => p.SystemAddress == newSystem) != _thisSave.Count(p => p.SystemAddress == newSystem);

        if (live && systemChanged)
        {
            CommitPendingNote(); // keep anything typed for the old system
            SaveSystemNote();
        }

        _snapshot = snapshot;
        _thisSave = newPlanets;
        _currentSystem = newSystem;
        _currentPlanet = newPlanet;
        var counts = NmsPlanets.CountDiscoveries(snapshot.Discoveries);
        _lastCounts = counts;
        bool learned = LearnNames(snapshot);
        if (learned && !systemChanged) { layoutChanged = true; RefreshNameBox(); }

        if (layoutChanged)
        {
            BuildCurrentSystem(counts);
            if (systemChanged) LoadSystemNote();
            if (live && systemChanged) FadeInCurrentSystem();
        }
        else
        {
            UpdateTileText(counts);
        }

        if (!live || systemChanged || planetsChanged) RefreshFinder();
    }

    private static readonly System.Text.RegularExpressions.Regex InSystemRegex = new(@"^In the (.+) system$");
    private static readonly System.Text.RegularExpressions.Regex OnPlanetRegex = new(@"^On Planet \((.+)\)$");

    /// <summary>Records the name the save's "where you saved" line gives for
    /// where you are now: "In the X system" (system), "On Planet (X)" (the
    /// planet you're on) or "Aboard X" (the system's space station; "Aboard the
    /// Space Anomaly" etc. are skipped). Returns whether anything new was stored.</summary>
    private bool LearnNames(PlanetSnapshot snapshot)
    {
        if (_currentSystem is not ulong system) return false;
        string summary = (snapshot.Summary ?? "").Trim();
        var note = SystemNotesService.Get(system) ?? new SystemNote();
        bool changed = false;

        if (InSystemRegex.Match(summary) is { Success: true } inSystem && note.SystemName != inSystem.Groups[1].Value)
        {
            note.SystemName = inSystem.Groups[1].Value;
            changed = true;
        }
        else if (OnPlanetRegex.Match(summary) is { Success: true } onPlanet && _currentPlanet > 0)
        {
            string key = _currentPlanet.ToString(CultureInfo.InvariantCulture);
            if (!note.PlanetNames.TryGetValue(key, out var known) || known != onPlanet.Groups[1].Value)
            {
                note.PlanetNames[key] = onPlanet.Groups[1].Value;
                changed = true;
            }
        }
        else if (_currentPlanet == 0 && summary.StartsWith("Aboard ", StringComparison.Ordinal))
        {
            string station = summary["Aboard ".Length..].Trim();
            if (station.Length > 0 && !station.StartsWith("the ", StringComparison.OrdinalIgnoreCase) && note.StationName != station)
            {
                note.StationName = station;
                changed = true;
            }
        }

        if (changed) SystemNotesService.Save(system, note);
        return changed;
    }

    private void RefreshNameBox()
    {
        if (_currentSystem is not ulong system) return;
        _suppressNoteEvents = true;
        NoteSystemNameBox.Text = SystemNotesService.Get(system)?.SystemName ?? "";
        _suppressNoteEvents = false;
    }

    /// <summary>Named map markers in a system: its space station, and your
    /// bases by planet number.</summary>
    private (string Station, Dictionary<int, List<string>> Bases) ReadMarkers(ulong system)
    {
        string station = "";
        var bases = new Dictionary<int, List<string>>();
        if (_snapshot.Markers is not JArray markers) return (station, bases);

        foreach (var marker in markers.OfType<JObject>())
        {
            if (S.ReadSystemAddress(marker["yhJ"]) != system) continue;
            string name = marker["NKm"]?.Value<string>() ?? "";
            if (name.Length == 0) continue;
            switch (marker["iAF"]?.Value<string>())
            {
                case "Spacestation":
                    station = name;
                    break;
                case "Base":
                    int planet = marker["yhJ"]?["oZw"]?["jsv"]?.Value<int>() ?? 0;
                    if (!bases.TryGetValue(planet, out var list)) bases[planet] = list = new();
                    list.Add(name);
                    break;
            }
        }
        return (station, bases);
    }

    private void UpdateTileText(Dictionary<ulong, PlanetDiscoveryCounts> counts)
    {
        foreach (var (address, lines) in _tileText)
        {
            counts.TryGetValue(address, out var tally);
            string discovered = DiscoveredText(tally);
            string visit = VisitText(ReadPlanetStats(address));
            if (lines.Discovered.Text != discovered) lines.Discovered.Text = discovered;
            if (lines.Visit.Text != visit)
            {
                lines.Visit.Text = visit;
                lines.Visit.Visibility = visit.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private const string LiveHelpHiddenKey = "PlanetsLiveHelpHidden";

    private void SetLiveHelpVisible(bool visible)
    {
        LiveHelpPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ShowLiveHelpLink.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HideLiveHelp_Click(object sender, RoutedEventArgs e)
    {
        AppSettingsService.SetFlag(LiveHelpHiddenKey, true);
        SetLiveHelpVisible(false);
    }

    private void ShowLiveHelp_Click(object sender, RoutedEventArgs e)
    {
        AppSettingsService.SetFlag(LiveHelpHiddenKey, false);
        SetLiveHelpVisible(true);
    }

    private Storyboard? _overlayStoryboard;

    /// <summary>A bold banner over the Current System card for a few seconds
    /// after live follow sees a new system, so the switch can't be missed.</summary>
    private void ShowSystemChangeBanner()
    {
        SystemChangeDetailTxt.Text = $"{SystemHeadingTxt.Text} · {SystemSubTxt.Text}";
        SystemChangeOverlay.Visibility = Visibility.Visible;
        _overlayStoryboard?.Stop();

        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            SystemChangeOverlay.Opacity = 1;
            var hide = DispatcherQueue.CreateTimer();
            hide.Interval = TimeSpan.FromSeconds(4);
            hide.IsRepeating = false;
            hide.Tick += (_, _) => SystemChangeOverlay.Visibility = Visibility.Collapsed;
            hide.Start();
            return;
        }

        // Pop in, hold ~2.5s, then fade out over 1.5s.
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
        fade.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(250), Value = 1 });
        fade.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(2750), Value = 1 });
        fade.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(4250), Value = 0 });
        Storyboard.SetTarget(fade, SystemChangeOverlay);
        Storyboard.SetTargetProperty(fade, "Opacity");
        _overlayStoryboard = new Storyboard();
        _overlayStoryboard.Children.Add(fade);
        _overlayStoryboard.Completed += (_, _) => SystemChangeOverlay.Visibility = Visibility.Collapsed;
        _overlayStoryboard.Begin();
    }

    private void FadeInCurrentSystem()
    {
        ShowSystemChangeBanner();
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;
        var fade = new DoubleAnimation { From = 0.15, To = 1, Duration = TimeSpan.FromMilliseconds(450) };
        Storyboard.SetTarget(fade, CurrentSystemBody);
        Storyboard.SetTargetProperty(fade, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);
        storyboard.Begin();
    }

    private static Brush ThemeBrush(string key) => ThemeResources.Brush(key);

    private async Task LoadPageAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        _glyphWarmup ??= CatalogService.WarmCacheAsync(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));
        await _glyphWarmup;

        await EnsureChoicesAsync();
        int version = ++_loadVersion;
        _otherSaves = new();
        ApplySnapshot(new PlanetSnapshot(
            SaveSessionManager.GetValue(S.CurrentUniverseAddressPath),
            SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) as JArray,
            SaveSessionManager.GetValue(S.DiscoveriesPath) as JArray,
            SaveSessionManager.GetValue(S.LocationDescriptionPath)?.Value<string>(),
            SaveSessionManager.GetValue(S.MarkersPath) as JArray), live: false);
        StartWatching();
        await LoadOtherSavesAsync(version);
    }

    private async Task LoadOtherSavesAsync(int version)
    {
        if (AllSlotsBox.IsChecked != true || _otherSaves.Count > 0) return;
        if (SaveSessionManager.ActivePlatformFolder is not string folder || SaveSessionManager.ActiveSlot is not { } slot) return;

        FinderStatusTxt.Text = "Reading your other save slots...";
        FinderStatusTxt.Visibility = Visibility.Visible;
        var others = await KnownPlanetsService.ReadOtherSlotsAsync(folder, slot.SlotId);
        if (version != _loadVersion) return; // a newer load started meanwhile

        _otherSaves = others;
        FinderStatusTxt.Visibility = Visibility.Collapsed;
        RefreshFinder();
    }

    private async void AllSlotsBox_Click(object sender, RoutedEventArgs e)
    {
        RefreshFinder();
        await LoadOtherSavesAsync(_loadVersion);
    }

    private void RefreshFinder()
    {
        _allPlanets = AllSlotsBox.IsChecked == true
            ? _thisSave.Concat(_otherSaves).GroupBy(p => p.Address).Select(g => g.First()).ToList()
            : _thisSave;
        BuildFilterChoices();
        ApplyFilters();
    }

    // ---------------------------------------------------------------- current system

    private void BuildCurrentSystem(Dictionary<ulong, PlanetDiscoveryCounts> counts)
    {
        ulong? systemAddress = _currentSystem;
        int currentPlanet = _currentPlanet;

        CurrentPlanetsPanel.Children.Clear();
        _tileText.Clear();
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
        var systemNote = SystemNotesService.Get(system);
        var (markerStation, bases) = ReadMarkers(system);
        _baseNames = bases;
        SystemHeadingTxt.Text = systemNote?.SystemName is { Length: > 0 } systemName ? systemName : "Unnamed system";
        string station = markerStation.Length > 0 ? markerStation : systemNote?.StationName ?? "";
        SystemStationTxt.Text = $"Space station: {station}";
        SystemStationTxt.Visibility = station.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        var planets = _thisSave.Where(p => p.SystemAddress == system).OrderBy(p => p.PlanetIndex).ToList();
        var note = SystemNotesService.Get(system);
        int moonCount = planets.Count(p => note?.IsMoon(p.PlanetIndex) == true);
        string bodies = moonCount > 0
            ? $"{planets.Count - moonCount} planet{(planets.Count - moonCount == 1 ? "" : "s")} · {moonCount} moon{(moonCount == 1 ? "" : "s")}"
            : $"{planets.Count} known planet{(planets.Count == 1 ? "" : "s")}";
        string where = currentPlanet == 0 ? "you're in space"
            : note?.IsMoon(currentPlanet) == true ? "you're on a moon"
            : $"you're on planet {currentPlanet}";
        string galaxyText = $"{NmsGalaxyNames.ForNumber(galaxy) ?? "Galaxy"} ({galaxy})";
        SystemSubTxt.Text = planets.Count == 0 ? $"{galaxyText} · no planets discovered here yet" : $"{galaxyText} · {bodies} · {where}";
        SystemSubTxt.Text += $" · about {NmsPlanets.DistanceToCoreLightYears(system):N0} ly from the galaxy core";

        string systemCode = S.PortalAddress(system, 0);
        SystemCodeTxt.Text = systemCode;
        CopySystemCodeBtn.Tag = systemCode;
        SystemGlyphsPanel.Children.Clear();
        foreach (var glyph in GlyphsFor(systemCode))
            SystemGlyphsPanel.Children.Add(GlyphTile(glyph, 28));

        DrawSystem(planets, currentPlanet, note);

        NoSystemPlanetsTxt.Visibility = planets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var planet in planets)
        {
            counts.TryGetValue(planet.Address, out var tally);
            CurrentPlanetsPanel.Children.Add(BuildPlanetTile(planet, tally, ReadPlanetStats(planet.Address), planet.PlanetIndex == currentPlanet,
                note?.IsMoon(planet.PlanetIndex) == true));
        }
    }

    /// <summary>The star plus one orbit per known planet. Orbits turn slowly
    /// (outer planets slower); nothing animates when Windows' "Animation
    /// effects" setting is off.</summary>
    private void DrawSystem(List<KnownPlanet> allBodies, int currentPlanet, SystemNote? note)
    {
        // Moons orbit planet 1 (see SystemNote.IsMoon) rather than the star -
        // unless planet 1 isn't known yet, then they're drawn like planets.
        var hostPlanet = allBodies.FirstOrDefault(p => p.PlanetIndex == 1);
        var moons = hostPlanet is null ? new List<KnownPlanet>() : allBodies.Where(p => note?.IsMoon(p.PlanetIndex) == true).ToList();
        var planets = allBodies.Except(moons).ToList();
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
            FrameworkElement body = PlanetDrawing(planet, size, here);
            if (planet == hostPlanet && moons.Count > 0)
                body = WithMoons((Grid)body, moons, currentPlanet, animate);

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

    /// <summary>Planet 1's drawing with its moons circling it on small orbits.</summary>
    private Grid WithMoons(Grid planetDrawing, List<KnownPlanet> moons, int currentPlanet, bool animate)
    {
        double extent = 34 + moons.Count * 16;
        var system = new Grid { Width = extent, Height = extent };
        planetDrawing.HorizontalAlignment = HorizontalAlignment.Center;
        planetDrawing.VerticalAlignment = VerticalAlignment.Center;
        system.Children.Add(planetDrawing);

        for (int k = 0; k < moons.Count; k++)
        {
            var moon = moons[k];
            bool here = moon.PlanetIndex == currentPlanet;
            double orbitRadius = 15 + (k + 1) * 8;
            double size = here ? 9 : 7;

            var path = new Ellipse
            {
                Width = orbitRadius * 2, Height = orbitRadius * 2,
                Stroke = ThemeBrush("ThemeLineBrush"), StrokeThickness = 0.8, Opacity = 0.5,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            system.Children.Add(path);

            var arm = new Grid
            {
                Width = orbitRadius * 2, Height = orbitRadius * 2,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            };
            arm.Children.Add(new Ellipse
            {
                Width = size, Height = size,
                Fill = new SolidColorBrush(ParseColour(NmsPlanets.TypeColour(moon.BiomeIndex))),
                Stroke = here ? ThemeBrush("ThemeAccentBrush") : null,
                StrokeThickness = here ? 1.5 : 0,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -size / 2, 0, 0),
            });
            double start = (moon.PlanetIndex * 113) % 360;
            var turn = new RotateTransform { Angle = start };
            arm.RenderTransform = turn;
            system.Children.Add(arm);

            if (!animate) continue;
            var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            storyboard.Children.Add(Spin(turn, start, start + 360, 9 + k * 5));
            storyboard.Begin();
            _orbitAnimations.Add(storyboard);
        }
        return system;
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

    private Border BuildPlanetTile(KnownPlanet planet, PlanetDiscoveryCounts? tally, Dictionary<string, double> stats, bool here, bool isMoon)
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
            Text = isMoon ? "MOON" : $"PLANET {planet.PlanetIndex}",
            Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"],
        });
        string typeText = planet.HasRings ? $"{planet.TypeName} · rings" : planet.TypeName;
        string? planetName = null;
        SystemNotesService.Get(planet.SystemAddress)?.PlanetNames.TryGetValue(planet.PlanetIndex.ToString(CultureInfo.InvariantCulture), out planetName);
        names.Children.Add(new TextBlock
        {
            Text = planetName ?? typeText,
            Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
            FontWeight = FontWeights.SemiBold,
        });
        if (planetName is not null)
            names.Children.Add(DimLine(typeText));
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

        var discoveredLine = DimLine(DiscoveredText(tally));
        stack.Children.Add(discoveredLine);
        string visitText = VisitText(stats);
        var visitLine = DimLine(visitText);
        visitLine.Visibility = visitText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        stack.Children.Add(visitLine);
        _tileText[planet.Address] = (discoveredLine, visitLine);

        if (_baseNames.TryGetValue(planet.PlanetIndex, out var baseNames))
            stack.Children.Add(DimLine(baseNames.Count == 1 ? $"Your base: {baseNames[0]}" : $"Your bases: {string.Join(", ", baseNames)}"));
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

    private static string DiscoveredText(PlanetDiscoveryCounts? tally)
    {
        var t = tally ?? new PlanetDiscoveryCounts(0, 0, 0);
        return $"Discovered: {t.Fauna} fauna · {t.Flora} flora · {t.Minerals} minerals";
    }

    // ^TIME / ^DIST_WALKED units assumed seconds / metres (Cholton: 199 and 85
    // after a few minutes on foot).
    private static string VisitText(Dictionary<string, double> stats)
    {
        var visit = new List<string>();
        if (stats.TryGetValue("^TIME", out double secondsHere) && secondsHere >= 1)
            visit.Add($"{FormatDuration(secondsHere)} here");
        if (stats.TryGetValue("^DIST_WALKED", out double walked) && walked >= 1)
            visit.Add($"{walked:N0} m walked");
        return string.Join(" · ", visit);
    }

    private static TextBlock DimLine(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
        FontSize = 12,
    };

    /// <summary>Numeric ^PLANET_STATS values for one planet (int ">vs" or float "eoL").</summary>
    private Dictionary<string, double> ReadPlanetStats(ulong planetAddress)
    {
        var result = new Dictionary<string, double>();
        if (_snapshot.StatGroups is not JArray groups) return result;

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

    // ---------------------------------------------------------------- system notes

    private static Task<List<(string Word, string Group, string Tier)>>? _descriptorsLoad;
    private bool _choicesFilled;

    /// <summary>Fills the note and filter dropdowns once per page. Economy,
    /// wealth and conflict list the game's own words with their category, e.g.
    /// "Mathematical (Scientific)"; their filters list the categories.</summary>
    private async Task EnsureChoicesAsync()
    {
        if (_choicesFilled) return;
        _descriptorsLoad ??= CatalogService.GetSystemDescriptorsAsync();
        var descriptors = await _descriptorsLoad;
        SystemNotesService.SetDescriptors(descriptors);

        IEnumerable<(string Label, string Value)> Words(string group) => descriptors
            .Where(d => d.Group == group)
            .Select(d => (Label: $"{d.Word} ({SystemNotesService.TierName(group, d.Tier)})", Value: d.Word))
            .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase);
        IEnumerable<(string Label, string Value)> Categories(string group) => descriptors
            .Where(d => d.Group == group)
            .Select(d => SystemNotesService.TierName(group, d.Tier))
            .Distinct()
            .Select(c => (c, c));
        static IEnumerable<(string Label, string Value)> Plain(IEnumerable<string> values) => values.Select(v => (v, v));

        _suppressNoteEvents = true;
        FillChoices(NoteStarBox, NotRecorded, Plain(SystemNotesService.StarColours));
        FillChoices(NoteEconomyBox, NotRecorded, Words("Economy").Concat(Plain(SystemNotesService.EconomyExtras)));
        FillChoices(NoteWealthBox, NotRecorded, Words("Wealth"));
        FillChoices(NoteConflictBox, NotRecorded, Words("Conflict"));
        FillChoices(NoteRaceBox, NotRecorded, Plain(SystemNotesService.Races));
        FillChoices(NoteMoonsBox, NotRecorded, Plain(new[] { "0", "1", "2", "3" }));
        _suppressNoteEvents = false;

        _suppressFilterEvents = true;
        FillChoices(StarFilterBox, "Any", Plain(SystemNotesService.StarColours));
        FillChoices(EconomyFilterBox, "Any", Categories("Economy").Concat(Plain(SystemNotesService.EconomyExtras)));
        FillChoices(WealthFilterBox, "Any", Categories("Wealth"));
        FillChoices(ConflictFilterBox, "Any", Categories("Conflict"));
        FillChoices(RaceFilterBox, "Any", Plain(SystemNotesService.Races));
        _suppressFilterEvents = false;

        _choicesFilled = true;
    }

    private static void FillChoices(ComboBox box, string first, IEnumerable<(string Label, string Value)> options)
    {
        box.Items.Clear();
        box.Items.Add(new ComboBoxItem { Content = first, Tag = "" });
        foreach (var (label, value) in options)
            box.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        box.SelectedIndex = 0;
    }

    private static void SelectChoice(ComboBox box, string value) =>
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == value) ?? box.Items.FirstOrDefault();

    private static string ChosenValue(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    private void LoadSystemNote()
    {
        SystemNotesPanel.Visibility = _currentSystem is null ? Visibility.Collapsed : Visibility.Visible;
        var note = _currentSystem is ulong system ? SystemNotesService.Get(system) : null;

        _suppressNoteEvents = true;
        SelectChoice(NoteStarBox, note?.StarColour ?? "");
        SelectChoice(NoteEconomyBox, note?.Economy ?? "");
        SelectChoice(NoteWealthBox, note?.Wealth ?? "");
        SelectChoice(NoteConflictBox, note?.Conflict ?? "");
        SelectChoice(NoteRaceBox, note?.Race ?? "");
        SelectChoice(NoteMoonsBox, note?.Moons?.ToString(CultureInfo.InvariantCulture) ?? "");
        _noteList = note?.Notes.ToList() ?? new();
        NewNoteBox.Text = "";
        RenderNotes();
        NoteClassBox.Text = note?.StarClass ?? "";
        NoteSystemNameBox.Text = note?.SystemName ?? "";
        NoteWaterBox.IsChecked = note?.Water == true;
        NoteDissonantBox.IsChecked = note?.Dissonant == true;
        _suppressNoteEvents = false;
    }

    private void SaveSystemNote()
    {
        if (_suppressNoteEvents || _currentSystem is not ulong system) return;

        var existing = SystemNotesService.Get(system);
        SystemNotesService.Save(system, new SystemNote
        {
            SystemName = NoteSystemNameBox.Text.Trim(),
            StationName = existing?.StationName ?? "",
            PlanetNames = existing?.PlanetNames ?? new(),
            StarColour = ChosenValue(NoteStarBox),
            Economy = ChosenValue(NoteEconomyBox),
            Wealth = ChosenValue(NoteWealthBox),
            Conflict = ChosenValue(NoteConflictBox),
            Race = ChosenValue(NoteRaceBox),
            Moons = int.TryParse(ChosenValue(NoteMoonsBox), out int moons) ? moons : null,
            Notes = _noteList.ToList(),
            StarClass = NoteClassBox.Text.Trim(),
            Water = NoteWaterBox.IsChecked == true,
            Dissonant = NoteDissonantBox.IsChecked == true,
        });
        ApplyFilters();
    }

    private void NoteField_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressNoteEvents) return;
        SaveSystemNote();
        if (sender == NoteMoonsBox) BuildCurrentSystem(_lastCounts); // moons change the drawing and tiles
    }

    // ---- notes list

    private void RenderNotes()
    {
        NotesListPanel.Children.Clear();
        for (int i = 0; i < _noteList.Count; i++)
        {
            int index = i;
            var row = new Grid { ColumnSpacing = 10, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock { Text = "•", Foreground = ThemeBrush("ThemeAccentBrush"), VerticalAlignment = VerticalAlignment.Top });
            var text = new TextBlock
            {
                Text = _noteList[i],
                Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
                FontSize = 13,
                IsTextSelectionEnabled = true,
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var remove = new Button { Content = "Remove", Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Top };
            remove.Click += (_, _) =>
            {
                _noteList.RemoveAt(index);
                RenderNotes();
                SaveSystemNote();
            };
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);

            NotesListPanel.Children.Add(row);
        }
    }

    /// <summary>Adds whatever is typed in the new-note box; returns whether it added one.</summary>
    private bool CommitPendingNote()
    {
        string text = NewNoteBox.Text.Trim();
        if (text.Length == 0) return false;
        _noteList.Add(text);
        NewNoteBox.Text = "";
        RenderNotes();
        return true;
    }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        if (CommitPendingNote()) SaveSystemNote();
        NewNoteBox.Focus(FocusState.Programmatic);
    }

    private void NewNoteBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        if (CommitPendingNote()) SaveSystemNote();
    }

    private void NoteTextBox_LostFocus(object sender, RoutedEventArgs e) => SaveSystemNote();

    private void NoteCheck_Click(object sender, RoutedEventArgs e) => SaveSystemNote();

    /// <summary>Typing a star class fills in its colour (the colour change saves the note).</summary>
    private void NoteClassBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressNoteEvents) return;
        if (SystemNotesService.ColourForClass(NoteClassBox.Text) is string colour && ChosenValue(NoteStarBox) != colour)
            SelectChoice(NoteStarBox, colour);
    }

    /// <summary>"Opulent (High ★★★ wealth)" - the game's word plus its category.</summary>
    private static string Described(string group, string word, string suffix)
    {
        string category = SystemNotesService.CategoryOf(group, word);
        string label = suffix.Length > 0 ? $"{category} {suffix}" : category;
        return category == word ? word : $"{word} ({label})";
    }

    /// <summary>"Yellow star · Mathematical (Scientific) · Opulent (High ★★★ wealth) · ...", or "".</summary>
    private static string NoteSummary(SystemNote? note)
    {
        if (note is null) return "";
        var parts = new List<string>();
        if (note.StarColour != "") parts.Add(note.StarClass != "" ? $"{note.StarColour} star ({note.StarClass})" : $"{note.StarColour} star");
        else if (note.StarClass != "") parts.Add($"Class {note.StarClass}");
        if (note.Economy != "") parts.Add(Described("Economy", note.Economy, ""));
        if (note.Wealth != "") parts.Add(Described("Wealth", note.Wealth, "wealth"));
        if (note.Conflict != "") parts.Add(Described("Conflict", note.Conflict, "conflict"));
        if (note.Race != "") parts.Add(note.Race);
        if (note.Water) parts.Add("Water");
        if (note.Dissonant) parts.Add("Dissonant");
        if (note.Moons is int moons) parts.Add(moons == 1 ? "1 moon" : $"{moons} moons");
        parts.AddRange(note.Notes);
        return string.Join(" · ", parts);
    }

    // ---------------------------------------------------------------- finder

    private void BuildFilterChoices()
    {
        _suppressFilterEvents = true;
        int previousType = (TypeFilterBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? -1;
        int previousGalaxy = (GalaxyFilterBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? -1;

        TypeFilterBox.Items.Clear();
        TypeFilterBox.Items.Add(new ComboBoxItem { Content = $"All types ({_allPlanets.Count})", Tag = -1 });
        foreach (int type in NmsPlanets.SelectableTypes)
        {
            int count = _allPlanets.Count(p => p.BiomeIndex == type);
            if (count > 0)
                TypeFilterBox.Items.Add(new ComboBoxItem { Content = $"{NmsPlanets.TypeName(type)} ({count})", Tag = type });
        }
        TypeFilterBox.SelectedItem = TypeFilterBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == previousType)
            ?? TypeFilterBox.Items[0];

        GalaxyFilterBox.Items.Clear();
        GalaxyFilterBox.Items.Add(new ComboBoxItem { Content = "All galaxies", Tag = -1 });
        foreach (var galaxy in _allPlanets.GroupBy(p => p.Galaxy).OrderBy(g => g.Key))
            GalaxyFilterBox.Items.Add(new ComboBoxItem { Content = $"{GalaxyLabel(galaxy.Key)} ({galaxy.Count()})", Tag = galaxy.Key });
        GalaxyFilterBox.SelectedItem = GalaxyFilterBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == previousGalaxy)
            ?? GalaxyFilterBox.Items[0];

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
        int sort = Math.Max(0, SortBox.SelectedIndex); // 0 nearest, 1 newest, 2 furthest from core, 3 nearest to core
        string star = ChosenValue(StarFilterBox), economy = ChosenValue(EconomyFilterBox), wealth = ChosenValue(WealthFilterBox);
        string conflict = ChosenValue(ConflictFilterBox), race = ChosenValue(RaceFilterBox);
        bool water = WaterFilterBox.IsChecked == true, dissonant = DissonantFilterBox.IsChecked == true;
        int bodies = BodiesFilterBox.SelectedIndex; // 0 all, 1 planets only, 2 moons only
        bool noteFilter = star != "" || economy != "" || wealth != "" || conflict != "" || race != "" || water || dissonant;

        var filtered = _allPlanets
            .Where(p => type < 0 || p.BiomeIndex == type)
            .Where(p => galaxy < 0 || p.Galaxy == galaxy)
            .Where(p => !ringsOnly || p.HasRings)
            .Where(p => !mineOnly || p.DiscoveredHere)
            .Where(p => !noteFilter || SystemNotesService.Get(p.SystemAddress) is { } n
                && (star == "" || n.StarColour == star)
                && (economy == "" || SystemNotesService.CategoryOf("Economy", n.Economy) == economy)
                && (wealth == "" || SystemNotesService.CategoryOf("Wealth", n.Wealth) == wealth)
                && (conflict == "" || SystemNotesService.CategoryOf("Conflict", n.Conflict) == conflict)
                && (race == "" || n.Race == race)
                && (!water || n.Water) && (!dissonant || n.Dissonant))
            .Where(p => bodies switch
            {
                1 => SystemNotesService.Get(p.SystemAddress)?.IsMoon(p.PlanetIndex) != true,
                2 => SystemNotesService.Get(p.SystemAddress)?.IsMoon(p.PlanetIndex) == true,
                _ => true,
            })
            .Select(p => (Planet: p, Distance: _currentSystem is ulong here ? NmsPlanets.DistanceLightYears(here, p.SystemAddress) : null));

        // Nearest first: same-galaxy planets by distance, other galaxies after.
        var ordered = sort switch
        {
            1 => filtered.OrderByDescending(x => x.Planet.DiscoveredUnixSeconds),
            2 => filtered.OrderByDescending(x => NmsPlanets.DistanceToCoreLightYears(x.Planet.Address)),
            3 => filtered.OrderBy(x => NmsPlanets.DistanceToCoreLightYears(x.Planet.Address)),
            _ => filtered.OrderBy(x => x.Distance ?? double.MaxValue).ThenByDescending(x => x.Planet.DiscoveredUnixSeconds),
        };

        var matches = ordered.Select(x => ToRow(x.Planet, x.Distance)).ToList();

        ResultsList.ItemsSource = matches;
        ResultCountTxt.Text = $"{matches.Count:N0} of {_allPlanets.Count:N0} planets";
    }

    private PlanetRowViewModel ToRow(KnownPlanet planet, double? distance)
    {
        string notes = NoteSummary(SystemNotesService.Get(planet.SystemAddress));
        return new()
        {
        NotesLabel = notes,
        NotesVisibility = notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
        DistanceLabel = DistanceText(planet, distance),
        TypeLabel = planet.TypeName,
        Swatch = new SolidColorBrush(ParseColour(NmsPlanets.TypeColour(planet.BiomeIndex))),
        RingVisibility = planet.HasRings ? Visibility.Visible : Visibility.Collapsed,
        LocationLabel = planet.Source.Length > 0
            ? $"{GalaxyLabel(planet.Galaxy)} · {BodyLabel(planet)} · {planet.Source}"
            : $"{GalaxyLabel(planet.Galaxy)} · {BodyLabel(planet)}",
        PortalCode = planet.PortalCode,
        Glyphs = GlyphsFor(planet.PortalCode),
        DiscoveredLabel = $"{(planet.DiscoveredHere ? "You" : planet.Discoverer)} · {FormatDate(planet.DiscoveredUnixSeconds)}",
        };
    }

    private static string BodyLabel(KnownPlanet planet)
    {
        var note = SystemNotesService.Get(planet.SystemAddress);
        string body = note?.IsMoon(planet.PlanetIndex) == true ? "moon" : $"planet {planet.PlanetIndex}";
        if (note?.PlanetNames.TryGetValue(planet.PlanetIndex.ToString(CultureInfo.InvariantCulture), out var planetName) == true)
            body = $"{planetName} ({body})";
        return note?.SystemName is { Length: > 0 } systemName ? $"{systemName} · {body}" : body;
    }

    private string DistanceText(KnownPlanet planet, double? distance)
    {
        string core = $"{NmsPlanets.DistanceToCoreLightYears(planet.Address):N0} ly from the galaxy core";
        if (_currentSystem is ulong here && planet.SystemAddress == here) return $"In the system you're in · {core}";
        string away = distance switch
        {
            null => "In a different galaxy from you",
            < 1 => "Under 400 ly from you",
            double ly => $"About {ly:N0} ly from you",
        };
        return $"{away} · {core}";
    }

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
