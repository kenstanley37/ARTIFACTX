using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Space station directorship: shows the station in the player's current
/// system with its claim progress, claims it in one click, and lists owned
/// stations and claims in progress. See <see cref="NmsSpaceStationPaths"/>
/// for the confirmed save mapping. Pilot page for the theme system - every
/// colour comes from the active theme's Theme* keys (see ThemeService).
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
    private const double TileWidth = 224;
    private const double TileHeight = 126;

    private static readonly string[] HowToStepTexts =
    {
        "In-game, fly to the station and dock inside it.",
        "Save while docked, then quit to the main menu (or close the game).",
        "Load that save in ArtifactX. The station appears under Current Station.",
        "Press Claim Station, then Save.",
        "Load the save in-game. The Station Core now offers Manage Station.",
    };

    // Static so the selected station survives navigating away and back.
    private static ulong? _selectedStationAddress;
    private List<OwnedStation> _owned = new();
    private int _detailsVersion;
    private int _heroVersion;

    // Decoded screenshots per address for this page's lifetime - LoadPage runs
    // on every edit, and re-reading ~900 KB per tile each time would be wasteful.
    private readonly Dictionary<ulong, Task<WriteableBitmap?>> _screenshots = new();

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
        BuildHowToSteps();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionChanged;
        Unloaded += Page_Unloaded;
        StationDetailsCard.SizeChanged += StationDetailsCard_SizeChanged;

        LoadPage();
    }

    private static Brush ThemeBrush(string key) => (Brush)Application.Current.Resources[key];
    private static FontFamily ThemeFont(string key) => (FontFamily)Application.Current.Resources[key];

    private void OnSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadPage);

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            StationDetailsCard.MinHeight = 0; // different save, different stations
            _screenshots.Clear();
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
        DirectorBadge.Visibility = Visibility.Collapsed;
        ClaimStationBtn.Visibility = Visibility.Collapsed;
        HeroShot.Source = null;
        RequirementsPanel.Visibility = Visibility.Collapsed;
        OwnedTilesPanel.Children.Clear();
        OwnedHeaderTxt.Text = string.Empty;
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
            DirectorBadge.Visibility = Visibility.Collapsed;
            ClaimStationBtn.Visibility = Visibility.Collapsed;
            RequirementsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        RequirementsPanel.Visibility = Visibility.Visible;
        bool hasStation = stationNames.TryGetValue(address, out var name);
        CurrentStationNameTxt.Text = hasStation ? name! : "No station recorded in this system";

        ShowRequirementValues();

        bool isOwned = ownedSystems.Contains(address);
        bool claimPending = SaveSessionManager.HasStagedEditsUnder(S.BasePath(BaseCount()));

        DirectorBadge.Visibility = isOwned ? Visibility.Visible : Visibility.Collapsed;
        ClaimStationBtn.Visibility = isOwned ? Visibility.Collapsed : Visibility.Visible;
        ClaimStationBtn.IsEnabled = hasStation;
        CurrentStatusTxt.Text = claimPending
            ? "Station claimed. Save your changes to write it to the save file."
            : isOwned
                ? string.Empty
                : !hasStation
                    ? "No station is recorded in this system yet - dock at it in-game first."
                    : "Claim Station meets every requirement and makes you director, just like the in-game takeover.";
        CurrentStatusTxt.Visibility = string.IsNullOrEmpty(CurrentStatusTxt.Text) ? Visibility.Collapsed : Visibility.Visible;

        _ = ShowHeroScreenshotAsync(address, hasStation ? name! : string.Empty);
    }

    /// <summary>The game only caches screenshots of the player's own bases, so
    /// an unclaimed station shows the plain panel behind the overlay.</summary>
    private async Task ShowHeroScreenshotAsync(ulong address, string name)
    {
        int version = ++_heroVersion;
        var bitmap = await GetScreenshotAsync(address, name);
        if (version == _heroVersion)
            HeroShot.Source = bitmap;
    }

    private void ShowRequirementValues()
    {
        var race = Best(S.RaceStats, _currentGroup);
        var guild = Best(S.GuildStats, _currentGroup);
        int contracts = S.ReadStat(_currentGroup, S.SalvageContractsStatId);

        RaceStandingTxt.Text = $"{race.Value} / {S.RaceStandingRequired}";
        GuildStandingTxt.Text = $"{guild.Value} / {S.GuildStandingRequired}";
        ContractsTxt.Text = $"{contracts} / {S.SalvageContractsRequired}";

        SetMeter(RaceBar, race.Value, S.RaceStandingRequired);
        SetMeter(GuildBar, guild.Value, S.GuildStandingRequired);
        SetMeter(ContractsBar, contracts, S.SalvageContractsRequired);
    }

    /// <summary>Fills a bar Grid to value/required, gold (the theme's "done"
    /// colour) once the requirement is met.</summary>
    private static void SetMeter(Grid bar, int value, int required)
    {
        double ratio = required <= 0 ? 0 : Math.Clamp((double)value / required, 0, 1);
        bar.Children.Clear();
        bar.ColumnDefinitions.Clear();
        bar.Background = ThemeBrush("ThemeTrackBrush");
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - ratio, GridUnitType.Star) });
        bar.Children.Add(new Border { Background = ThemeBrush(ratio >= 1 ? "ThemeDoneBrush" : "ThemeAccentBrush") });
    }

    /// <summary>The race/guild with the most standing in this system - the
    /// one the player has been working on is almost certainly the one the
    /// station asks for. Null name when there's no standing with any.</summary>
    private static (string? Name, int Value) Best((string StatId, string Name)[] stats, JObject? group)
    {
        var best = stats.Select(s => (s.Name, Value: S.ReadStat(group, s.StatId))).MaxBy(s => s.Value);
        return best.Value > 0 ? best : (null, 0);
    }

    private void BuildHowToSteps()
    {
        for (int i = 0; i < HowToStepTexts.Length; i++)
        {
            var step = new StackPanel { Spacing = 4, Width = 190, Padding = new Thickness(12), Background = ThemeBrush("ThemePanelAltBrush") };
            step.Children.Add(new Border { Height = 2, Background = ThemeBrush("ThemeAccentBrush"), Margin = new Thickness(-12, -12, -12, 6) });
            step.Children.Add(new TextBlock
            {
                Text = (i + 1).ToString(),
                FontFamily = ThemeFont("ThemeDisplayFont"),
                FontWeight = FontWeights.Bold,
                FontSize = 22,
                Foreground = ThemeBrush("ThemeAccentBrush"),
            });
            step.Children.Add(new TextBlock
            {
                Text = HowToStepTexts[i],
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeBrush("ThemeTextDimBrush"),
            });
            HowToSteps.Children.Add(step);
        }
    }

    private void HowToToggle_Click(object sender, RoutedEventArgs e)
    {
        bool open = HowToSteps.Visibility != Visibility.Visible;
        HowToSteps.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        HowToGlyph.Text = open ? "–" : "+";
    }

    private void ShowOwnedStations(List<OwnedStation> owned)
    {
        _owned = owned;
        OwnedHeaderTxt.Text = owned.Count == 1 ? "1 station" : $"{owned.Count} stations";

        // Keep the previous pick; otherwise default to the station in the
        // current system, else the first one.
        ulong? target = owned.Any(o => o.Address == _selectedStationAddress) ? _selectedStationAddress
            : owned.Any(o => o.Address == _currentSystemAddress) ? _currentSystemAddress
            : owned.FirstOrDefault()?.Address;
        _selectedStationAddress = target;

        OwnedTilesPanel.Children.Clear();
        if (owned.Count == 0)
        {
            OwnedTilesPanel.Children.Add(new TextBlock { Text = "None yet. Claim the station you're docked at above.", Foreground = ThemeBrush("ThemeTextDimBrush") });
            StationDetailsCard.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var station in owned)
            OwnedTilesPanel.Children.Add(CreateStationTile(station));

        var selected = owned.FirstOrDefault(o => o.Address == target);
        if (selected is not null)
            _ = ShowStationDetailsAsync(selected);
    }

    /// <summary>Screenshot tile: image, bottom fade, name, optional "CURRENT"
    /// badge. The selected tile gets the accent border.</summary>
    private Button CreateStationTile(OwnedStation station)
    {
        bool isSelected = station.Address == _selectedStationAddress;
        var image = new Image { Stretch = Stretch.UniformToFill };
        var content = new Grid { Width = TileWidth, Height = TileHeight, Background = ThemeBrush("ThemePanelAltBrush") };
        content.Children.Add(image);
        content.Children.Add(new Rectangle { Fill = ThemeBrush("ThemeTileFadeBrush") });
        content.Children.Add(new TextBlock
        {
            Text = station.Name,
            FontFamily = ThemeFont("ThemeDisplayFont"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrush("ThemeTextBrush"),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(10, 0, 10, 8),
        });
        if (station.Address == _currentSystemAddress)
        {
            content.Children.Add(new Border
            {
                Background = ThemeBrush("ThemeDoneBrush"),
                Padding = new Thickness(7, 3, 7, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8),
                Child = new TextBlock
                {
                    Text = "CURRENT",
                    FontFamily = ThemeFont("ThemeDisplayFont"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    CharacterSpacing = 200,
                    Foreground = ThemeBrush("ThemeOnAccentBrush"),
                },
            });
        }

        var tile = new Button
        {
            Content = content,
            Padding = new Thickness(0),
            Background = ThemeBrush("ThemePanelAltBrush"),
            BorderBrush = ThemeBrush(isSelected ? "ThemeAccentBrush" : "ThemeLineBrush"),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            CornerRadius = (CornerRadius)Application.Current.Resources["ThemeCardCornerRadius"],
            Tag = station.Address,
        };
        ToolTipService.SetToolTip(tile, station.Name);
        tile.Click += (_, _) =>
        {
            if (_selectedStationAddress == station.Address) return;
            _selectedStationAddress = station.Address;
            foreach (var other in OwnedTilesPanel.Children.OfType<Button>())
            {
                bool on = other.Tag is ulong a && a == station.Address;
                other.BorderBrush = ThemeBrush(on ? "ThemeAccentBrush" : "ThemeLineBrush");
                other.BorderThickness = new Thickness(on ? 2 : 1);
            }
            _ = ShowStationDetailsAsync(station);
        };
        _ = SetTileImageAsync(image, station);
        return tile;
    }

    private async Task SetTileImageAsync(Image image, OwnedStation station) =>
        image.Source = await GetScreenshotAsync(station.Address, station.Name);

    private static Task WarmStationAssetsAsync() =>
        _stationAssetsWarmup ??= CatalogService.WarmCacheAsync(
            Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));

    private async Task ShowStationDetailsAsync(OwnedStation station)
    {
        int version = ++_detailsVersion;
        StationDetailsCard.Visibility = Visibility.Visible;
        DetailNameTxt.Text = station.Name.ToUpperInvariant();

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

        // Galaxy
        int galaxy = S.GalaxyNumber(address);
        var galaxyLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        galaxyLine.Children.Add(new TextBlock
        {
            Text = NmsGalaxyNames.ForNumber(galaxy) ?? $"Galaxy {galaxy}",
            FontFamily = ThemeFont("ThemeDisplayFont"),
            FontWeight = FontWeights.Bold,
            FontSize = 24,
            Foreground = ThemeBrush("ThemeTextBrush"),
        });
        if (NmsGalaxyNames.ForNumber(galaxy) is not null)
            galaxyLine.Children.Add(new TextBlock { Text = $"Galaxy {galaxy}", VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4), Foreground = ThemeBrush("ThemeTextDimBrush") });
        AddFactSection("GALAXY", galaxyLine);

        // History: discovered -> claimed
        var discoveries = ReadDiscoveriesInSystem(address);
        var systemRecord = discoveries.FirstOrDefault(d => d["8P3"]?["<Dn"]?.Value<string>() == "SolarSystem");
        long discoveredAt = systemRecord?["ksu"]?["3I1"]?.Value<long>() ?? 0;
        string discoverer = systemRecord?["ksu"]?["V?:"]?.Value<string>() ?? string.Empty;
        long claimedAt = station.Record["3?K"]?["3I1"]?.Value<long>() ?? 0;
        var timeline = new Grid { ColumnSpacing = 12 };
        timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 24 });
        timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var discoveredText = TimelinePoint("Discovered", discoveredAt, discoverer);
        var arrow = new Rectangle { Height = 1, VerticalAlignment = VerticalAlignment.Center, Fill = ThemeBrush("ThemeAccentBrush"), Opacity = 0.6 };
        var claimedText = TimelinePoint("Claimed", claimedAt, null);
        Grid.SetColumn(arrow, 1);
        Grid.SetColumn(claimedText, 2);
        timeline.Children.Add(discoveredText);
        timeline.Children.Add(arrow);
        timeline.Children.Add(claimedText);
        AddFactSection("HISTORY", timeline);

        // Discoveries as count chips
        var chips = SummarizeDiscoveries(discoveries);
        if (chips.Count > 0)
        {
            var chipPanel = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            foreach (var (count, label) in chips)
                chipPanel.Children.Add(CreateChip(count, label));
            AddFactSection("YOUR DISCOVERIES HERE", chipPanel);
        }

        // Local standing as bars with a tick at the claim requirement
        var group = ReadSystemStats(SaveSessionManager.GetValue(S.StatGroupsPath) as JArray)
            .FirstOrDefault(g => g.Address == address)?.Group;
        var standings = S.RaceStats.Select(r => (Label: r.Name, Value: S.ReadStat(group, r.StatId), Required: S.RaceStandingRequired))
            .Concat(S.GuildStats.Select(g => (Label: g.Name, Value: S.ReadStat(group, g.StatId), Required: S.GuildStandingRequired)))
            .Append((Label: "Salvage contracts", Value: S.ReadStat(group, S.SalvageContractsStatId), Required: S.SalvageContractsRequired))
            .Where(s => s.Value > 0)
            .ToList();
        if (standings.Count > 0)
        {
            var bars = new StackPanel { Spacing = 9 };
            foreach (var (label, value, required) in standings)
                bars.Children.Add(CreateStandingRow(label, value, required));
            bars.Children.Add(new TextBlock { Text = "Tick marks show the claim requirement.", FontSize = 12, Foreground = ThemeBrush("ThemeTextFaintBrush") });
            AddFactSection("LOCAL STANDING", bars);
        }

        // Small facts row
        var mini = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 24, VerticalSpacing = 6 };
        if (station.Record["@ZJ"] is JArray parts)
            mini.Children.Add(MiniFact("Parts placed", parts.Count.ToString("N0")));
        var otherBases = ReadOtherBasesInSystem(address, station.Record);
        mini.Children.Add(MiniFact("Other bases here", otherBases.Count == 0 ? "None" : string.Join(", ", otherBases.Select(b => b.Item2))));
        DetailFactsPanel.Children.Add(mini);

        DetailScreenshot.Source = null;
        DetailNoScreenshotTxt.Visibility = Visibility.Collapsed;
        var bitmap = await GetScreenshotAsync(station.Address, station.Name);
        if (version != _detailsVersion) return;
        DetailScreenshot.Source = bitmap;
        DetailNoScreenshotTxt.Visibility = bitmap is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddFactSection(string title, UIElement body)
    {
        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = ThemeFont("ThemeDisplayFont"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            CharacterSpacing = 260,
            Foreground = ThemeBrush("ThemeAccentBrush"),
        });
        section.Children.Add(body);
        DetailFactsPanel.Children.Add(section);
    }

    private static StackPanel TimelinePoint(string label, long unixSeconds, string? by)
    {
        var point = new StackPanel { Spacing = 0 };
        point.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = ThemeBrush("ThemeTextDimBrush") });
        point.Children.Add(new TextBlock { Text = FormatTimestamp(unixSeconds), FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = ThemeBrush("ThemeTextBrush") });
        if (!string.IsNullOrEmpty(by))
            point.Children.Add(new TextBlock { Text = $"by {by}", FontSize = 12, Foreground = ThemeBrush("ThemeTextDimBrush") });
        return point;
    }

    private static Border CreateChip(int count, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new TextBlock
        {
            Text = count.ToString("N0"),
            FontFamily = ThemeFont("ThemeDisplayFont"),
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            Foreground = ThemeBrush("ThemeAccentBrush"),
        });
        row.Children.Add(new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Foreground = ThemeBrush("ThemeTextBrush") });
        return new Border
        {
            Child = row,
            Padding = new Thickness(12, 5, 12, 5),
            Background = ThemeBrush("ThemePanelAltBrush"),
            BorderBrush = ThemeBrush("ThemeLineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)Application.Current.Resources["ThemeCardCornerRadius"],
        };
    }

    /// <summary>Label | bar (scaled so the requirement sits at ~60%, with a
    /// tick there) | value. Gold once the requirement is met.</summary>
    private static Grid CreateStandingRow(string label, int value, int required)
    {
        double max = Math.Max(required * 1.6, value);
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

        row.Children.Add(new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Foreground = ThemeBrush("ThemeTextBrush") });

        var track = new Grid { Height = 6, VerticalAlignment = VerticalAlignment.Center, Background = ThemeBrush("ThemeTrackBrush") };
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(value / max, GridUnitType.Star) });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - value / max, GridUnitType.Star) });
        track.Children.Add(new Border { Background = ThemeBrush(value >= required ? "ThemeDoneBrush" : "ThemeAccentAltBrush") });

        // Tick at the requirement: a second grid split at required/max.
        var tickHost = new Grid { Height = 14, VerticalAlignment = VerticalAlignment.Center };
        tickHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(required / max, GridUnitType.Star) });
        tickHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - required / max, GridUnitType.Star) });
        var tick = new Rectangle { Width = 2, HorizontalAlignment = HorizontalAlignment.Left, Fill = ThemeBrush("ThemeTextDimBrush") };
        Grid.SetColumn(tick, 1);
        tickHost.Children.Add(tick);

        var barCell = new Grid();
        barCell.Children.Add(track);
        barCell.Children.Add(tickHost);
        Grid.SetColumn(barCell, 1);
        row.Children.Add(barCell);

        var number = new TextBlock
        {
            Text = value.ToString("N0"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = ThemeFont("ThemeDisplayFont"),
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeBrush("ThemeTextBrush"),
        };
        Grid.SetColumn(number, 2);
        row.Children.Add(number);
        return row;
    }

    private static StackPanel MiniFact(string label, string value)
    {
        var fact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        fact.Children.Add(new TextBlock { Text = label, FontSize = 14, Foreground = ThemeBrush("ThemeTextDimBrush") });
        fact.Children.Add(new TextBlock { Text = value, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = ThemeBrush("ThemeTextBrush") });
        return fact;
    }

    /// <summary>One glyph tile per hex digit of the portal address; the first
    /// (planet) glyph is outlined. Falls back to the plain code if the catalog
    /// predates add-station-assets.</summary>
    private void ShowPortalGlyphs(ulong address)
    {
        string code = S.PortalAddress(address);
        PortalGlyphsPanel.Children.Clear();
        ToolTipService.SetToolTip(PortalGlyphsBorder, $"Portal code {code}");

        var icons = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        if (icons.Any(i => i is null))
        {
            PortalGlyphsPanel.Children.Add(new TextBlock { Text = code, FontSize = 16, IsTextSelectionEnabled = true, Foreground = ThemeBrush("ThemeTextBrush") });
            return;
        }

        for (int i = 0; i < icons.Count; i++)
        {
            PortalGlyphsPanel.Children.Add(new Border
            {
                Width = 30,
                Height = 30,
                Background = ThemeBrush("ThemePanelAltBrush"),
                BorderBrush = i == 0 ? ThemeBrush("ThemeAccentBrush") : null,
                BorderThickness = new Thickness(i == 0 ? 1 : 0),
                Child = new Image { Source = icons[i], Width = 24, Height = 24 },
            });
        }
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

    /// <summary>(count, label) pairs for discovery types with a plain-English
    /// name; planets counted by distinct planet index.</summary>
    private static List<(int Count, string Label)> SummarizeDiscoveries(List<JObject> discoveries)
    {
        int CountOf(string type) => discoveries.Count(d => d["8P3"]?["<Dn"]?.Value<string>() == type);

        int planets = discoveries
            .Where(d => d["8P3"]?["<Dn"]?.Value<string>() == "Planet")
            .Select(d => S.ParsePackedAddress(d["8P3"]?["5L6"]) is ulong a ? a >> 52 : 0)
            .Distinct()
            .Count();

        var result = new List<(int, string)>();
        if (planets > 0) result.Add((planets, planets == 1 ? "planet" : "planets"));
        if (CountOf("Animal") is int fauna and > 0) result.Add((fauna, "fauna"));
        if (CountOf("Flora") is int flora and > 0) result.Add((flora, "flora"));
        if (CountOf("Mineral") is int minerals and > 0) result.Add((minerals, minerals == 1 ? "mineral" : "minerals"));
        return result;
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

    /// <summary>Cached per address for the page's lifetime; null when the game
    /// has no screenshot for that address.</summary>
    private Task<WriteableBitmap?> GetScreenshotAsync(ulong address, string name)
    {
        if (!_screenshots.TryGetValue(address, out var task))
        {
            task = LoadScreenshotAsync(SaveSessionManager.ActivePlatformFolder, address, name);
            _screenshots[address] = task;
        }
        return task;
    }

    private static async Task<WriteableBitmap?> LoadScreenshotAsync(string? platformFolder, ulong address, string name)
    {
        byte[]? pixels = await Task.Run(() => ReadScreenshotPixels(platformFolder, address, name));
        if (pixels is null) return null;

        var bitmap = new WriteableBitmap(ScreenshotWidth, ScreenshotHeight);
        using (var stream = bitmap.PixelBuffer.AsStream())
            await stream.WriteAsync(pixels);
        bitmap.Invalidate();
        return bitmap;
    }

    /// <summary>Newest cached screenshot for this address (renamed stations
    /// keep their old files too, so an exact-name match wins), with the game's
    /// RGBA pixels swapped to the BGRA order WriteableBitmap wants.</summary>
    private static byte[]? ReadScreenshotPixels(string? platformFolder, ulong address, string name)
    {
        try
        {
            if (platformFolder is null) return null;
            string cacheFolder = System.IO.Path.Combine(platformFolder, "cache");
            if (!Directory.Exists(cacheFolder)) return null;

            var newest = new DirectoryInfo(cacheFolder)
                .GetFiles($"{address:x16}.*.DDS")
                .Where(f => f.Length == ScreenshotWidth * ScreenshotHeight * 4)
                .OrderByDescending(f => name.Length > 0 && f.Name.Contains($".{name}.", StringComparison.Ordinal))
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
                Foreground = ThemeBrush("ThemeTextDimBrush"),
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
