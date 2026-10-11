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
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Tools > Fishing: the fish that live on the planet you're on, from the
/// game's fish table (CatalogService.GetFishAsync), with your catches from
/// the save's fishing records (NmsFishingPaths). Follows the game through
/// LiveSaveWatcher. Read-only - marking fish caught stays on the Fishing
/// Records page.
/// </summary>
public sealed partial class FishingPage : Page
{
    private const int WhereIAm = -2;
    private const int AllTypes = -1;

    private readonly LiveSaveWatcher _live;
    private List<FishInfo> _fish = new();
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, (uint Count, float Largest)> _caught = new(StringComparer.OrdinalIgnoreCase);
    private ulong? _hereSystem;
    private int _herePlanet;
    private int? _hereBiome;
    private List<KnownPlanet> _thisSave = new();
    private List<KnownPlanet>? _otherSaves;
    private Dictionary<ulong, string> _bases = new();
    private FishInfo? _whereFish;
    private bool _ready;

    public FishingPage()
    {
        InitializeComponent();
        _live = new LiveSaveWatcher(DispatcherQueue);
        _live.SnapshotRead += OnLiveSnapshot;
        Unloaded += (_, _) =>
        {
            _live.SnapshotRead -= OnLiveSnapshot;
            _live.Dispose();
        };
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        // Only the 210 real species - junk catches and mission-only fish are left out,
        // the same set the Fishing Records page shows.
        _fish = (await CatalogService.GetFishAsync()).Where(f => CatalogService.IsFishSpecies(f.Id) && !f.NeedsMission).ToList();
        _names = await CatalogService.GetItemNamesAsync();
        await CatalogService.WarmCacheAsync(_fish.Select(f => f.Id)
            .Concat(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}")));

        foreach (var (label, tag) in new[] { ("All fish", 0), ("Not caught yet", 1), ("Caught", 2) })
            ShowBox.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        ShowBox.SelectedIndex = 0;
        foreach (var (label, tag) in new[] { ("Any time", ""), ("Bites by day", "Day"), ("Bites at night", "Night") })
            TimeBox.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        TimeBox.SelectedIndex = 0;
        RarityBox.Items.Add(new ComboBoxItem { Content = "Any rarity", Tag = "" });
        foreach (var q in new[] { "Common", "Rare", "Epic", "Legendary" })
            RarityBox.Items.Add(new ComboBoxItem { Content = q, Tag = q });
        RarityBox.SelectedIndex = 0;

        PlanetTypeBox.Items.Add(new ComboBoxItem { Content = "Where I am", Tag = WhereIAm });
        PlanetTypeBox.Items.Add(new ComboBoxItem { Content = "All planet types", Tag = AllTypes });
        foreach (int biome in NmsPlanets.SelectableTypes)
            PlanetTypeBox.Items.Add(new ComboBoxItem { Content = NmsPlanets.TypeName(biome), Tag = biome });
        PlanetTypeBox.SelectedIndex = 0;

        if (SaveSessionManager.IsSaveLoaded)
        {
            ReadSnapshot(LiveSaveWatcher.FromSession());
            _live.Start();
        }

        _ready = true;
        UpdateHere();
        Apply();
    }

    private void OnLiveSnapshot(PlanetSnapshot snapshot)
    {
        ReadSnapshot(snapshot);
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        UpdateHere();
        Apply();
        UpdateWhere();
    }

    private void ReadSnapshot(PlanetSnapshot snapshot)
    {
        _hereSystem = S.ReadSystemAddress(snapshot.Universe);
        _herePlanet = snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        _thisSave = NmsPlanets.ReadAll(snapshot.Discoveries);
        _hereBiome = _hereSystem is ulong system && _herePlanet > 0
            ? _thisSave.FirstOrDefault(p => p.SystemAddress == system && p.PlanetIndex == _herePlanet)?.BiomeIndex
            : null;
        // Your bases by planet address (same rule as the Bases page).
        _bases = new();
        foreach (var b in (snapshot.Bases ?? new JArray()).OfType<JObject>())
        {
            if (b["peI"]?["DPp"]?.Value<string>() != "HomePlanetBase") continue;
            if (S.ParsePackedAddress(b["oZw"]) is not ulong address) continue;
            string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base";
            _bases[address] = _bases.TryGetValue(address, out var other) ? $"{other}, {name}" : name;
        }
        _caught = ReadCaught(snapshot.Fishing);
    }

    /// <summary>The save's three parallel fishing-record arrays (see NmsFishingPaths)
    /// as id to (times caught, largest catch).</summary>
    private static Dictionary<string, (uint, float)> ReadCaught(JToken? records)
    {
        var caught = new Dictionary<string, (uint, float)>(StringComparer.OrdinalIgnoreCase);
        if (records?["5gB"] is not JArray ids) return caught;
        var counts = records["yv6"] as JArray;
        var largest = records["CXv"] as JArray;
        for (int i = 0; i < ids.Count; i++)
        {
            string id = CatalogService.NormalizeId(ids[i]?.Value<string>() ?? "");
            if (id.Length == 0) continue;
            uint count = counts is not null && i < counts.Count ? counts[i].Value<uint>() : 0;
            float size = largest is not null && i < largest.Count ? largest[i].Value<float>() : 0;
            caught[id] = (count, size);
        }
        return caught;
    }

    // ---------------------------------------------------------------- here

    private void UpdateHere()
    {
        string typeName = _hereBiome is int b ? NmsPlanets.TypeName(b) : "";
        if (PlanetTypeBox.Items.FirstOrDefault() is ComboBoxItem whereItem)
            whereItem.Content = typeName.Length > 0 ? $"Where I am ({typeName})" : "Where I am";

        if (_hereSystem is not ulong system)
        {
            HerePanel.Visibility = Visibility.Collapsed;
            return;
        }
        HerePanel.Visibility = Visibility.Visible;
        var note = SystemNotesService.Get(system);
        string systemName = note?.SystemName is { Length: > 0 } n ? n : "Unnamed system";

        if (_herePlanet == 0)
        {
            HereTitleTxt.Text = "In space";
            HerePlaceTxt.Text = $"{systemName} · {GalaxyLabel(S.GalaxyNumber(system))} · save on a planet to see its fish";
            HereProgressPanel.Visibility = Visibility.Collapsed;
            return;
        }

        string planetName = note is not null
            && note.PlanetNames.TryGetValue(_herePlanet.ToString(CultureInfo.InvariantCulture), out var pn) ? pn : $"Planet {_herePlanet}";
        HereTitleTxt.Text = typeName.Length > 0 ? $"{planetName} · {typeName} planet" : planetName;
        HerePlaceTxt.Text = typeName.Length > 0
            ? $"{systemName} · {GalaxyLabel(S.GalaxyNumber(system))}"
            : $"{systemName} · {GalaxyLabel(S.GalaxyNumber(system))} · this planet's type isn't in the save yet - scan it, or pick a type below";

        if (_hereBiome is int biome)
        {
            var here = _fish.Where(f => f.LivesOn(biome)).ToList();
            HereProgressTxt.Text = $"{here.Count(f => _caught.ContainsKey(f.Id))} / {here.Count}";
            HereProgressPanel.Visibility = Visibility.Visible;
        }
        else HereProgressPanel.Visibility = Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- list

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Apply();
    private void Filter_Click(object sender, RoutedEventArgs e) => Apply();
    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Apply();

    private void Apply()
    {
        if (!_ready) return;
        int typeTag = (PlanetTypeBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? WhereIAm;
        int? biome = typeTag switch
        {
            WhereIAm => _hereBiome,
            AllTypes => null,
            _ => typeTag,
        };
        int show = (ShowBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? 0;
        string time = (TimeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        string rarity = (RarityBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        bool anywhere = AnywhereBox.IsChecked == true;
        string text = SearchBox.Text.Trim();

        var planetFish = _fish.Where(f => biome is not int b || f.LivesOn(b))
            .Where(f => anywhere || !f.Biomes.Contains(FishInfo.AnyPlanet))
            .ToList();
        var shown = planetFish
            .Where(f => show == 0 || (show == 2) == _caught.ContainsKey(f.Id))
            .Where(f => time.Length == 0 || f.Time == time || f.Time == "Both")
            .Where(f => rarity.Length == 0 || f.Quality == rarity)
            .Where(f => text.Length == 0 || NameOf(f.Id).Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(f => _caught.ContainsKey(f.Id))
            .ThenBy(f => RarityRank(f.Quality))
            .ThenBy(f => NameOf(f.Id), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var spots = FishingSpots();
        FishList.ItemsSource = shown.Select(f => ToCard(f, spots)).ToList();
        int caughtCount = planetFish.Count(f => _caught.ContainsKey(f.Id));
        CountTxt.Text = $"Showing {shown.Count} · {caughtCount} of {planetFish.Count} caught";
    }

    /// <summary>Fishing spots recorded on the Finds page, by fish id.</summary>
    private static Dictionary<string, List<string>> FishingSpots()
    {
        var spots = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var find in FindsService.All.Where(f => f.Type == "Fishing spot"))
            foreach (var id in find.ItemIds)
            {
                if (!spots.TryGetValue(id, out var list)) spots[id] = list = new();
                list.Add(find.Title.Length > 0 ? find.Title : find.PortalCode);
            }
        return spots;
    }

    private FishCardViewModel ToCard(FishInfo f, Dictionary<string, List<string>> spots)
    {
        bool caught = _caught.TryGetValue(f.Id, out var record);
        var traits = new List<string> { SizeLabel(f.Size), TimeLabel(f.Time) };
        if (f.NeedsStorm) traits.Add("Storms only");
        string habitat = f.Biomes.Contains(FishInfo.AnyPlanet)
            ? "Any planet with water"
            : string.Join(", ", f.Biomes.Select(NmsPlanets.TypeName)) + " planets";
        spots.TryGetValue(f.Id, out var spotList);

        return new FishCardViewModel
        {
            Id = f.Id,
            Name = NameOf(f.Id),
            Icon = CatalogService.TryGet(f.Id)?.Icon,
            Rarity = f.Quality.ToUpperInvariant(),
            RarityBrush = new SolidColorBrush(RarityColour(f.Quality)),
            Traits = string.Join(" · ", traits),
            Habitat = habitat,
            Caught = caught
                ? $"Caught {record.Count:N0} time{(record.Count == 1 ? "" : "s")}{(record.Largest > 0 ? $" · largest {record.Largest:0.##} kg" : "")}"
                : "Not caught yet",
            CaughtBrush = ThemeResources.Brush(caught ? "ThemeDoneBrush" : "ThemeWarningBrush"),
            CardOpacity = caught ? 0.6 : 1.0,
            Spots = spotList is null ? "" : $"Your spots: {string.Join(", ", spotList)}",
            SpotsVisibility = spotList is null ? Visibility.Collapsed : Visibility.Visible,
        };
    }

    // ---------------------------------------------------------------- where to catch it

    private async void FishList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not FishCardViewModel card) return;
        _whereFish = _fish.FirstOrDefault(f => f.Id == card.Id);
        if (_whereFish is null) return;
        WhereIcon.Source = card.Icon;
        WhereNameTxt.Text = card.Name;
        WhereHabitatTxt.Text = $"{card.Habitat} · {card.Traits}";
        WherePanel.Visibility = Visibility.Visible;
        UpdateWhere();
        await LoadOtherSavesAsync();
    }

    private void WhereClose_Click(object sender, RoutedEventArgs e)
    {
        _whereFish = null;
        WherePanel.Visibility = Visibility.Collapsed;
    }

    private async void OtherSlots_Click(object sender, RoutedEventArgs e)
    {
        UpdateWhere();
        await LoadOtherSavesAsync();
    }

    /// <summary>Reads the other save slots' planets once (KnownPlanetsService
    /// caches them by file time), only when the panel first needs them.</summary>
    private async Task LoadOtherSavesAsync()
    {
        if (OtherSlotsBox.IsChecked != true || _otherSaves is not null) return;
        if (SaveSessionManager.ActivePlatformFolder is not string folder || SaveSessionManager.ActiveSlot is not { } slot) return;
        WhereCountTxt.Text = "Reading your other save slots...";
        _otherSaves = await KnownPlanetsService.ReadOtherSlotsAsync(folder, slot.SlotId);
        UpdateWhere();
    }

    /// <summary>Every known planet whose type the fish lives on, nearest first.
    /// Fish that live on any planet need water, which the save doesn't record,
    /// so those lists say so.</summary>
    private void UpdateWhere()
    {
        if (_whereFish is not FishInfo fish) return;
        bool anyPlanet = fish.Biomes.Contains(FishInfo.AnyPlanet);
        var planets = OtherSlotsBox.IsChecked == true && _otherSaves is not null
            ? _thisSave.Concat(_otherSaves).GroupBy(p => p.Address).Select(g => g.First())
            : _thisSave;

        var rows = planets.Where(p => fish.LivesOn(p.BiomeIndex))
            .Select(p => (Planet: p, Distance: _hereSystem is ulong h ? NmsPlanets.DistanceLightYears(h, p.SystemAddress) : null))
            .OrderBy(x => _bases.ContainsKey(x.Planet.Address) ? 0 : 1)
            .ThenBy(x => x.Distance ?? double.MaxValue)
            .ToList();

        var shown = rows.Take(100).Select(x => ToWhereRow(x.Planet, x.Distance)).ToList();
        WhereList.ItemsSource = shown;
        string where = anyPlanet ? "planets" : string.Join(" or ", fish.Biomes.Select(NmsPlanets.TypeName)) + " planets";
        WhereCountTxt.Text = rows.Count == 0
            ? $"You haven't discovered any {where} on {(OtherSlotsBox.IsChecked == true ? "your saves" : "this save")} yet."
            : $"{rows.Count:N0} known {where}{(rows.Count > shown.Count ? $", nearest {shown.Count} shown" : "")} - your bases first, then nearest."
              + (anyPlanet ? " Not every planet has water: look for lakes or seas from orbit." : "");
    }

    private FishPlanetRowViewModel ToWhereRow(KnownPlanet p, double? distance)
    {
        var note = SystemNotesService.Get(p.SystemAddress);
        string planetName = note is not null
            && note.PlanetNames.TryGetValue(p.PlanetIndex.ToString(CultureInfo.InvariantCulture), out var n) ? n : $"Planet {p.PlanetIndex}";
        var detail = new List<string> { p.HasRings ? $"{p.TypeName} · rings" : p.TypeName };
        detail.Add(note?.SystemName is { Length: > 0 } system ? system : "Unnamed system");
        detail.Add(GalaxyLabel(p.Galaxy));
        if (p.Source.Length > 0) detail.Add(p.Source);

        string distanceText = _hereSystem is ulong here && p.SystemAddress == here
            ? (p.PlanetIndex == _herePlanet ? "You're on this planet" : "In the system you're in")
            : distance switch
            {
                null => "In a different galaxy from you",
                < 1 => "Under 400 ly from you",
                double ly => $"About {ly:N0} ly from you",
            };
        _bases.TryGetValue(p.Address, out var baseName);

        return new FishPlanetRowViewModel
        {
            Title = planetName,
            Detail = string.Join(" · ", detail),
            Distance = distanceText,
            Swatch = new SolidColorBrush(ParseColour(NmsPlanets.TypeColour(p.BiomeIndex))),
            PortalCode = p.PortalCode,
            Glyphs = GlyphsFor(p.PortalCode),
            BaseText = baseName is null ? "" : $"Your base: {baseName}",
            BaseVisibility = baseName is null ? Visibility.Collapsed : Visibility.Visible,
        };
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code || code.Length == 0) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }

    // ---------------------------------------------------------------- helpers

    private static Windows.UI.Color ParseColour(string hex) => Windows.UI.Color.FromArgb(255,
        byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber), byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber),
        byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber));

    private static List<ImageSource> GlyphsFor(string code)
    {
        if (code.Length != 12) return new();
        var images = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }

    private string NameOf(string id) =>
        _names.TryGetValue(id, out var n) ? n
        : CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } raw
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant()) : id;

    private static int RarityRank(string quality) => quality switch
    {
        "Common" => 0, "Rare" => 1, "Epic" => 2, "Legendary" => 3, _ => 4,
    };

    // Rarity colours - fixed like the class badges on Finds, not themed.
    private static Windows.UI.Color RarityColour(string quality) => quality switch
    {
        "Rare" => Windows.UI.Color.FromArgb(255, 60, 110, 200),
        "Epic" => Windows.UI.Color.FromArgb(255, 130, 80, 200),
        "Legendary" => Windows.UI.Color.FromArgb(255, 201, 151, 34),
        _ => Windows.UI.Color.FromArgb(255, 95, 110, 120),
    };

    private static string SizeLabel(string size) => size switch
    {
        "ExtraLarge" => "Extra large",
        "" => "",
        _ => size,
    };

    private static string TimeLabel(string time) => time switch
    {
        "Day" => "Day only",
        "Night" => "Night only",
        _ => "Day or night",
    };

    private static string GalaxyLabel(int galaxy) =>
        NmsGalaxyNames.ForNumber(galaxy) is string name ? $"{name} ({galaxy})" : $"Galaxy {galaxy}";
}
