using System.Collections.ObjectModel;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Recipes;
using ArtifactX.Core.Universe;
using ArtifactX.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>A planet or moon on System Details, with its portal survey on demand: where the portal is, the base
/// computer spots near it that reach deep-level hotspots, and the hotspots around it (PortalSurvey, from the rules
/// file) - all as Analysis Visor coordinates.</summary>
public sealed partial class SystemBodyItemViewModel : ObservableObject
{
    /// <summary>How many hotspots near the portal to list: the closest, enough to pick from without a long scroll.</summary>
    private const int NearbyCount = 8;

    /// <summary>Base spot options shown before "Show more": the best ones fit a phone screen (seven made a long scroll).</summary>
    private const int FirstSpots = 3;

    private List<BaseSpotItemViewModel> _allSpots = [];

    private readonly int _galaxyNumber;

    public SystemBodyItemViewModel(GeneratedSystemBody body, int galaxyNumber)
    {
        Body = body;
        _galaxyNumber = galaxyNumber;
        SwatchColor = Color.FromArgb(body.SwatchHex);
    }

    /// <summary>The body's colour dot: the readout's neutral colour until its type is known, then the planet type's
    /// colour (NmsPlanets.TypeColour, as the desktop's lists).</summary>
    [ObservableProperty] public partial Color SwatchColor { get; private set; }

    public GeneratedSystemBody Body { get; }
    public string Name => Body.Name;
    public string PrimaryLine => Body.PrimaryLine;
    public string SecondaryLine => Body.SecondaryLine;
    public string BodyPortalCode => Body.BodyPortalCode;
    public bool IsDialedBody => Body.IsDialedBody;
    public bool HasRings => Body.HasRings;

    // ---------------------------------------------------------------- planet details (SystemProfiles, from the rules file)

    [ObservableProperty] public partial bool HasProfile { get; private set; }
    /// <summary>The game's word for the planet's type ("Paradise", "Frozen"), as its Discoveries page shows.</summary>
    [ObservableProperty] public partial string TypeText { get; private set; } = "";
    [ObservableProperty] public partial string WeatherText { get; private set; } = "";
    [ObservableProperty] public partial string SentinelsText { get; private set; } = "";
    [ObservableProperty] public partial bool HasAggressiveSentinels { get; private set; }
    [ObservableProperty] public partial string LifeText { get; private set; } = "";
    [ObservableProperty] public partial string ResourcesText { get; private set; } = "";

    /// <summary>Fills the planet's weather, sentinels, life and resources (Core PlanetProfileText, the desktop's words).</summary>
    public void ApplyProfile(PlanetProfile profile, PlanetType? type, GameRules rules)
    {
        TypeText = profile.Descriptor;
        if (type is not null) SwatchColor = Color.FromArgb(NmsPlanets.TypeColour(type.Biome));
        WeatherText = PlanetProfileText.Weather(profile, type);
        string sentinels = PlanetProfileText.Sentinels(profile);
        SentinelsText = $"{sentinels} sentinels";
        HasAggressiveSentinels = sentinels is "Aggressive" or "Corrupted";
        LifeText = $"Flora: {PlanetProfileText.Flora(profile)} · Fauna: {PlanetProfileText.Fauna(profile)}";
        ResourcesText = string.Join(", ", PlanetProfileText.Resources(profile, rules.ItemName, rules.Profiles?.HintNames));
        HasProfile = true;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyButtonLabel))]
    public partial bool IsCopied { get; set; }

    public string CopyButtonLabel => IsCopied ? "Copied" : "Copy address";

    // ---------------------------------------------------------------- portal survey

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SurveyButtonLabel))]
    public partial bool IsSurveyOpen { get; set; }

    public string SurveyButtonLabel => IsSurveyOpen ? "Hide portal and base spots" : "Portal and base spots";

    [ObservableProperty] public partial bool IsSurveying { get; set; }
    [ObservableProperty] public partial bool HasPortal { get; set; }
    [ObservableProperty] public partial string PortalCoordinates { get; set; } = "";
    [ObservableProperty] public partial string SurveyNote { get; set; } = "";
    [ObservableProperty] public partial bool HasSurveyNote { get; set; }
    [ObservableProperty] public partial bool HasBaseSpots { get; set; }
    [ObservableProperty] public partial bool HasNearbyHotspots { get; set; }
    [ObservableProperty] public partial bool HasMoreSpots { get; set; }
    [ObservableProperty] public partial string MoreSpotsLabel { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PortalCopyLabel))]
    public partial bool IsPortalCopied { get; set; }

    public string PortalCopyLabel => IsPortalCopied ? "Copied" : "Copy";

    public ObservableCollection<BaseSpotItemViewModel> BaseSpots { get; } = [];
    public ObservableCollection<NearbyHotspotViewModel> NearbyHotspots { get; } = [];

    private bool _surveyed;

    [RelayCommand]
    private async Task ToggleSurveyAsync()
    {
        IsSurveyOpen = !IsSurveyOpen;
        if (!IsSurveyOpen || _surveyed || IsSurveying) return;
        IsSurveying = true;
        try
        {
            var rules = await GameRulesService.GetAsync();
            if (rules?.Biomes is not { } biomes || rules.Profiles is not { } profiles || rules.Extraction is not { } extraction
                || rules.Stations is not { } stations || rules.BuildingOverrides is not { } overrides)
            {
                ShowNote("This copy of ArtifactX doesn't have the game rules it needs to work out the portal and hotspots.");
                return;
            }
            ulong planetAddress = NmsPortalCode.ToAddress(Body.BodyPortalCode, _galaxyNumber);
            var survey = await Task.Run(() => PortalSurvey.ForPlanet(planetAddress, biomes, profiles, extraction, stations, overrides, rules.ItemName));
            _surveyed = true;
            if (survey is null)
            {
                ShowNote("The portal on this body can't be placed yet - gas-giant systems aren't worked out.");
                return;
            }
            HasPortal = true;
            PortalCoordinates = survey.Portal.Coordinates;
            int rank = 1;
            _allSpots = (survey.Plan.PortalSites ?? []).Select(spot => new BaseSpotItemViewModel(spot, survey.Plan.BaseReach, rank++)).ToList();
            BaseSpots.Clear();
            foreach (var spot in _allSpots.Take(FirstSpots)) BaseSpots.Add(spot);
            HasBaseSpots = BaseSpots.Count > 0;
            int more = _allSpots.Count - BaseSpots.Count;
            HasMoreSpots = more > 0;
            MoreSpotsLabel = more == 1 ? "Show 1 more option" : $"Show {more} more options";
            NearbyHotspots.Clear();
            foreach (var hotspot in survey.NearPortal(NearbyCount))
                NearbyHotspots.Add(new NearbyHotspotViewModel(hotspot));
            HasNearbyHotspots = NearbyHotspots.Count > 0;
            if (survey.Plan.PortalNote is { } note) ShowNote(note);
        }
        finally { IsSurveying = false; }
    }

    // ---------------------------------------------------------------- crashed ships and shops (PlanetFindsService)

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FindsButtonLabel))]
    public partial bool IsFindsOpen { get; set; }

    public string FindsButtonLabel => IsFindsOpen ? "Hide crashed ships and shops" : "Crashed ships and shops";

    [ObservableProperty] public partial bool IsFinding { get; set; }
    [ObservableProperty] public partial string FindsNote { get; set; } = "";
    [ObservableProperty] public partial bool HasFindsNote { get; set; }
    [ObservableProperty] public partial string WreckCounts { get; set; } = "";
    [ObservableProperty] public partial bool HasWreckCounts { get; set; }
    [ObservableProperty] public partial IReadOnlyList<PlanetFindViewModel> Wrecks { get; set; } = [];
    [ObservableProperty] public partial bool HasWrecks { get; set; }
    [ObservableProperty] public partial IReadOnlyList<PlanetFindViewModel> Shops { get; set; } = [];
    [ObservableProperty] public partial bool HasShops { get; set; }

    private bool _found;

    /// <summary>The planet's crashed ships by class and the best ones and nearest shops from the portal; worked out once,
    /// off the UI thread (a whole-planet pass, a second or two on a phone).</summary>
    [RelayCommand]
    private async Task ToggleFindsAsync()
    {
        IsFindsOpen = !IsFindsOpen;
        if (!IsFindsOpen || _found || IsFinding) return;
        IsFinding = true;
        try
        {
            if (await GameRulesService.GetAsync() is not { } rules)
            {
                SetFindsNote("This copy of ArtifactX doesn't have the game rules it needs for crashed ships.");
                return;
            }
            ulong planetAddress = NmsPortalCode.ToAddress(Body.BodyPortalCode, _galaxyNumber);
            var (finds, reason) = await Task.Run(() => PlanetFindsService.ForPlanet(planetAddress, rules));
            _found = true;
            if (finds is null) { SetFindsNote(reason); return; }
            WreckCounts = finds.ClassCounts.Length > 0 ? $"Crashed ships on this planet: {finds.ClassCounts}" : "";
            HasWreckCounts = WreckCounts.Length > 0;
            Wrecks = finds.Wrecks.Select(f => new PlanetFindViewModel(f)).ToArray();
            HasWrecks = Wrecks.Count > 0;
            Shops = finds.Shops.Select(f => new PlanetFindViewModel(f)).ToArray();
            HasShops = Shops.Count > 0;
            if (finds.Note.Length > 0) SetFindsNote(finds.Note);
        }
        finally { IsFinding = false; }
    }

    private void SetFindsNote(string note)
    {
        FindsNote = note;
        HasFindsNote = true;
    }

    [RelayCommand]
    private void ShowMoreSpots()
    {
        foreach (var spot in _allSpots.Skip(BaseSpots.Count)) BaseSpots.Add(spot);
        HasMoreSpots = false;
    }

    private void ShowNote(string note)
    {
        SurveyNote = note;
        HasSurveyNote = true;
    }

    [RelayCommand]
    private async Task CopyPortalCoordinatesAsync()
    {
        await Clipboard.Default.SetTextAsync(PortalCoordinates);
        IsPortalCopied = true;
        await Task.Delay(1500);
        IsPortalCopied = false;
    }
}
