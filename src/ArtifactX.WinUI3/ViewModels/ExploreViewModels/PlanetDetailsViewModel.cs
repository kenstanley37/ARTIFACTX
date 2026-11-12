using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Helpers.Fauna;
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
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SummaryDetails))] public partial bool IsHere { get; private set; }
    [ObservableProperty] public partial string HereLabel { get; private set; } = "";
    [ObservableProperty] public partial ImageSource? SystemPicture { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SummaryDetails))] public partial IReadOnlyList<Fact> Conditions { get; private set; } = [];

    /// <summary>"Planet 2 of Hikovsky" / "Moon of Hikovsky", for the pinned summary bar.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SummaryDetails))] public partial string Position { get; private set; } = "";

    /// <summary>The pinned summary bar's second line: where the planet is, then the two conditions that change how
    /// you play there (sentinels, weather), then "You are here" when it's true. Unrecorded values are left out.</summary>
    public string SummaryDetails
    {
        get
        {
            var parts = new List<string> { Position };
            foreach (var label in new[] { "Sentinels", "Weather" })
                if (Conditions.FirstOrDefault(f => f.Label == label) is { } fact && fact.Value.Length > 0 && fact.Value != NotRecorded)
                    parts.Add($"{label} {fact.Value.Replace(" · ", ", ")}"); // "Extreme burning, occasional storms": one fact, not two
            if (IsHere) parts.Add("You are here");
            return string.Join("  ·  ", parts.Where(p => p.Length > 0));
        }
    }
    [ObservableProperty] public partial IReadOnlyList<string> Resources { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<Fact> Recorded { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<Fact> SaveFacts { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<PlanetBase> Bases { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<string> Finds { get; private set; } = [];
    [ObservableProperty] public partial PlanetFishViewModel? Fish { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<CreatureCardViewModel> Creatures { get; private set; } = [];
    /// <summary>One greyed tile per species not found yet (#36), and "3 of 8 species found".</summary>
    [ObservableProperty] public partial IReadOnlyList<string> UndiscoveredCreatures { get; private set; } = [];
    [ObservableProperty] public partial string SpeciesFound { get; private set; } = "";
    public PlanetEquipmentViewModel Equipment { get; } = new();

    [ObservableProperty] public partial bool HasSiblings { get; private set; }
    [ObservableProperty] public partial bool CanGoPrevious { get; private set; }
    [ObservableProperty] public partial bool CanGoNext { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditLabel))]
    public partial bool IsEditing { get; private set; }

    [ObservableProperty] public partial PlanetDetailsEditorViewModel? Editor { get; private set; }

    [ObservableProperty] public partial bool IsFishCollapsed { get; private set; } = AppSettingsService.GetFlag(FishCollapsedKey);

    public string EditLabel => IsEditing ? "Close editor" : "Edit details";
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
    public bool HasSpeciesFound => SpeciesFound.Length > 0;
    partial void OnSpeciesFoundChanged(string value) => OnPropertyChanged(nameof(HasSpeciesFound));

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
        await PlanetTypesService.EnsureLoadedAsync();

        _siblings = await Task.Run(() => NmsPlanets.ReadAll(_snapshot.Discoveries).Where(p => p.SystemAddress == planet.SystemAddress)
            .GroupBy(p => p.PlanetIndex).Select(g => g.First()).OrderBy(p => p.PlanetIndex).ToList());
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
        _ = Equipment.LoadAsync(planet, _snapshot);
        _ = LoadSystemPictureAsync();
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
        Position = isMoon ? $"Moon of {SystemName}" : $"Planet {planet.PlanetIndex} of {SystemName}";
        Eyebrow = Position.ToUpperInvariant();
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

        bool isMoon = BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex);
        var profile = PlanetTypesService.Profile(planet.SystemAddress, planet.PlanetIndex);
        string? desc = PlanetTypesService.Descriptor(planet.SystemAddress, planet.PlanetIndex);
        Classification = planet.Classification(desc, isMoon: isMoon);
        // Weather, sentinels and resources from the game's own generator where the game data
        // has its tables (format 16+); what was recorded otherwise.
        Conditions =
        (Fact[])[
            new("Type", desc is { Length: > 0 }
                ? (desc.EndsWith(" Moon", StringComparison.OrdinalIgnoreCase) || desc.EndsWith(" Planet", StringComparison.OrdinalIgnoreCase) || desc.EndsWith(" Anomaly", StringComparison.OrdinalIgnoreCase) ? desc : (isMoon ? $"{desc} Moon" : $"{desc} Planet"))
                : (isMoon ? $"{planet.BiomeGroup} Moon" : $"{planet.BiomeGroup} Planet")),
            new("Biome", planet.BiomeGroup),
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
        // A discovery the game just saved shrinks the species still to find.
        ShowSpeciesLeft(planet, Creatures);
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

        var profile = PlanetTypesService.Profile(planet.SystemAddress, planet.PlanetIndex);
        var predicted = profile?.PredictedCreatures ?? [];
        var nameService = await CreatureNameService.LoadAsync();

        // 1. The save's discoveries on this planet (bEr[0] is the creature's seed, bEr[2]/[3] its name seeds).
        var saveDiscoveries = FaunaDiscoveryService.ReadAll(_snapshot.Discoveries as JArray, nameService)
            .GetValueOrDefault(planet.Address) ?? [];

        // 2. One card per predicted creature, its discovery found by seed (PlanetFaunaPairing), then any discoveries
        //    beyond the predicted list.
        var allCards = new List<CreatureCardViewModel>();
        var slots = PlanetFaunaPairing.Pair(predicted, saveDiscoveries.Select(d => d.Seed).ToList());
        foreach (var slot in slots)
        {
            var pred = slot.Predicted;
            string rigId = pred is null ? FallbackRig(saveDiscoveries[slot.Discovery].SpeciesName)
                : await CreatureModelService.ResolveRigAsync(pred.Id,
                    scenePath: PlanetTypesService.CreatureTables?.Creatures.FirstOrDefault(c => c.Id == pred.Id)?.Scene) ?? "antelope";
            string habitat = pred is null ? "Native Fauna" : $"{PlanetTypesService.CreatureTables?.HabitatOf(pred) ?? pred.Habitat} Fauna";
            var (rarity, activity) = pred is null ? ("", "") : CreatureTraitText.RarityAndActivity(pred, PlanetTypesService.CreatureTables);

            if (slot.Discovery >= 0)
            {
                string name = saveDiscoveries[slot.Discovery].SpeciesName;
                if (CreatureListService.IsDeleted(planet.SystemAddress, planet.PlanetIndex, name)) continue;
                // Rarity and activity only when the discovery is this very creature (same seed), not a leftover slot.
                bool same = pred is not null && saveDiscoveries[slot.Discovery].Seed == pred.Seed;
                var creature = new Creature(Name: name, Rarity: same && rarity.Length > 0 ? rarity : "Discovered", Habitat: habitat,
                    Activity: same ? activity : "", Gender: "", Size: "", Picture: false, SeenAt: "Discovered");
                var disc = saveDiscoveries[slot.Discovery];
                allCards.Add(new CreatureCardViewModel(creature, planet.SystemAddress, planet.PlanetIndex, rigId, isDiscovered: true,
                    about: same ? CreatureTraitText.About(pred!, PlanetTypesService.CreatureTables) : "",
                    battle: same ? Panel(CreatureTraitText.Battle(pred!, PlanetTypesService.CreatureTables, disc.SpeciesSeed, disc.GenusSeed)) : null,
                    canTame: same ? PlanetTypesService.CreatureTables?.CanBattle(pred!) : null));
                continue;
            }

            // Not discovered yet: the species name the game will give it, marked as a prediction; older game data
            // can't predict it, so the kind is shown instead.
            if (pred is null) continue;
            string? predictedName = nameService.PredictedSpecies(pred, planet.Address);
            string unscannedName = predictedName ?? $"Unscanned {pred.DefaultDisplayName}";
            if (CreatureListService.IsDeleted(planet.SystemAddress, planet.PlanetIndex, unscannedName)) continue;
            // Traits as the game's discovery list shows them, then the species' two forms (gender with weight and height).
            var unscanned = new Creature(Name: unscannedName,
                Rarity: predictedName is null ? "Undiscovered" : rarity.Length > 0 ? $"Predicted · {rarity}" : "Predicted",
                Habitat: habitat, Activity: predictedName is null ? "" : activity, Gender: "",
                Size: predictedName is null ? "" : CreatureTraitText.Forms(pred, PlanetTypesService.CreatureTables),
                Picture: false, SeenAt: "Unscanned");
            var (speciesSeed, genusSeed) = SpeciesNames.FaunaSeeds(pred.Seed, string.IsNullOrEmpty(pred.SpeciesId) ? pred.Id : pred.SpeciesId,
                planet.Address, pred.CreatureType, pred.SizeClass);
            allCards.Add(new CreatureCardViewModel(unscanned, planet.SystemAddress, planet.PlanetIndex, rigId, isDiscovered: false,
                about: predictedName is null ? "" : CreatureTraitText.About(pred, PlanetTypesService.CreatureTables),
                battle: predictedName is null ? null : Panel(CreatureTraitText.Battle(pred, PlanetTypesService.CreatureTables, speciesSeed, genusSeed)),
                canTame: PlanetTypesService.CreatureTables?.CanBattle(pred)));
        }

        // Creatures a player can adopt first (their battle stats are what people look for), the rest after; the
        // sort is stable, so each group keeps the game's order.
        allCards = allCards.OrderBy(c => c.CanTame ? 0 : 1).ToList();
        Creatures = allCards;
        ShowSpeciesLeft(planet, allCards);
    }

    private static CreatureBattleViewModel? Panel(CreatureBattle? battle) => battle is null ? null : new CreatureBattleViewModel(battle);

    /// <summary>A stable land model for a discovery the generator didn't predict, picked from its name.</summary>
    private static string FallbackRig(string speciesName)
    {
        uint hash = 2166136261;
        foreach (char c in speciesName) hash = (hash ^ c) * 16777619;
        return CreaturePreviewControl.LandRigs[hash % (uint)CreaturePreviewControl.LandRigs.Length];
    }

    /// <summary>
    /// Shows species count found vs total.
    /// </summary>
    private void ShowSpeciesLeft(KnownPlanet planet, IReadOnlyList<CreatureCardViewModel> cards)
    {
        int? total = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.FaunaTotal
            ?? PlanetTypesService.Profile(planet.SystemAddress, planet.PlanetIndex)?.FaunaCount;
        int all = total ?? cards.Count;
        int found = cards.Count(c => c.IsDiscovered);
        if (all == 0)
        {
            SpeciesFound = "No fauna on this planet.";
            UndiscoveredCreatures = [];
            return;
        }
        SpeciesFound = found == all ? $"All {all} species found." : $"{found} of {all} species found.";
        UndiscoveredCreatures = [];
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

    public void DeleteCreature(CreatureCardViewModel creature)
    {
        if (Planet is { } planet)
        {
            CreatureListService.Delete(planet.SystemAddress, planet.PlanetIndex, creature.Name);
            _ = ShowCreaturesAsync();
        }
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
        GameProcessMonitorService.RunningStateChanged += OnGameRunningChanged;
        SaveSessionManager.PendingEditsChanged += OnPendingEditsChanged;
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
        GameProcessMonitorService.RunningStateChanged -= OnGameRunningChanged;
        SaveSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
    }

    private bool IsThisPlanet(ulong system, int planet) =>
        Planet is { } p && (system == 0 || system == p.SystemAddress) && (planet == -1 || planet == p.PlanetIndex);

    private void OnUi(Action action) => _dispatcher?.TryEnqueue(() => action());

    private void OnPlanetChanged(ulong system, int planet) => OnUi(() => { if (IsThisPlanet(system, planet)) ShowRecorded(); });
    private void OnSystemChanged(ulong system) => OnUi(() => { if (IsThisPlanet(system, -1)) ShowNames(); });
    private void OnFindsChanged() => OnUi(() =>
    {
        ShowFinds();
        if (Planet is { } p) Equipment.UpdateLocations(p, _snapshot);
    });
    private void OnGameRunningChanged(object? sender, bool running) => OnUi(Equipment.RefreshMarkers);
    private void OnPendingEditsChanged(object? sender, EventArgs e) => OnUi(Equipment.RefreshMarkers);
    private void OnCreaturesChanged(ulong system, int planet) => OnUi(() => { if (IsThisPlanet(system, planet)) _ = ShowCreaturesAsync(); });
    private void OnSystemPictureSaved(ulong system) => OnUi(() => { if (IsThisPlanet(system, -1)) _ = LoadSystemPictureAsync(); });

    private void OnSnapshotSeen(PlanetSnapshot snapshot) => OnUi(() =>
    {
        _snapshot = snapshot;
        ShowSave();
        ShowRecorded(); // fish caught
        _ = ShowCreaturesAsync();
        if (Planet is { } p) Equipment.UpdateLocations(p, _snapshot);
    });
}
