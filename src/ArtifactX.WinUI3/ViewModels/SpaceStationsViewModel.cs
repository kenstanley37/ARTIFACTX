using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// Space Stations page: the station in the player's current system with its claim
/// requirements, the stations they direct (tiles + the selected one's details) and
/// claims in progress elsewhere. Reads and claims through SpaceStationService.
/// </summary>
public sealed partial class SpaceStationsViewModel : ObservableObject
{
    private const string HowToFlag = "SpaceStationsHowToExpanded";

    // Static so the selected station survives navigating away and back.
    private static ulong? _selectedStationAddress;

    // Decoded screenshots per address while the page is open - Load runs on every
    // edit, and re-reading ~900 KB per tile each time would be wasteful.
    private readonly Dictionary<ulong, Task<WriteableBitmap?>> _screenshots = new();
    private StationOverview? _overview;
    private int _heroVersion;
    private int _detailsVersion;
    private int _claimsVersion;

    public SpaceStationsViewModel() => HowToOpen = AppSettingsService.GetFlag(HowToFlag, false);

    public static IReadOnlyList<StationHowToStep> HowToSteps { get; } = new[]
    {
        "In-game, fly to the station and dock inside it.",
        "Save while docked, then quit to the main menu (or close the game).",
        "Load that save in ArtifactX. The station appears under Current Station.",
        "Press Claim Station, then Save.",
        "Load the save in-game. The Station Core now offers Manage Station.",
    }.Select((text, i) => new StationHowToStep(i + 1, text)).ToList();

    public static IReadOnlyList<string> ClaimSortOptions { get; } =
        new[] { "Closest to done first", "Nearest to you first", "Richest first", "Name (A to Z)" };

    // ------------------------------------------------------------ current station

    [ObservableProperty] public partial string CurrentName { get; private set; } = "";
    [ObservableProperty] public partial string CurrentLocation { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string CurrentStatus { get; private set; } = "";

    public bool HasStatus => CurrentStatus.Length > 0;

    [ObservableProperty] public partial bool IsDirector { get; private set; }

    /// <summary>Furniture parts in the station you direct here (counted toward the 21,000
    /// build limit) - offered for clearing right after a claim (user idea, 2026-10-21).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClearFurniture), nameof(FurnitureText), nameof(ClearFurnitureLabel))]
    public partial int FurnitureCount { get; private set; }

    public bool ShowClearFurniture => FurnitureCount > 0;
    public string FurnitureText => $"This station has {FurnitureCount:N0} furniture parts - they count toward your 21,000 build limit. Clearing them keeps the station's own structure.";
    public string ClearFurnitureLabel => $"Clear the furniture ({FurnitureCount:N0} parts)";
    [ObservableProperty] public partial bool ShowClaim { get; private set; }
    [ObservableProperty] public partial bool CanClaim { get; private set; }
    [ObservableProperty] public partial bool ShowRequirements { get; private set; }

    /// <summary>The station's screenshot behind the hero card. The game only caches
    /// screenshots of the player's own bases, so an unclaimed station has none.</summary>
    [ObservableProperty] public partial ImageSource? Hero { get; private set; }

    [ObservableProperty] public partial StationRequirementViewModel? Race { get; private set; }
    [ObservableProperty] public partial StationRequirementViewModel? Guild { get; private set; }
    [ObservableProperty] public partial StationRequirementViewModel? Contracts { get; private set; }

    [ObservableProperty] public partial bool HasEdits { get; private set; }

    [ObservableProperty] public partial bool HowToOpen { get; set; }

    partial void OnHowToOpenChanged(bool value) => AppSettingsService.SetFlag(HowToFlag, value);

    // ------------------------------------------------------------ owned stations

    public ObservableCollection<StationTileViewModel> OwnedStations { get; } = new();

    [ObservableProperty] public partial string OwnedHeader { get; private set; } = "";
    [ObservableProperty] public partial bool NoOwned { get; private set; }
    [ObservableProperty] public partial StationDetailsViewModel? Details { get; private set; }

    // ------------------------------------------------------------ claims in progress

    public ObservableCollection<StationClaimViewModel> Claims { get; } = new();

    [ObservableProperty] public partial bool HasClaims { get; private set; }
    [ObservableProperty] public partial string ClaimsHeader { get; private set; } = "CLAIMS IN PROGRESS";
    [ObservableProperty] public partial int ClaimSortIndex { get; set; }
    [ObservableProperty] public partial bool ShowHidden { get; set; }
    [ObservableProperty] public partial string ShowHiddenLabel { get; private set; } = "Show hidden";
    [ObservableProperty] public partial bool ShowHiddenEnabled { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClaimHint))]
    public partial string ClaimHint { get; private set; } = "";

    public bool HasClaimHint => ClaimHint.Length > 0;

    partial void OnClaimSortIndexChanged(int value) => _ = RenderClaimsAsync();
    partial void OnShowHiddenChanged(bool value) => _ = RenderClaimsAsync();

    // ------------------------------------------------------------ loading

    /// <summary>A different save: its stations and screenshots replace this one's.</summary>
    public void ResetForNewSave()
    {
        _screenshots.Clear();
        Load();
    }

    public void Load()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            ShowNoSave();
            return;
        }

        var overview = SpaceStationService.ReadOverview();
        _overview = overview;
        ShowCurrentStation(overview);
        HasClaims = overview.Claims.Count > 0;
        _ = ShowStationsAsync(overview);
        HasEdits = SpaceStationService.HasEdits(overview);
    }

    private void ShowNoSave()
    {
        _overview = null;
        CurrentName = "No save loaded";
        CurrentLocation = "";
        CurrentStatus = "Load a save to view and edit space stations.";
        IsDirector = ShowClaim = ShowRequirements = false;
        Hero = null;
        OwnedStations.Clear();
        OwnedHeader = "";
        NoOwned = false;
        Details = null;
        Claims.Clear();
        HasClaims = false;
        HasEdits = false;
    }

    private void ShowCurrentStation(StationOverview overview)
    {
        CurrentLocation = overview.Location;

        if (overview.CurrentAddress is not ulong address)
        {
            CurrentName = "Unknown location";
            CurrentStatus = "This save's current system couldn't be read.";
            IsDirector = ShowClaim = ShowRequirements = false;
            return;
        }

        ShowRequirements = true;
        bool hasStation = overview.StationNames.TryGetValue(address, out var name);
        CurrentName = hasStation ? name! : "No station recorded in this system";

        var race = SpaceStationService.Best(S.RaceStats, overview.CurrentGroup);
        var guild = SpaceStationService.Best(S.GuildStats, overview.CurrentGroup);
        Race = new StationRequirementViewModel("Local race standing", race.Value, S.RaceStandingRequired);
        Guild = new StationRequirementViewModel("Local guild standing", guild.Value, S.GuildStandingRequired);
        Contracts = new StationRequirementViewModel("Salvage contracts", S.ReadStat(overview.CurrentGroup, S.SalvageContractsStatId), S.SalvageContractsRequired);

        bool isOwned = overview.IsOwned(address);
        IsDirector = isOwned;
        ShowClaim = !isOwned;
        FurnitureCount = 0;
        if (isOwned) _ = CountFurnitureAsync(address);
        CanClaim = hasStation;
        CurrentStatus = SpaceStationService.IsClaimPending()
            ? "Station claimed. Save your changes to write it to the save file."
            : isOwned
                ? ""
                : !hasStation
                    ? "No station is recorded in this system yet - dock at it in-game first."
                    : "Claim Station meets every requirement and makes you director, just like the in-game takeover.";

        _ = ShowHeroAsync(address, hasStation ? name! : "");
    }

    private async Task CountFurnitureAsync(ulong address)
    {
        if (SpaceStationService.DirectedStationParts(address) is not JArray parts) return;
        var ids = parts.OfType<JObject>().Select(p => p["r<7"]?.Value<string>() ?? "").Where(id => id.Length > 0).ToList();
        await CatalogService.WarmCacheAsync(ids.Distinct());
        if (_overview?.CurrentAddress != address) return; // moved on meanwhile
        FurnitureCount = ids.Count(BasePartNames.CanRemoveFromStation);
    }

    /// <summary>The furniture kinds and counts, for the confirmation.</summary>
    public IReadOnlyList<string> FurnitureSummary()
    {
        if (_overview?.CurrentAddress is not ulong address || SpaceStationService.DirectedStationParts(address) is not JArray parts) return [];
        return parts.OfType<JObject>().Select(p => p["r<7"]?.Value<string>() ?? "").Where(BasePartNames.CanRemoveFromStation)
            .GroupBy(id => id).OrderByDescending(g => g.Count())
            .Select(g => $"{BasePartNames.Name(g.Key)} · {g.Count():N0}").ToList();
    }

    public void ClearFurniture()
    {
        if (_overview?.CurrentAddress is ulong address) SpaceStationService.ClearFurniture(address);
        // A staged edit raises PendingEditsChanged, and the page reloads.
    }

    private async Task ShowHeroAsync(ulong address, string name)
    {
        int version = ++_heroVersion;
        var bitmap = await GetScreenshotAsync(address, name);
        if (version == _heroVersion) Hero = bitmap;
    }

    private void ShowOwnedStations(StationOverview overview)
    {
        var owned = overview.Owned;
        OwnedHeader = owned.Count == 1 ? "1 station" : $"{owned.Count} stations";
        NoOwned = owned.Count == 0;

        // Keep the previous pick; otherwise default to the station in the
        // current system, else the first one.
        _selectedStationAddress = owned.Any(o => o.Address == _selectedStationAddress) ? _selectedStationAddress
            : owned.Any(o => o.Address == overview.CurrentAddress) ? overview.CurrentAddress
            : owned.FirstOrDefault()?.Address;

        OwnedStations.Clear();
        // The station you're at first, then the nearest (user, 2026-10-21) - they were in
        // the save's order. Stations in another galaxy have no distance and go last.
        ulong? here = overview.CurrentAddress;
        var ordered = owned
            .OrderBy(o => o.Address == here ? 0 : 1)
            .ThenBy(o => here is ulong from ? NmsPlanets.DistanceLightYears(from, o.Address) ?? double.MaxValue : 0)
            .ThenBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var station in ordered)
        {
            var tile = new StationTileViewModel(station, station.Address == overview.CurrentAddress)
            {
                IsSelected = station.Address == _selectedStationAddress,
            };
            OwnedStations.Add(tile);
            _ = LoadTilePictureAsync(tile);
        }

        if (OwnedStations.FirstOrDefault(t => t.IsSelected) is { } selected)
            _ = ShowDetailsAsync(selected.Station);
        else
            Details = null;
    }

    private async Task LoadTilePictureAsync(StationTileViewModel tile) =>
        tile.Picture = await GetScreenshotAsync(tile.Address, tile.Name);

    /// <summary>Shows a station's details; false when it already was the one shown.</summary>
    public bool Select(StationTileViewModel tile)
    {
        if (_selectedStationAddress == tile.Address) return false;
        _selectedStationAddress = tile.Address;
        foreach (var other in OwnedStations) other.IsSelected = other == tile;
        _ = ShowDetailsAsync(tile.Station);
        return true;
    }

    private async Task ShowDetailsAsync(OwnedStation station)
    {
        int version = ++_detailsVersion;
        try
        {
            await PortalGlyphs.WarmAsync();
        }
        catch (Exception)
        {
            // The glyph strip falls back to plainer glyphs; the details still show.
        }
        if (version != _detailsVersion) return; // a newer selection won

        var details = new StationDetailsViewModel(station);
        Details = details;
        details.Screenshot = await GetScreenshotAsync(station.Address, station.Name);
        details.ScreenshotLoaded = true;
    }

    /// <summary>Cached per address while the page is open; null when the game has
    /// no screenshot for that address.</summary>
    private Task<WriteableBitmap?> GetScreenshotAsync(ulong address, string name)
    {
        if (!_screenshots.TryGetValue(address, out var task))
        {
            task = GameScreenshotCache.LoadAsync(SaveSessionManager.ActivePlatformFolder, address, name);
            _screenshots[address] = task;
        }
        return task;
    }

    // ------------------------------------------------------------ claims in progress

    public void ToggleHidden(StationClaimViewModel claim)
    {
        HiddenStationsService.SetHidden(claim.Address, !claim.IsHidden);
        _ = RenderClaimsAsync();
    }

    /// <summary>The tiles and claim cards, once the descriptor words are in: a noted
    /// "Flourishing" only reads as High ★★★ after they load - the tiles were built first
    /// and showed the bare word (2026-10-21).</summary>
    private async Task ShowStationsAsync(StationOverview overview)
    {
        await LoadDescriptorsAsync();
        if (!ReferenceEquals(overview, _overview)) return; // a newer Load took over
        ShowOwnedStations(overview);
        await RenderClaimsAsync();
    }

    /// <summary>The player's own notes on a system (wealth, economy, conflict) are game
    /// words; the descriptor table turns them into tiers.</summary>
    private static async Task LoadDescriptorsAsync()
    {
        try { await SystemNotesService.EnsureDescriptorsAsync(); }
        catch (Exception) { } // wealth labels just show plainer; the cards still render
    }

    /// <summary>Claims in progress as cards, sorted and filtered.</summary>
    private async Task RenderClaimsAsync()
    {
        int version = ++_claimsVersion;
        try { await PortalGlyphs.WarmAsync(); }
        catch (Exception) { } // glyphs just don't show; the cards still render
        await LoadDescriptorsAsync();
        if (version != _claimsVersion || _overview is not { } overview) return;

        var all = overview.Claims
            .Select(s => new StationClaimViewModel(s, overview.StationName(s.Address), overview.CurrentAddress))
            .ToList();
        int hiddenCount = all.Count(c => c.IsHidden);
        var shown = all.Where(c => ShowHidden || !c.IsHidden);
        var ordered = ClaimSortIndex switch
        {
            1 => shown.OrderBy(c => c.Distance ?? double.MaxValue),
            2 => shown.OrderByDescending(c => c.Wealth).ThenByDescending(c => c.Progress),
            3 => shown.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => shown.OrderByDescending(c => c.Progress),
        };

        ClaimsHeader = $"CLAIMS IN PROGRESS · {all.Count}";
        ShowHiddenLabel = hiddenCount > 0 ? $"Show hidden ({hiddenCount})" : "Show hidden";
        ShowHiddenEnabled = hiddenCount > 0 || ShowHidden;

        Claims.Clear();
        foreach (var claim in ordered) Claims.Add(claim);
        ClaimHint = Claims.Count == 0 && hiddenCount > 0
            ? $"You've hidden all {hiddenCount}. Tick Show hidden to see them again."
            : "";
    }

    // ------------------------------------------------------------ editing

    public void Claim()
    {
        if (_overview is null) return;
        if (SpaceStationService.Claim(_overview) is string problem)
            CurrentStatus = problem;
        // A staged claim raises PendingEditsChanged, and the page reloads.
    }

    public void Reset()
    {
        if (_overview is null) return;
        SpaceStationService.RevertClaim(_overview);
        Load();
    }
}
