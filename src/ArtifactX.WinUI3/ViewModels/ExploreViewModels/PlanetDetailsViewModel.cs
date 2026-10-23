using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>A label and its value ("Weather · Painfully Hot Rain").</summary>
public sealed record Fact(string Label, string Value);

/// <summary>A base on the planet: yours, or another player's (the save caches those).</summary>
public sealed record PlanetBase(string Name, string Owner);

/// <summary>
/// A planet's own page: what the save says about it, what the player recorded (and
/// screen recording read), its bases, finds, fish and creatures - kept up to date while
/// the game runs (Attach / Detach). Opened from Current System with a KnownPlanet.
/// </summary>
public sealed partial class PlanetDetailsViewModel : ObservableObject
{
    private const string FishCollapsedKey = "PlanetDetailsFishCollapsed";
    private const string NotRecorded = "Not recorded";

    private DispatcherQueue? _dispatcher;
    private PlanetSnapshot _snapshot = LiveSaveWatcher.FromSession();
    private PlanetVocabulary? _vocabulary;
    private List<KnownPlanet> _siblings = new();

    [ObservableProperty] public partial KnownPlanet? Planet { get; private set; }
    [ObservableProperty] public partial string Eyebrow { get; private set; } = "";
    [ObservableProperty] public partial string Title { get; private set; } = "";
    [ObservableProperty] public partial string OriginalName { get; private set; } = "";
    [ObservableProperty] public partial string Classification { get; private set; } = "";
    [ObservableProperty] public partial string SystemName { get; private set; } = "";
    [ObservableProperty] public partial string GalaxyLabel { get; private set; } = "";
    [ObservableProperty] public partial string Coordinates { get; private set; } = "";
    [ObservableProperty] public partial string PortalCode { get; private set; } = "";
    [ObservableProperty] public partial bool IsHere { get; private set; }
    [ObservableProperty] public partial string HereLabel { get; private set; } = "";
    [ObservableProperty] public partial ImageSource? SystemPicture { get; private set; }

    [ObservableProperty] public partial IReadOnlyList<Fact> Conditions { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<string> Resources { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<Fact> Recorded { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<Fact> SaveFacts { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<PlanetBase> Bases { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<string> Finds { get; private set; } = [];
    [ObservableProperty] public partial PlanetFishViewModel? Fish { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<CreatureCardViewModel> Creatures { get; private set; } = [];

    [ObservableProperty] public partial bool HasSiblings { get; private set; }
    [ObservableProperty] public partial bool CanGoPrevious { get; private set; }
    [ObservableProperty] public partial bool CanGoNext { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditLabel))]
    public partial bool IsEditing { get; private set; }

    [ObservableProperty] public partial PlanetDetailsEditorViewModel? Editor { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureLabel), nameof(CanCapture))]
    public partial bool IsCapturing { get; private set; }

    [ObservableProperty] public partial bool IsFishCollapsed { get; private set; } = AppSettingsService.GetFlag(FishCollapsedKey);

    public string EditLabel => IsEditing ? "Close editor" : "Edit details";
    public string CaptureLabel => IsCapturing ? "Capturing..." : "Capture creature";
    public bool CanCapture => !IsCapturing;
    public bool HasOriginalName => OriginalName.Length > 0;
    public bool HasResources => Resources.Count > 0;
    public bool HasRecorded => Recorded.Count > 0;
    public bool HasBases => Bases.Count > 0;
    public bool HasFinds => Finds.Count > 0;
    public bool HasFish => Fish is not null;
    public bool ShowFishCard => Fish is not null && !IsFishCollapsed;
    public bool ShowFishRail => Fish is not null && IsFishCollapsed;
    public bool HasCreatures => Creatures.Count > 0;
    public bool NoCreatures => Creatures.Count == 0;

    partial void OnOriginalNameChanged(string value) => OnPropertyChanged(nameof(HasOriginalName));
    partial void OnResourcesChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HasResources));
    partial void OnRecordedChanged(IReadOnlyList<Fact> value) => OnPropertyChanged(nameof(HasRecorded));
    partial void OnBasesChanged(IReadOnlyList<PlanetBase> value) => OnPropertyChanged(nameof(HasBases));
    partial void OnFindsChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HasFinds));
    partial void OnCreaturesChanged(IReadOnlyList<CreatureCardViewModel> value)
    {
        OnPropertyChanged(nameof(HasCreatures));
        OnPropertyChanged(nameof(NoCreatures));
    }
    partial void OnFishChanged(PlanetFishViewModel? value) => FishLayoutChanged();
    partial void OnIsFishCollapsedChanged(bool value) => FishLayoutChanged();

    private void FishLayoutChanged()
    {
        OnPropertyChanged(nameof(HasFish));
        OnPropertyChanged(nameof(ShowFishCard));
        OnPropertyChanged(nameof(ShowFishRail));
    }

    // ---------------------------------------------------------------- Loading

    public async Task LoadAsync(KnownPlanet planet)
    {
        _vocabulary ??= await PlanetVocabulary.LoadAsync();
        _snapshot = LiveSaveWatcher.FromSession();
        Planet = planet;
        IsEditing = false;
        Editor = null;

        _siblings = NmsPlanets.ReadAll(_snapshot.Discoveries).Where(p => p.SystemAddress == planet.SystemAddress)
            .GroupBy(p => p.PlanetIndex).Select(g => g.First()).OrderBy(p => p.PlanetIndex).ToList();
        int at = _siblings.FindIndex(p => p.PlanetIndex == planet.PlanetIndex);
        HasSiblings = _siblings.Count > 1;
        CanGoPrevious = at > 0;
        CanGoNext = at >= 0 && at < _siblings.Count - 1;

        PortalCode = planet.PortalCode;
        GalaxyLabel = $"{UniverseInfo.Region(planet.SystemAddress)} · {PlanetText.GalaxyLabel(planet.Galaxy)}";
        ShowNames();
        ShowRecorded();
        ShowSave();
        ShowFinds();
        await ShowCreaturesAsync();
        await LoadSystemPictureAsync();
    }

    public Task GoPreviousAsync() => GoToSiblingAsync(-1);
    public Task GoNextAsync() => GoToSiblingAsync(+1);

    private Task GoToSiblingAsync(int step)
    {
        int at = Planet is null ? -1 : _siblings.FindIndex(p => p.PlanetIndex == Planet.PlanetIndex);
        int to = at + step;
        return at >= 0 && to >= 0 && to < _siblings.Count ? LoadAsync(_siblings[to]) : Task.CompletedTask;
    }

    private void ShowNames()
    {
        if (Planet is not { } planet) return;
        bool isMoon = BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex);
        SystemName = UniverseInfo.SystemName(planet.SystemAddress);
        Eyebrow = $"{(isMoon ? "MOON" : $"PLANET {planet.PlanetIndex}")} OF {SystemName.ToUpperInvariant()}";
        Title = UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex);
        string generated = UniverseInfo.GeneratedPlanetName(planet.SystemAddress, planet.PlanetIndex);
        OriginalName = string.Equals(Title, generated, StringComparison.OrdinalIgnoreCase) ? "" : $"Originally {generated}";
        Coordinates = UniverseInfo.Compact(planet.SystemAddress);
    }

    /// <summary>What the player recorded and screen recording read - the save doesn't hold these.</summary>
    private void ShowRecorded()
    {
        if (Planet is not { } planet) return;
        var d = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex);
        bool? water = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex);
        string Or(string? value) => string.IsNullOrEmpty(value) ? NotRecorded : value;

        Classification = planet.Classification(d?.Descriptor);
        // Weather, sentinels and resources from the game's own generator where the game data
        // has its tables (format 16+); what was recorded otherwise.
        var profile = PlanetTypesService.Profile(planet.SystemAddress, planet.PlanetIndex);
        Conditions =
        [
            new("Weather", profile is not null ? PlanetText.Weather(profile) : Or(d?.Weather)),
            new("Sentinels", profile is not null ? PlanetText.Sentinels(profile) : Or(d?.Sentinels)),
            new("Flora", d?.Flora is { Length: > 0 } flora ? flora : profile is not null ? PlanetText.Flora(profile) : Or(null)),
            new("Fauna", d?.Fauna is { Length: > 0 } fauna ? fauna : profile is not null ? PlanetText.Fauna(profile) : (d?.FaunaTotal is int n ? (n == 0 ? "None" : $"{n} species") : Or(null))),
            new("Water", water switch { true => "Yes", false => "No", _ => NotRecorded }),
        ];
        Resources = profile is not null && _vocabulary is not null
            ? PlanetText.Resources(profile, _vocabulary.ItemNames).ToList()
            : d?.Resources.ToList() ?? new();

        var recorded = new List<Fact>();
        void Add(string label, string? value) { if (!string.IsNullOrEmpty(value)) recorded.Add(new(label, value)); }
        Add("Descriptor", d?.Descriptor);
        Add("Weather", d?.Weather);
        Add("Sentinels", d?.Sentinels);
        Add("Flora", d?.Flora);
        Add("Fauna", d?.Fauna);
        Add("Water", water switch { true => "Yes", false => "No", _ => null });
        Add("Resources", d is { Resources.Count: > 0 } ? string.Join(", ", d.Resources) : null);
        Add("Notes", d is { Notes.Count: > 0 } ? string.Join(" · ", d.Notes) : null);
        Recorded = recorded;
        Fish = _vocabulary is null ? null : PlanetFishViewModel.For(planet, _vocabulary.Fish, FishCatchService.ReadCaught(_snapshot.Fishing),
            id => PlanetText.FishName(id, _vocabulary.ItemNames));
    }

    /// <summary>What the save says: discoveries, the visit, bases, who found it.</summary>
    private void ShowSave()
    {
        if (Planet is not { } planet) return;
        ulong here = S.ReadSystemAddress(_snapshot.Universe) ?? 0;
        int herePlanet = _snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        IsHere = here == planet.SystemAddress && herePlanet == planet.PlanetIndex;
        HereLabel = _snapshot.Coordinates is { } you ? $"YOU ARE HERE · {NmsPlanetCoordinates.Format(you.Latitude, you.Longitude)}" : "YOU ARE HERE";

        var facts = new List<Fact>();
        void Add(string label, string value) { if (value.Length > 0) facts.Add(new(label, value)); }
        NmsPlanets.CountDiscoveries(_snapshot.Discoveries).TryGetValue(planet.Address, out var tally);
        if (here == planet.SystemAddress || tally is not null) Add("You found", PlanetText.Discovered(planet.Address, tally));
        Add("Visited", PlanetText.Visit(NmsPlanets.PlanetStats(_snapshot.StatGroups, planet.Address)));
        if (tally is { Waypoints: > 0 } w) Add("Waypoints", w.Waypoints.ToString(CultureInfo.CurrentCulture));
        Add("Found by", planet.DiscoveredHere ? "You" : planet.Discoverer is { Length: > 0 } who ? who : NotRecorded);
        Add("Found on", planet.DiscoveredUnixSeconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(planet.DiscoveredUnixSeconds).LocalDateTime.ToString("d MMMM yyyy", CultureInfo.CurrentCulture) : "");
        Add("Portal code", planet.PortalCode);
        SaveFacts = facts;

        static string At(JObject b) => NmsPlanetCoordinates.OfBase(b) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "";
        Bases = (_snapshot.Bases ?? new JArray()).OfType<JObject>()
            .Where(b => S.ParsePackedAddress(b["oZw"]) == planet.Address)
            .Select(b =>
            {
                string name = (b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base") + At(b);
                bool mine = b["peI"]?["DPp"]?.Value<string>() == "HomePlanetBase";
                return new PlanetBase(name, mine ? "Your base" : $"{b["3?K"]?["V?:"]?.Value<string>() ?? "Another player"}'s base");
            })
            .OrderBy(b => b.Owner != "Your base").ToList();
    }

    private void ShowFinds()
    {
        if (Planet is not { } planet) return;
        Finds = FindsService.All.Where(f => f.PortalCode == planet.PortalCode && f.Galaxy == planet.Galaxy)
            .Select(f => string.Join(" · ", new[] { f.Type, f.Title, f.Class, f.Location }.Where(s => s.Length > 0)))
            .ToList();
    }

    private async Task ShowCreaturesAsync()
    {
        if (Planet is not { } planet) return;
        var cards = CreatureListService.On(planet.SystemAddress, planet.PlanetIndex)
            .Select(c => new CreatureCardViewModel(c, planet.SystemAddress, planet.PlanetIndex)).ToList();
        Creatures = cards;
        foreach (var card in cards) await card.LoadPictureAsync();
    }

    private async Task LoadSystemPictureAsync()
    {
        if (Planet is not { } planet) return;
        ulong system = planet.SystemAddress;
        var picture = ScreenCaptureLogService.SystemPicture(system) is string path
            ? await GameScreenshotCache.LoadFadedFileAsync(path, 3.0) : null;
        if (Planet?.SystemAddress == system) SystemPicture = picture;
    }

    // ---------------------------------------------------------------- Editing, fish, creatures

    public void ToggleEditor()
    {
        if (Planet is null || _vocabulary is null) return;
        if (!IsEditing) Editor = new PlanetDetailsEditorViewModel(Planet, _vocabulary);
        IsEditing = !IsEditing;
    }

    public void SetFishCollapsed(bool collapsed)
    {
        AppSettingsService.SetFlag(FishCollapsedKey, collapsed);
        IsFishCollapsed = collapsed;
    }

    /// <summary>Reads the game screen now: the open fauna list, or - with a name - that
    /// creature's picture (screen recording's manual capture).</summary>
    public async Task<CaptureResult> CaptureAsync(string? creatureName)
    {
        if (IsCapturing || Planet is not { } planet) return new CaptureResult(false, "A capture is already running.");
        IsCapturing = true;
        try
        {
            return await ScreenCaptureLogService.CaptureNowAsync(
                hintSystem: planet.SystemAddress,
                forceCreaturePicture: string.IsNullOrWhiteSpace(creatureName) ? null : (planet.SystemAddress, planet.PlanetIndex, creatureName),
                hintPlanet: (planet.SystemAddress, planet.PlanetIndex));
        }
        finally
        {
            IsCapturing = false;
            await ShowCreaturesAsync();
        }
    }

    public void DeleteCreature(CreatureCardViewModel creature)
    {
        if (Planet is { } planet) CreatureListService.Delete(planet.SystemAddress, planet.PlanetIndex, creature.Name);
    }

    // ---------------------------------------------------------------- Following the game

    public void Attach(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        CreatureListService.Changed += OnCreaturesChanged;
        ScreenCaptureLogService.SystemPictureSaved += OnSystemPictureSaved;
        FishCatchRecorder.SnapshotSeen += OnSnapshotSeen;
        PlanetDetailsService.Captured += OnPlanetChanged;
        PlanetDetailsService.Changed += OnPlanetChanged;
        PlanetWaterService.Changed += OnPlanetChanged;
        SystemNotesService.Changed += OnSystemChanged;
        SystemPageReader.Captured += OnSystemChanged;
        BodyKindsService.Captured += OnSystemChanged;
        FindsService.Changed += OnFindsChanged;
    }

    public void Detach()
    {
        CreatureListService.Changed -= OnCreaturesChanged;
        ScreenCaptureLogService.SystemPictureSaved -= OnSystemPictureSaved;
        FishCatchRecorder.SnapshotSeen -= OnSnapshotSeen;
        PlanetDetailsService.Captured -= OnPlanetChanged;
        PlanetDetailsService.Changed -= OnPlanetChanged;
        PlanetWaterService.Changed -= OnPlanetChanged;
        SystemNotesService.Changed -= OnSystemChanged;
        SystemPageReader.Captured -= OnSystemChanged;
        BodyKindsService.Captured -= OnSystemChanged;
        FindsService.Changed -= OnFindsChanged;
    }

    private bool IsThisPlanet(ulong system, int planet) =>
        Planet is { } p && (system == 0 || system == p.SystemAddress) && (planet == -1 || planet == p.PlanetIndex);

    private void OnUi(Action action) => _dispatcher?.TryEnqueue(() => action());

    private void OnPlanetChanged(ulong system, int planet) => OnUi(() => { if (IsThisPlanet(system, planet)) ShowRecorded(); });
    private void OnSystemChanged(ulong system) => OnUi(() => { if (IsThisPlanet(system, -1)) ShowNames(); });
    private void OnFindsChanged() => OnUi(ShowFinds);
    private void OnCreaturesChanged(ulong system, int planet) => OnUi(() => { if (IsThisPlanet(system, planet)) _ = ShowCreaturesAsync(); });
    private void OnSystemPictureSaved(ulong system) => OnUi(() => { if (IsThisPlanet(system, -1)) _ = LoadSystemPictureAsync(); });

    private void OnSnapshotSeen(PlanetSnapshot snapshot) => OnUi(() =>
    {
        _snapshot = snapshot;
        ShowSave();
        ShowRecorded(); // fish caught
    });
}
