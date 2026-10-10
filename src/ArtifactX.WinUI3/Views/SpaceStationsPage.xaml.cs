using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Space station directorship: shows the station in the player's current
/// system with its claim progress, claims it in one click, and lists owned
/// stations and claims in progress. See <see cref="NmsSpaceStationPaths"/>
/// for the confirmed save mapping.
/// </summary>
public sealed partial class SpaceStationsPage : Page
{
    private sealed record SystemStats(int Index, ulong Address, JObject Group);

    private sealed record OwnedStation(string Name, ulong Address, JObject Record);

    // The game caches a 640x360 RGBA screenshot per base/station in the
    // platform folder's cache/ dir, named "<16-hex address>.Normal.<name>.<uid>.DDS"
    // despite having no DDS header - raw pixels only (confirmed 2026-10-10).
    private const int ScreenshotWidth = 640;
    private const int ScreenshotHeight = 360;

    // Static so the selected station survives navigating away and back.
    private static ulong? _selectedStationAddress;
    private List<OwnedStation> _owned = new();
    private bool _suppressStationSelection;
    private int _detailsVersion;

    // Portal glyph images (PORTALSYMBOL_0..F) come from the catalog DB - see
    // DataCataloger's add-station-assets. Galaxy names: NmsGalaxyNames.
    private static Task? _stationAssetsWarmup;

    // Resolved on every LoadPage. _currentGroupIndex is where the current
    // system's ^SYSTEM_STATS group lives - or, if the game hasn't created it
    // yet, the index a new one gets appended at.
    private ulong? _currentSystemAddress;
    private int _currentGroupIndex = -1;
    private JObject? _currentGroup;

    public SpaceStationsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionChanged;
        Unloaded += Page_Unloaded;
        StationDetailsCard.SizeChanged += StationDetailsCard_SizeChanged;

        LoadPage();
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadPage);

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            StationDetailsCard.MinHeight = 0; // different save, different stations
            LoadPage();
        });

    /// <summary>Grow-only height: each station has a different number of fact
    /// rows, and a card that shrinks while the page is scrolled down made the
    /// whole page jump on every selection (user report 2026-10-10).</summary>
    private void StationDetailsCard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Height > StationDetailsCard.MinHeight)
            StationDetailsCard.MinHeight = e.NewSize.Height;
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionChanged;
    }

    private void LoadPage()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            ShowNoSave();
            return;
        }

        var groups = SaveSessionManager.GetValue(S.StatGroupsPath) as JArray;
        var systemStats = ReadSystemStats(groups);
        var stationNames = ReadStationNamesBySystem();
        var owned = ReadOwnedStations();
        var ownedSystems = owned.Select(o => o.Address).ToHashSet();

        _currentSystemAddress = S.ReadSystemAddress(SaveSessionManager.GetValue(S.CurrentUniverseAddressPath));
        var current = systemStats.FirstOrDefault(s => s.Address == _currentSystemAddress);
        _currentGroupIndex = current?.Index ?? groups?.Count ?? -1;
        _currentGroup = current?.Group;

        ShowCurrentStation(stationNames, ownedSystems);
        ShowOwnedStations(owned);
        ShowInProgress(systemStats, stationNames, ownedSystems);
        UpdateResetButton();
    }

    private void ShowNoSave()
    {
        _currentSystemAddress = null;
        _currentGroupIndex = -1;
        _currentGroup = null;

        CurrentStationNameTxt.Text = "No save loaded";
        CurrentLocationTxt.Text = string.Empty;
        CurrentStatusTxt.Text = "Load a save to view and edit space stations.";
        ClaimStationBtn.IsEnabled = false;
        RequirementsPanel.Visibility = Visibility.Collapsed;
        OwnedStationsList.Items.Clear();
        StationDetailsCard.Visibility = Visibility.Collapsed;
        InProgressSection.Visibility = Visibility.Collapsed;
        InProgressPanel.Children.Clear();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }

    private void ShowCurrentStation(Dictionary<ulong, string> stationNames, HashSet<ulong> ownedSystems)
    {
        CurrentLocationTxt.Text = SaveSessionManager.GetValue(S.LocationDescriptionPath)?.Value<string>() ?? string.Empty;

        if (_currentSystemAddress is not ulong address)
        {
            CurrentStationNameTxt.Text = "Unknown location";
            CurrentStatusTxt.Text = "This save's current system couldn't be read.";
            ClaimStationBtn.IsEnabled = false;
            RequirementsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        RequirementsPanel.Visibility = Visibility.Visible;
        CurrentStationNameTxt.Text = stationNames.TryGetValue(address, out var name) ? name : "No station recorded in this system";

        ShowRequirementValues();

        bool isOwned = ownedSystems.Contains(address);
        bool claimPending = SaveSessionManager.HasStagedEditsUnder(S.BasePath(BaseCount()));

        ClaimStationBtn.IsEnabled = !isOwned && stationNames.ContainsKey(address);
        CurrentStatusTxt.Text = claimPending
            ? "Station claimed. Save your changes to write it to the save file."
            : isOwned
                ? "You are the director of this station."
                : !stationNames.ContainsKey(address)
                    ? "No station is recorded in this system yet - dock at it in-game first."
                    : "Claim Station meets every requirement and makes you director, just like the in-game takeover.";
    }

    private void ShowRequirementValues()
    {
        var race = Best(S.RaceStats, _currentGroup);
        var guild = Best(S.GuildStats, _currentGroup);
        RaceStandingTxt.Text = $"{race.Value} / {S.RaceStandingRequired}";
        GuildStandingTxt.Text = $"{guild.Value} / {S.GuildStandingRequired}";
        ContractsTxt.Text = $"{S.ReadStat(_currentGroup, S.SalvageContractsStatId)} / {S.SalvageContractsRequired}";
    }

    /// <summary>The race/guild with the most standing in this system - the
    /// one the player has been working on is almost certainly the one the
    /// station asks for. Null name when there's no standing with any.</summary>
    private static (string? Name, int Value) Best((string StatId, string Name)[] stats, JObject? group)
    {
        var best = stats.Select(s => (s.Name, Value: S.ReadStat(group, s.StatId))).MaxBy(s => s.Value);
        return best.Value > 0 ? best : (null, 0);
    }

    private void ShowOwnedStations(List<OwnedStation> owned)
    {
        _owned = owned;
        OwnedHeaderTxt.Text = $"Stations You Direct ({owned.Count})";

        _suppressStationSelection = true;
        OwnedStationsList.Items.Clear();
        foreach (var station in owned)
        {
            OwnedStationsList.Items.Add(new ListViewItem
            {
                Content = CreateRow(station.Name, station.Address == _currentSystemAddress ? "Current system" : null),
                Tag = station.Address,
            });
        }

        // Keep the previous pick; otherwise default to the station in the
        // current system, else the first one.
        ulong? target = owned.Any(o => o.Address == _selectedStationAddress) ? _selectedStationAddress
            : owned.Any(o => o.Address == _currentSystemAddress) ? _currentSystemAddress
            : owned.FirstOrDefault()?.Address;
        int index = owned.FindIndex(o => o.Address == target);
        OwnedStationsList.SelectedIndex = index;
        _suppressStationSelection = false;

        if (index >= 0)
            _ = ShowStationDetailsAsync(owned[index]);
        else
            StationDetailsCard.Visibility = Visibility.Collapsed;
    }

    private void OwnedStationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressStationSelection || OwnedStationsList.SelectedItem is not ListViewItem { Tag: ulong address }) return;

        var station = _owned.FirstOrDefault(o => o.Address == address);
        if (station is null) return;

        _selectedStationAddress = address;
        _ = ShowStationDetailsAsync(station);
    }

    private static Task WarmStationAssetsAsync() =>
        _stationAssetsWarmup ??= CatalogService.WarmCacheAsync(
            Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));

    private async Task ShowStationDetailsAsync(OwnedStation station)
    {
        int version = ++_detailsVersion;
        StationDetailsCard.Visibility = Visibility.Visible;
        DetailNameTxt.Text = station.Name;

        try
        {
            await WarmStationAssetsAsync();
        }
        catch (Exception)
        {
            // Don't cache a failed lookup forever - retry on the next selection.
            // Glyphs fall back to the plain portal code meanwhile.
            _stationAssetsWarmup = null;
        }
        if (version != _detailsVersion) return; // a newer selection won

        DetailFactsPanel.Children.Clear();
        ulong address = station.Address;
        ShowPortalGlyphs(address);

        int galaxy = S.GalaxyNumber(address);
        AddFactSection("Location", new[]
        {
            ("Galaxy", NmsGalaxyNames.ForNumber(galaxy) is string galaxyName
                ? $"{galaxyName} (Galaxy {galaxy})"
                : $"Galaxy {galaxy}"),
        });

        var history = new List<(string, string)>();
        if (station.Record["3?K"]?["3I1"]?.Value<long>() is long claimed and > 0)
            history.Add(("Claimed", FormatTimestamp(claimed)));

        var discoveries = ReadDiscoveriesInSystem(address);
        var systemRecord = discoveries.FirstOrDefault(d => d["8P3"]?["<Dn"]?.Value<string>() == "SolarSystem");
        if (systemRecord?["ksu"] is JObject discoverer)
        {
            string by = discoverer["V?:"]?.Value<string>() ?? string.Empty;
            string when = FormatTimestamp(discoverer["3I1"]?.Value<long>() ?? 0);
            history.Add(("System discovered", string.IsNullOrEmpty(by) ? when : $"{when} by {by}"));
        }
        AddFactSection("History", history);

        string? discoverySummary = SummarizeDiscoveries(discoveries);
        if (discoverySummary is not null)
            AddFactSection("Your Discoveries Here", new[] { ("Found", discoverySummary) });

        var group = ReadSystemStats(SaveSessionManager.GetValue(S.StatGroupsPath) as JArray)
            .FirstOrDefault(g => g.Address == address)?.Group;
        var standings = S.RaceStats.Select(r => (Label: $"{r.Name} standing", Value: S.ReadStat(group, r.StatId)))
            .Concat(S.GuildStats.Select(g => (Label: $"{g.Name} standing", Value: S.ReadStat(group, g.StatId))))
            .Append((Label: "Salvage contracts", Value: S.ReadStat(group, S.SalvageContractsStatId)))
            .Where(s => s.Value > 0)
            .Select(s => (s.Label, s.Value.ToString("N0")))
            .ToList();
        AddFactSection("Local Standing", standings);

        AddFactSection("Other Bases in This System", ReadOtherBasesInSystem(address, station.Record));

        if (station.Record["@ZJ"] is JArray parts)
            AddFactSection("Station", new[] { ("Parts placed", parts.Count.ToString("N0")) });

        await LoadScreenshotAsync(station, version);
    }

    /// <summary>One glyph image per hex digit of the portal address. Falls back
    /// to the plain code if the catalog predates add-station-assets.</summary>
    private void ShowPortalGlyphs(ulong address)
    {
        string code = S.PortalAddress(address);
        PortalGlyphsPanel.Children.Clear();
        ToolTipService.SetToolTip(PortalGlyphsBorder, $"Portal code {code}");

        var icons = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        if (icons.Any(i => i is null))
        {
            PortalGlyphsPanel.Children.Add(new TextBlock { Text = code, FontSize = 16, IsTextSelectionEnabled = true });
            return;
        }

        foreach (var icon in icons)
            PortalGlyphsPanel.Children.Add(new Image { Source = icon, Width = 29, Height = 29 });
    }

    private void AddFactSection(string title, IEnumerable<(string Label, string Value)> facts)
    {
        var list = facts.ToList();
        if (list.Count == 0) return;

        var section = new StackPanel { Spacing = 4 };
        section.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        foreach (var (label, value) in list)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NmsTextSecondary"],
            };
            var valueText = new TextBlock { Text = value, FontSize = 13, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            Grid.SetColumn(valueText, 1);
            row.Children.Add(labelText);
            row.Children.Add(valueText);
            section.Children.Add(row);
        }
        DetailFactsPanel.Children.Add(section);
    }

    private static string FormatTimestamp(long unixSeconds) =>
        unixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.ToString("g") : "Unknown";

    private static List<JObject> ReadDiscoveriesInSystem(ulong systemAddress)
    {
        if (SaveSessionManager.GetValue(S.DiscoveriesPath) is not JArray records) return new();

        return records.OfType<JObject>()
            .Where(r => S.ParsePackedAddress(r["8P3"]?["5L6"]) is ulong a && S.ToSystemAddress(a) == systemAddress)
            .ToList();
    }

    /// <summary>"6 planets · 3 fauna · 2 flora" - only discovery types with a
    /// plain-English name; planets counted by distinct planet index.</summary>
    private static string? SummarizeDiscoveries(List<JObject> discoveries)
    {
        int CountOf(string type) => discoveries.Count(d => d["8P3"]?["<Dn"]?.Value<string>() == type);

        int planets = discoveries
            .Where(d => d["8P3"]?["<Dn"]?.Value<string>() == "Planet")
            .Select(d => S.ParsePackedAddress(d["8P3"]?["5L6"]) is ulong a ? a >> 52 : 0)
            .Distinct()
            .Count();

        var parts = new List<string>();
        if (planets > 0) parts.Add(planets == 1 ? "1 planet" : $"{planets} planets");
        if (CountOf("Animal") is int fauna and > 0) parts.Add($"{fauna} fauna");
        if (CountOf("Flora") is int flora and > 0) parts.Add($"{flora} flora");
        if (CountOf("Mineral") is int minerals and > 0) parts.Add(minerals == 1 ? "1 mineral" : $"{minerals} minerals");

        return parts.Count > 0 ? string.Join("  ·  ", parts) : null;
    }

    /// <summary>Planet bases and space bases sharing the station's system.
    /// Freighter and ship-dock records also carry an address but just follow
    /// the player around, so they're left out.</summary>
    private static List<(string, string)> ReadOtherBasesInSystem(ulong systemAddress, JObject station)
    {
        var result = new List<(string, string)>();
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases) return result;

        foreach (var record in bases.OfType<JObject>())
        {
            if (ReferenceEquals(record, station)) continue;
            string? kind = record["peI"]?["DPp"]?.Value<string>() switch
            {
                "HomePlanetBase" => "Planet base",
                "PlayerSpaceBase" => "Space base",
                _ => null,
            };
            if (kind is null) continue;
            if (S.ParsePackedAddress(record["oZw"]) is not ulong a || S.ToSystemAddress(a) != systemAddress) continue;

            string name = record["NKm"]?.Value<string>() ?? string.Empty;
            result.Add((kind, string.IsNullOrWhiteSpace(name) ? "Unnamed" : name));
        }
        return result;
    }

    private async Task LoadScreenshotAsync(OwnedStation station, int version)
    {
        DetailScreenshot.Source = null;
        DetailNoScreenshotTxt.Visibility = Visibility.Collapsed;

        string? platformFolder = SaveSessionManager.ActivePlatformFolder;
        byte[]? pixels = await Task.Run(() => ReadScreenshotPixels(platformFolder, station));
        if (version != _detailsVersion) return; // a newer selection won

        if (pixels is null)
        {
            DetailNoScreenshotTxt.Visibility = Visibility.Visible;
            return;
        }

        var bitmap = new WriteableBitmap(ScreenshotWidth, ScreenshotHeight);
        using (var stream = bitmap.PixelBuffer.AsStream())
            await stream.WriteAsync(pixels);
        bitmap.Invalidate();
        DetailScreenshot.Source = bitmap;
    }

    /// <summary>Newest cached screenshot for this station's address (renamed
    /// stations keep their old files too, so an exact-name match wins), with
    /// the game's RGBA pixels swapped to the BGRA order WriteableBitmap wants.</summary>
    private static byte[]? ReadScreenshotPixels(string? platformFolder, OwnedStation station)
    {
        try
        {
            if (platformFolder is null) return null;
            string cacheFolder = Path.Combine(platformFolder, "cache");
            if (!Directory.Exists(cacheFolder)) return null;

            var newest = new DirectoryInfo(cacheFolder)
                .GetFiles($"{station.Address:x16}.*.DDS")
                .Where(f => f.Length == ScreenshotWidth * ScreenshotHeight * 4)
                .OrderByDescending(f => f.Name.Contains($".{station.Name}.", StringComparison.Ordinal))
                .ThenByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest is null) return null;

            byte[] pixels = File.ReadAllBytes(newest.FullName);
            for (int i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            return pixels;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void ShowInProgress(List<SystemStats> systemStats, Dictionary<ulong, string> stationNames, HashSet<ulong> ownedSystems)
    {
        InProgressPanel.Children.Clear();

        var rows = systemStats
            .Where(s => !ownedSystems.Contains(s.Address) && HasAnyProgress(s.Group))
            .ToList();

        InProgressSection.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var stats in rows)
        {
            string name = stationNames.TryGetValue(stats.Address, out var n) ? n : "Unnamed station";
            if (stats.Address == _currentSystemAddress) name += " (current system)";
            InProgressPanel.Children.Add(CreateRow(name, DescribeProgress(stats)));
        }
    }

    private static StackPanel CreateRow(string title, string? detail)
    {
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(detail))
        {
            row.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NmsTextSecondary"],
            });
        }
        return row;
    }

    /// <summary>Replicates the in-game takeover (see NmsSpaceStationPaths for
    /// the diff it's based on): requirements, the F?0 station
    /// record, ^STATIONS_OWNED + 1, and the marker's ownership stamp.</summary>
    private void ClaimStationBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded || _currentSystemAddress is not ulong address) return;

        int markerIndex = FindStationMarkerIndex(address, out string stationName);
        JObject? owner = FindOwnerBlock();
        if (markerIndex < 0 || owner is null)
        {
            CurrentStatusTxt.Text = markerIndex < 0
                ? "No station is recorded in this system yet - dock at it in-game first."
                : "Couldn't find your player details on any existing base to sign the station with.";
            return;
        }

        MeetRequirements();

        // The game deducts a 1,000,000,000 Units donation on takeover - deliberately
        // NOT replicated here (user decision 2026-10-09: claiming shouldn't cost Units).

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SaveSessionManager.StageEdit(S.CreateClaimedStationRecord(address, stationName, owner, now), S.BasePath(BaseCount()));

        if (FindStationsOwnedStat() is (int g, int s))
        {
            var path = NmsPlayerStatsPaths.StatIntValuePath(g, s);
            SaveSessionManager.StageEdit((int)(SaveSessionManager.GetLong(path) ?? 0) + 1, path);
        }

        if (FindOwnershipStamp() is JArray stamp)
            SaveSessionManager.StageEdit(stamp.DeepClone(), S.MarkerOwnershipStampPath(markerIndex));
    }

    private void MeetRequirements()
    {
        // Raise, never lower - a player who already overshot keeps their standing.
        var raised = new Dictionary<string, int>();
        // Each station checks one race and one guild, decided by the game at
        // runtime and not stored in the save - so raise all three of each.
        // Extra standing is local to this one system, so it's harmless.
        var requirements = S.RaceStats.Select(r => (r.StatId, Required: S.RaceStandingRequired))
            .Concat(S.GuildStats.Select(g => (g.StatId, Required: S.GuildStandingRequired)))
            .Append((S.SalvageContractsStatId, Required: S.SalvageContractsRequired));

        foreach (var (statId, required) in requirements)
        {
            if (S.ReadStat(_currentGroup, statId) < required)
                raised[statId] = required;
        }
        if (raised.Count > 0)
            StageStats(raised);
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGroupIndex >= 0)
            SaveSessionManager.RevertEditsUnder(S.StatGroupPath(_currentGroupIndex));

        SaveSessionManager.RevertEditsUnder(S.BasePath(BaseCount()));
        if (FindStationsOwnedStat() is (int g, int s))
            SaveSessionManager.RevertEditsUnder(NmsPlayerStatsPaths.StatIntValuePath(g, s));
        if (_currentSystemAddress is ulong address && FindStationMarkerIndex(address, out _) is int marker and >= 0)
            SaveSessionManager.RevertEditsUnder(S.MarkerOwnershipStampPath(marker));

        LoadPage();
    }

    /// <summary>Stages the current system's whole stats group at gUR/[index]
    /// (appending a new group if the game hasn't created one yet). Staging the
    /// group rather than the top-level gUR array keeps this page from
    /// colliding with other pages' edits to ^GLOBAL_STATS in the same array.</summary>
    private void StageStats(Dictionary<string, int> values)
    {
        if (_currentSystemAddress is not ulong address || _currentGroupIndex < 0) return;

        var group = (JObject?)_currentGroup?.DeepClone() ?? S.CreateSystemStatsGroup(address);
        foreach (var (statId, value) in values)
            S.WriteStat(group, statId, value);

        _currentGroup = group;
        SaveSessionManager.StageEdit(group, S.StatGroupPath(_currentGroupIndex));
    }

    private void UpdateResetButton()
    {
        bool hasEdits = (_currentGroupIndex >= 0 && SaveSessionManager.HasStagedEditsUnder(S.StatGroupPath(_currentGroupIndex)))
            || SaveSessionManager.HasStagedEditsUnder(S.BasePath(BaseCount()));
        PageResetBtn.Visibility = hasEdits ? Visibility.Visible : Visibility.Collapsed;
    }

    private static int BaseCount() => (SaveSessionManager.GetValue(S.BasesPath) as JArray)?.Count ?? 0;

    private static int FindStationMarkerIndex(ulong systemAddress, out string stationName)
    {
        stationName = string.Empty;
        if (SaveSessionManager.GetValue(S.MarkersPath) is not JArray markers) return -1;

        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i] is not JObject marker) continue;
            if (!string.Equals(marker["iAF"]?.Value<string>(), S.StationMarkerType, StringComparison.Ordinal)) continue;
            if (S.ReadSystemAddress(marker["yhJ"]) != systemAddress) continue;

            stationName = marker["NKm"]?.Value<string>() ?? string.Empty;
            return i;
        }

        return -1;
    }

    /// <summary>The 4 x uint32 stamp the game writes on an owned station's
    /// marker - identical across all of a player's stations, so it's copied
    /// from any marker that already carries one. Null on a first-ever claim,
    /// in which case the marker is left as-is - CONFIRMED fine in-game
    /// 2026-10-09 (first claim on a fresh Slot 5 still showed Manage Station).</summary>
    private static JArray? FindOwnershipStamp()
    {
        if (SaveSessionManager.GetValue(S.MarkersPath) is not JArray markers) return null;

        foreach (var marker in markers.OfType<JObject>())
        {
            if (!string.Equals(marker["iAF"]?.Value<string>(), S.StationMarkerType, StringComparison.Ordinal)) continue;
            if (marker["6H2"]?["8?J"] is JArray stamp && stamp.Any(v => v.Value<long>() != 0))
                return stamp;
        }

        return null;
    }

    /// <summary>Owner block (3?K: platform user id, username, platform) from
    /// the player's own bases, preferring an existing owned station's.</summary>
    private static JObject? FindOwnerBlock()
    {
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases) return null;

        return bases.OfType<JObject>()
            .OrderByDescending(b => string.Equals(b["peI"]?["DPp"]?.Value<string>(), S.PlayerStationBaseType, StringComparison.Ordinal))
            .Select(b => b["3?K"] as JObject)
            .FirstOrDefault(o => !string.IsNullOrEmpty(o?["f5Q"]?.Value<string>()));
    }

    private static (int Group, int Stat)? FindStationsOwnedStat()
    {
        if (SaveSessionManager.GetValue(S.StatGroupsPath) is not JArray groups) return null;

        for (int g = 0; g < groups.Count; g++)
        {
            if (groups[g] is not JObject group) continue;
            if (!string.Equals(group[":rc"]?.Value<string>(), S.GlobalStatsGroupId, StringComparison.Ordinal)) continue;
            if (group["gUR"] is not JArray stats) return null;

            for (int i = 0; i < stats.Count; i++)
            {
                if (string.Equals(stats[i]?["b2n"]?.Value<string>(), S.StationsOwnedStatId, StringComparison.Ordinal))
                    return (g, i);
            }
            return null;
        }

        return null;
    }

    /// <summary>Every ^SYSTEM_STATS group, read through any staged edit so the
    /// page reflects pending changes - plus a staged-but-not-yet-committed new
    /// group sitting one past the end of the array.</summary>
    private static List<SystemStats> ReadSystemStats(JArray? groups)
    {
        var result = new List<SystemStats>();
        if (groups is null) return result;

        for (int i = 0; i <= groups.Count; i++)
        {
            if (SaveSessionManager.GetValue(S.StatGroupPath(i)) is not JObject group) continue;
            if (!string.Equals(group[":rc"]?.Value<string>(), S.SystemStatsGroupId, StringComparison.Ordinal)) continue;
            if (S.ParsePackedAddress(group["2Ak"]) is not ulong address) continue;

            result.Add(new SystemStats(i, S.ToSystemAddress(address), group));
        }

        return result;
    }

    private static Dictionary<ulong, string> ReadStationNamesBySystem()
    {
        var names = new Dictionary<ulong, string>();
        if (SaveSessionManager.GetValue(S.MarkersPath) is not JArray markers) return names;

        foreach (var token in markers)
        {
            if (token is not JObject marker) continue;
            if (!string.Equals(marker["iAF"]?.Value<string>(), S.StationMarkerType, StringComparison.Ordinal)) continue;
            if (S.ReadSystemAddress(marker["yhJ"]) is not ulong address) continue;

            string name = marker["NKm"]?.Value<string>() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
                names.TryAdd(address, name);
        }

        return names;
    }

    private static List<OwnedStation> ReadOwnedStations()
    {
        var owned = new List<OwnedStation>();
        int count = BaseCount();

        // <= count picks up a claim staged at the append slot but not yet saved.
        for (int i = 0; i <= count; i++)
        {
            if (SaveSessionManager.GetValue(S.BasePath(i)) is not JObject record) continue;
            if (!string.Equals(record["peI"]?["DPp"]?.Value<string>(), S.PlayerStationBaseType, StringComparison.Ordinal)) continue;
            if (S.ParsePackedAddress(record["oZw"]) is not ulong address) continue;

            string name = record["NKm"]?.Value<string>() ?? string.Empty;
            owned.Add(new OwnedStation(string.IsNullOrWhiteSpace(name) ? $"Station {owned.Count + 1}" : name, S.ToSystemAddress(address), record));
        }

        return owned;
    }

    private static bool HasAnyProgress(JObject group) =>
        S.RaceStats.Concat(S.GuildStats).Any(s => S.ReadStat(group, s.StatId) > 0)
        || S.ReadStat(group, S.SalvageContractsStatId) > 0;

    private static string DescribeProgress(SystemStats stats)
    {
        var race = Best(S.RaceStats, stats.Group);
        var guild = Best(S.GuildStats, stats.Group);

        return string.Join("  ·  ",
            $"{race.Name ?? "Race"} {race.Value}/{S.RaceStandingRequired}",
            $"{guild.Name?.Replace(" Guild", "") ?? "Guild"} {guild.Value}/{S.GuildStandingRequired}",
            $"Contracts {S.ReadStat(stats.Group, S.SalvageContractsStatId)}/{S.SalvageContractsRequired}");
    }
}
