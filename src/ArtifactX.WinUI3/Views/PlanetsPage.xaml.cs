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
        PlanetDetailsService.Captured += OnDetailsCaptured;
        SystemPageReader.Captured += OnSystemCaptured;
        ScreenCaptureLogService.SystemPictureSaved += OnSystemPictureSaved;
        BodyKindsService.Captured += OnSystemCaptured;
        SystemMarketService.Changed += OnSystemCaptured;
        Unloaded += Page_Unloaded;
        JumpPlanner.RangeChanged += ApplyFilters;
        _ = LoadPageAsync();
    }

    /// <summary>A planet's details were read off the game screen while this page is open
    /// (game screen recording): show them straight away. An open planet's own editor isn't
    /// rebuilt under the player - it shows them next time it opens.</summary>
    private void OnDetailsCaptured(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_snapshot is null) return;
        if (system == _currentSystem) BuildCurrentSystem(_lastCounts);
        RefreshFinder();
    });

    /// <summary>The current system's notes were read off a game screen: show them, keeping
    /// anything half-typed in the new-note box.</summary>
    private void OnSystemCaptured(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_snapshot is null || system != _currentSystem) { RefreshFinder(); return; }
        string typing = NewNoteBox.Text;
        LoadSystemNote();
        NewNoteBox.Text = typing;
        BuildCurrentSystem(_lastCounts);
        RefreshFinder();
    });

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadPageAsync());

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        PlanetDetailsService.Captured -= OnDetailsCaptured;
        SystemPageReader.Captured -= OnSystemCaptured;
        ScreenCaptureLogService.SystemPictureSaved -= OnSystemPictureSaved;
        BodyKindsService.Captured -= OnSystemCaptured;
        SystemMarketService.Changed -= OnSystemCaptured;
        JumpPlanner.RangeChanged -= ApplyFilters;
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

        // Where fish were caught: credit any new catches to this planet.
        bool newCatch = FishCatchService.Observe(FishCatchService.ActiveSaveKey(), snapshot, _fish);
        var caught = FishCatchService.ReadCaught(snapshot.Fishing);
        bool caughtChanged = caught.Count != _caught.Count || caught.Any(c => !_caught.TryGetValue(c.Key, out var was) || was.Count != c.Value.Count);
        _caught = caught;
        if (newCatch || caughtChanged) layoutChanged = true;

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
        if (live) RefreshPlanetPageLive();
        UpdateHereText(_hereCardText);
        UpdateHereText(_herePageText);
    }


    /// <summary>Records the name the save's "where you saved" line gives for
    /// where you are now: "In the X system" (system), "On Planet (X)" (the
    /// planet you're on) or "Aboard X" (the system's space station; "Aboard the
    /// Space Anomaly" etc. are skipped). Returns whether anything new was stored.</summary>
    private bool LearnNames(PlanetSnapshot snapshot) =>
        SystemNameLearner.Learn(_currentSystem, _currentPlanet, snapshot.Summary);

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
            string discovered = DiscoveredText(tally, address);
            string visit = VisitText(ReadPlanetStats(address));
            if (lines.Discovered.Text != discovered) lines.Discovered.Text = discovered;
            if (visit.Length == 0) visit = "-";
            if (lines.Visit.Text != visit) lines.Visit.Text = visit;
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
        ApplySnapshot(LiveSaveWatcher.FromSession(), live: false);
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
        if (_choicesFilled) RefreshNoteFilters(); // system facts depend on which planets are known
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
        SystemHeadingTxt.Text = UniverseInfo.SystemName(system);
        SystemFactsTxt.Text = UniverseInfo.Summary(system);
        NoteSystemNameBox.PlaceholderText = $"{UniverseInfo.For(system).GeneratedName} (the game's name)";
        string station = markerStation.Length > 0 ? markerStation : systemNote?.StationName ?? "";
        SystemStationTxt.Text = $"Space station: {station}";
        SystemStationTxt.Visibility = station.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        var planets = _thisSave.Where(p => p.SystemAddress == system).OrderBy(p => p.PlanetIndex).ToList();
        var note = SystemNotesService.Get(system);
        int moonCount = planets.Count(p => BodyKindsService.IsMoon(system, p.PlanetIndex));
        string bodies = moonCount > 0
            ? $"{planets.Count - moonCount} planet{(planets.Count - moonCount == 1 ? "" : "s")} · {moonCount} moon{(moonCount == 1 ? "" : "s")}"
            : $"{planets.Count} known planet{(planets.Count == 1 ? "" : "s")}";
        string where = currentPlanet == 0 ? "you're in space"
            : BodyKindsService.IsMoon(system, currentPlanet) ? "you're on a moon"
            : $"you're on planet {currentPlanet}";
        string galaxyText = $"{NmsGalaxyNames.ForNumber(galaxy) ?? "Galaxy"} ({galaxy})";
        SystemSubTxt.Text = planets.Count == 0 ? $"{galaxyText} · no planets discovered here yet" : $"{galaxyText} · {bodies} · {where}";
        SystemSubTxt.Text += $" · about {NmsPlanets.DistanceToCoreLightYears(system):N0} ly from the galaxy core";

        string systemCode = S.PortalAddress(system, 0);
        SystemCodeTxt.Text = systemCode;
        CopySystemCodeBtn.Tag = systemCode;
        SystemGlyphsPanel.Children.Clear();
        SystemGlyphStrip.Code = systemCode;
        SystemGlyphStrip.Title = UniverseInfo.SystemName(system);
        foreach (var glyph in GlyphsFor(systemCode))
            SystemGlyphsPanel.Children.Add(GlyphTile(glyph, 28));

        DrawSystem(planets, currentPlanet, note);
        ShowSystemPicture(system);
        BuildSystemData(system, note);
        BuildMarket(system);

        NoSystemPlanetsTxt.Visibility = planets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AnyFishPanel.Visibility = Visibility.Collapsed; // each card counts every fish it can have now
        foreach (var planet in planets)
        {
            counts.TryGetValue(planet.Address, out var tally);
            CurrentPlanetsPanel.Children.Add(BuildPlanetTile(planet, tally, ReadPlanetStats(planet.Address), planet.PlanetIndex == currentPlanet,
                BodyKindsService.IsMoon(system, planet.PlanetIndex)));
        }
    }

    /// <summary>The star plus one orbit per known planet. Orbits turn slowly
    /// (outer planets slower); nothing animates when Windows' "Animation
    /// effects" setting is off.</summary>
    private void DrawSystem(List<KnownPlanet> allBodies, int currentPlanet, SystemNote? note)
    {
        // Moons orbit their own planet (BodyKindsService) rather than the star - unless
        // that planet isn't known yet, then they're drawn like planets.
        ulong system = allBodies.FirstOrDefault()?.SystemAddress ?? 0;
        var moonsOf = allBodies
            .Select(p => (Moon: p, Host: BodyKindsService.Host(system, p.PlanetIndex)))
            .Where(m => m.Host != 0)
            .Select(m => (m.Moon, Host: allBodies.FirstOrDefault(p => p.PlanetIndex == (m.Host > 0 ? m.Host : 1))))
            .Where(m => m.Host is not null && m.Host != m.Moon)
            .GroupBy(m => m.Host!).ToDictionary(g => g.Key, g => g.Select(m => m.Moon).ToList());
        var planets = allBodies.Except(moonsOf.Values.SelectMany(m => m)).ToList();
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
            if (moonsOf.TryGetValue(planet, out var moons))
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
        string planetName = UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex);
        names.Children.Add(new TextBlock
        {
            Text = planetName,
            Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"],
            FontWeight = FontWeights.SemiBold,
        });
        names.Children.Add(DimLine(typeText));
        Grid.SetColumn(names, 1);
        header.Children.Add(names);
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var open = new Button { Content = "Open ›", Padding = new Thickness(10, 3, 10, 3), VerticalAlignment = VerticalAlignment.Top };
        ToolTipService.SetToolTip(open, "Open this planet - everything about it, and your details");
        open.Click += (_, _) => OpenPlanet(planet);
        Grid.SetColumn(open, 2);
        header.Children.Add(open);
        stack.Children.Add(header);

        if (here) stack.Children.Add(HereBadge(page: false));

        // What the save knows, as a label / value table (2026-10-13 redesign:
        // a stack of small dim lines was hard to read and easy to miss).
        var facts = new FactTable();
        var discoveredLine = facts.Add("You found", DiscoveredText(tally, planet.Address));
        var visitLine = facts.Add("Visited", VisitText(stats));
        _tileText[planet.Address] = (discoveredLine, visitLine);
        if (_baseNames.TryGetValue(planet.PlanetIndex, out var baseNames))
            facts.Add(baseNames.Count == 1 ? "Your base" : "Your bases", string.Join(", ", baseNames));
        facts.Add("Found by", planet.DiscoveredHere ? "You" : planet.Discoverer);
        stack.Children.Add(facts.Grid);

        AddPlanetFish(stack, planet, full: false);
        AddDetailsSummary(stack, planet);

        var glyphRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var glyphTiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var glyph in GlyphsFor(planet.PortalCode))
            glyphTiles.Children.Add(GlyphTile(glyph, 20));
        glyphRow.Children.Add(new ArtifactX.WinUI3.Controls.GlyphStrip { Content = glyphTiles, Code = planet.PortalCode, VerticalAlignment = VerticalAlignment.Center });
        var copy = new Button { Content = "Copy", Tag = planet.PortalCode, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        copy.Click += CopyCode_Click;
        ToolTipService.SetToolTip(copy, $"Copy portal address {planet.PortalCode}");
        glyphRow.Children.Add(copy);
        stack.Children.Add(glyphRow);

        var card = new Border
        {
            Width = 372,
            Padding = new Thickness(14, 12, 14, 12),
            Background = ThemeBrush("ThemePanelAltBrush"),
            BorderBrush = ThemeBrush(here ? "ThemeAccentBrush" : "ThemeLineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = ThemeResources.CardCornerRadius,
            Child = stack,
        };
        ToolTipService.SetToolTip(card, "Click to open this planet");
        // The whole card opens the planet - except clicks meant for its own
        // buttons, links and glyphs.
        card.Tapped += (_, e) =>
        {
            for (var element = e.OriginalSource as DependencyObject; element is not null && element != card;
                 element = VisualTreeHelper.GetParent(element))
                if (element is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase or ArtifactX.WinUI3.Controls.GlyphStrip or TextBlock { IsTextSelectionEnabled: true })
                    return;
            OpenPlanet(planet);
        };
        return card;
    }

    /// <summary>The planet's recorded details in one line on its card (the
    /// full view and the editor are on the planet's own page).</summary>
    private void AddDetailsSummary(StackPanel stack, KnownPlanet planet)
    {
        var details = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex);
        var parts = new List<string>();
        if (details is not null)
        {
            if (details.Sentinels != "") parts.Add($"{details.Sentinels} sentinels");
            if (details.Weather != "") parts.Add(details.Weather);
            if (details.Resources.Count > 0) parts.Add(string.Join(", ", details.Resources));
            if (details.Notes.Count > 0) parts.Add(details.Notes.Count == 1 ? "1 note" : $"{details.Notes.Count} notes");
        }
        var section = Section("YOUR DETAILS");
        if (parts.Count > 0)
            section.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", parts),
                FontSize = 13,
                Foreground = ThemeBrush("ThemeTextBrush"),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 3,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        else
            section.Children.Add(DimLine("None yet - open the planet to add weather, sentinels, resources and notes."));
        stack.Children.Add(section);
    }

    /// <summary>"2 / 6 fauna · 0 flora · 0 minerals": fauna out of how many kinds the planet
    /// has, when its Discoveries page said (game screen recording or typed in).</summary>
    private void OnSystemPictureSaved(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        if (system == _currentSystem) ShowSystemPicture(system);
    });

    /// <summary>The game's picture of the system's planets in place of the drawn orbits, when there is one.</summary>
    private void ShowSystemPicture(ulong system)
    {
        if (ScreenCaptureLogService.SystemPicture(system) is { } path)
        {
            // Read into memory so a newer picture can replace the file while it's shown.
            var image = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                image.SetSource(stream.AsRandomAccessStream());
            SystemPictureImage.Source = image;
            SystemPictureImage.Visibility = Visibility.Visible;
            SystemCanvas.Visibility = Visibility.Collapsed;
            SystemViewFrame.Width = 540; // the picture's own shape (about 3:2), not the square orbit view
        }
        else
        {
            SystemPictureImage.Source = null;
            SystemPictureImage.Visibility = Visibility.Collapsed;
            SystemCanvas.Visibility = Visibility.Visible;
            SystemViewFrame.Width = 360;
        }
    }

    /// <summary>SOLD AT THE STATION: one chip per item - icon, name, price, change, stock.</summary>
    private void BuildMarket(ulong system)
    {
        MarketItemsPanel.Children.Clear();
        var items = SystemMarketService.For(system);
        MarketEmptyTxt.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MarketSeenTxt.Text = items.Count == 0 ? "" : $"{items.Count} item{(items.Count == 1 ? "" : "s")} · last seen {SeenAgo(items.Max(i => i.SeenAt))}";
        foreach (var item in items)
        {
            // Fixed width, so the cards line up in columns whatever the name's length.
            var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Thickness(8, 6, 12, 6), Width = 290 };
            if (CatalogService.TryGet(item.ItemId)?.Icon is { } icon)
                chip.Children.Add(new Image { Source = icon, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = 226 };
            text.Children.Add(new TextBlock { Text = item.Name, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = ThemeBrush("ThemeTextBrush"), TextTrimming = TextTrimming.CharacterEllipsis });
            ToolTipService.SetToolTip(chip, item.Name);
            text.Children.Add(new TextBlock
            {
                Text = $"{item.Price:N0} units · {item.Change}{(item.Stock > 0 ? $" · {item.Stock} in stock" : "")}",
                FontSize = 11, Foreground = ThemeBrush("ThemeTextDimBrush"), TextTrimming = TextTrimming.CharacterEllipsis,
            });
            chip.Children.Add(text);
            MarketItemsPanel.Children.Add(new Border
            {
                Child = chip, CornerRadius = new CornerRadius(6), Background = ThemeBrush("ThemeVoidBrush"),
                BorderBrush = ThemeBrush("ThemeLineBrush"), BorderThickness = new Thickness(1),
            });
        }
        ApplyMarketCollapsed();
        _ = WarmMarketIconsAsync(system, items);
    }

    private const string MarketCollapsedKey = "PlanetsMarketCollapsed";
    private bool MarketCollapsed => SettingsStore.Get(SettingsStore.Scope.Profile, MarketCollapsedKey) == "1";

    private void ApplyMarketCollapsed()
    {
        MarketBody.Visibility = MarketCollapsed ? Visibility.Collapsed : Visibility.Visible;
        MarketToggleBtn.Content = MarketCollapsed ? "Show" : "Hide";
    }

    private void MarketToggle_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Set(SettingsStore.Scope.Profile, MarketCollapsedKey, MarketCollapsed ? "0" : "1");
        ApplyMarketCollapsed();
    }

    private async Task WarmMarketIconsAsync(ulong system, IReadOnlyList<MarketItem> items)
    {
        var missing = items.Where(i => CatalogService.TryGet(i.ItemId)?.Icon is null).Select(i => i.ItemId).ToList();
        if (missing.Count == 0) return;
        await CatalogService.WarmCacheAsync(missing);
        if (system == _currentSystem && items.Any(i => CatalogService.TryGet(i.ItemId)?.Icon is not null)) BuildMarket(system);
    }

    private static string SeenAgo(string iso)
    {
        if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)) return "";
        var ago = DateTime.UtcNow - at.ToUniversalTime();
        return ago.TotalMinutes < 1 ? "just now" : ago.TotalHours < 1 ? $"{(int)ago.TotalMinutes} min ago"
            : ago.TotalDays < 1 ? $"{(int)ago.TotalHours} h ago" : at.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
    }

    /// <summary>The SYSTEM DATA box: the game's own pop-up lines, with what the game showed
    /// (system notes - read off the screen or typed in) in gold and the generator's answer
    /// in grey where nothing was recorded.</summary>
    private void BuildSystemData(ulong system, SystemNote? note)
    {
        SystemDataPanel.Children.Clear();
        var f = UniverseInfo.For(system);
        void Row(string label, string? recorded, string estimate)
        {
            bool known = !string.IsNullOrEmpty(recorded);
            var line = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 15 };
            line.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = label + ": ", Foreground = ThemeBrush("ThemeTextBrush") });
            line.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text = known ? recorded! : estimate,
                Foreground = ThemeBrush(known ? "ThemeWarningBrush" : "ThemeTextDimBrush"),
            });
            SystemDataPanel.Children.Add(line);
        }
        int galaxy = S.GalaxyNumber(system);
        Row("Galaxy", NmsGalaxyNames.ForNumber(galaxy) is { } g ? $"{g} Galaxy" : $"Galaxy {galaxy}", "");
        string bodies(int planets, int moons) => $"{planets} Planet{(planets == 1 ? "" : "s")} // {moons} Moon{(moons == 1 ? "" : "s")}";
        int total = f.PlanetCount + f.MoonCount;
        Row("Celestial Bodies", note?.Moons is int m ? bodies(total - m, m) : null, bodies(f.PlanetCount, f.MoonCount));
        Row("Dominant Lifeform", note?.Race is { Length: > 0 } r ? r : null, f.RaceLabel);
        string? economy = note?.Economy is { Length: > 0 } e ? (note.Wealth is { Length: > 0 } w ? $"{e} // {w}" : e) : null;
        Row("Economy", economy, f.Uncharted ? "None" : $"{f.EconomyLabel} // {f.WealthLabel}");
        Row("Conflict level", note?.Conflict is { Length: > 0 } c ? c : null, f.Uncharted ? "None" : f.ConflictLabel);
        Row("Star", note?.StarClass is { Length: > 0 } cls ? $"{cls} ({note.StarColour})" : note?.StarColour is { Length: > 0 } sc ? sc : null, f.StarLabel);
        if (note?.Water == true) Row("Water", "Yes", "");
        if (note?.Dissonant == true) Row("Dissonance", "Detected", "");
        if (f.Pirate) Row("Pirates", null, "Pirate-controlled");
    }

    private static string DiscoveredText(PlanetDiscoveryCounts? tally, ulong address)
    {
        var t = tally ?? new PlanetDiscoveryCounts(0, 0, 0);
        int? total = PlanetDetailsService.Get(S.ToSystemAddress(address), (int)((address >> 52) & 0xF))?.FaunaTotal;
        string fauna = total switch { 0 => "no fauna", int n => $"{t.Fauna} / {n} fauna", _ => $"{t.Fauna} fauna" };
        return $"{fauna} · {t.Flora} flora · {t.Minerals} minerals";
    }

    // ^TIME / ^DIST_WALKED units assumed seconds / metres (Cholton: 199 and 85
    // after a few minutes on foot).
    private static string VisitText(Dictionary<string, double> stats)
    {
        var visit = new List<string>();
        if (stats.TryGetValue("^TIME", out double secondsHere) && secondsHere >= 1)
            visit.Add(FormatDuration(secondsHere));
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
    private static Task<List<(string Word, string Group, string Tier)>>? _planetWordsLoad;
    private static Task<List<string>>? _substancesLoad;
    private List<(string Word, string Group, string Tier)> _planetWords = new();
    private List<string> _substances = new();
    private static Task<List<FishInfo>>? _fishLoad;
    private static Task<Dictionary<string, string>>? _namesLoad;
    private List<FishInfo> _fish = new();
    private Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, (uint Count, float Largest)> _caught = new(StringComparer.OrdinalIgnoreCase);
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
        _planetWordsLoad ??= CatalogService.GetPlanetDescriptorsAsync();
        _substancesLoad ??= CatalogService.GetSubstanceNamesAsync();
        _planetWords = await _planetWordsLoad;
        // Plus the special finds a planet lists among its resources (Ancient Bones...).
        _substances = (await _substancesLoad)
            .Concat(_planetWords.Where(w => w.Group == "Resource").Select(w => w.Word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        // The 210 real species, as on the Fishing page (no junk or mission-only fish).
        _fishLoad ??= CatalogService.GetFishAsync();
        _namesLoad ??= CatalogService.GetItemNamesAsync();
        _fish = (await _fishLoad).Where(f => CatalogService.IsFishSpecies(f.Id) && !f.NeedsMission).ToList();
        _itemNames = await _namesLoad;

        IEnumerable<(string Label, string Value)> Words(string group) => descriptors
            .Where(d => d.Group == group)
            .Select(d => (Label: $"{d.Word} ({SystemNotesService.TierName(group, d.Tier)})", Value: d.Word))
            .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase);
        static IEnumerable<(string Label, string Value)> Plain(IEnumerable<string> values) => values.Select(v => (v, v));

        _suppressNoteEvents = true;
        FillChoices(NoteStarBox, NotRecorded, Plain(SystemNotesService.StarColours));
        FillChoices(NoteEconomyBox, NotRecorded, Words("Economy").Concat(Plain(SystemNotesService.EconomyExtras)));
        FillChoices(NoteWealthBox, NotRecorded, Words("Wealth"));
        FillChoices(NoteConflictBox, NotRecorded, Words("Conflict"));
        FillChoices(NoteRaceBox, NotRecorded, Plain(SystemNotesService.Races));
        FillChoices(NoteMoonsBox, NotRecorded, Plain(new[] { "0", "1", "2", "3" }));
        _suppressNoteEvents = false;

        _choicesFilled = true;
        RefreshNoteFilters();
    }

    /// <summary>The "(your notes)" search filters list only values you've
    /// actually recorded, with how many systems have each - unlike the note
    /// entry dropdowns, which list every possible word. A filter with nothing
    /// recorded is hidden, and the current selection is kept where possible.</summary>
    private void RefreshNoteFilters()
    {
        var notes = SystemNotesService.All;

        // Star, economy, wealth, conflict and race come from the game's generator for
        // every planet the finder knows (the player's notes override), counted in planets.
        IEnumerable<(string Label, string Value)> Known(Func<ulong, string> value) => _allPlanets
            .Select(p => value(p.SystemAddress))
            .Where(v => v.Length > 0 && v != "None")
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => (Label: $"{g.Key} ({g.Count():N0})", Value: g.Key));

        _suppressFilterEvents = true;
        Refill(StarFilterBox, Known(StarOf));
        Refill(EconomyFilterBox, Known(UniverseInfo.Economy));
        Refill(WealthFilterBox, Known(UniverseInfo.Wealth));
        Refill(ConflictFilterBox, Known(UniverseInfo.Conflict));
        Refill(RaceFilterBox, Known(RaceOf));

        int water = notes.Count(n => n.Water), dissonant = notes.Count(n => n.Dissonant), moons = notes.Count(n => n.Moons is not null);
        WaterFilterBox.Content = $"Water ({water})";
        WaterFilterBox.Visibility = water > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (water == 0) WaterFilterBox.IsChecked = false;
        DissonantFilterBox.Content = $"Dissonant ({dissonant})";
        DissonantFilterBox.Visibility = dissonant > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (dissonant == 0) DissonantFilterBox.IsChecked = false;
        BodiesFilterBox.Visibility = moons > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (moons == 0) BodiesFilterBox.SelectedIndex = 0;

        // Planet details: only what's been recorded, counted in planets, by level
        // ("Frenzied" and "Aggressive" are both High sentinels).
        var details = PlanetDetailsService.All;
        IEnumerable<(string Label, string Value)> Levels(IEnumerable<string> values) => values
            .Where(v => v.Length > 0)
            .GroupBy(v => v)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => (Label: $"{g.Key} ({g.Count():N0})", Value: g.Key));
        Refill(ShipFilterBox, Levels(notes.SelectMany(n => n.Ships)));
        Refill(SentinelsFilterBox, Levels(details.Select(d => LevelOf("Sentinels", d.Sentinels))));
        Refill(FloraFilterBox, Levels(details.Select(d => LevelOf("Life", d.Flora))));
        Refill(FaunaFilterBox, Levels(details.Select(d => LevelOf("Life", d.Fauna))));
        Refill(ResourceFilterBox, Levels(details.SelectMany(d => d.Resources)));

        bool any = new[] { StarFilterBox, EconomyFilterBox, WealthFilterBox, ConflictFilterBox, RaceFilterBox,
                ShipFilterBox, SentinelsFilterBox, FloraFilterBox, FaunaFilterBox, ResourceFilterBox }.Any(b => b.Visibility == Visibility.Visible)
            || water > 0 || dissonant > 0 || moons > 0;
        NoteFiltersPanel.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        NoNoteFiltersTxt.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        _suppressFilterEvents = false;
    }

    private static void Refill(ComboBox box, IEnumerable<(string Label, string Value)> options)
    {
        string previous = ChosenValue(box);
        var list = options.ToList();
        FillChoices(box, "Any", list);
        if (previous.Length > 0 && list.Any(o => o.Value == previous)) SelectChoice(box, previous);
        box.Visibility = list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
        BuildShipToggles(note?.Ships ?? new());
        _suppressNoteEvents = false;
        ApplySystemNotesCollapsed();
    }

    private const string SystemNotesCollapsedKey = "PlanetsSystemNotesCollapsed";

    private bool SystemNotesCollapsed => SettingsStore.Get(SettingsStore.Scope.Profile, SystemNotesCollapsedKey) == "1";

    /// <summary>Open: every field. Closed: one line with what's recorded.</summary>
    private void ApplySystemNotesCollapsed()
    {
        bool collapsed = SystemNotesCollapsed;
        SystemNotesBody.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SystemNotesToggleBtn.Content = collapsed ? "Show" : "Hide";
        string summary = _currentSystem is ulong system ? NoteSummary(SystemNotesService.Get(system)) : "";
        SystemNotesSummaryTxt.Text = collapsed ? (summary.Length > 0 ? summary : "Nothing recorded yet") : "";
        ToolTipService.SetToolTip(SystemNotesSummaryTxt, collapsed && summary.Length > 0 ? summary : null);
    }

    private void SystemNotesToggle_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Set(SettingsStore.Scope.Profile, SystemNotesCollapsedKey, SystemNotesCollapsed ? "0" : "1");
        ApplySystemNotesCollapsed();
    }

    /// <summary>One toggle per ship type; on = seen in this system.</summary>
    private void BuildShipToggles(List<string> seen)
    {
        ShipTypesPanel.Children.Clear();
        foreach (string type in SystemNotesService.ShipTypes)
        {
            var toggle = new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton
            {
                Content = type,
                Tag = type,
                IsChecked = seen.Contains(type, StringComparer.OrdinalIgnoreCase),
                Padding = new Thickness(12, 4, 12, 4),
            };
            toggle.Click += (_, _) => SaveSystemNote();
            ShipTypesPanel.Children.Add(toggle);
        }
    }

    private List<string> CheckedShips() => ShipTypesPanel.Children
        .OfType<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>()
        .Where(t => t.IsChecked == true).Select(t => (string)t.Tag).ToList();

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
            Ships = CheckedShips(),
        });
        ApplySystemNotesCollapsed();
        BuildSystemData(system, SystemNotesService.Get(system));
        RefreshNoteFilters();
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
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Top };
            if (MoveToPlanetButton(index) is { } move) actions.Children.Add(move);
            actions.Children.Add(remove);
            Grid.SetColumn(actions, 2);
            row.Children.Add(actions);

            NotesListPanel.Children.Add(row);
        }
    }

    /// <summary>"Move to planet" for a system note: planet facts typed here
    /// before planets had their own details (user, 2026-10-13: "delete them
    /// from the system and add them to the planet" - moved, not copied).</summary>
    private DropDownButton? MoveToPlanetButton(int noteIndex)
    {
        if (_currentSystem is not ulong system) return null;
        var planets = _thisSave.Where(p => p.SystemAddress == system).OrderBy(p => p.PlanetIndex).ToList();
        if (planets.Count == 0) return null;
        var menu = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight };
        foreach (var planet in planets)
        {
            var item = new MenuFlyoutItem { Text = $"{UniverseInfo.PlanetName(system, planet.PlanetIndex)} ({planet.TypeName})" };
            item.Click += (_, _) =>
            {
                if (noteIndex >= _noteList.Count) return;
                string text = _noteList[noteIndex];
                var details = PlanetDetailsService.Get(system, planet.PlanetIndex)?.Copy() ?? new PlanetDetails();
                details.Notes.Add(text);
                PlanetDetailsService.Save(system, planet.PlanetIndex, details);
                _noteList.RemoveAt(noteIndex);
                RenderNotes();
                SaveSystemNote();
                BuildCurrentSystem(_lastCounts);
            };
            menu.Items.Add(item);
        }
        var button = new DropDownButton { Content = "Move to planet", Flyout = menu, Padding = new Thickness(8, 2, 8, 2) };
        ToolTipService.SetToolTip(button, "This note is about one planet - move it to that planet's card");
        return button;
    }

    // ---- planet details (PlanetDetailsService)

    private static readonly string[] LevelOrder = { "None", "Low", "Medium", "High", "Corrupted", "Infested", "Synthetic", "Weird" };

    /// <summary>The game's tier names (from the loc ids, e.g. SENTINEL_RARE3,
    /// RARITY_HIGH6) as levels: sentinels RARE and LOW are both low, DEFAULT
    /// is the usual medium, HIGH and AGGRESSIVE share their words (both high).</summary>
    private static string LevelName(string tier) => tier.ToUpperInvariant() switch
    {
        "NONE" => "None",
        "LOW" or "RARE" => "Low",
        "MID" or "DEFAULT" => "Medium",
        "HIGH" or "AGGRESSIVE" => "High",
        "CORRUPT" => "Corrupted",
        "INFESTED" => "Infested",
        "ROBOT" => "Synthetic",
        "WEIRD" => "Weird",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier.ToLowerInvariant()),
    };

    /// <summary>The level a recorded word belongs to ("Frenzied" -> "High"), or the word itself.</summary>
    private string LevelOf(string group, string word)
    {
        if (word.Length == 0) return "";
        var match = _planetWords.FirstOrDefault(w => w.Group == group && string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase));
        return match.Word is null ? word : LevelName(match.Tier);
    }

    /// <summary>"Sentinels: Infrequent · Flora: Generous · Paraffinium, Silver · Water · ..."</summary>
    private static string DetailsSummary(PlanetDetails? d, bool? water)
    {
        var parts = new List<string>();
        if (d is not null)
        {
            if (d.Weather != "") parts.Add($"Weather: {d.Weather}");
            if (d.Sentinels != "") parts.Add($"Sentinels: {d.Sentinels}");
            if (d.Flora != "") parts.Add($"Flora: {d.Flora}");
            if (d.Fauna != "") parts.Add($"Fauna: {d.Fauna}");
            if (d.Resources.Count > 0) parts.Add(string.Join(", ", d.Resources));
        }
        if (water == true) parts.Add("Has water");
        else if (water == false) parts.Add("No water");
        if (d is not null) parts.AddRange(d.Notes);
        return string.Join(" · ", parts);
    }

    /// <summary>Everything the finder's search box looks through for a planet.</summary>
    private string SearchText(KnownPlanet p) => string.Join(" ",
        UniverseInfo.PlanetName(p.SystemAddress, p.PlanetIndex), UniverseInfo.SystemName(p.SystemAddress), p.TypeName,
        DetailsSummary(PlanetDetailsService.Get(p.SystemAddress, p.PlanetIndex), PlanetWaterService.Get(p.SystemAddress, p.PlanetIndex)),
        NoteSummary(SystemNotesService.Get(p.SystemAddress)));

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _searchTimer;

    /// <summary>Waits for a pause in typing, so the finder isn't re-run per key.</summary>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer ??= DispatcherQueue.CreateTimer();
        _searchTimer.Interval = TimeSpan.FromMilliseconds(250);
        _searchTimer.IsRepeating = false;
        _searchTimer.Tick -= SearchTimer_Tick;
        _searchTimer.Tick += SearchTimer_Tick;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void SearchTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args) => ApplyFilters();

    /// <summary>A fish's name in normal case ("Glass Angel", not "GLASS ANGEL").</summary>
    private string FishName(string id)
    {
        string name = _itemNames.TryGetValue(id, out var n) ? n
            : CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } shown ? shown : id;
        return name == name.ToUpperInvariant() ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant()) : name;
    }

    /// <summary>Label / value rows for a planet card: labels dim, values in
    /// normal text so they're easy to read.</summary>
    private sealed class FactTable
    {
        public Grid Grid { get; } = new() { ColumnSpacing = 10, RowSpacing = 3 };

        public FactTable()
        {
            Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        public TextBlock Add(string label, string value)
        {
            int row = Grid.RowDefinitions.Count;
            Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var labelText = new TextBlock { Text = label, FontSize = 12, Foreground = ThemeBrush("ThemeTextDimBrush") };
            var valueText = new TextBlock
            {
                Text = value.Length > 0 ? value : "-",
                FontSize = 13,
                Foreground = ThemeBrush("ThemeTextBrush"),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            Grid.SetRow(labelText, row);
            Grid.SetRow(valueText, row);
            Grid.SetColumn(valueText, 1);
            Grid.Children.Add(labelText);
            Grid.Children.Add(valueText);
            return valueText;
        }
    }

    /// <summary>A card section: a thin rule and a small heading.</summary>
    private static StackPanel Section(string title)
    {
        var section = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        section.Children.Add(new Border { Height = 1, Background = ThemeBrush("ThemeLineBrush"), Margin = new Thickness(0, 0, 0, 4) });
        section.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] });
        return section;
    }

    /// <summary>A "Show ... / Hide" link that opens a block below it.</summary>
    private static (HyperlinkButton Toggle, StackPanel Body) Expandable(string showText)
    {
        var body = new StackPanel { Spacing = 6, Visibility = Visibility.Collapsed };
        var toggle = new HyperlinkButton { Content = showText, Padding = new Thickness(0), FontSize = 12 };
        toggle.Click += (_, _) =>
        {
            bool open = body.Visibility == Visibility.Collapsed;
            body.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            toggle.Content = open ? "Hide" : showText;
        };
        return (toggle, body);
    }

    // The "YOU ARE HERE" badges on the current planet's card and its open page,
    // kept so each new save can update the coordinates without a rebuild.
    private TextBlock? _hereCardText, _herePageText;

    /// <summary>"YOU ARE HERE", plus the coordinates the game shows on the
    /// ship's dashboard when the save has them (NmsPlanetCoordinates).</summary>
    private Border HereBadge(bool page)
    {
        var text = new TextBlock { FontSize = 10, FontWeight = FontWeights.Bold, Foreground = ThemeBrush("ThemeOnAccentBrush"), IsTextSelectionEnabled = true };
        if (page) _herePageText = text; else _hereCardText = text;
        UpdateHereText(text);
        var badge = new Border
        {
            Background = ThemeBrush("ThemeAccentBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = text,
        };
        ToolTipService.SetToolTip(badge, "Your coordinates on this planet when the game last saved - the same numbers the game shows on your ship's dashboard.");
        return badge;
    }

    private void UpdateHereText(TextBlock? text)
    {
        if (text is null) return;
        text.Text = _snapshot.Coordinates is { } at
            ? $"YOU ARE HERE · {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}"
            : "YOU ARE HERE";
    }

    private TextBlock NameList(IEnumerable<string> names) => new()
    {
        Text = string.Join(", ", names),
        FontSize = 12,
        Foreground = ThemeBrush("ThemeTextBrush"),
        TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true,
    };

    /// <summary>Label on its own line, then a full-width bar and the count -
    /// so every card's bar starts and ends in the same place (a label beside
    /// the bar made each one a different length, 2026-10-13).</summary>
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

    /// <summary>The planet's own fish - the ones that live only on its planet
    /// type. The fish found on any planet with water are shown once above the
    /// cards: counting them on every card made each planet read "5 / 42" after
    /// catching 5 anywhere (user, 2026-10-13). Lists start closed to keep the
    /// card short; "Caught here" is what FishCatchService recorded on it.</summary>
    private void AddPlanetFish(StackPanel stack, KnownPlanet planet, bool full)
    {
        if (_fish.Count == 0) return;
        // Only for planets known to have water: marked in details (or on the
        // Fishing page), a catch recorded here, or a water world. Most planets
        // have none, and a fish bar on them suggests they do (user, 2026-10-13).
        var caughtHere = FishCatchService.CaughtOn(planet.SystemAddress, planet.PlanetIndex);
        bool? water = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex);
        // A recorded catch beats a "no water" mark (catches also correct the mark).
        bool hasWater = water == true || caughtHere.Count > 0 || (water is null && NmsPlanets.AlwaysHasWater(planet.BiomeIndex));
        if (!hasWater) return;
        var section = Section("FISH");

        // The bar is this planet type's own fish - the reason to fish HERE. The
        // 21 found on any planet with water are the same on every planet, so
        // they're a line of text, not part of the bar (in it, every planet read
        // "5 / 42" after 5 catches anywhere).
        var own = _fish.Where(f => f.LivesOn(planet.BiomeIndex) && !f.Biomes.Contains(FishInfo.AnyPlanet)).ToList();
        var anywhere = _fish.Where(f => f.Biomes.Contains(FishInfo.AnyPlanet)).ToList();
        int Caught(List<FishInfo> fish) => fish.Count(f => _caught.ContainsKey(f.Id));
        // A finished goal shrinks to one line - nothing to hide or manage, and
        // the card stays short (2026-10-15). Labels say "any X planet" so the
        // bar isn't read as catches on THIS planet; that's "Caught here" below.
        if (own.Count > 0)
        {
            if (Caught(own) >= own.Count)
                section.Children.Add(DimLine($"✓ All {own.Count} {planet.TypeName} fish caught (on any {planet.TypeName} planet)"));
            else
                section.Children.Add(ProgressRow($"{planet.TypeName} fish - on any {planet.TypeName} planet", Caught(own), own.Count));
        }
        section.Children.Add(DimLine(Caught(anywhere) >= anywhere.Count
            ? $"✓ All {anywhere.Count} fish found on any planet with water caught"
            : $"Plus {anywhere.Count} fish found on any planet with water - you've caught {Caught(anywhere)}."));
        // Catches ArtifactX recorded on this exact planet - a running count that
        // can pass the number of kinds, shown even at 0 so "nothing caught here
        // yet" is clear (2026-10-15).
        int totalHere = caughtHere.Values.Sum();
        section.Children.Add(new TextBlock
        {
            Text = totalHere == 0 ? "Caught here: none yet"
                : $"Caught here: {totalHere:N0} fish · {caughtHere.Count} kind{(caughtHere.Count == 1 ? "" : "s")}",
            FontSize = 13,
            Foreground = ThemeBrush("ThemeTextBrush"),
        });

        var missingOwn = own.Where(f => !_caught.ContainsKey(f.Id)).Select(f => FishName(f.Id)).OrderBy(x => x).ToList();
        var missingAny = anywhere.Where(f => !_caught.ContainsKey(f.Id)).Select(f => FishName(f.Id)).OrderBy(x => x).ToList();
        int missing = missingOwn.Count + missingAny.Count;
        var parts = new List<string>();
        if (missing > 0) parts.Add($"{missing} still to catch");
        if (caughtHere.Count > 0) parts.Add($"{caughtHere.Count} caught here");
        if (parts.Count == 0)
        {
            section.Children.Add(DimLine("You've caught every fish you can find here."));
            stack.Children.Add(section);
            return;
        }

        if (!full)
        {
            stack.Children.Add(section);
            return;
        }
        var (_, body) = Expandable("");
        body.Visibility = Visibility.Visible;
        void Heading(string text) => body.Children.Add(new TextBlock { Text = text, FontSize = 11, Foreground = ThemeBrush("ThemeTextDimBrush") });
        if (missingOwn.Count > 0)
        {
            Heading($"STILL TO CATCH - ONLY ON {planet.TypeName.ToUpperInvariant()} PLANETS ({missingOwn.Count})");
            body.Children.Add(NameList(missingOwn));
        }
        if (missingAny.Count > 0)
        {
            Heading($"STILL TO CATCH - ANY PLANET WITH WATER ({missingAny.Count})");
            body.Children.Add(NameList(missingAny));
        }
        if (caughtHere.Count > 0)
        {
            Heading($"CAUGHT ON THIS PLANET ({caughtHere.Count} KIND{(caughtHere.Count == 1 ? "" : "S")})");
            // Fish with a known spot get a line each with the coordinates on the
            // ship's dashboard (2026-10-14; up to 3 spots, newest first). The
            // rest - caught before coordinates were kept - stay one compact
            // list: a line per fish made the card very tall (user, 2026-10-14).
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
                body.Children.Add(list);
            }
            if (unlocated.Count > 0)
            {
                if (located.Count > 0) Heading("CAUGHT HERE BEFORE LOCATIONS WERE KEPT");
                var list = NameList(unlocated);
                ToolTipService.SetToolTip(list, Recorded);
                body.Children.Add(list);
            }
        }
        section.Children.Add(body);
        stack.Children.Add(section);
    }

    /// <summary>The fish that live on any planet with water, once for the
    /// whole system rather than repeated on every card.</summary>
    private void BuildAnyPlanetFish(List<KnownPlanet> planets)
    {
        AnyFishPanel.Children.Clear();
        var anywhere = _fish.Where(f => f.Biomes.Contains(FishInfo.AnyPlanet)).ToList();
        bool anyWet = planets.Any(p => PlanetWaterService.Get(p.SystemAddress, p.PlanetIndex) != false);
        AnyFishPanel.Visibility = anywhere.Count > 0 && anyWet ? Visibility.Visible : Visibility.Collapsed;
        if (AnyFishPanel.Visibility == Visibility.Collapsed) return;

        var row = ProgressRow("Fish on any planet with water", anywhere.Count(f => _caught.ContainsKey(f.Id)), anywhere.Count);
        row.Width = 420; // left-aligned, so it needs a width or the bar has none
        row.HorizontalAlignment = HorizontalAlignment.Left;
        AnyFishPanel.Children.Add(row);
        var missing = anywhere.Where(f => !_caught.ContainsKey(f.Id)).Select(f => FishName(f.Id)).OrderBy(x => x).ToList();
        if (missing.Count == 0) return;
        var (toggle, body) = Expandable($"Show the {missing.Count} still to catch");
        body.MaxWidth = 900;
        body.HorizontalAlignment = HorizontalAlignment.Left;
        body.Children.Add(NameList(missing));
        AnyFishPanel.Children.Add(toggle);
        AnyFishPanel.Children.Add(body);
    }

    /// <summary>The editor for one planet's details, shown on the planet's own
    /// page. Words come from the game (catalog "PlanetDescriptors"); typing
    /// anything else is kept too. Commit saves what's in it.</summary>
    private (StackPanel Content, Action Commit) BuildDetailsEditor(KnownPlanet planet)
    {
        ulong system = planet.SystemAddress;
        var details = PlanetDetailsService.Get(system, planet.PlanetIndex)?.Copy() ?? new PlanetDetails();

        static string Label(string word, string tier) => $"{word} ({LevelName(tier)})";
        List<string> Words(string group) => _planetWords.Where(w => w.Group == group)
            .GroupBy(w => w.Word, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(w => Array.IndexOf(LevelOrder, LevelName(w.Tier)) is var i && i < 0 ? 99 : i)
            .ThenBy(w => w.Word, StringComparer.CurrentCultureIgnoreCase)
            .Select(w => Label(w.Word, w.Tier)).ToList();
        string WordOf(string label) => label.LastIndexOf(" (", StringComparison.Ordinal) is int cut and > 0 ? label[..cut] : label;

        ComboBox Choice(string header, List<string> options, string current)
        {
            var combo = new ComboBox { Header = header, Width = 240, IsEditable = true, ItemsSource = options, PlaceholderText = "Not recorded" };
            // Select the matching choice; Text set before the box is on screen is
            // ignored by an editable ComboBox (picks didn't show on reopening, 2026-10-13).
            if (current.Length > 0)
            {
                string? match = options.FirstOrDefault(o => string.Equals(WordOf(o), current, StringComparison.OrdinalIgnoreCase));
                if (match is not null) combo.SelectedItem = match;
                else combo.Loaded += (_, _) => combo.Text = current; // a word the player typed
            }
            return combo;
        }
        // The name the game shows. Its discoverer may have renamed it - the new
        // name is on the game's servers, not in the save - so the player can type
        // it (2026-10-15: a renamed planet didn't match until they landed, when
        // the save's "On Planet (...)" line taught it). Empty = the original name.
        string original = UniverseInfo.GeneratedPlanetName(system, planet.PlanetIndex);
        string indexKey = planet.PlanetIndex.ToString(CultureInfo.InvariantCulture);
        string currentName = SystemNotesService.Get(system)?.PlanetNames.TryGetValue(indexKey, out var named) == true ? named : "";
        var nameBox = new TextBox
        {
            Header = "Name in game", Width = 360, HorizontalAlignment = HorizontalAlignment.Left,
            Text = currentName == original ? "" : currentName, PlaceholderText = original,
        };
        var nameHint = DimLine($"Original name: {original}. If whoever discovered it renamed it, type the name the game shows - ArtifactX also learns it when you land there.");

        var sentinelsBox = Choice("Sentinels", Words("Sentinels"), details.Sentinels);
        var floraBox = Choice("Flora", Words("Life"), details.Flora);
        var faunaBox = Choice("Fauna", Words("Life"), details.Fauna);

        var weatherWords = _planetWords.Where(w => w.Group == "Weather").Select(w => w.Word)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(w => w, StringComparer.CurrentCultureIgnoreCase).ToList();
        var weatherBox = new AutoSuggestBox { Header = "Weather", Width = 360, HorizontalAlignment = HorizontalAlignment.Left, PlaceholderText = "Start typing, e.g. Painfully Hot Rain" };
        var weatherHint = new StackPanel();
        void CheckWeather()
        {
            weatherHint.Children.Clear();
            string typed = weatherBox.Text.Trim();
            if (Suggestion(typed, weatherWords) is string better)
                weatherHint.Children.Add(DidYouMean(typed, "weathers", better, () => { weatherBox.Text = better; CheckWeather(); }));
        }
        weatherBox.Loaded += (_, _) => { weatherBox.Text = details.Weather; CheckWeather(); };
        weatherBox.LostFocus += (_, _) => CheckWeather();
        weatherBox.SuggestionChosen += (_, _) => DispatcherQueue.TryEnqueue(CheckWeather);
        weatherBox.TextChanged += (s, e) =>
        {
            if (e.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            s.ItemsSource = s.Text.Length == 0 ? null
                : weatherWords.Where(w => w.Contains(s.Text, StringComparison.CurrentCultureIgnoreCase)).Take(20).ToList();
        };

        var waterBox = new ComboBox { Header = "Water", Width = 160, ItemsSource = new[] { "Not recorded", "Yes", "No" } };
        waterBox.SelectedIndex = PlanetWaterService.Get(system, planet.PlanetIndex) switch { true => 1, false => 2, _ => 0 };

        // Resources: chips + a picker of the game's raw resources.
        var resources = details.Resources.ToList();
        var chips = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6 };
        var resourceHints = new StackPanel { Spacing = 4 };
        void RenderChips()
        {
            // "Coblat isn't one of the game's resources - did you mean Cobalt?"
            // (typos are kept as typed, so they'd never match a search; 2026-10-14).
            resourceHints.Children.Clear();
            foreach (var r in resources.ToList())
                if (Suggestion(r, _substances) is string better)
                    resourceHints.Children.Add(DidYouMean(r, "resources", better, () =>
                    {
                        int at = resources.IndexOf(r);
                        if (at < 0) return;
                        if (resources.Contains(better, StringComparer.OrdinalIgnoreCase)) resources.RemoveAt(at);
                        else resources[at] = better;
                        RenderChips();
                    }));
            chips.Children.Clear();
            foreach (var r in resources.ToList())
            {
                var chip = new Button { Content = $"{r}  ✕", Padding = new Thickness(8, 2, 8, 2) };
                ToolTipService.SetToolTip(chip, $"Remove {r}");
                chip.Click += (_, _) => { resources.Remove(r); RenderChips(); };
                chips.Children.Add(chip);
            }
        }
        RenderChips();
        var resourceBox = new AutoSuggestBox { Header = "Resources", Width = 360, HorizontalAlignment = HorizontalAlignment.Left, PlaceholderText = "Type a resource and press Enter, e.g. Paraffinium" };
        resourceBox.TextChanged += (s, e) =>
        {
            if (e.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            s.ItemsSource = s.Text.Length == 0 ? null
                : _substances.Where(n => n.Contains(s.Text, StringComparison.CurrentCultureIgnoreCase)).Take(15).ToList();
        };
        void AddResource(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return;
            string name = _substances.FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase)) ?? text;
            if (!resources.Contains(name, StringComparer.OrdinalIgnoreCase)) resources.Add(name);
            resourceBox.Text = "";
            resourceBox.ItemsSource = null;
            RenderChips();
        }
        // A click on a suggestion adds it at once (it only filled the box
        // before, so it took a second click - 2026-10-13). Arrow keys also
        // raise SuggestionChosen while moving through the list, so a choice
        // straight after an arrow key is just a highlight; Enter adds it.
        var lastArrow = DateTime.MinValue;
        resourceBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down) lastArrow = DateTime.UtcNow;
        };
        resourceBox.SuggestionChosen += (_, e) =>
        {
            if (DateTime.UtcNow - lastArrow < TimeSpan.FromMilliseconds(400) || e.SelectedItem is not string chosen) return;
            // After the box has finished filling itself in, or it puts the text back.
            DispatcherQueue.TryEnqueue(() => AddResource(chosen));
        };
        // Enter (or a click that also submits): adding is duplicate-safe.
        resourceBox.QuerySubmitted += (_, e) => AddResource(e.ChosenSuggestion as string ?? e.QueryText);

        // Notes.
        var notes = details.Notes.ToList();
        var notesPanel = new StackPanel { Spacing = 4 };
        void RenderNotesList()
        {
            notesPanel.Children.Clear();
            foreach (var n in notes.ToList())
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = $"• {n}", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
                var remove = new Button { Content = "Remove", Padding = new Thickness(8, 2, 8, 2) };
                remove.Click += (_, _) => { notes.Remove(n); RenderNotesList(); };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                notesPanel.Children.Add(row);
            }
        }
        RenderNotesList();
        var newNote = new TextBox { PlaceholderText = "e.g. crashed freighter, Vile Brood, great base spot", Width = 360 };
        void AddNote()
        {
            string text = newNote.Text.Trim();
            if (text.Length == 0) return;
            notes.Add(text);
            newNote.Text = "";
            RenderNotesList();
        }
        newNote.KeyDown += (_, e) =>
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            e.Handled = true;
            AddNote();
        };
        var addNote = new Button { Content = "Add note" };
        addNote.Click += (_, _) => AddNote();

        var row1 = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 12, VerticalSpacing = 10 };
        row1.Children.Add(sentinelsBox);
        row1.Children.Add(floraBox);
        row1.Children.Add(faunaBox);
        row1.Children.Add(waterBox);
        var content = new StackPanel { Spacing = 12, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
        content.Children.Add(new TextBlock
        {
            Text = "As the game's discovery screen shows them. Pick the game's word, or type your own. Kept by ArtifactX on this PC, never in your save.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.75,
        });
        content.Children.Add(nameBox);
        content.Children.Add(nameHint);
        content.Children.Add(weatherBox);
        content.Children.Add(weatherHint);
        content.Children.Add(row1);
        content.Children.Add(resourceBox);
        content.Children.Add(chips);
        content.Children.Add(resourceHints);
        content.Children.Add(new TextBlock { Text = "NOTES", Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"], Margin = new Thickness(0, 6, 0, 0) });
        content.Children.Add(notesPanel);
        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        addRow.Children.Add(newNote);
        addRow.Children.Add(addNote);
        content.Children.Add(addRow);

        // The box shows its selection as its text; clearing the text clears the field.
        string Picked(ComboBox box) => WordOf((box.Text ?? "").Trim());
        void Commit()
        {
            AddNote(); // a note typed but not added yet still counts
            if (resourceBox.Text.Trim().Length > 0) AddResource(resourceBox.Text);
            PlanetDetailsService.Save(system, planet.PlanetIndex, new PlanetDetails
            {
                Weather = weatherBox.Text.Trim(),
                Sentinels = Picked(sentinelsBox),
                Flora = Picked(floraBox),
                Fauna = Picked(faunaBox),
                Resources = resources.ToList(),
                Notes = notes.ToList(),
            });
            string typedName = nameBox.Text.Trim();
            var nameNote = SystemNotesService.Get(system) ?? new SystemNote();
            nameNote.PlanetNames.TryGetValue(indexKey, out var oldName);
            if (typedName.Length > 0 && typedName != oldName)
            {
                nameNote.PlanetNames[indexKey] = typedName;
                SystemNotesService.Save(system, nameNote);
            }
            else if (typedName.Length == 0 && !string.IsNullOrEmpty(oldName))
            {
                nameNote.PlanetNames.Remove(indexKey);
                SystemNotesService.Save(system, nameNote);
            }
            bool? newWater = waterBox.SelectedIndex switch { 1 => true, 2 => false, _ => null };
            if (newWater != PlanetWaterService.Get(system, planet.PlanetIndex))
                PlanetWaterService.Set(system, planet.PlanetIndex, newWater);
        }
        return (content, Commit);
    }

    /// <summary>The closest of the game's words to a typed one that isn't one of
    /// them ("Coblat" -> "Cobalt"), or null when it matches or nothing is close.</summary>
    private static string? Suggestion(string typed, IReadOnlyList<string> words)
    {
        if (typed.Length < 3 || words.Count == 0) return null;
        if (words.Any(w => string.Equals(w, typed, StringComparison.OrdinalIgnoreCase))) return null;
        string a = typed.ToLowerInvariant();
        var best = words.Select(w => (Word: w, Distance: Distance(a, w.ToLowerInvariant())))
            .OrderBy(x => x.Distance).ThenBy(x => Math.Abs(x.Word.Length - typed.Length)).First();
        // Up to 1 edit per 4 letters (2 for "Coblat"), so real one-offs aren't flagged.
        return best.Distance <= Math.Max(1, typed.Length / 4) ? best.Word : null;
    }

    /// <summary>Edit distance where swapping two neighbouring letters counts as one.</summary>
    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }

    private static UIElement DidYouMean(string typed, string what, string better, Action use)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock
        {
            Text = $"\"{typed}\" isn't one of the game's {what} - did you mean {better}?",
            FontSize = 12,
            Foreground = ThemeBrush("ThemeWarningBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });
        var fix = new HyperlinkButton { Content = $"Use {better}", Padding = new Thickness(0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        fix.Click += (_, _) => use();
        row.Children.Add(fix);
        return row;
    }

    // ---------------------------------------------------------------- one planet

    private KnownPlanet? _openPlanet;
    private double _overviewOffset;
    private Border? _saveFactsCard, _fishCard;

    /// <summary>Shows one planet in full, in place of the overview (user,
    /// 2026-10-13: more room for each planet). The overview is only hidden, so
    /// Back returns to it as it was - scroll, filters and all.</summary>
    private void OpenPlanet(KnownPlanet planet)
    {
        if (_openPlanet is null) _overviewOffset = MainScroll.VerticalOffset;
        _openPlanet = planet;
        RenderPlanetPage();
        MainScroll.Visibility = Visibility.Collapsed;
        DetailScroll.Visibility = Visibility.Visible;
        DetailScroll.ChangeView(null, 0, null, disableAnimation: true);
    }

    private void ClosePlanet()
    {
        _openPlanet = null;
        _saveFactsCard = _fishCard = null;
        DetailScroll.Visibility = Visibility.Collapsed;
        MainScroll.Visibility = Visibility.Visible;
        BuildCurrentSystem(_lastCounts); // show anything edited on the planet's page
        RefreshNoteFilters();
        ApplyFilters();
        DispatcherQueue.TryEnqueue(() => MainScroll.ChangeView(null, _overviewOffset, null, disableAnimation: true));
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PlanetRowViewModel row) OpenPlanet(row.Planet);
    }

    /// <summary>The other planets of the open planet's system, for Prev / Next.</summary>
    private List<KnownPlanet> SystemPlanets(KnownPlanet planet) =>
        (planet.SystemAddress == _currentSystem ? _thisSave : _allPlanets)
            .Where(p => p.SystemAddress == planet.SystemAddress)
            .GroupBy(p => p.PlanetIndex).Select(g => g.First())
            .OrderBy(p => p.PlanetIndex).ToList();

    private Border DetailCard(string title, UIElement body, double width = 460) => new()
    {
        Width = width,
        Padding = new Thickness(18, 14, 18, 16),
        Background = ThemeBrush("ThemePanelAltBrush"),
        BorderBrush = ThemeBrush("ThemeLineBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = ThemeResources.CardCornerRadius,
        VerticalAlignment = VerticalAlignment.Top,
        Child = new StackPanel
        {
            Spacing = 10,
            Children = { new TextBlock { Text = title, Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] }, body },
        },
    };

    private void RenderPlanetPage()
    {
        if (_openPlanet is not KnownPlanet planet) return;
        DetailPanel.Children.Clear();
        bool inThisSystem = planet.SystemAddress == _currentSystem;
        bool here = inThisSystem && planet.PlanetIndex == _currentPlanet;

        // Back + Prev / Next.
        var bar = new Grid { ColumnSpacing = 8 };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var back = new Button { Content = "‹  Back to Planets", Style = (Style)Application.Current.Resources["ThemeAccentButtonStyle"] };
        back.Click += (_, _) => ClosePlanet();
        bar.Children.Add(back);
        var siblings = SystemPlanets(planet);
        int at = siblings.FindIndex(p => p.PlanetIndex == planet.PlanetIndex);
        if (siblings.Count > 1 && at >= 0)
        {
            var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var prev = new Button { Content = "‹ Previous planet", IsEnabled = at > 0 };
            prev.Click += (_, _) => OpenPlanet(siblings[at - 1]);
            var next = new Button { Content = "Next planet ›", IsEnabled = at < siblings.Count - 1 };
            next.Click += (_, _) => OpenPlanet(siblings[at + 1]);
            nav.Children.Add(prev);
            nav.Children.Add(next);
            Grid.SetColumn(nav, 2);
            bar.Children.Add(nav);
        }
        DetailPanel.Children.Add(bar);

        // Header.
        var header = new Grid { ColumnSpacing = 20 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var drawing = PlanetDrawing(planet, 76, here);
        drawing.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(drawing);
        var titles = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        bool isMoon = BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex);
        titles.Children.Add(new TextBlock
        {
            Text = $"{(isMoon ? "MOON" : $"PLANET {planet.PlanetIndex}")} OF {UniverseInfo.SystemName(planet.SystemAddress).ToUpperInvariant()}",
            Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"],
        });
        titles.Children.Add(new TextBlock { Text = UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex), Style = (Style)Application.Current.Resources["ThemePageTitleStyle"] });
        titles.Children.Add(new TextBlock
        {
            Text = string.Join(" · ", new[] { planet.HasRings ? $"{planet.TypeName} with rings" : planet.TypeName, GalaxyLabel(planet.Galaxy), UniverseInfo.Compact(planet.SystemAddress), planet.Source }.Where(x => x.Length > 0)),
            Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
            TextWrapping = TextWrapping.Wrap,
        });
        if (here) titles.Children.Add(HereBadge(page: true));
        var glyphRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Margin = new Thickness(0, 6, 0, 0) };
        var glyphTiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var glyph in GlyphsFor(planet.PortalCode))
            glyphTiles.Children.Add(GlyphTile(glyph, 34));
        glyphRow.Children.Add(new ArtifactX.WinUI3.Controls.GlyphStrip { Content = glyphTiles, Code = planet.PortalCode, VerticalAlignment = VerticalAlignment.Center });
        var copy = new Button { Content = "Copy", Tag = planet.PortalCode, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        copy.Click += CopyCode_Click;
        glyphRow.Children.Add(copy);
        titles.Children.Add(glyphRow);
        Grid.SetColumn(titles, 1);
        header.Children.Add(titles);
        DetailPanel.Children.Add(header);

        var cards = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 18, VerticalSpacing = 18 };
        var left = new StackPanel { Spacing = 18, VerticalAlignment = VerticalAlignment.Top };

        // What the save knows (live: refreshed by RefreshPlanetPageLive).
        _saveFactsCard = DetailCard("FROM YOUR SAVE", SaveFacts(planet, inThisSystem));
        left.Children.Add(_saveFactsCard);

        // Your details: edited right here.
        var (editor, commit) = BuildDetailsEditor(planet);
        var status = new TextBlock { FontSize = 12, Foreground = ThemeBrush("ThemeDoneBrush"), VerticalAlignment = VerticalAlignment.Center };
        var saveBtn = new Button { Content = "Save details", Style = (Style)Application.Current.Resources["ThemeAccentButtonStyle"] };
        saveBtn.Click += (_, _) =>
        {
            commit();
            status.Text = $"Saved {DateTime.Now:t}";
            RefreshPlanetPageLive();
        };
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 6, 0, 0) };
        saveRow.Children.Add(saveBtn);
        saveRow.Children.Add(status);
        editor.Children.Add(saveRow);
        var detailsCard = DetailCard("YOUR DETAILS", editor, 640);

        _fishCard = DetailCard("FISH", FishBody(planet));
        _fishCard.Visibility = ((StackPanel)((StackPanel)_fishCard.Child).Children[1]).Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        left.Children.Add(_fishCard);

        // Your Finds on this planet (Finds page).
        var finds = FindsService.All.Where(f => f.PortalCode == planet.PortalCode && f.Galaxy == planet.Galaxy).ToList();
        if (finds.Count > 0)
        {
            var list = new StackPanel { Spacing = 6 };
            foreach (var find in finds)
                list.Children.Add(new TextBlock
                {
                    Text = string.Join(" · ", new[] { find.Type, find.Title, find.Class, find.Location }.Where(x => x.Length > 0)),
                    FontSize = 13,
                    Foreground = ThemeBrush("ThemeTextBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                });
            left.Children.Add(DetailCard("YOUR FINDS HERE", list));
        }

        cards.Children.Add(left);
        cards.Children.Add(detailsCard);
        DetailPanel.Children.Add(cards);
    }

    private UIElement SaveFacts(KnownPlanet planet, bool inThisSystem)
    {
        var facts = new FactTable();
        _lastCounts.TryGetValue(planet.Address, out var tally);
        facts.Add("You found", inThisSystem || tally is not null ? DiscoveredText(tally, planet.Address) : "");
        facts.Add("Visited", VisitText(ReadPlanetStats(planet.Address)));
        // Bases with where they are on the planet (2026-10-14) - the save
        // stores each base's position from the planet's centre.
        string At(JObject b) => NmsPlanetCoordinates.OfBase(b) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "";
        var mine = (_snapshot.Bases ?? new JArray()).OfType<JObject>()
            .Where(b => b["peI"]?["DPp"]?.Value<string>() == "HomePlanetBase" && S.ParsePackedAddress(b["oZw"]) == planet.Address)
            .Select(b => $"{(b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base")}{At(b)}")
            .ToList();
        if (mine.Count == 0 && inThisSystem && _baseNames.TryGetValue(planet.PlanetIndex, out var baseNames)) mine = baseNames.ToList();
        if (mine.Count > 0) facts.Add(mine.Count == 1 ? "Your base" : "Your bases", string.Join(", ", mine));
        // Other players' bases on this planet: the save keeps them as
        // ExternalPlanetBase entries, and the game counts them in its "& 1 other
        // bases" (Ippianes III, 2026-10-14: "Аванпост Isvil" by Bihram).
        var others = (_snapshot.Bases ?? new JArray()).OfType<JObject>()
            .Where(b => b["peI"]?["DPp"]?.Value<string>() == "ExternalPlanetBase"
                && S.ParsePackedAddress(b["oZw"]) == planet.Address)
            .Select(b => $"{(b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed")} ({b["3?K"]?["V?:"]?.Value<string>() ?? "another player"}){At(b)}")
            .ToList();
        if (others.Count > 0) facts.Add("Others' bases", string.Join(", ", others));
        facts.Add("Waypoints", tally is { Waypoints: > 0 } w ? w.Waypoints.ToString(CultureInfo.CurrentCulture) : "");
        facts.Add("Found by", planet.DiscoveredHere ? "You" : planet.Discoverer);
        facts.Add("Found on", FormatDate(planet.DiscoveredUnixSeconds));
        facts.Add("Portal code", planet.PortalCode);
        return facts.Grid;
    }

    private StackPanel FishBody(KnownPlanet planet)
    {
        var holder = new StackPanel();
        AddPlanetFish(holder, planet, full: true);
        // AddPlanetFish adds its own titled section; use its contents only.
        if (holder.Children.FirstOrDefault() is StackPanel section)
        {
            holder.Children.Clear();
            var body = new StackPanel { Spacing = 6 };
            foreach (var child in section.Children.Skip(2).ToList()) // skip the rule and heading
            {
                section.Children.Remove(child);
                body.Children.Add(child);
            }
            return body;
        }
        return holder;
    }

    /// <summary>A new save while a planet is open: refresh what the save says
    /// about it (discoveries, time, fish) without touching the details editor.</summary>
    private void RefreshPlanetPageLive()
    {
        if (_openPlanet is not KnownPlanet planet || _saveFactsCard is null || _fishCard is null) return;
        var latest = _thisSave.FirstOrDefault(p => p.Address == planet.Address);
        if (latest is not null) _openPlanet = planet = latest;
        ((StackPanel)_saveFactsCard.Child).Children[1] = SaveFacts(planet, planet.SystemAddress == _currentSystem);
        var fish = FishBody(planet);
        ((StackPanel)_fishCard.Child).Children[1] = fish;
        _fishCard.Visibility = fish.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
        if (note.Ships.Count > 0) parts.Add($"Ships: {string.Join(", ", note.Ships)}");
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

    private ComboBox[] ChoiceFilters => new[]
    {
        StarFilterBox, EconomyFilterBox, WealthFilterBox, ConflictFilterBox, RaceFilterBox,
        ShipFilterBox, SentinelsFilterBox, FloraFilterBox, FaunaFilterBox, ResourceFilterBox,
    };
    private CheckBox[] CheckFilters => new[] { RingsOnlyBox, MineOnlyBox, WaterFilterBox, DissonantFilterBox };

    /// <summary>Which filters narrow the list right now (sort, save slots and
    /// hyperdrive range are display choices, not filters).</summary>
    private IEnumerable<bool> ActiveFilters() =>
        new[]
        {
            SearchBox.Text.Trim().Length > 0,
            TypeFilterBox.SelectedIndex > 0,
            GalaxyFilterBox.SelectedIndex > 0,
            BodiesFilterBox.SelectedIndex > 0,
        }
        .Concat(ChoiceFilters.Select(b => ChosenValue(b) != ""))
        .Concat(CheckFilters.Select(c => c.IsChecked == true));

    /// <summary>Back to the whole list in one click (user, 2026-10-13: a search
    /// left in the box quietly hid nearly every planet).</summary>
    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        _suppressFilterEvents = true;
        SearchBox.Text = "";
        _searchTimer?.Stop();
        TypeFilterBox.SelectedIndex = 0;
        GalaxyFilterBox.SelectedIndex = 0;
        BodiesFilterBox.SelectedIndex = 0;
        foreach (var box in ChoiceFilters) if (box.Items.Count > 0) box.SelectedIndex = 0;
        foreach (var check in CheckFilters) check.IsChecked = false;
        _suppressFilterEvents = false;
        ApplyFilters();
    }

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
        string sentinels = ChosenValue(SentinelsFilterBox), flora = ChosenValue(FloraFilterBox);
        string fauna = ChosenValue(FaunaFilterBox), resource = ChosenValue(ResourceFilterBox);
        string search = SearchBox.Text.Trim();
        string ship = ChosenValue(ShipFilterBox);

        var filtered = _allPlanets
            .Where(p => type < 0 || p.BiomeIndex == type)
            .Where(p => galaxy < 0 || p.Galaxy == galaxy)
            .Where(p => !ringsOnly || p.HasRings)
            .Where(p => !mineOnly || p.DiscoveredHere)
            .Where(p => !noteFilter
                || ((star == "" || StarOf(p.SystemAddress) == star)
                    && (economy == "" || UniverseInfo.Economy(p.SystemAddress) == economy)
                    && (wealth == "" || UniverseInfo.Wealth(p.SystemAddress) == wealth)
                    && (conflict == "" || UniverseInfo.Conflict(p.SystemAddress) == conflict)
                    && (race == "" || RaceOf(p.SystemAddress) == race)
                    && (!water || SystemNotesService.Get(p.SystemAddress)?.Water == true)
                    && (!dissonant || SystemNotesService.Get(p.SystemAddress)?.Dissonant == true)))
            .Where(p => (sentinels == "" && flora == "" && fauna == "" && resource == "")
                || PlanetDetailsService.Get(p.SystemAddress, p.PlanetIndex) is { } d
                    && (sentinels == "" || LevelOf("Sentinels", d.Sentinels) == sentinels)
                    && (flora == "" || LevelOf("Life", d.Flora) == flora)
                    && (fauna == "" || LevelOf("Life", d.Fauna) == fauna)
                    && (resource == "" || d.Resources.Contains(resource, StringComparer.OrdinalIgnoreCase)))
            .Where(p => ship == "" || SystemNotesService.Get(p.SystemAddress)?.Ships.Contains(ship, StringComparer.OrdinalIgnoreCase) == true)
            .Where(p => search.Length == 0 || SearchText(p).Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .Where(p => bodies switch
            {
                1 => !BodyKindsService.IsMoon(p.SystemAddress, p.PlanetIndex),
                2 => BodyKindsService.IsMoon(p.SystemAddress, p.PlanetIndex),
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
        int active = ActiveFilters().Count(on => on);
        ClearFiltersBtn.Content = $"Clear filters ({active})";
        ClearFiltersBtn.Visibility = active > 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultCountTxt.Text = $"{matches.Count:N0} of {_allPlanets.Count:N0} planets";
    }

    private PlanetRowViewModel ToRow(KnownPlanet planet, double? distance)
    {
        string notes = string.Join(" · ", new[]
        {
            DetailsSummary(PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex), PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex)),
            NoteSummary(SystemNotesService.Get(planet.SystemAddress)),
        }.Where(s => s.Length > 0));
        return new()
        {
        NotesLabel = notes,
        NotesVisibility = notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
        DistanceLabel = DistanceText(planet, distance),
        TypeLabel = UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex),
        FactsLabel = UniverseInfo.Compact(planet.SystemAddress),
        Swatch = new SolidColorBrush(ParseColour(NmsPlanets.TypeColour(planet.BiomeIndex))),
        RingVisibility = planet.HasRings ? Visibility.Visible : Visibility.Collapsed,
        LocationLabel = string.Join(" · ", new[]
        {
            planet.HasRings ? $"{planet.TypeName} with rings" : planet.TypeName,
            BodyLabel(planet), GalaxyLabel(planet.Galaxy), planet.Source,
        }.Where(s => s.Length > 0)),
        PortalCode = planet.PortalCode,
        Planet = planet,
        Glyphs = GlyphsFor(planet.PortalCode),
        DiscoveredLabel = $"{(planet.DiscoveredHere ? "You" : planet.Discoverer)} · {FormatDate(planet.DiscoveredUnixSeconds)}",
        };
    }

    private static string BodyLabel(KnownPlanet planet)
    {
        var note = SystemNotesService.Get(planet.SystemAddress);
        string body = BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex) ? "moon" : $"planet {planet.PlanetIndex}";
        return $"{body} of {UniverseInfo.SystemName(planet.SystemAddress)}";
    }

    // System facts for the finder: the player's note when they took one, else the generator.
    private static string StarOf(ulong system) =>
        SystemNotesService.Get(system)?.StarColour is { Length: > 0 } s ? s : UniverseInfo.For(system).StarLabel;

    private static string RaceOf(ulong system) =>
        SystemNotesService.Get(system)?.Race is { Length: > 0 } r ? r : UniverseInfo.For(system).RaceLabel;

    private string DistanceText(KnownPlanet planet, double? distance)
    {
        string core = $"{NmsPlanets.DistanceToCoreLightYears(planet.Address):N0} ly from the galaxy core";
        if (_currentSystem is ulong here && planet.SystemAddress == here) return $"In the system you're in · {core}";
        string away = distance switch
        {
            null => "In a different galaxy from you",
            < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
            double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
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
        DecoderGlyphStrip.Code = ok ? code : "";
        if (!ok) return;

        foreach (var glyph in GlyphsFor(code))
            DecoderGlyphsPanel.Children.Add(GlyphTile(glyph, 40));

        bool fromCoordinates = DecoderInputBox.Text.Contains(':');
        DecoderDetailTxt.Text = fromCoordinates
            ? $"Portal code {code} (planet 1 - coordinates don't include a planet)"
            : $"Portal code {code} · galactic coordinates {NmsPortalCode.ToCoordinates(code)} · planet {code[0]}";
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var rows = ResultsList.ItemsSource as List<PlanetRowViewModel> ?? new();
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX planets",
            new[] { "Type", "Location", "Portal code", "Discovered", "Distance", "Your notes" },
            rows.Select(r => new[] { r.TypeLabel, r.LocationLabel, r.PortalCode, r.DiscoveredLabel, r.DistanceLabel, r.NotesLabel }));
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }
}
