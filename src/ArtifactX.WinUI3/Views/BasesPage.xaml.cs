using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Read-only list of the player's planet bases (the save's base list, F?0,
/// entries whose type peI.DPp is "HomePlanetBase") with portal addresses.
/// Each base's oZw is a packed address whose top nibble is the planet. Planet
/// type comes from the matching discovery record when the save has one.
/// </summary>
public sealed partial class BasesPage : Page
{
    private static readonly string[] BasesPath = { "vLc", "6f=", "F?0" };
    private List<BaseRowViewModel> _rows = new();
    private readonly LiveSaveWatcher _live;
    private string _lastKey = "";

    public BasesPage()
    {
        InitializeComponent();
        SortBox.SelectedIndex = 0;
        _live = new LiveSaveWatcher(DispatcherQueue);
        _live.SnapshotRead += OnLiveSnapshot;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        JumpPlanner.RangeChanged += OnRangeChanged;
        Unloaded += (_, _) =>
        {
            JumpPlanner.RangeChanged -= OnRangeChanged;
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            _live.SnapshotRead -= OnLiveSnapshot;
            _live.Dispose();
        };
        _ = LoadAsync();
    }

    private void OnLiveSnapshot(PlanetSnapshot snapshot)
    {
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        Build(snapshot);
    }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        await CatalogService.WarmCacheAsync(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));
        _lastKey = "";
        Build(LiveSaveWatcher.FromSession());
        _live.Start();
    }

    /// <summary>Builds the rows from a snapshot; skipped when neither your
    /// position nor the base list changed, so the list doesn't reset.</summary>
    private PlanetSnapshot? _lastSnapshot;

    /// <summary>Rebuilds the rows so their distances pick up the new jump estimate.</summary>
    private void OnRangeChanged()
    {
        if (_lastSnapshot is null) return;
        _lastKey = "";
        Build(_lastSnapshot);
    }

    private void Build(PlanetSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        ulong? here = S.ReadSystemAddress(snapshot.Universe);
        bool objects = _showObjects;
        // Each tab says how many it holds, so placed objects aren't missed.
        BasesTab.Text = $"Bases ({(snapshot.Bases ?? new JArray()).OfType<JObject>().Count(x => x["peI"]?["DPp"]?.Value<string>() == "HomePlanetBase"):N0})";
        ObjectsTab.Text = $"Placed objects ({snapshot.LooseObjects?.Count ?? 0:N0})";
        string key = $"{objects}|{here}|{snapshot.Bases?.Count}|{snapshot.LooseObjects?.Count}|{NmsBuildCounts.Total(snapshot.Bases, snapshot.LooseObjects)}|{string.Join(",", snapshot.Bases?.OfType<JObject>().Select(b => b["NKm"]?.Value<string>()) ?? Enumerable.Empty<string?>())}";
        if (key == _lastKey) return;
        _lastKey = key;

        // Parts placed: the game caps them per base and in total (2026-10-18).
        var counts = NmsBuildCounts.PerBase(snapshot.Bases).ToDictionary(c => c.Base, c => c.Parts);
        int total = NmsBuildCounts.Total(snapshot.Bases, snapshot.LooseObjects);
        PartsTotalTxt.Text = $"Building parts placed: {NmsBuildCounts.GlobalLabel(total)} before the game's construction limit";
        PartsTotalTxt.Foreground = new SolidColorBrush(LevelColour(NmsBuildCounts.GlobalLevel(total)));
        PartsBreakdownTxt.Text = NmsBuildCounts.Breakdown(snapshot.Bases, snapshot.LooseObjects);

        var planets = NmsPlanets.ReadAll(snapshot.Discoveries)
            .GroupBy(p => p.Address).ToDictionary(g => g.Key, g => g.First());

        _rows = new();
        if (objects)
        {
            BuildPlacedObjects(snapshot, here, planets);
            Apply();
            return;
        }
        if (snapshot.Bases is JArray bases)
        {
            foreach (var b in bases.OfType<JObject>())
            {
                if (b["peI"]?["DPp"]?.Value<string>() != "HomePlanetBase") continue;
                if (S.ParsePackedAddress(b["oZw"]) is not ulong address) continue;

                int planetIndex = (int)((address >> 52) & 0xF);
                int galaxy = S.GalaxyNumber(address);
                planets.TryGetValue(address, out var planet);
                string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base";
                string type = planet is null ? "Planet" : planet.HasRings ? $"{planet.TypeName} · rings" : planet.TypeName;
                string galaxyName = NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
                double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = S.PortalAddress(address, planetIndex);

                _rows.Add(new BaseRowViewModel
                {
                    Name = name,
                    // Where on the planet, as the game's scanner shows it (2026-10-14).
                    Detail = $"{UniverseInfo.PlanetName(address, planetIndex)} ({type})"
                        + (NmsPlanetCoordinates.OfBase(b) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "")
                        + $" · {UniverseInfo.SystemName(address)} · {galaxyName}"
                        ,
                    PartsLabel = counts.TryGetValue(b, out int parts) ? NmsBuildCounts.UploadLabel(parts) : "",
                    PartsBrush = new SolidColorBrush(LevelColour(NmsBuildCounts.UploadLevel(parts))),
                    Facts = UniverseInfo.Compact(address),
                    Swatch = new SolidColorBrush(ParseColour(planet is null ? "#777777" : NmsPlanets.TypeColour(planet.BiomeIndex))),
                    PortalCode = code,
                    Glyphs = GlyphsFor(code),
                    DistanceLabel = DistanceText(here, address, distance),
                    AreaLabel = AreaText(address, planetIndex, galaxy, code) is { Length: > 0 } area ? area
                        : "Nothing recorded about this planet yet - add its resources, sentinels and notes on the Planets page.",
                    AreaOpacity = AreaText(address, planetIndex, galaxy, code).Length > 0 ? 1.0 : 0.55,
                    SortDistance = distance ?? double.MaxValue,
                    Galaxy = galaxy,
                    Base = b,
                    Picture = Picture(address, name),
                });
            }
        }
        Apply();
    }

    /// <summary>Everything placed outside a base (the save's c5s list): beacons, signal
    /// boosters, save points, refiners, garages, decor - easily forgotten, and each one counts
    /// toward the game's building cap (user, 2026-10-18). Each has its type, its planet's
    /// address, its position from the planet's centre (so the scanner's coordinates) and the
    /// time it was placed.</summary>
    private void BuildPlacedObjects(PlanetSnapshot snapshot, ulong? here, Dictionary<ulong, KnownPlanet> planets)
    {
        var placed = (snapshot.LooseObjects ?? new JArray()).OfType<JObject>().ToList();
        var ids = placed.Select(o => o["r<7"]?.Value<string>()).Where(id => id is not null).Distinct().ToList();
        if (!_warmedObjects && ids.Any(id => CatalogService.TryGet(id) is null))
        {
            // Names and icons for these ids aren't loaded yet: load them, then build again.
            _warmedObjects = true;
            _ = CatalogService.WarmCacheAsync(ids).ContinueWith(_ => DispatcherQueue.TryEnqueue(() =>
            {
                _lastKey = "";
                if (_lastSnapshot is not null) Build(_lastSnapshot);
            }), System.Threading.Tasks.TaskScheduler.Default);
        }
        foreach (var o in placed)
        {
            if (S.ParsePackedAddress(o["oZw"]) is not ulong address) continue;
            string id = o["r<7"]?.Value<string>() ?? "";
            var entry = CatalogService.TryGet(id);
            string name = entry?.DisplayName is { Length: > 0 } n ? n
                : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id.TrimStart('^').Replace('_', ' ').ToLowerInvariant());
            int planetIndex = (int)((address >> 52) & 0xF);
            int galaxy = S.GalaxyNumber(address);
            planets.TryGetValue(address, out var planet);
            string type = planet is null ? "Planet" : planet.TypeName;
            string galaxyName = NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
            double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
            string code = S.PortalAddress(address, planetIndex);
            long placedAt = o["b1:"]?.Value<long>() ?? 0;
            _rows.Add(new BaseRowViewModel
            {
                Name = name,
                Detail = $"{UniverseInfo.PlanetName(address, planetIndex)} ({type})"
                    + (NmsPlanetCoordinates.OfBase(o) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "")
                    + $" · {UniverseInfo.SystemName(address)} · {galaxyName}",
                Icon = entry?.Icon,
                Facts = placedAt > 0 ? $"Placed {DateTimeOffset.FromUnixTimeSeconds(placedAt).LocalDateTime:d}" : "",
                Swatch = new SolidColorBrush(ParseColour(planet is null ? "#777777" : NmsPlanets.TypeColour(planet.BiomeIndex))),
                PortalCode = code,
                Glyphs = GlyphsFor(code),
                DistanceLabel = DistanceText(here, address, distance),
                AreaLabel = AreaText(address, planetIndex, galaxy, code),
                AreaOpacity = 1.0,
                SortDistance = distance ?? double.MaxValue,
                Galaxy = galaxy,
            });
        }
    }

    private bool _warmedObjects;

    private bool _showObjects;

    // Base pictures from the game's cache, by planet address; loaded once, then the cards rebuilt.
    private readonly Dictionary<ulong, Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap?> _pictures = new();
    private readonly HashSet<ulong> _picturesLoading = new();

    private ImageSource? Picture(ulong address, string name)
    {
        if (_pictures.TryGetValue(address, out var picture)) return picture;
        if (_picturesLoading.Add(address))
            _ = LoadPictureAsync(address, name);
        return null;
    }

    private async System.Threading.Tasks.Task LoadPictureAsync(ulong address, string name)
    {
        _pictures[address] = await GameScreenshotCache.LoadAsync(SaveSessionManager.ActivePlatformFolder, address, name);
        if (_pictures[address] is null || _lastSnapshot is null) return;
        _lastKey = "";
        Build(_lastSnapshot);
    }

    private void ShowBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        _showObjects = sender.SelectedItem == ObjectsTab;
        if (IntroTxt is null) return;
        IntroTxt.Text = _showObjects
            ? "Everything you've built outside a base - beacons, signal boosters, save points, refiners, garages - with where it is, so you can go back for it. Each one counts toward the game's building limit. This page only reads your save."
            : "Every planet base in this save, with the portal address to dial to get back to it. Handy for old bases you've lost track of. This page only reads your save.";
        _lastKey = "";
        if (_lastSnapshot is not null) Build(_lastSnapshot);
    }

    /// <summary>"Resources: Paraffinium, Copper, Silver, Ancient Bones · Infrequent
    /// sentinels · Painfully Hot Rain · Water · 13 kinds of fish caught here ·
    /// 2 finds · Ships seen: Solar" - from the planet's details (PlanetDetailsService),
    /// water, recorded catches, Finds and the system's notes. Empty when nothing
    /// is recorded. User request 2026-10-14: see what an area has without
    /// needing its glyphs (bases are a teleport away).</summary>
    private static string AreaText(ulong address, int planet, int galaxy, string code)
    {
        ulong system = S.ToSystemAddress(address);
        var parts = new List<string>();
        var d = PlanetDetailsService.Get(system, planet);
        if (d is not null)
        {
            if (d.Resources.Count > 0) parts.Add($"Resources: {string.Join(", ", d.Resources)}");
            if (d.Sentinels != "") parts.Add($"{d.Sentinels} sentinels");
            if (d.Weather != "") parts.Add(d.Weather);
            if (d.Flora != "") parts.Add($"Flora: {d.Flora}");
            if (d.Fauna != "") parts.Add($"Fauna: {d.Fauna}");
        }
        if (PlanetWaterService.Get(system, planet) == true) parts.Add("Water");
        int fish = FishCatchService.CaughtOn(system, planet).Count;
        if (fish > 0) parts.Add(fish == 1 ? "1 kind of fish caught here" : $"{fish} kinds of fish caught here");
        int finds = FindsService.All.Count(f => f.PortalCode == code && f.Galaxy == galaxy);
        if (finds > 0) parts.Add(finds == 1 ? "1 find" : $"{finds} finds");
        if (SystemNotesService.Get(system)?.Ships is { Count: > 0 } ships) parts.Add($"Ships seen: {string.Join(", ", ships)}");
        if (d is not null) parts.AddRange(d.Notes);
        return string.Join(" · ", parts);
    }

    /// <summary>"About 1,131 ly from you · 742,100 ly from the galaxy core".
    /// Distances to another galaxy can't be measured, so only the core distance shows.</summary>
    private static string DistanceText(ulong? here, ulong address, double? distance)
    {
        string core = $"{NmsPlanets.DistanceToCoreLightYears(address):N0} ly from the galaxy core";
        string fromYou = (here is ulong h && S.ToSystemAddress(address) == h) ? "In the system you're in"
            : distance switch
            {
                null => "In a different galaxy from you",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };
        return $"{fromYou} · {core}";
    }

    private void Apply()
    {
        string search = SearchBox.Text.Trim();
        // Names and what's recorded about the area ("copper" finds bases near copper).
        var shown = _rows.Where(r => search.Length == 0 || r.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || (r.AreaOpacity > 0.9 && r.AreaLabel.Contains(search, StringComparison.CurrentCultureIgnoreCase)));
        shown = SortBox.SelectedIndex switch
        {
            1 => shown.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => shown.OrderBy(r => r.Galaxy).ThenBy(r => r.SortDistance),
            _ => shown.OrderBy(r => r.SortDistance).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        var list = shown.ToList();
        // Bases and placed objects have their own card layouts.
        BasesList.Visibility = _showObjects ? Visibility.Collapsed : Visibility.Visible;
        ObjectsList.Visibility = _showObjects ? Visibility.Visible : Visibility.Collapsed;
        if (_showObjects) ObjectsList.ItemsSource = list; else BasesList.ItemsSource = list;
        bool objects = _showObjects;
        CountTxt.Text = _rows.Count == 0 ? (objects ? "Nothing placed outside a base" : "No planet bases in this save")
            : $"{list.Count} of {_rows.Count} {(objects ? "placed objects" : "bases")}";
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var rows = (_showObjects ? ObjectsList.ItemsSource : BasesList.ItemsSource) as List<BaseRowViewModel> ?? new();
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX bases",
            new[] { "Base", "Planet", "Portal code", "Distance" },
            rows.Select(r => new[] { r.Name, r.Detail, r.PortalCode, r.DistanceLabel }));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Apply();

    /// <summary>A base clicked: its parts, one row per kind ("Wooden Floor · 600"), biggest first
    /// (user request 2026-10-18) - what to thin out when a base is near its limit.</summary>
    private async void BasesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BaseRowViewModel { Base: JObject b } row) return;
        var objects = (b["@ZJ"] as JArray ?? new JArray()).OfType<JObject>()
            .Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => id != "^BASE_FLAG").ToList();
        var groups = objects.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        await CatalogService.WarmCacheAsync(groups.Select(g => g.Key));

        DetailTitleTxt.Text = row.Name;
        DetailSubTxt.Text = row.Detail;
        DetailPartsTxt.Text = row.PartsLabel;
        DetailPartsTxt.Foreground = row.PartsBrush;
        DetailKindsTxt.Text = $"{groups.Count:N0} kinds of part";
        DetailPicture.Source = row.Picture;
        DetailPictureBorder.Visibility = row.PictureVisibility;
        DetailFactsTxt.Text = row.Facts;
        DetailAreaTxt.Text = row.AreaLabel;
        DetailAreaTxt.Opacity = row.AreaOpacity;
        DetailDistanceTxt.Text = row.DistanceLabel;
        DetailGlyphStrip.Code = row.PortalCode;
        DetailGlyphs.ItemsSource = row.Glyphs;
        DetailCopyBtn.Tag = row.PortalCode;
        ToolTipService.SetToolTip(DetailCopyBtn, row.PortalCode);
        DetailList.Children.Clear();
        foreach (var g in groups)
        {
            var entry = CatalogService.TryGet(g.Key);
            string name = entry?.DisplayName is { Length: > 0 } n ? n
                : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(g.Key.TrimStart('^').Replace('_', ' ').ToLowerInvariant());
            var line = new Grid { ColumnSpacing = 12, Padding = new Thickness(10, 6, 14, 6), Width = 360 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (entry?.Icon is { } icon) line.Children.Add(new Image { Source = icon, Width = 32, Height = 32 });
            var label = new TextBlock { Text = name, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources["ThemeTextBrush"] };
            ToolTipService.SetToolTip(label, name);
            Grid.SetColumn(label, 1);
            line.Children.Add(label);
            var count = new TextBlock { Text = g.Count().ToString("N0", CultureInfo.CurrentCulture), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.Resources["ThemeAccentBrush"] };
            Grid.SetColumn(count, 2);
            line.Children.Add(count);
            DetailList.Children.Add(new Border
            {
                Child = line, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1),
                Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"], BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
            });
        }
        ListArea.Visibility = Visibility.Collapsed;
        DetailArea.Visibility = Visibility.Visible;
    }

    private void DetailBack_Click(object sender, RoutedEventArgs e)
    {
        DetailArea.Visibility = Visibility.Collapsed;
        ListArea.Visibility = Visibility.Visible;
    }

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BasesList is not null) Apply();
    }

    private static List<ImageSource> GlyphsFor(string code)
    {
        var images = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }

    /// <summary>Green with room to spare, yellow when close, red when over (user request 2026-10-18).</summary>
    private static Windows.UI.Color LevelColour(int level) => level switch
    {
        2 => Windows.UI.Color.FromArgb(255, 0xFF, 0x5A, 0x5A),
        1 => Windows.UI.Color.FromArgb(255, 0xF5, 0xC5, 0x42),
        _ => Windows.UI.Color.FromArgb(255, 0x3D, 0xDC, 0x84),
    };

    private static Windows.UI.Color ParseColour(string hex)
    {
        uint rgb = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Windows.UI.Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }
}
