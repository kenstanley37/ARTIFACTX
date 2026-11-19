using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// Tools > Finds: player-recorded finds with portal addresses (FindsService, user.db) - the You are here band, the
/// search, filters and sort, the cards, and the Add / Edit form (<see cref="Form"/>). Follows the game (the page passes
/// FishCatchRecorder.SnapshotSeen snapshots to OnSnapshot) so "Use my current location" and the distances stay current.
/// Never writes to the save.
/// </summary>
public sealed partial class FindsPageViewModel : ObservableObject
{
    private Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
    private FindsHere? _here;
    private bool _ready;

    public FindFormViewModel Form { get; } = new();

    [ObservableProperty] public partial string LiveStatus { get; private set; } = "Following your game";
    [ObservableProperty] public partial bool ShowHere { get; private set; }
    [ObservableProperty] public partial string HereSystem { get; private set; } = "";
    [ObservableProperty] public partial string HerePlace { get; private set; } = "";

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<TextOption> TypeFilters { get; private set; } = [];
    [ObservableProperty] public partial TextOption? SelectedTypeFilter { get; set; }

    public IReadOnlyList<TextOption> ClassFilters { get; } =
        new[] { new TextOption("Any class", "") }.Concat(new[] { "S", "A", "B", "C" }.Select(c => new TextOption(c, c))).ToList();

    [ObservableProperty] public partial TextOption? SelectedClassFilter { get; set; }

    public IReadOnlyList<TextOption> SortOptions { get; } =
        new TextOption[] { new("Newest first", "newest"), new("Nearest to you first", "nearest"), new("Title (A to Z)", "title") };

    [ObservableProperty] public partial TextOption? SelectedSort { get; set; }

    [ObservableProperty] public partial IReadOnlyList<FindCardViewModel> Cards { get; private set; } = [];
    [ObservableProperty] public partial string CountText { get; private set; } = "";
    [ObservableProperty] public partial bool ShowEmpty { get; private set; }

    public FindsPageViewModel()
    {
        SelectedClassFilter = ClassFilters[0];
        SelectedSort = SortOptions[0];
    }

    partial void OnSearchTextChanged(string value) => Apply();
    partial void OnSelectedTypeFilterChanged(TextOption? value) => Apply();
    partial void OnSelectedClassFilterChanged(TextOption? value) => Apply();
    partial void OnSelectedSortChanged(TextOption? value) => Apply();

    public async Task LoadAsync()
    {
        await PortalGlyphs.WarmAsync();
        _itemNames = await CatalogService.GetItemNamesAsync();
        await Form.LoadAsync(_itemNames, NameOf);
        // Icons for the fish picker and for every item already on a card.
        await CatalogService.WarmCacheAsync(Form.FishIds.Concat(FindsService.All.SelectMany(f => f.ItemIds)));

        if (SaveSessionManager.IsSaveLoaded) ReadHere(LiveSaveWatcher.FromSession());
        Form.Start();
        _ready = true;
        RefreshTypeFilter();
        Apply();
    }

    public void OnSnapshot(PlanetSnapshot snapshot)
    {
        ReadHere(snapshot);
        LiveStatus = $"Following your game · updated {DateTime.Now:t}";
        Apply();
    }

    /// <summary>The finds or a system's notes changed (here or on another page).</summary>
    public void Refresh()
    {
        UpdateHere();
        RefreshTypeFilter();
        Apply();
    }

    private void ReadHere(PlanetSnapshot snapshot)
    {
        _here = S.ReadSystemAddress(snapshot.Universe) is ulong system
            ? new FindsHere(system, snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0, snapshot.Coordinates, NmsPlanets.ReadAll(snapshot.Discoveries))
            : null;
        Form.Here = _here;
        UpdateHere();
    }

    /// <summary>The "You are here" band: the system and planet names (the game's generator, or what a save recorded) plus
    /// the planet's type from its discovery record.</summary>
    public void UpdateHere()
    {
        if (_here is not { } here)
        {
            ShowHere = false;
            return;
        }
        ShowHere = true;
        ulong system = here.System;
        HereSystem = $"{UniverseInfo.SystemName(system)} · {UniverseInfo.Region(system)}";
        var parts = new List<string>();
        if (here.Planet > 0)
        {
            parts.Add($"On {UniverseInfo.PlanetName(system, here.Planet)} (planet {here.Planet})");
            var planet = here.Planets.FirstOrDefault(p => p.SystemAddress == system && p.PlanetIndex == here.Planet);
            if (planet is not null)
                parts.Add(planet.Classification(PlanetTypesService.Descriptor(system, here.Planet), isMoon: BodyKindsService.IsMoon(system, here.Planet)));
        }
        else parts.Add("In space");
        parts.Add(PlanetText.GalaxyLabel(S.GalaxyNumber(system)));
        parts.Add(S.PortalAddress(system, here.Planet > 0 ? here.Planet : 1));
        HerePlace = string.Join(" · ", parts) + "\n" + UniverseInfo.Summary(system);
    }

    // ---------------------------------------------------------------- list

    /// <summary>The Type filter with each type's count; keeps the type picked.</summary>
    private void RefreshTypeFilter()
    {
        string previous = SelectedTypeFilter?.Key ?? "";
        var finds = FindsService.All;
        var options = new List<TextOption> { new($"All types ({finds.Count})", "") };
        foreach (var type in FindsService.Types)
        {
            int count = finds.Count(f => f.Type == type);
            if (count > 0) options.Add(new($"{type} ({count})", type));
        }
        bool ready = _ready;
        _ready = false; // one Apply from the caller, not one per change
        TypeFilters = options;
        SelectedTypeFilter = options.FirstOrDefault(o => o.Key == previous) ?? options[0];
        _ready = ready;
    }

    public void Apply()
    {
        if (!_ready) return;
        var finds = FindsService.All;
        string text = SearchText.Trim();
        string type = SelectedTypeFilter?.Key ?? "";
        string cls = SelectedClassFilter?.Key ?? "";

        bool Matches(Find f) => text.Length == 0
            || f.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || f.Notes.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || f.Location.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || f.Subtype.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || f.ItemIds.Any(i => NameOf(i).Contains(text, StringComparison.CurrentCultureIgnoreCase))
            || SystemName(f).Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || PlanetName(f, PlanetOf(f)).Contains(text, StringComparison.CurrentCultureIgnoreCase);

        var filtered = finds.Where(f => type.Length == 0 || f.Type == type)
            .Where(f => cls.Length == 0 || f.Class == cls)
            .Where(Matches);
        var ordered = SelectedSort?.Key switch
        {
            "nearest" => filtered.OrderBy(f => DistanceFrom(f) ?? double.MaxValue),
            "title" => filtered.OrderBy(f => f.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => filtered.OrderByDescending(f => f.CreatedAt, StringComparer.Ordinal),
        };

        Cards = ordered.Select(ToCard).ToList();
        CountText = $"{Cards.Count} of {finds.Count} finds";
        ShowEmpty = finds.Count == 0;
    }

    public string NameOf(string id) => _itemNames.TryGetValue(id, out var n) ? n : CatalogService.TryGet(id)?.DisplayName ?? id;

    private static ulong? AddressOf(Find f) => f.PortalCode.Length == 12 ? NmsPortalCode.ToAddress(f.PortalCode, f.Galaxy) : null;

    private static string SystemName(Find f) => AddressOf(f) is ulong a ? UniverseInfo.SystemName(a) : "";

    private static int PlanetOf(Find f) =>
        f.PortalCode.Length == 12 ? int.Parse(f.PortalCode[..1], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : 0;

    /// <summary>The planet's name: learned from a save, else the game's generated name.</summary>
    private static string PlanetName(Find f, int planet) =>
        AddressOf(f) is ulong a && planet > 0 ? UniverseInfo.PlanetName(a, planet) : "";

    private double? DistanceFrom(Find f) =>
        _here is { } h && AddressOf(f) is ulong a ? NmsPlanets.DistanceLightYears(h.System, S.ToSystemAddress(a)) : null;

    private FindCardViewModel ToCard(Find f)
    {
        var detail = new List<string>();
        if (f.Subtype.Length > 0) detail.Add(f.Subtype);
        if (f.Location.Length > 0) detail.Add(f.Location);

        string system = SystemName(f);
        int planet = PlanetOf(f);
        string planetName = PlanetName(f, planet);
        string planetText = planetName.Length > 0 ? $"{planetName} (planet {planet})" : $"planet {planet}";
        string place = f.PortalCode.Length == 12
            ? $"{(system.Length > 0 ? system : "Unnamed system")} · {planetText} · {PlanetText.GalaxyLabel(f.Galaxy)}"
            : "No address recorded";

        string distance = AddressOf(f) is not ulong address ? ""
            : _here is { } here && S.ToSystemAddress(address) == here.System ? "In the system you're in"
            : DistanceFrom(f) switch
            {
                null => "In a different galaxy from you - portals can't cross galaxies",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };

        return new FindCardViewModel
        {
            Id = f.Id,
            TypeLabel = f.Type.ToUpperInvariant(),
            Title = f.Title.Length > 0 ? f.Title : f.Type,
            Detail = string.Join(" · ", detail),
            DetailVisibility = detail.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
            ClassLetter = f.Class,
            ClassVisibility = f.Class.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
            ClassBrush = new SolidColorBrush(ThemeResources.ClassColour(f.Class)),
            Items = f.ItemIds.Select(id => new FindItemChip { Label = NameOf(id), Icon = CatalogService.TryGet(id)?.Icon }).ToList(),
            ItemsVisibility = f.ItemIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
            Place = place,
            Facts = AddressOf(f) is ulong factsAddress ? UniverseInfo.Compact(factsAddress) : "",
            Distance = distance,
            PortalCode = f.PortalCode,
            Notes = f.Notes,
            NotesVisibility = f.Notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
    }

    /// <summary>The list as shown, for Export CSV.</summary>
    public (string[] Headers, List<string[]> Rows) CsvRows()
    {
        var finds = FindsService.All.ToDictionary(f => f.Id);
        var rows = new List<string[]>();
        foreach (var card in Cards)
        {
            if (!finds.TryGetValue(card.Id, out var f)) continue;
            rows.Add(new[]
            {
                f.Type, f.Title, f.Class, f.Subtype, string.Join(", ", f.ItemIds.Select(NameOf)), f.Location,
                f.PortalCode, f.PortalCode.Length == 12 ? PlanetText.GalaxyLabel(f.Galaxy) : "", SystemName(f), PlanetName(f, PlanetOf(f)),
                card.Distance, f.Notes, LocalTime(f.CreatedAt),
            });
        }
        return (new[] { "Type", "Title", "Class", "Kind", "Items or fish", "Where", "Portal code", "Galaxy", "System", "Planet", "Distance", "Notes", "Added" }, rows);
    }

    /// <summary>user.db timestamps are UTC (ISO 8601); shown in local time.</summary>
    private static string LocalTime(string utc) =>
        DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
            ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : utc;

    public Find? FindById(string id) => FindsService.All.FirstOrDefault(f => f.Id == id);

    /// <summary>Deletes a find (the page confirms first); a find being edited leaves the form.</summary>
    public void Delete(string id)
    {
        FindsService.Delete(id);
        if (Form.IsEditing(id)) Form.Reset();
        RefreshTypeFilter();
        Apply();
    }

    public void Edit(string id)
    {
        if (FindById(id) is { } f) Form.Edit(f, NameOf);
    }
}
