using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Read-only ranking of every known star system by distance to its galaxy's
/// core (NmsPlanets.ReadSystems / DistanceToCoreLightYears). Every discovery
/// record carries its system's full address, so this covers all discovered
/// systems, not just bases. Built to take crowd-sourced systems later (see the
/// project's API plan) - rows only need an address plus optional notes.
/// </summary>
public sealed partial class NearCorePage : Page
{
    private List<KnownSystem> _thisSave = new();
    private List<KnownSystem> _otherSaves = new();
    private ulong? _here;
    private bool _suppressFilterEvents;
    private int _loadVersion;
    private readonly LiveSaveWatcher _live;
    private string? _lastName;

    public NearCorePage()
    {
        InitializeComponent();
        _live = new LiveSaveWatcher(DispatcherQueue);
        _live.SnapshotRead += OnLiveSnapshot;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            _live.SnapshotRead -= OnLiveSnapshot;
            _live.Dispose();
        };
        _ = LoadAsync();
    }

    /// <summary>The game saved: pick up newly discovered systems and your new
    /// position. The list is only rebuilt when something actually changed, so
    /// an unrelated save doesn't reset your scroll position.</summary>
    private void OnLiveSnapshot(PlanetSnapshot snapshot)
    {
        var systems = NmsPlanets.ReadSystems(snapshot.Discoveries);
        ulong? here = S.ReadSystemAddress(snapshot.Universe);
        // Names may have just been learned (SystemNameLearner runs in the watcher),
        // so refresh whenever the current system has a new name too.
        string? name = here is ulong h ? SystemNotesService.Get(h)?.SystemName : null;
        bool changed = here != _here || systems.Count != _thisSave.Count || name != _lastName;
        _lastName = name;
        _thisSave = systems;
        _here = here;
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        if (changed) Refresh();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    private async Task LoadAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        int version = ++_loadVersion;
        await CatalogService.WarmCacheAsync(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));

        _thisSave = NmsPlanets.ReadSystems(SaveSessionManager.GetValue(S.DiscoveriesPath) as JArray);
        var universe = SaveSessionManager.GetValue(S.CurrentUniverseAddressPath);
        _here = S.ReadSystemAddress(universe);
        SystemNameLearner.Learn(_here, universe?["oZw"]?["jsv"]?.Value<int>() ?? 0,
            SaveSessionManager.GetValue(S.LocationDescriptionPath)?.Value<string>());
        _otherSaves = new();
        Refresh();
        _live.Start();

        if (AllSlotsBox.IsChecked == true) await LoadOtherSlotsAsync(version);
    }

    private async Task LoadOtherSlotsAsync(int version)
    {
        if (_otherSaves.Count > 0) return;
        if (SaveSessionManager.ActivePlatformFolder is not string folder || SaveSessionManager.ActiveSlot is not { } slot) return;

        CountTxt.Text = "Reading your other save slots...";
        var others = await KnownPlanetsService.ReadOtherSlotsSystemsAsync(folder, slot.SlotId);
        if (version != _loadVersion) return;
        _otherSaves = others;
        Refresh();
    }

    private List<KnownSystem> AllSystems() => AllSlotsBox.IsChecked == true
        ? _thisSave.Concat(_otherSaves)
            .GroupBy(s => s.SystemAddress)
            .Select(g => g.First() with
            {
                DiscoveredHere = g.Any(s => s.DiscoveredHere),
                PlanetCount = g.Max(s => s.PlanetCount),
            })
            .ToList()
        : _thisSave;

    private void Refresh()
    {
        var systems = AllSystems();

        _suppressFilterEvents = true;
        int previous = (GalaxyFilterBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? -1;
        GalaxyFilterBox.Items.Clear();
        GalaxyFilterBox.Items.Add(new ComboBoxItem { Content = $"All galaxies ({systems.Count})", Tag = -1 });
        foreach (var galaxy in systems.GroupBy(s => s.Galaxy).OrderBy(g => g.Key))
            GalaxyFilterBox.Items.Add(new ComboBoxItem { Content = $"{GalaxyLabel(galaxy.Key)} ({galaxy.Count()})", Tag = galaxy.Key });
        // Default to the galaxy you're in - core distance only matters within one galaxy.
        int wanted = previous >= 0 ? previous : _here is ulong h ? S.GalaxyNumber(h) : -1;
        GalaxyFilterBox.SelectedItem = GalaxyFilterBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == wanted) ?? GalaxyFilterBox.Items[0];
        _suppressFilterEvents = false;

        Apply(systems);
    }

    private void Apply(List<KnownSystem>? systems = null)
    {
        systems ??= AllSystems();
        int galaxy = (GalaxyFilterBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? -1;
        bool mineOnly = MineOnlyBox.IsChecked == true;

        var rows = systems
            .Where(s => galaxy < 0 || s.Galaxy == galaxy)
            .Where(s => !mineOnly || s.DiscoveredHere)
            .OrderBy(s => s.DistanceToCore)
            .ThenBy(s => _here is ulong h ? NmsPlanets.DistanceLightYears(h, s.SystemAddress) ?? double.MaxValue : 0)
            .Select((s, i) => ToRow(s, i + 1))
            .ToList();

        SystemsList.ItemsSource = rows;
        CountTxt.Text = $"{rows.Count:N0} systems";
    }

    private CoreSystemRowViewModel ToRow(KnownSystem system, int rank)
    {
        var note = SystemNotesService.Get(system.SystemAddress);
        string name = note?.SystemName is { Length: > 0 } n ? n : "Unnamed system";
        string planets = system.PlanetCount switch { 0 => "no planets recorded", 1 => "1 planet known", var c => $"{c} planets known" };
        string who = system.DiscoveredHere ? "found by you" : "found by another player";
        string source = system.Source.Length > 0 ? $" · {system.Source}" : "";

        string fromYou = _here is ulong here && here == system.SystemAddress ? "The system you're in"
            : (_here is ulong h ? NmsPlanets.DistanceLightYears(h, system.SystemAddress) : null) switch
            {
                null => "In a different galaxy from you",
                < 1 => "Under 400 ly from you",
                double ly => $"About {ly:N0} ly from you",
            };

        // Portal addresses need a planet glyph; planet 1 lands you in the system.
        string code = S.PortalAddress(system.SystemAddress, 1);
        string notes = NotesSummary(note);
        return new CoreSystemRowViewModel
        {
            Rank = $"#{rank}",
            Name = name,
            Detail = $"{GalaxyLabel(system.Galaxy)} · {planets} · {who}{source}",
            CoreDistance = $"{system.DistanceToCore:N0} ly",
            FromYou = fromYou,
            PortalCode = code,
            Glyphs = GlyphsFor(code),
            NotesLabel = notes,
            NotesVisibility = notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
    }

    private static string NotesSummary(SystemNote? note)
    {
        if (note is null) return "";
        var parts = new List<string>();
        if (note.StarColour != "") parts.Add($"{note.StarColour} star");
        if (note.Economy != "") parts.Add(note.Economy);
        if (note.Race != "") parts.Add(note.Race);
        if (note.Water) parts.Add("Water");
        if (note.Dissonant) parts.Add("Dissonant");
        return string.Join(" · ", parts);
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressFilterEvents) Apply();
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => Apply();

    private async void AllSlotsBox_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
        if (AllSlotsBox.IsChecked == true) await LoadOtherSlotsAsync(_loadVersion);
    }

    private static string GalaxyLabel(int galaxy) =>
        NmsGalaxyNames.ForNumber(galaxy) is string name ? $"{name} ({galaxy})" : $"Galaxy {galaxy}";

    private static List<ImageSource> GlyphsFor(string code)
    {
        var images = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }
}
