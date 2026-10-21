using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>Something a settlement produces: the item and the building making it.</summary>
public sealed record SettlementProduct(ImageSource? Icon, string Name, string Tip);

/// <summary>
/// A settlement in the picker, like the game's Settlements Register (user request
/// 2026-10-14): name, where it is (planet, system, galaxy), the system's race,
/// population, what it produces and the building queued for an upgrade. All read from
/// the save or generated from its address.
/// </summary>
public sealed class SettlementCardViewModel
{
    public SettlementCardViewModel(SettlementEntry settlement, bool isSelected)
    {
        Index = settlement.Index;
        Name = settlement.Name;
        IsSelected = isSelected;

        if (SettlementService.Address(Index) is ulong a)
        {
            int planet = (int)((a >> 52) & 0xF);
            int galaxy = S.GalaxyNumber(a);
            Where = planet > 0 ? $"{UniverseInfo.PlanetName(a, planet)} · {UniverseInfo.SystemName(a)} system" : $"{UniverseInfo.SystemName(a)} system";
            string galaxyName = NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
            GalaxyAndRace = $"{galaxyName} · {UniverseInfo.RaceLabel(a)}";
        }
        int population = SaveSessionManager.GetValue(NmsSettlementPaths.PopulationPath(Index))?.Value<int>() ?? 0;
        Population = $"Population {population.ToString("N0", CultureInfo.CurrentCulture)}";

        // What it produces: ProductionState (abj), two slots of item (CZw) and the building
        // making it (N6t.iqv). Slots with no building ("None") aren't shown in game either
        // (Inuyam's Cove lists Nitrogen/Sulphurine but the register shows nothing) - checked
        // against the Settlements Register 2026-10-14.
        if (SaveSessionManager.GetValue(NmsSettlementPaths.SettlementPath(Index).Append("abj").ToArray()) is JArray production)
        {
            foreach (var slot in production.OfType<JObject>())
            {
                string item = slot["CZw"]?.Value<string>() ?? "";
                string maker = slot["N6t"]?["iqv"]?.Value<string>() ?? "None";
                if (item.Trim('^').Length == 0 || maker == "None") continue;
                var entry = CatalogService.TryGet(item);
                Products.Add(new SettlementProduct(entry?.Icon, entry?.DisplayName ?? CatalogService.NormalizeId(item),
                    $"{entry?.DisplayName ?? item} - made by the {SettlementService.BuildingLabel(maker)}"));
            }
        }

        string next = SettlementService.NextBuilding(Index);
        Building = next.Length > 0 ? $"Building: {SettlementService.BuildingLabel(next)}" : "";
    }

    /// <summary>Production item ids of every settlement, to warm the catalog's icons first.</summary>
    public static IEnumerable<string?> ProductIds(IEnumerable<SettlementEntry> settlements) =>
        SaveSessionManager.GetValue(NmsSettlementPaths.SettlementArrayPath) is JArray all
            ? settlements.SelectMany(s => (all[s.Index]?["abj"] as JArray)?.Select(x => x["CZw"]?.Value<string>()) ?? Enumerable.Empty<string?>())
            : Enumerable.Empty<string?>();

    public int Index { get; }
    public string Name { get; }
    public bool IsSelected { get; }
    public string Where { get; } = "";
    public string GalaxyAndRace { get; } = "";
    public string Population { get; }
    public List<SettlementProduct> Products { get; } = new();
    public bool HasProducts => Products.Count > 0;
    public string Building { get; }
    public bool HasBuilding => Building.Length > 0;
    public bool HasWhere => Where.Length > 0;

    public Windows.UI.Text.FontWeight NameWeight => IsSelected ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.SemiBold;
    public Brush Border => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Thickness BorderThickness => new(IsSelected ? 2 : 1);
    public Brush Background => ThemeResources.Brush(IsSelected ? "ThemeSelectedFillBrush" : "ThemePanelAltBrush");
}
