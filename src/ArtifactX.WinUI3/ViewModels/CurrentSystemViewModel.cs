using ArtifactX.Core.Collections;
using System.Collections.ObjectModel;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A line of the SYSTEM DATA box: gold when the game showed it (system notes, read off
/// the screen or typed in), grey for the generator's answer where nothing was recorded.</summary>
public sealed record SystemDataRow(string Label, string Value, bool Recorded)
{
    public Brush ValueBrush => ThemeResources.Brush(Recorded ? "ThemeWarningBrush" : "ThemeTextDimBrush");
}

/// <summary>A planet name read off the game screen that ArtifactX doesn't know (usually one
/// someone renamed): the player says which body it is, or that it isn't in this system.</summary>
public sealed partial class UnmatchedBodyViewModel : ObservableObject
{
    public UnmatchedBodyViewModel(UnmatchedBodiesService.Held held, IReadOnlyList<ChoiceItem> bodies)
    {
        Held = held;
        Bodies = bodies;
        Name = held.Name + (held.Moon is bool moon ? (moon ? " (moon)" : " (planet)") : "");
    }

    public UnmatchedBodiesService.Held Held { get; }
    public string Name { get; }
    public IReadOnlyList<ChoiceItem> Bodies { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial ChoiceItem? Selected { get; set; }

    public bool CanSave => Selected is not null;
}

/// <summary>
/// Current System: the system you're in (an orrery, or the game's own picture of it), what the
/// save, the game's generator and your notes say about it, what its station sells, and a card
/// per known planet. Follows the game's saves without redrawing what didn't change: a new
/// system rebuilds everything (SystemArrived), landing elsewhere rebuilds the cards, and new
/// discoveries only update their numbers.
/// </summary>
public sealed partial class CurrentSystemViewModel : ObservableObject
{
    private const string CompactKey = "PlanetsSystemCompact";
    private const string HelpHiddenKey = "PlanetsLiveHelpHidden";

    private PlanetSnapshot _snapshot = new(null, null, null);
    private List<KnownPlanet> _planets = new();
    private Dictionary<ulong, PlanetDiscoveryCounts> _counts = new();
    private Dictionary<string, (uint Count, float Largest)> _caught = new(StringComparer.OrdinalIgnoreCase);
    private PlanetVocabulary? _vocabulary;
    private bool _descriptorsLoaded;
    private byte[]? _pictureBytes;
    private ulong? _pictureSystem;
    private int _pictureVersion;

    public CurrentSystemViewModel()
    {
        Notes.MoonsChanged += Build;
        Tiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(NoPlanets));
            OnPropertyChanged(nameof(PlanetSectionSummary));
        };
    }

    public StationMultiToolsViewModel StationTools { get; } = new();
    public StationMarketViewModel Market { get; } = new();
    public SystemNotesViewModel Notes { get; } = new();
    public SystemStarshipsViewModel Starships { get; } = new();

    [ObservableProperty] public partial ulong? SystemAddress { get; private set; }
    [ObservableProperty] public partial int CurrentPlanet { get; private set; }
    [ObservableProperty] public partial string Heading { get; private set; } = "";
    [ObservableProperty] public partial string Sub { get; private set; } = "";
    /// <summary>The region's system total from the game's own generator, and how many of them you've visited.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasRegionSystems))] public partial string RegionSystems { get; private set; } = "";
    public bool HasRegionSystems => RegionSystems.Length > 0;
    [ObservableProperty] public partial string Facts { get; private set; } = "";
    [ObservableProperty] public partial string Station { get; private set; } = "";
    [ObservableProperty] public partial string SystemCode { get; private set; } = "";
    [ObservableProperty] public partial bool IsLoading { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<KnownPlanet> Bodies { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<SystemDataRow> SystemData { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<UnmatchedBodyViewModel> Unmatched { get; private set; } = [];
    public ObservableCollection<PlanetTileViewModel> Tiles { get; } = new();
    [ObservableProperty] public partial ImageSource? SystemPicture { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureLabel), nameof(CanCapture))]
    public partial bool IsCapturing { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFullDetails), nameof(CompactLabel), nameof(FrameHeight), nameof(FrameWidth), nameof(BodyPadding))]
    public partial bool IsCompact { get; private set; } = SettingsStore.Get(SettingsStore.Scope.Profile, CompactKey) == "1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HelpHidden))]
    public partial bool IsHelpVisible { get; private set; } = !AppSettingsService.GetFlag(HelpHiddenKey);

    public bool HelpHidden => !IsHelpVisible;
    public bool HasStation => Station.Length > 0;
    public bool HasPicture => SystemPicture is not null;
    public bool ShowOrbits => SystemPicture is null;
    public bool HasUnmatched => Unmatched.Count > 0;
    public bool NoPlanets => Tiles.Count == 0;
    public string PlanetSectionSummary => Tiles.Count == 0 ? "No planets yet" : $"{Tiles.Count} bodies";
    public string CaptureLabel => IsCapturing ? "Capturing..." : "Capture system";
    public bool CanCapture => !IsCapturing;

    /// <summary>"Show less": a small picture, the name, the facts line and the glyphs - the System
    /// Data box, explanation and text code hidden - so the planet cards show without scrolling
    /// (2026-10-18). Remembered.</summary>
    public bool ShowFullDetails => !IsCompact;
    public string CompactLabel => IsCompact ? "Show more" : "Show less";

    /// <summary>Square for the orbit drawing, about 3:2 for the game's picture; smaller with "Show less".</summary>
    public double FrameHeight => IsCompact ? 150 : 360;
    public double FrameWidth => HasPicture ? FrameHeight * 1.5 : FrameHeight;
    public Thickness BodyPadding => IsCompact ? new Thickness(20, 12, 20, 14) : new Thickness(20, 16, 20, 20);

    partial void OnStationChanged(string value) => OnPropertyChanged(nameof(HasStation));
    partial void OnUnmatchedChanged(IReadOnlyList<UnmatchedBodyViewModel> value) => OnPropertyChanged(nameof(HasUnmatched));
    partial void OnSystemPictureChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(HasPicture));
        OnPropertyChanged(nameof(ShowOrbits));
        OnPropertyChanged(nameof(FrameWidth));
    }

    /// <summary>Live follow saw a new system: "SYSTEM CHANGE DETECTED" and its name.</summary>
    public event Action<string>? SystemArrived;

    public void ToggleCompact()
    {
        IsCompact = !IsCompact;
        SettingsStore.Set(SettingsStore.Scope.Profile, CompactKey, IsCompact ? "1" : "0");
    }

    public void SetHelpVisible(bool visible)
    {
        AppSettingsService.SetFlag(HelpHiddenKey, !visible);
        IsHelpVisible = visible;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await PortalGlyphs.WarmAsync();
            _vocabulary = await PlanetVocabulary.LoadAsync();
            if (!_descriptorsLoaded)
            {
                var descriptors = await CatalogService.GetSystemDescriptorsAsync();
                SystemNotesService.SetDescriptors(descriptors);
                Notes.FillChoices(descriptors);
                _descriptorsLoaded = true;
            }
            Show(await LiveSaveWatcher.GetLatestAsync(), live: false);
        }
        finally { IsLoading = false; }
    }

    /// <summary>Shows a save, redrawing only what changed.</summary>
    public void Show(PlanetSnapshot snapshot, bool live)
    {
        ulong? system = S.ReadSystemAddress(snapshot.Universe);
        int planet = snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        var planets = NmsPlanets.ReadAll(snapshot.Discoveries);
        bool systemChanged = !live || system != SystemAddress;
        bool layoutChanged = systemChanged || planet != CurrentPlanet
            || planets.Count(p => p.SystemAddress == system) != _planets.Count(p => p.SystemAddress == system);

        if (live && systemChanged) Notes.AddNote(); // keep anything typed for the old system

        var caught = FishCatchService.ReadCaught(snapshot.Fishing);
        if (caught.Count != _caught.Count || caught.Any(c => !_caught.TryGetValue(c.Key, out var was) || was.Count != c.Value.Count))
            layoutChanged = true;
        _caught = caught;

        _snapshot = snapshot;
        _planets = planets;
        SystemAddress = system;
        CurrentPlanet = planet;
        _ = StationTools.UpdateAsync(system, snapshot);
        _counts = NmsPlanets.CountDiscoveries(snapshot.Discoveries);
        // The name the save's "where you saved" line gives for where you are now.
        if (SystemNameLearner.Learn(system, planet, snapshot.Summary) && !systemChanged)
        {
            layoutChanged = true;
            Notes.Load(system, SystemPlanets());
        }

        if (layoutChanged)
        {
            Build();
            if (systemChanged) Notes.Load(system, SystemPlanets());
            if (live && systemChanged) SystemArrived?.Invoke($"{Heading} · {Sub}");
        }
        else
        {
            UpdateTiles();
        }
    }

    /// <summary>Something recorded about the system or its planets changed (screen recording, the
    /// player's notes, a planet's page): show it, keeping anything half-typed in the notes.</summary>
    public void Refresh(bool notes)
    {
        if (notes) Notes.Load(SystemAddress, SystemPlanets());
        Build();
    }

    public void ShowMarket()
    {
        if (SystemAddress is ulong system) Market.Show(system);
    }

    public async Task RefreshPictureAsync()
    {
        int version = ++_pictureVersion;
        if (SystemAddress is not ulong system)
        {
            _pictureSystem = null;
            _pictureBytes = null;
            SystemPicture = null;
            return;
        }
        // Read off the UI thread, but retain the existing image when the bytes did not change.
        byte[]? bytes = await Task.Run(() => ScreenCaptureLogService.SystemPicture(system) is string path && File.Exists(path) ? File.ReadAllBytes(path) : null);
        if (SystemAddress != system || version != _pictureVersion) return;
        if (bytes is null)
        {
            _pictureSystem = system;
            _pictureBytes = null;
            SystemPicture = null;
            return;
        }
        if (_pictureSystem == system && _pictureBytes is not null && bytes.AsSpan().SequenceEqual(_pictureBytes)) return;
        var image = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        await image.SetSourceAsync(stream.AsRandomAccessStream());
        if (SystemAddress != system || version != _pictureVersion) return;
        _pictureSystem = system;
        _pictureBytes = bytes;
        SystemPicture = image;
    }

    /// <summary>The game's Discoveries page picture of this system, taken now (screen recording's manual capture).</summary>
    public async Task<CaptureResult> CaptureSystemAsync()
    {
        if (IsCapturing) return new CaptureResult(false, "A capture is already running.");
        IsCapturing = true;
        try { return await ScreenCaptureLogService.CaptureNowAsync(SystemAddress, forceSystemPicture: true); }
        finally
        {
            IsCapturing = false;
            await RefreshPictureAsync();
        }
    }

    public void AssignUnmatched(UnmatchedBodyViewModel row)
    {
        if (row.Selected?.Value is string value && int.TryParse(value, out int body)) UnmatchedBodiesService.Assign(row.Held, body);
    }

    public void DismissUnmatched(UnmatchedBodyViewModel row) => UnmatchedBodiesService.Dismiss(row.Held);

    private List<KnownPlanet> SystemPlanets() =>
        SystemAddress is ulong system ? _planets.Where(p => p.SystemAddress == system).OrderBy(p => p.PlanetIndex).ToList() : new();

    private void Build()
    {
        if (SystemAddress is not ulong system)
        {
            Heading = "Unknown location";
            Sub = Facts = Station = SystemCode = "";
            Bodies = [];
            SystemData = [];
            Unmatched = [];
            Tiles.Clear();
            ++_pictureVersion;
            _pictureBytes = null;
            _pictureSystem = null;
            SystemPicture = null;
            return;
        }

        var note = SystemNotesService.Get(system);
        var (markerStation, bases) = ReadMarkers(system);
        var planets = SystemPlanets();
        int galaxy = S.GalaxyNumber(system);
        Heading = UniverseInfo.SystemName(system);
        Facts = UniverseInfo.Summary(system);
        var facts = UniverseInfo.For(system);
        bool isUncharted = facts.Uncharted || note?.Race == "None (uncharted)";
        string station = markerStation.Length > 0 ? markerStation : note?.StationName ?? "";
        Station = isUncharted ? "Space station: None (uncharted)"
            : facts.Abandoned ? (station.Length > 0 ? $"Space station: {station} (Abandoned)" : $"Space station: {Heading} Station (Abandoned)")
            : station.Length > 0 ? $"Space station: {station}" : $"Space station: {Heading} Station";

        int moons = planets.Count(p => BodyKindsService.IsMoon(system, p.PlanetIndex));
        string bodies = moons > 0
            ? $"{planets.Count - moons} planet{(planets.Count - moons == 1 ? "" : "s")} · {moons} moon{(moons == 1 ? "" : "s")}"
            : $"{planets.Count} known planet{(planets.Count == 1 ? "" : "s")}";
        string where = CurrentPlanet == 0 ? "you're in space"
            : BodyKindsService.IsMoon(system, CurrentPlanet) ? "you're on a moon"
            : $"you're on planet {CurrentPlanet}";
        string galaxyText = $"{UniverseInfo.Region(system)} · {NmsGalaxyNames.ForNumber(galaxy) ?? "Galaxy"} ({galaxy})";
        Sub = (planets.Count == 0 ? $"{galaxyText} · no planets discovered here yet" : $"{galaxyText} · {bodies} · {where}")
            + $" · about {NmsPlanets.DistanceToCoreLightYears(system):N0} ly from the galaxy core";
        SystemCode = S.PortalAddress(system, 0);
        _ = UpdateRegionSystemsAsync(system);
        if (!Bodies.SequenceEqual(planets)) Bodies = planets;
        var data = BuildSystemData(system, note, station);
        if (!SystemData.SequenceEqual(data)) SystemData = data;
        Market.Show(system);
        ulong portalCode = UniverseInfo.PortalCode(system, 0);
        var unmatched = BuildUnmatched(system, planets);
        if (Unmatched.Count != unmatched.Count || Unmatched.Zip(unmatched).Any(pair =>
            pair.First.Held != pair.Second.Held || !pair.First.Bodies.SequenceEqual(pair.Second.Bodies)))
            Unmatched = unmatched;

        // Every body, discovered or not: the ones nobody has found yet with the type the
        // game's own generator gives them (PlanetTypesService).
        if (!PlanetTypesService.IsReady) _ = PlanetTypesService.EnsureLoadedAsync();
        bool hasCorruptedPlanet = PlanetTypesService.SystemHasCorruptedPlanet(system);
        _ = Starships.LoadAsync(system, portalCode, facts, hasCorruptedPlanet);

        var known = planets.Select(p => p.PlanetIndex).ToHashSet();
        var allBodies = planets.Concat(PlanetTypesService.Undiscovered(system, known)).OrderBy(p => p.PlanetIndex).ToList();

        var tiles = allBodies.Select(p =>
        {
            var fish = _vocabulary is null ? null
                : PlanetFishViewModel.For(p, _vocabulary.Fish, _caught, id => PlanetText.FishName(id, _vocabulary.ItemNames));
            return new PlanetTileViewModel(p, p.PlanetIndex == CurrentPlanet, bases.GetValueOrDefault(p.PlanetIndex) ?? [], fish,
                _vocabulary?.ItemNames ?? new Dictionary<string, string>());
        }).ToList();
        ObservableCollectionSync.Apply(Tiles, tiles, tile => tile.Planet.Address,
            (current, next) => current.HasSamePresentation(next));
        UpdateTiles();
        _ = RefreshPictureAsync();
    }

    /// <summary>
    /// "Noturbo Spur holds 539 star systems and 65 purple systems · you've visited 165 of them".
    /// Visited = distinct systems of this region in the save's discovery records.
    /// </summary>
    private async Task UpdateRegionSystemsAsync(ulong system)
    {
        try
        {
            var count = await RegionSystemsService.GetAsync(system);
            if (SystemAddress != system) return;
            if (count is null) { RegionSystems = ""; return; }
            string region = UniverseInfo.Region(system);
            if (count.Ordinary == 0)
            {
                RegionSystems = $"{region} lies inside the galactic centre and holds no star systems";
                return;
            }
            ulong key = RegionStarGeneration.RegionKey(system);
            int visited = NmsPlanets.ReadSystems(_snapshot.Discoveries).Count(s => RegionStarGeneration.RegionKey(s.SystemAddress) == key);
            RegionSystems = string.Create(CultureInfo.CurrentCulture,
                $"{region} holds {count.Ordinary:N0} star systems and {count.Purple} purple systems · you've visited {visited:N0} of them");
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"[CurrentSystem] Region system count unavailable: {error.Message}");
        }
    }

    private void UpdateTiles()
    {
        foreach (var tile in Tiles)
        {
            _counts.TryGetValue(tile.Planet.Address, out var tally);
            tile.Update(tally, NmsPlanets.PlanetStats(_snapshot.StatGroups, tile.Planet.Address), _snapshot);
        }
    }

    /// <summary>Named map markers in a system: its space station, and your bases by planet number.</summary>
    private (string Station, Dictionary<int, List<string>> Bases) ReadMarkers(ulong system)
    {
        string station = "";
        var bases = new Dictionary<int, List<string>>();
        foreach (var marker in _snapshot.Markers?.OfType<JObject>() ?? [])
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

    /// <summary>The SYSTEM DATA box, laid out like the game's own pop-up lines (2026-10-17).</summary>
    private static List<SystemDataRow> BuildSystemData(ulong system, SystemNote? note, string station)
    {
        var f = UniverseInfo.For(system);
        var rows = new List<SystemDataRow>();
        void Row(string label, string? recorded, string estimate) =>
            rows.Add(new(label, !string.IsNullOrEmpty(recorded) ? recorded : estimate, !string.IsNullOrEmpty(recorded)));
        int galaxy = S.GalaxyNumber(system);
        Row("Galaxy", NmsGalaxyNames.ForNumber(galaxy) is { } g ? $"{g} Galaxy" : $"Galaxy {galaxy}", "");
        string Bodies(int planets, int moons) => $"{planets} Planet{(planets == 1 ? "" : "s")} // {moons} Moon{(moons == 1 ? "" : "s")}";
        int total = f.PlanetCount + f.MoonCount;
        Row("Celestial Bodies", note?.Moons is int m ? Bodies(total - m, m) : null, Bodies(f.PlanetCount, f.MoonCount));
        Row("Dominant Lifeform", note?.Race is { Length: > 0 } r ? r : null, f.RaceLabel);
        // The game itself shows "Data Unavailable" for an uncharted system's economy and conflict.
        bool uncharted = note?.Race == "None (uncharted)";
        string? economy = note?.Economy is { Length: > 0 } e ? (note.Wealth is { Length: > 0 } w ? $"{e} // {w}" : e)
            : uncharted ? "- Data Unavailable -" : null;
        Row("Economy", economy, f.Uncharted ? "None" : $"{f.EconomyLabel} // {f.WealthLabel}");
        Row("Conflict level", note?.Conflict is { Length: > 0 } c ? c : uncharted ? "- Data Unavailable -" : null, f.Uncharted ? "None" : f.ConflictLabel);
        string defaultStation = $"{UniverseInfo.SystemName(system)} Station";
        if (f.Uncharted || uncharted)
            Row("Space Station", uncharted ? "None (uncharted)" : null, "None (uncharted)");
        else if (f.Abandoned)
            Row("Space Station", station.Length > 0 ? $"{station} (Abandoned)" : null, $"{defaultStation} (Abandoned)");
        else
            Row("Space Station", station.Length > 0 ? station : null, defaultStation);
        Row("Star", note?.StarClass is { Length: > 0 } cls ? $"{cls} ({note.StarColour})" : note?.StarColour is { Length: > 0 } sc ? sc : null, f.StarLabel);
        if (note?.Water == true) Row("Water", "Yes", "");
        if (note?.Dissonant == true) Row("Dissonance", "Detected", "");
        // The game says it in the conflict line ("Pirate Controlled"): gold once that was read.
        bool piratesSeen = note?.Conflict?.Contains("Pirate", StringComparison.OrdinalIgnoreCase) == true;
        if (f.Pirate || piratesSeen) Row("Pirates", piratesSeen ? "Pirate-controlled" : null, "Pirate-controlled");
        return rows;
    }

    private static List<UnmatchedBodyViewModel> BuildUnmatched(ulong system, List<KnownPlanet> planets)
    {
        // The bodies no name or details are known for (all of them when every one is).
        var open = UnmatchedBodiesService.UnaccountedBodies(system);
        var bodies = (open.Count > 0 ? open : Enumerable.Range(1, UniverseInfo.For(system).PlanetCount + UniverseInfo.For(system).MoonCount).ToList()).Select(b =>
        {
            string kind = BodyKindsService.IsMoon(system, b) ? "Moon" : "Planet";
            var planet = planets.FirstOrDefault(p => p.PlanetIndex == b);
            string type = planet is not null ? $" · {planet.Classification(PlanetDetailsService.Get(system, b)?.Descriptor)}" : "";
            return new ChoiceItem($"{kind} {b} · {UniverseInfo.PlanetName(system, b)}{type}", b.ToString(CultureInfo.InvariantCulture));
        }).ToList();
        return UnmatchedBodiesService.For(system).Select(h => new UnmatchedBodyViewModel(h, bodies)).ToList();
    }
}
