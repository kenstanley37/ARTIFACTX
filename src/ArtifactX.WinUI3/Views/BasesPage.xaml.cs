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
        string key = $"{here}|{snapshot.Bases?.Count}|{string.Join(",", snapshot.Bases?.OfType<JObject>().Select(b => b["NKm"]?.Value<string>()) ?? Enumerable.Empty<string?>())}";
        if (key == _lastKey) return;
        _lastKey = key;

        var planets = NmsPlanets.ReadAll(snapshot.Discoveries)
            .GroupBy(p => p.Address).ToDictionary(g => g.Key, g => g.First());

        _rows = new();
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
                    Detail = $"{UniverseInfo.PlanetName(address, planetIndex)} ({type}) · {UniverseInfo.SystemName(address)} · {galaxyName}",
                    Facts = UniverseInfo.Compact(address),
                    Swatch = new SolidColorBrush(ParseColour(planet is null ? "#777777" : NmsPlanets.TypeColour(planet.BiomeIndex))),
                    PortalCode = code,
                    Glyphs = GlyphsFor(code),
                    DistanceLabel = DistanceText(here, address, distance),
                    SortDistance = distance ?? double.MaxValue,
                    Galaxy = galaxy,
                });
            }
        }
        Apply();
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
        var shown = _rows.Where(r => search.Length == 0 || r.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        shown = SortBox.SelectedIndex switch
        {
            1 => shown.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => shown.OrderBy(r => r.Galaxy).ThenBy(r => r.SortDistance),
            _ => shown.OrderBy(r => r.SortDistance).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        var list = shown.ToList();
        BasesList.ItemsSource = list;
        CountTxt.Text = _rows.Count == 0 ? "No planet bases in this save" : $"{list.Count} of {_rows.Count} bases";
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var rows = BasesList.ItemsSource as List<BaseRowViewModel> ?? new();
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX bases",
            new[] { "Base", "Planet", "Portal code", "Distance" },
            rows.Select(r => new[] { r.Name, r.Detail, r.PortalCode, r.DistanceLabel }));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Apply();

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BasesList is not null) Apply();
    }

    private static List<ImageSource> GlyphsFor(string code)
    {
        var images = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }

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
