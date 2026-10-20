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
        _suppressFilterEvents = true;
        SortBox.SelectedIndex = 0;
        _suppressFilterEvents = false;
        _live = new LiveSaveWatcher(DispatcherQueue);
        _live.SnapshotRead += OnLiveSnapshot;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        JumpPlanner.RangeChanged += OnRangeChanged;
        SystemNotesService.Changed += OnSystemNotesChanged;
        SystemPageReader.Captured += OnSystemCaptured;
        Unloaded += (_, _) =>
        {
            JumpPlanner.RangeChanged -= OnRangeChanged;
            SystemNotesService.Changed -= OnSystemNotesChanged;
            SystemPageReader.Captured -= OnSystemCaptured;
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            _live.SnapshotRead -= OnLiveSnapshot;
            _live.Dispose();
        };
        _ = LoadAsync();
    }

    /// <summary>The game saved: pick up newly discovered systems and your new
    /// position. The list is only rebuilt when something actually changed, so
    /// an unrelated save doesn't reset your scroll position.</summary>
    private void OnSystemNotesChanged(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_thisSave.Count > 0) Apply();
    });

    private void OnSystemCaptured(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_thisSave.Count > 0) Apply();
    });

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

    // Slider stops for "within N ly of the core"; the last one means no limit.
    private static readonly double[] RangeSteps = { 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000, double.MaxValue };

    private double MaxCoreDistance => RangeSteps[Math.Clamp((int)Math.Round(RangeSlider.Value), 0, RangeSteps.Length - 1)];

    private double FromYou(KnownSystem s) =>
        _here is ulong h ? NmsPlanets.DistanceLightYears(h, s.SystemAddress) ?? double.MaxValue : double.MaxValue;

    private void Apply(List<KnownSystem>? systems = null)
    {
        systems ??= AllSystems();
        int galaxy = (GalaxyFilterBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? -1;
        bool mineOnly = MineOnlyBox.IsChecked == true;
        double maxCore = MaxCoreDistance;

        var filtered = systems
            .Where(s => galaxy < 0 || s.Galaxy == galaxy)
            .Where(s => !mineOnly || s.DiscoveredHere)
            .Where(s => s.DistanceToCore <= maxCore);

        // Ties are common near the core (the save positions systems in 400 ly
        // regions), so each sort falls back to the other distance.
        var ordered = SortBox.SelectedIndex switch
        {
            1 => filtered.OrderBy(FromYou).ThenBy(s => s.DistanceToCore),
            2 => filtered.OrderByDescending(s => s.PlanetCount).ThenBy(s => s.DistanceToCore),
            _ => filtered.OrderBy(s => s.DistanceToCore).ThenBy(FromYou),
        };
        var rows = ordered.Select((s, i) => ToRow(s, i + 1)).ToList();

        SystemsList.ItemsSource = rows;
        CountTxt.Text = $"{rows.Count:N0} systems";
        UpdateHereCard(systems);
    }

    /// <summary>The "YOU ARE HERE" strip above the list: your system and where
    /// it ranks among the systems shown.</summary>
    private void UpdateHereCard(List<KnownSystem> systems)
    {
        var here = _here is ulong h ? systems.FirstOrDefault(s => s.SystemAddress == h) : null;
        if (here is null)
        {
            HereCard.Visibility = Visibility.Collapsed;
            return;
        }

        string name = UniverseInfo.SystemName(here.SystemAddress);
        int rank = (SystemsList.ItemsSource as List<CoreSystemRowViewModel>)?.FindIndex(r => r.HereVisibility == Visibility.Visible) ?? -1;
        string rankText = rank >= 0 ? $" · #{rank + 1} in this list" : " · not in this list (check the filters)";
        HereTxt.Text = $"{name} · {here.DistanceToCore:N0} ly from the galaxy core{rankText}";
        HereCard.Visibility = Visibility.Visible;
    }

    private void ShowHere_Click(object sender, RoutedEventArgs e)
    {
        if (SystemsList.ItemsSource is not List<CoreSystemRowViewModel> rows) return;
        var row = rows.FirstOrDefault(r => r.HereVisibility == Visibility.Visible);
        if (row is not null) SystemsList.ScrollIntoView(row, ScrollIntoViewAlignment.Leading);
    }

    private void RangeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (RangeLabelTxt is null) return;
        double max = MaxCoreDistance;
        RangeLabelTxt.Text = max == double.MaxValue ? "Within any distance of the core" : $"Within {max:N0} ly of the core";
        if (!_suppressFilterEvents && SystemsList is not null) Apply();
    }

    private CoreSystemRowViewModel ToRow(KnownSystem system, int rank)
    {
        var note = SystemNotesService.Get(system.SystemAddress);
        string name = UniverseInfo.SystemName(system.SystemAddress);
        string planets = system.PlanetCount switch { 0 => "no planets recorded", 1 => "1 planet known", var c => $"{c} planets known" };
        string who = system.DiscoveredHere ? "found by you" : "found by another player";
        string source = system.Source.Length > 0 ? $" · {system.Source}" : "";

        bool isHere = _here is ulong hereAddress && hereAddress == system.SystemAddress;
        string fromYou = isHere ? "The system you're in"
            : (_here is ulong h ? NmsPlanets.DistanceLightYears(h, system.SystemAddress) : null) switch
            {
                null => "In a different galaxy from you",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };

        // Portal addresses need a planet glyph; planet 1 lands you in the system.
        string code = S.PortalAddress(system.SystemAddress, 1);
        // Star, race, economy, wealth and conflict (the game's generator; notes override),
        // then anything else the player noted.
        string extra = NotesSummary(note);
        string notes = UniverseInfo.Compact(system.SystemAddress) + (extra.Length > 0 ? $"  \u00B7  {extra}" : "");
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
            RowBorder = ThemeResources.Brush(isHere ? "ThemeAccentBrush" : "ThemeLineBrush"),
            RowBorderThickness = new Thickness(isHere ? 2 : 1),
            HereVisibility = isHere ? Visibility.Visible : Visibility.Collapsed,
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

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var rows = SystemsList.ItemsSource as List<CoreSystemRowViewModel> ?? new();
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX systems near the core",
            new[] { "Rank", "System", "Details", "To the galaxy core", "From you", "Portal code", "Your notes" },
            rows.Select(r => new[] { r.Rank, r.Name, r.Detail, r.CoreDistance, r.FromYou, r.PortalCode, r.NotesLabel }));
    }

    private void OnRangeChanged() => Apply();

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }
}
