using ArtifactX.Core.Collections;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

public sealed record ShopCabinetOffer(
    string ToolName,
    string ClassLetter,
    string DamageText,
    string ScannerText,
    string SlotSummary,
    IReadOnlyList<string> TechnologyIds,
    string AlternateText);

/// <summary>One shop (a trader and a multi-tool cabinet; the game labels it "Minor Settlement", not to be confused
/// with the settlements a player runs), its predicted cabinet multi-tool, and its optional save marker.</summary>
public sealed partial class ShopSiteRowViewModel : ObservableObject
{
    private readonly BuildingSiteLocation _site;

    public ShopSiteRowViewModel(BuildingSiteLocation site, string fromLabel, ShopCabinetOffer? offer)
    {
        _site = site;
        Distance = $"{site.DistanceKm:0.0} km {fromLabel}";
        ApplyOffer(offer);
        Refresh();
    }

    public ulong Key => _site.Key;
    public string Coordinates => _site.Coordinates;
    [ObservableProperty] public partial string Distance { get; private set; }
    public string AddMarkerName => $"Add a marker for the shop at {Coordinates} to my save";
    public string UndoMarkerName => $"Undo the marker for the shop at {Coordinates}";

    [ObservableProperty] public partial bool HasCabinetOffer { get; private set; }
    [ObservableProperty] public partial string ToolName { get; private set; } = "Shop";
    [ObservableProperty] public partial string ClassLetter { get; private set; } = "";
    [ObservableProperty] public partial Brush ClassBrush { get; private set; } = ThemeResources.ClassBrush(null);
    [ObservableProperty] public partial bool HasStats { get; private set; }
    [ObservableProperty] public partial bool HasDamage { get; private set; }
    [ObservableProperty] public partial string DamageText { get; private set; } = "";
    [ObservableProperty] public partial string ScannerText { get; private set; } = "";
    [ObservableProperty] public partial string SlotSummary { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<StationToolTechChip> Technologies { get; private set; } = [];
    [ObservableProperty] public partial bool HasTechnologies { get; private set; }
    [ObservableProperty] public partial bool HasAlternateOffer { get; private set; }
    [ObservableProperty] public partial string AlternateOfferText { get; private set; } = "";

    [ObservableProperty] public partial string Status { get; private set; } = "";
    [ObservableProperty] public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Add marker";
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }

    public void UpdateFrom(ShopSiteRowViewModel next)
    {
        Distance = next.Distance;
        HasCabinetOffer = next.HasCabinetOffer;
        ToolName = next.ToolName;
        ClassLetter = next.ClassLetter;
        ClassBrush = next.ClassBrush;
        HasStats = next.HasStats;
        HasDamage = next.HasDamage;
        DamageText = next.DamageText;
        ScannerText = next.ScannerText;
        SlotSummary = next.SlotSummary;
        Technologies = next.Technologies;
        HasTechnologies = next.HasTechnologies;
        HasAlternateOffer = next.HasAlternateOffer;
        AlternateOfferText = next.AlternateOfferText;
        Refresh();
    }

    private void ApplyOffer(ShopCabinetOffer? offer)
    {
        if (offer is null)
        {
            HasCabinetOffer = false;
            ToolName = "Shop";
            ClassLetter = "";
            ClassBrush = ThemeResources.ClassBrush(null);
            HasStats = false;
            HasDamage = false;
            DamageText = "";
            ScannerText = "";
            SlotSummary = "";
            Technologies = [];
            HasTechnologies = false;
            HasAlternateOffer = false;
            AlternateOfferText = "";
            return;
        }
        HasCabinetOffer = true;
        ToolName = offer.ToolName;
        ClassLetter = offer.ClassLetter;
        ClassBrush = ThemeResources.ClassBrush(offer.ClassLetter);
        DamageText = offer.DamageText;
        HasDamage = offer.DamageText.Length > 0;
        ScannerText = offer.ScannerText;
        HasStats = offer.ScannerText.Length > 0;
        SlotSummary = offer.SlotSummary;
        var chips = offer.TechnologyIds
            .Select(id => new StationToolTechChip(
                CatalogService.TryGet(id)?.DisplayName ?? "Technology",
                CatalogService.TryGet(id)?.Icon,
                CatalogService.ModuleClassLetter(id)))
            .ToArray();
        Technologies = chips;
        HasTechnologies = chips.Length > 0;
        AlternateOfferText = offer.AlternateText;
        HasAlternateOffer = offer.AlternateText.Length > 0;
    }

    public void Refresh()
    {
        bool marked = ScanMarkerService.IsMarked(_site.Key), unsaved = ScanMarkerService.IsUnsaved(_site.Key);
        CanAddMarker = !unsaved && ScanMarkerService.CanAdd;
        CanUndoMarker = unsaved;
        AddMarkerText = marked && !unsaved ? "Mark again" : "Add marker";
        Status = unsaved ? "Marker added - save your changes."
            : marked ? "Marked in your save. Not showing in game? Mark it again."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to add a marker."
            : "";
    }

    /// <summary>The game's own planetary event for shops ("SHOP", scaneventtableplanet.mbin: nearest Shop building),
    /// at the site on the planet radius - confirmed 2026-11-05 on Kaim 30/19, every marker led to its shop.</summary>
    [RelayCommand]
    private void AddMarker()
    {
        ScanMarkerService.Add(_site.PlanetAddress, _site.Key, _site.X, _site.Y, _site.Z, 1, "SHOP", "Shop");
        Refresh();
    }

    [RelayCommand]
    private void UndoMarker()
    {
        ScanMarkerService.Remove(_site.Key);
        Refresh();
    }
}

/// <summary>The shops nearest you (when you're on the planet) or nearest its portal, from the game's building grid
/// (BuildingSiteService), each with a save marker so the player can fly straight there, the multi-tools their cabinets
/// can sell under the current load list (CabinetPoolService), and each shop's own cabinet offer worked out from its
/// procedural building layout and cluster placement (PlanetShopCabinetGenerator; verified 2026-11-06 across 8 shops on
/// Kaim 30/I9).</summary>
public sealed partial class PlanetShopsViewModel : ObservableObject
{
    private const int BuildingClassShop = 5;   // GcBuildingClassification.Shop
    private const int ShownShops = 5;
    private static readonly StationCabinetDefinition ShopCabinetDefinition = new(
        "SHOP", "SHOP", "SHOP", "WeaponTerminal", "Standard", false, [0f, 0f, 0f], [0f, 0f, 1f], []);
    private ulong _planetAddress;
    private string _toolsKey = "";

    public ObservableCollection<ShopSiteRowViewModel> Shops { get; } = [];
    [ObservableProperty] public partial bool HasShops { get; private set; }
    [ObservableProperty] public partial string Heading { get; private set; } = "SHOPS";
    [ObservableProperty] public partial IReadOnlyList<SystemMultiToolItemViewModel> Tools { get; private set; } = [];
    [ObservableProperty] public partial string ToolsLabel { get; private set; } = "";

    public string Hint => "Shops with a trader and a multi-tool cabinet (the game labels them \"Minor Settlement\"), worked out from the game's own building rules, with the coordinates your Analysis Visor shows there. Add a marker and the game leads you there next time you load.";

    /// <param name="savedBody">Where the save loads in this system (0 = space or the station), or null when it was
    /// saved elsewhere - then the station's list, which is what a player sees after warping in.</param>
    public async Task LoadAsync(ulong planetAddress, double latitude, double longitude, bool fromYou, int? savedBody)
    {
        if (_planetAddress != planetAddress)
        {
            Shops.Clear();
            HasShops = false;
        }
        _planetAddress = planetAddress;
        var sites = await BuildingSiteService.NearAsync(planetAddress, BuildingClassShop, latitude, longitude, ShownShops);
        int body = savedBody ?? 0;
        var pool = await CabinetPoolService.ForAsync(planetAddress, body);
        var invTables = PlanetTypesService.InventoryTables ?? await CatalogService.GetInventoryGenerationTablesAsync();
        var techTables = await CatalogService.GetMultiToolTechnologyTablesAsync();
        var modelTables = await CatalogService.GetMultiToolModelTablesAsync();
        var snapshot = await LiveSaveWatcher.GetLatestAsync();
        if (_planetAddress != planetAddress) return;

        var offers = await Task.Run(() =>
        {
            var (code, galaxy) = UniverseInfo.Generator(planetAddress & ~(0xFUL << 52));
            var attributes = SystemGenerator.Attributes(code, galaxy);
            int wealthRow = attributes.Pirate ? 3 : Math.Clamp(attributes.Wealth - 1, 0, 2);
            var runtime = snapshot.EquipmentSaveInputs is null ? null
                : NmsMultiToolGenerationPaths.ReadActiveSavedState(snapshot.EquipmentSaveInputs, wealthRow)?.Runtime;
            if (runtime is not null)
            {
                if (MultiToolPreviewRuntimeService.ReadOverheatDuration() is { } duration)
                    runtime = runtime with { PreviewOverheatDuration = duration };
                runtime = runtime with
                {
                    HasVerifiedStandardIllegalModuleRoll = runtime.HasVerifiedStandardIllegalModuleRoll
                        || MultiToolPreviewRuntimeService.HasStandardIllegalModuleRoll(),
                };
            }
            return sites.Select(s => BuildOffer(s.Cabinet, pool, wealthRow, runtime, invTables, techTables, modelTables)).ToArray();
        });
        var techIds = offers.Where(o => o is not null).SelectMany(o => o!.TechnologyIds).Distinct().ToArray();
        if (techIds.Length > 0) await CatalogService.WarmCacheAsync(techIds);
        if (_planetAddress != planetAddress) return;

        string fromLabel = fromYou ? "away" : "from the portal";
        var rows = sites.Select((s, i) => new ShopSiteRowViewModel(s, fromLabel, offers[i])).ToArray();
        ObservableCollectionSync.Apply(Shops, rows, row => row.Key, (_, _) => true, (row, next) => row.UpdateFrom(next));
        Heading = fromYou ? "SHOPS NEAR YOU" : "SHOPS NEAR THE PORTAL";
        HasShops = rows.Length > 0;

        string where = body == 0 ? "at the space station" : $"on {UniverseInfo.PlanetName(planetAddress & ~(0xFUL << 52), body)}";
        ToolsLabel = (savedBody is null ? "After warping in, each of these shops sells one of these multi-tools."
                : $"Your save loads {where}, so each of these shops sells one of these multi-tools.")
            + (rows.Any(r => r.HasCabinetOffer)
                ? " Each shop below shows the multi-tool and class in its cabinet. Reloading somewhere else in the system changes the list (see Current System)."
                : " Update game data to see which multi-tool and class each shop's cabinet holds. Reloading somewhere else in the system changes the list (see Current System).");
        string toolsKey = string.Join(",", pool.Select(t => t.Seed));
        if (toolsKey != _toolsKey)
        {
            _toolsKey = toolsKey;
            var tools = pool.Select(t => new SystemMultiToolItemViewModel(t)).ToArray();
            Tools = tools;
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            foreach (var tool in tools) _ = tool.ResolveKindAsync(dispatcher);
        }
    }

    private static ShopCabinetOffer? BuildOffer(
        PlanetShopCabinet? cabinet,
        IReadOnlyList<SystemMultiTool> pool,
        int wealthRow,
        MultiToolGenerationRuntime? runtime,
        InventoryGenerationTables? invTables,
        MultiToolTechnologyTables? techTables,
        MultiToolModelTables? modelTables)
    {
        if (cabinet is null || pool.Count == 0 || invTables is null) return null;
        var primary = EvaluateSingle(cabinet.CabinetPoint, cabinet.CabinetSeed, pool, wealthRow, runtime, invTables, techTables, modelTables);
        if (primary is null) return null;

        string altText = "";
        if (cabinet.AlternateNearEdge is { } alt
            && EvaluateSingle(alt.Point, alt.Seed, pool, wealthRow, runtime, invTables, techTables, modelTables) is { } altOffer
            && (altOffer.ToolName != primary.Value.ToolName || altOffer.ClassLetter != primary.Value.ClassLetter
                || altOffer.DamageText != primary.Value.DamageText || altOffer.ScannerText != primary.Value.ScannerText))
        {
            altText = altOffer.ScannerText.Length > 0 && altOffer.DamageText.Length > 0
                ? $"Or: {altOffer.ToolName} ({altOffer.ClassLetter}-Class · {altOffer.DamageText} dmg · {altOffer.ScannerText} scan)"
                : $"Or: {altOffer.ToolName} ({altOffer.ClassLetter}-Class)";
        }

        return new ShopCabinetOffer(
            primary.Value.ToolName,
            primary.Value.ClassLetter,
            primary.Value.DamageText,
            primary.Value.ScannerText,
            primary.Value.SlotSummary,
            primary.Value.TechnologyIds,
            altText);
    }

    private static (string ToolName, string ClassLetter, string DamageText, string ScannerText, string SlotSummary, IReadOnlyList<string> TechnologyIds)? EvaluateSingle(
        (int X, int Y, int Z) pos,
        ulong cabinetSeed,
        IReadOnlyList<SystemMultiTool> pool,
        int wealthRow,
        MultiToolGenerationRuntime? runtime,
        InventoryGenerationTables invTables,
        MultiToolTechnologyTables? techTables,
        MultiToolModelTables? modelTables)
    {
        var tool = SystemMultiToolGenerator.SelectForSite(pool, cabinetSeed, "Standard");
        int? rating = MultiToolSiteGeneration.InventoryClass(cabinetSeed, wealthRow, invTables);
        if (tool is null || rating is not int cls) return null;
        string classLetter = cls switch { 0 => "C", 1 => "B", 2 => "A", 3 => "S", _ => "C" };

        if (runtime is not null && techTables is not null && modelTables is not null
            && MultiToolModelGeneration.Resolve(tool.Seed, modelTables) is { } resolvedModel)
        {
            var loc = new StationCabinetLocation(ShopCabinetDefinition, new Vector3(pos.X, pos.Y, pos.Z), cabinetSeed);
            var contents = new StationMultiToolContents(loc, tool, cls);
            if (LocatedMultiToolGeneration.Generate(contents, resolvedModel, runtime, invTables, techTables) is { } located)
            {
                string damage = located.HasSupportedDamagePotential
                    ? located.DamagePotential.ToString("0.0", CultureInfo.InvariantCulture) : "";
                string scanner = located.ScannerRange.ToString("0.0", CultureInfo.InvariantCulture);
                string slots = $"{located.Inventory.Layout.UnlockedSlots.Length} slots · {located.Inventory.Technologies.Count} tech · {located.Inventory.SuperchargedSlots.Count} supercharged";
                var techIds = located.Inventory.Technologies
                    .OrderBy(t => t.Position.Y)
                    .ThenBy(t => t.Position.X)
                    .Select(t => t.Id)
                    .ToArray();
                return (tool.DefaultName, classLetter, damage, scanner, slots, techIds);
            }
        }

        return (tool.DefaultName, classLetter, "", "", "", []);
    }

    public void RefreshMarkers()
    {
        foreach (var shop in Shops) shop.Refresh();
    }
}
