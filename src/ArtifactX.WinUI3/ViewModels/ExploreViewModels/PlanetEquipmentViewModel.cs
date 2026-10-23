using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>
/// A building type's procedural availability and hunting instructions on a specific planet.
/// </summary>
public sealed record PlanetBuildingRule(
    string BuildingName,
    string Purpose,
    string StatusText,
    string HowToLocate,
    bool Available)
{
    public Brush StatusBrush => ThemeResources.Brush(Available ? "ThemeDoneBrush" : "ThemeWarningBrush");
}

/// <summary>
/// An active located building, crashed ship, or multi-tool site on this planet from the save's
/// active scan events (vLc.6f=.A1f), settlements, markers, or player Finds.
/// </summary>
public sealed record PlanetLocatedSite(
    string CategoryBadge,
    string Title,
    string Coordinates,
    string Details,
    bool IsHighlight)
{
    public Brush BadgeBackground => ThemeResources.Brush(IsHighlight ? "ThemeDoneBrush" : "ThemeAccentBrush");
    public Brush BadgeForeground => ThemeResources.Brush("ThemeOnAccentBrush");
}

/// <summary>
/// ViewModel for a planet's Multi-Tools, Crashed Ships / Starships, Building Availability Rules,
/// and exact Located Sites on the planet (used on PlanetDetailsPage via PlanetEquipmentPanel).
/// </summary>
public sealed partial class PlanetEquipmentViewModel : ObservableObject
{
    private List<SystemStarshipItemViewModel> _allShips = new();

    [ObservableProperty] public partial string MultiToolsHeading { get; private set; } = "PLANET MULTI-TOOLS & CABINETS";
    [ObservableProperty] public partial string MultiToolsSummary { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<SystemMultiToolItemViewModel> MultiTools { get; private set; } = [];

    [ObservableProperty] public partial IReadOnlyList<PlanetBuildingRule> BuildingRules { get; private set; } = [];

    [ObservableProperty] public partial string LocatedSitesHeading { get; private set; } = "LOCATED CRASHED SHIPS, CABINETS & BUILDINGS ON THIS PLANET";
    [ObservableProperty] public partial string LocatedSitesHint { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<PlanetLocatedSite> LocatedSites { get; private set; } = [];
    [ObservableProperty] public partial bool HasLocatedSites { get; private set; }
    [ObservableProperty] public partial bool NoLocatedSites { get; private set; } = true;

    [ObservableProperty] public partial string StarshipsHeading { get; private set; } = "CRASHED SHIPS & STARSHIPS ON THIS PLANET";
    [ObservableProperty] public partial string StarshipsSummary { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<SystemStarshipItemViewModel> FilteredShips { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<StarshipFilterOption> ShipFilterOptions { get; private set; } = [];
    [ObservableProperty] public partial string SelectedShipFilter { get; set; } = "All";

    partial void OnLocatedSitesChanged(IReadOnlyList<PlanetLocatedSite> value)
    {
        HasLocatedSites = value.Count > 0;
        NoLocatedSites = value.Count == 0;
    }

    public async Task LoadAsync(KnownPlanet planet, PlanetSnapshot snapshot)
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (!PlanetTypesService.IsReady) await PlanetTypesService.EnsureLoadedAsync();

        ulong system = planet.SystemAddress;
        int galaxy = S.GalaxyNumber(system);
        ulong systemPortalCode = UniverseInfo.PortalCode(system, 0);
        var facts = UniverseInfo.For(system);
        var note = SystemNotesService.Get(system);
        bool isUncharted = facts.Uncharted || note?.Race == "None (uncharted)";
        bool isAbandoned = facts.Abandoned;
        bool hasCorrupted = PlanetTypesService.HasCorruptedSentinels(system, planet.PlanetIndex);
        string planetName = UniverseInfo.PlanetName(system, planet.PlanetIndex);
        string classification = planet.Classification(PlanetDetailsService.Get(system, planet.PlanetIndex)?.Descriptor);

        var invTables = PlanetTypesService.InventoryTables ?? await CatalogService.GetInventoryGenerationTablesAsync();
        var words = await CatalogService.GetProceduralNameWordsAsync();

        // 1. Building availability rules for this planet
        bool canSpawnShop = EquipmentGeneration.CanBuildingSpawnOnPlanet("Shop", planet.BiomeIndex, isUncharted, isAbandoned, hasCorrupted, invTables);
        bool canSpawnPillar = EquipmentGeneration.CanBuildingSpawnOnPlanet("DroneHive", planet.BiomeIndex, isUncharted, isAbandoned, hasCorrupted, invTables);
        bool canSpawnOutpost = EquipmentGeneration.CanBuildingSpawnOnPlanet("Outpost", planet.BiomeIndex, isUncharted, isAbandoned, hasCorrupted, invTables);
        bool canSpawnHarmonic = EquipmentGeneration.CanBuildingSpawnOnPlanet("AbandonedRobotCamp", planet.BiomeIndex, isUncharted, isAbandoned, hasCorrupted, invTables);

        var rules = new List<PlanetBuildingRule>
        {
            new(
                "Minor Settlements (Weapon Cabinets)",
                "Standard, Rifle, Pistol, Alien & Experimental Multi-Tools",
                canSpawnShop ? "Spawns on this planet" : "Does NOT spawn on this planet",
                canSpawnShop
                    ? $"Use Commercial Planetary Charts (bought from Space Station Cartographer) to locate Minor Settlements on {planetName}."
                    : isUncharted
                        ? "Uncharted system — no Minor Settlements or Space Station exist in this system."
                        : isAbandoned
                            ? "Abandoned system — Minor Settlements have no working Multi-Tool cabinets."
                            : $"{classification} worlds have 0 Minor Settlements. Save & reload on {planetName}, then check the Space Station cabinet or a Minor Settlement on another planet in this system.",
                canSpawnShop),
            new(
                "Sentinel Pillars (Exotic Multi-Tool)",
                "Royal / Exotic Multi-Tool (#04 in pool)",
                canSpawnPillar ? "Spawns on this planet" : "Does NOT spawn on this planet",
                canSpawnPillar
                    ? $"Use a Sentinel Boundary Map (or defeat all 5 Sentinel waves) on {planetName} to reveal exact Sentinel Pillar coordinates."
                    : isUncharted
                        ? "Uncharted system — Sentinel Pillars do not spawn in uncharted systems."
                        : $"{classification} worlds have 0 Sentinel Pillars. Save & reload on {planetName}, then use a Sentinel Boundary Map on another planet in this system.",
                canSpawnPillar),
            new(
                "Crash Sites (Distress Signals)",
                "Crashed Starships (all 21 system models including Exotic)",
                "Spawns on this planet",
                $"Use Emergency Planetary Charts, Transmission Towers, or Minotaur / Nautilon Radar on {planetName} to mark Crashed Starship coordinates.",
                true),
            new(
                "Trading Posts (Landing Pads)",
                "Live NPC Starship waves (all 21 system models)",
                canSpawnOutpost ? "Spawns on this planet" : "Does NOT spawn on this planet",
                canSpawnOutpost
                    ? $"Use your starship's Economy Scanner while in {planetName}'s atmosphere to pinpoint a Trading Post."
                    : isUncharted || isAbandoned
                        ? "No NPC starship traffic or active Trading Posts in Uncharted/Abandoned systems (crashed ships only)."
                        : $"{classification} worlds do not generate Trading Posts.",
                canSpawnOutpost),
        };

        if (hasCorrupted)
        {
            rules.Add(new(
                "Harmonic Camps & Dissonance Spikes",
                "Sentinel Multi-Tools & Crashed Sentinel Interceptors",
                "Spawns on this planet (Dissonant)",
                $"Destroy Dissonance Resonators on {planetName} for an Echo Locator (reveals a Harmonic Camp with a Sentinel Multi-Tool cabinet and Interceptor locator) or use a Carrier AI Fragment.",
                true));
        }

        BuildingRules = rules;

        // 2. Located Sites on this planet (ScanEvents A1f + Settlements + Finds)
        UpdateLocations(planet, snapshot);

        // 3. Planet Multi-Tool Pool (4 native tools from this planet's seed)
        var seeds = SystemGenerator.Bodies(systemPortalCode, galaxy).PlanetSeeds;
        int pIdx = planet.PlanetIndex - 1;
        ulong planetSeed = (pIdx >= 0 && pIdx < seeds.Length) ? seeds[pIdx] : 0UL;

        if (planetSeed != 0)
        {
            var pool = await Task.Run(() => SystemMultiToolGenerator.GeneratePool(
                planetSeed, MultiToolPoolSource.Planet, planetName, k => words.GetValueOrDefault(k)));

            string BuildToolGuide(SystemMultiTool t, string kind)
            {
                if (t.IsRoyal || string.Equals(kind, "Exotic", StringComparison.OrdinalIgnoreCase))
                {
                    if (isUncharted)
                        return "Cannot be claimed in Uncharted systems (no Sentinel Pillars spawn).";
                    if (canSpawnPillar)
                        return $"How to find on {planetName}: Save & reload on {planetName}, then use a Sentinel Boundary Map to locate a Sentinel Pillar on {planetName}.";
                    return $"No Sentinel Pillars on {planetName} ({classification}): Save & reload on {planetName} first, then fly to another planet in this system and use a Sentinel Boundary Map.";
                }

                if (isUncharted)
                    return "Cannot be claimed in Uncharted systems (no Minor Settlements or Space Station cabinets).";
                if (isAbandoned)
                    return "Cannot be claimed in Abandoned systems (no working Minor Settlement or Space Station cabinets).";
                if (canSpawnShop)
                    return $"How to find on {planetName}: Save & reload on {planetName}, then check Minor Settlement cabinets on {planetName} (or the Space Station cabinet).";
                return $"No Minor Settlements on {planetName} ({classification}): Save & reload on {planetName} first, then fly to the Space Station cabinet or another planet's Minor Settlement.";
            }

            var toolItems = new List<SystemMultiToolItemViewModel>(pool.Count);
            foreach (var t in pool)
            {
                string initialKind = t.IsRoyal ? "Exotic" : "Standard";
                var vm = new SystemMultiToolItemViewModel(
                    t, invTables, isUncharted, isAbandoned, facts.Pirate, OnToolCopied, BuildToolGuide(t, initialKind));
                toolItems.Add(vm);
            }

            MultiTools = toolItems;
            MultiToolsHeading = $"MULTI-TOOLS FOR {planetName.ToUpperInvariant()} ({toolItems.Count})";

            var (_, _, aProb, sProb) = EquipmentGeneration.GetClassProbabilities(facts.Wealth, facts.Pirate, invTables);
            string classNote = isUncharted
                ? "Uncharted system: no Multi-Tool cabinets or Sentinel Pillars spawn here."
                : isAbandoned
                    ? $"Abandoned system: only the Exotic (#04) can be claimed at Sentinel Pillars ({sProb:0}% S-Class, {aProb:0}% A-Class)."
                    : sProb <= 0f
                        ? $"1★ Low Economy: 0% natural S-Class cabinets ({aProb:0}% A-Class; upgradeable to S-Class at the Space Station)."
                        : $"Cabinet S-Class chance in this system: {sProb:0}% S-Class · {aProb:0}% A-Class.";

            MultiToolsSummary = $"{classNote} Saving and reloading on {planetName} activates these 4 tools for every cabinet/pillar in the system.";

            _ = Task.Run(async () =>
            {
                foreach (var item in toolItems)
                {
                    await item.ResolveKindAsync(dispatcher);
                    dispatcher?.TryEnqueue(() =>
                    {
                        item.HowToFindSummary = BuildToolGuide(item.Tool, item.Kind);
                    });
                }
            });
        }
        else
        {
            MultiTools = [];
            MultiToolsSummary = "Planet seed unavailable.";
        }

        // 4. Crashed Ships & Starships on this planet
        var ships = await Task.Run(() => SystemStarshipGenerator.Generate(
            systemPortalCode,
            facts.Race,
            facts.Pirate,
            k => words.GetValueOrDefault(k),
            includeFleet: false,
            includeSentinel: hasCorrupted));

        var orderedShips = ships
            .OrderByDescending(s => s.Type == SystemStarshipType.Exotic)
            .ThenByDescending(s => s.Type == SystemStarshipType.SentinelInterceptor)
            .ThenBy(s => s.Index)
            .ToList();

        var shipItems = new List<SystemStarshipItemViewModel>(orderedShips.Count);
        foreach (var s in orderedShips)
        {
            var stats = EquipmentGeneration.CalculateShipSClassStats(s.Type, s.Seed, invTables);
            string spawnChance = FormatPlanetShipLocation(s.Type, planetName, facts.Wealth, facts.Pirate, isUncharted, isAbandoned, canSpawnOutpost, invTables);
            shipItems.Add(new SystemStarshipItemViewModel(s, stats, spawnChance, OnShipCopied));
        }

        _allShips = shipItems;
        StarshipsHeading = $"CRASHED SHIPS & STARSHIPS ON {planetName.ToUpperInvariant()} ({shipItems.Count})";

        var (cShip, bShip, aShip, sShip) = EquipmentGeneration.GetClassProbabilities(facts.Wealth, facts.Pirate, invTables);
        if (isUncharted || isAbandoned)
        {
            StarshipsSummary =
                $"No NPC ship traffic lands on {planetName} ({(isUncharted ? "Uncharted" : "Abandoned")} system). " +
                $"All {shipItems.Count} models below can be found as Crashed Ships (Distress Signals) on {planetName}'s surface. " +
                $"The Exotic (#21) is always 100% S-Class when found at a crash site" +
                (hasCorrupted ? $", and the Sentinel Interceptor (#36) spawns at Dissonance Spikes on {planetName}." : ".");
        }
        else
        {
            string sRate = sShip <= 0f
                ? $"0% natural S-Class for standard ships ({aShip:0}% A-Class), 100% S-Class for the Exotic (#21)"
                : $"{sShip:0}% S-Class · {aShip:0}% A-Class · {bShip:0}% B-Class · {cShip:0}% C-Class (Exotic #21 is always 100% S-Class)";
            StarshipsSummary =
                $"Class odds on {planetName}: {sRate}. " +
                (canSpawnOutpost
                    ? $"All models below land at Trading Posts on {planetName} and spawn at Crashed Ship sites (Distress Signals)."
                    : $"No Trading Posts spawn on {planetName} ({classification}); on this planet these ships are found at Crash Sites (Distress Signals).");
        }

        UpdateShipFilterOptions();
        ApplyShipFilter();
    }

    /// <summary>
    /// Reads active scan events (A1f), settlements (GQA), bases, and player Finds for this planet
    /// to show exact [Lat, Lon] coordinates of located crashed ships, multi-tool cabinets, and buildings.
    /// </summary>
    public void UpdateLocations(KnownPlanet planet, PlanetSnapshot snapshot)
    {
        var sites = ReadLocatedSites(planet, snapshot);

        LocatedSites = sites;
        LocatedSitesHeading = $"LOCATED SITES, CRASHED SHIPS & CABINETS ON THIS PLANET ({sites.Count})";
        LocatedSitesHint = sites.Count > 0
            ? "Exact planetary coordinates (+Lat, +Lon) read directly from active markers/charts in your save and your logged Finds."
            : "No active Crashed Ship, Minor Settlement, or Sentinel Pillar markers are currently open on this planet in your save. Use a Planetary Chart, Sentinel Boundary Map, Echo Locator, or Signal Booster in game and save to see its exact +Lat, +Lon coordinates here automatically.";
    }

    /// <summary>Reads active markers and player-recorded Finds with exact locations for one planet.</summary>
    public static IReadOnlyList<PlanetLocatedSite> ReadLocatedSites(KnownPlanet planet, PlanetSnapshot snapshot)
    {
        var sites = new List<PlanetLocatedSite>();

        // 1. Active planetary scan events from save (vLc.6f=.A1f)
        foreach (var ev in (snapshot.ScanEvents ?? new JArray()).OfType<JObject>())
        {
            if (S.ParsePackedAddress(ev["oZw"]) != planet.Address) continue;

            var coords = NmsPlanetCoordinates.OfScanEvent(ev);
            if (coords is null) continue;

            string coordText = NmsPlanetCoordinates.Format(coords.Value.Latitude, coords.Value.Longitude);
            string bldClass = ev["iqv"]?["iqv"]?.Value<string>() ?? "Unknown";
            string eventId = (ev["FN5"]?.Value<string>() ?? "").TrimStart('^');

            var (badge, title, details, highlight) = DescribeScanEvent(bldClass, eventId);
            sites.Add(new PlanetLocatedSite(badge, title, coordText, details, highlight));
        }

        // 2. Planetary Settlements on this planet (vLc.6f=.GQA)
        foreach (var st in (snapshot.Settlements ?? new JArray()).OfType<JObject>())
        {
            if (S.ParsePackedAddress(st["oZw"]) != planet.Address) continue;

            string name = st["NKm"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            var coords = NmsPlanetCoordinates.OfBase(st);
            string coordText = coords is { } c ? NmsPlanetCoordinates.Format(c.Latitude, c.Longitude) : "Surface";
            sites.Add(new PlanetLocatedSite(
                "SETTLEMENT",
                name,
                coordText,
                "Planetary Settlement recorded in your save.",
                false));
        }

        // 3. Player Finds recorded in ArtifactX for this planet
        foreach (var find in FindsService.All.Where(f => f.PortalCode == planet.PortalCode && f.Galaxy == planet.Galaxy))
        {
            string badge = string.IsNullOrEmpty(find.Type) ? "FIND" : find.Type.ToUpperInvariant();
            string title = !string.IsNullOrEmpty(find.Title) ? find.Title : find.Type;
            if (!string.IsNullOrEmpty(find.Class))
                title += $" ({find.Class}-Class)";
            string coords = !string.IsNullOrEmpty(find.Location) ? find.Location : "Recorded find";
            string details = !string.IsNullOrEmpty(find.Notes) ? find.Notes : "Saved in your Finds log for this planet.";
            sites.Add(new PlanetLocatedSite(badge, title, coords, details, true));
        }

        return sites;
    }

    private static (string Badge, string Title, string Details, bool IsHighlight) DescribeScanEvent(string buildingClass, string eventId)
    {
        return buildingClass switch
        {
            "DistressSignal" or "WaterDistressSignal" or "AbandonedShip" => (
                "CRASHED SHIP",
                "Distress Signal (Crashed Starship)",
                "Active crash site marker in your save. Fly to these coordinates to claim or salvage the crashed starship.",
                true),
            "SentinelDistressSignal" => (
                "SENTINEL SHIP",
                "Dissonance Spike (Crashed Sentinel Interceptor)",
                "Active crashed Sentinel Interceptor marker in your save. Repair the Hyaline Brain to claim the S/A/B/C Interceptor.",
                true),
            "Shop" => (
                "MULTI-TOOL CABINET",
                "Minor Settlement (Weapon Cabinet)",
                "Contains a Multi-Tool cabinet drawing from the currently active Standard/Alien/Experimental pool, plus a Technology Merchant.",
                true),
            "DroneHive" or "DroneHiveDisabled" => (
                "EXOTIC MULTI-TOOL",
                "Sentinel Pillar (Royal Multi-Tool Terminal)",
                "Access the Pillar Control Terminal at these coordinates to claim the active pool's Exotic (Royal) Multi-Tool and disable Sentinels.",
                true),
            "AbandonedRobotCamp" => (
                "SENTINEL CABINET",
                "Harmonic Camp (Sentinel Multi-Tool & Ship Locator)",
                "Solve the math glyphs at the terminal to unlock the Sentinel Multi-Tool cabinet and locate Crashed Sentinel Interceptors.",
                true),
            "Outpost" => (
                "TRADING POST",
                "Trading Post (Starship Waves)",
                "All 21 starships native to this system land here continuously. Reload here to cycle waves faster than the Space Station.",
                true),
            "Monolith" => (
                "MONOLITH",
                "Alien Monolith (Portal Locator / Discordant Interface)",
                "Answers lore riddles to locate the planet's Portal; on Dissonant Korvax worlds, its side terminal contains an Atlantid Multi-Tool.",
                false),
            "Portal" => (
                "PORTAL",
                "Planetary Portal",
                "Ancient glyph portal for interstellar travel.",
                false),
            "ScrapYard" => (
                "CRASHED FREIGHTER",
                "Crashed Freighter",
                "Contains 6 buried cargo pods with Salvaged Frigate Modules, Storage Augmentations, and loot.",
                false),
            "Observatory" or "RadioTower" => (
                "SCANNER BUILDING",
                buildingClass == "RadioTower" ? "Transmission Tower (Reveals Crashed Ship)" : "Observatory (Reveals Ruin)",
                buildingClass == "RadioTower"
                    ? "Solve the terminal puzzle inside to mark a Crashed Starship (Distress Signal) or Crashed Freighter on this planet."
                    : "Solve the terminal puzzle inside to mark an Ancient Ruin.",
                buildingClass == "RadioTower"),
            "Factory" or "Harvester" => (
                "FACILITY",
                buildingClass == "Factory" ? "Manufacturing Facility" : "Operations Centre",
                "Secured facility — solve the terminal override to learn crafting recipes or earn Multi-Tool Expansion Slots.",
                false),
            "Plaque" or "Ruin" or "TreasureRuins" => (
                "ANCIENT RUIN",
                buildingClass == "Plaque" ? "Ancient Plaque" : "Ancient Ruin",
                "Historical monument and artifact site.",
                false),
            "Settlement_Hub" => (
                "SETTLEMENT",
                "Planetary Settlement",
                "Planetary settlement hub.",
                false),
            _ => (
                buildingClass.ToUpperInvariant(),
                $"Located {buildingClass} ({eventId})",
                "Active planetary scan marker in your save.",
                false),
        };
    }

    private static string FormatPlanetShipLocation(
        SystemStarshipType type,
        string planetName,
        int wealth,
        bool isPirate,
        bool isUncharted,
        bool isAbandoned,
        bool canSpawnOutpost,
        InventoryGenerationTables? tables)
    {
        if (type == SystemStarshipType.Exotic)
        {
            if (isUncharted || isAbandoned || !canSpawnOutpost)
                return $"Always S-Class (100% S) · Found at Crash Sites (Distress Signals) on {planetName}";
            return $"Always S-Class (100% S) · Lands at Trading Posts & spawns at Crash Sites on {planetName}";
        }

        var (_, _, a, s) = EquipmentGeneration.GetClassProbabilities(wealth, isPirate, tables);
        string classChance = s <= 0f ? $"0% natural S-Class ({a:0}% A-Class)" : $"{s:0}% S-Class ({a:0}% A-Class)";

        if (type == SystemStarshipType.SentinelInterceptor)
            return $"{classChance} · Crashed on {planetName} (use Echo Locator at Harmonic Camp or Carrier AI Fragment)";

        if (isUncharted || isAbandoned || !canSpawnOutpost)
            return $"{classChance} · Found at Crash Sites (Distress Signals) on {planetName}";

        return $"{classChance} · Lands at Trading Posts & spawns at Crash Sites on {planetName}";
    }

    public void SelectShipFilter(string filterKey)
    {
        if (SelectedShipFilter == filterKey) return;
        SelectedShipFilter = filterKey;
        UpdateShipFilterOptions();
        ApplyShipFilter();
    }

    private void UpdateShipFilterOptions()
    {
        var options = new List<StarshipFilterOption>
        {
            new("All", "All", _allShips.Count, SelectedShipFilter == "All")
        };

        void AddOption(string label, string key, SystemStarshipType type)
        {
            int c = _allShips.Count(s => s.Type == type);
            if (c > 0)
                options.Add(new(label, key, c, SelectedShipFilter == key));
        }

        AddOption("Exotic", "Exotic", SystemStarshipType.Exotic);
        AddOption("Sentinel", "SentinelInterceptor", SystemStarshipType.SentinelInterceptor);
        AddOption("Fighters", "Fighter", SystemStarshipType.Fighter);
        AddOption("Haulers", "Hauler", SystemStarshipType.Hauler);
        AddOption("Explorers", "Explorer", SystemStarshipType.Explorer);
        AddOption("Shuttles", "Shuttle", SystemStarshipType.Shuttle);
        AddOption("Solars", "Solar", SystemStarshipType.Solar);

        ShipFilterOptions = options;
    }

    private void ApplyShipFilter()
    {
        if (SelectedShipFilter == "All")
        {
            FilteredShips = _allShips;
        }
        else if (Enum.TryParse<SystemStarshipType>(SelectedShipFilter, out var targetType))
        {
            FilteredShips = _allShips.Where(s => s.Type == targetType).ToList();
        }
        else
        {
            FilteredShips = _allShips;
        }
    }

    private static async void OnToolCopied(SystemMultiToolItemViewModel item)
    {
        item.IsCopied = true;
        await Task.Delay(1500);
        item.IsCopied = false;
    }

    private static async void OnShipCopied(SystemStarshipItemViewModel item)
    {
        item.IsCopied = true;
        await Task.Delay(1500);
        item.IsCopied = false;
    }
}
