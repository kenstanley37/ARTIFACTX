using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// "Add as new": builds a Full Build template into an EMPTY Multi-Tool or
/// Starship slot instead of over an existing one, and captures the extra
/// fields that needs when a Full Build template is saved.
///
/// Both lists are fixed-size arrays (checked 2026-10-12 on every slot: always
/// 6 Multi-Tools (SuJ) and 12 Starships (@Cs)); an unused slot is a
/// placeholder entry with an empty model path (NTx.93M) and its seed flag
/// false. Field names are libMBIN's: GcMultitoolData (CA4 Layout, OsQ Store,
/// 1Lx ScreenData, @EL Seed, 7DH CustomisationData, NKm Name, OGV IsLarge,
/// qVG PrimaryMode, jl; SecondaryMode, J&lt;o UseLegacyColours, NTx Resource)
/// and GcPlayerOwnershipData (NKm Name, NTx Resource, ;l5 Inventory (cargo),
/// gan Inventory_Cargo (unused), PMT Inventory_TechOnly, pMa InventoryLayout,
/// YTa Location, wMC/l?l Position/Direction, FA8 VehicleCargo).
///
/// The new entry starts as a copy of an owned one (a known-good skeleton: its
/// inventory sizes, layout and screen data are what the game wrote), then
/// everything a template decides is replaced, and anything the player owns
/// in it (cargo items, name) is cleared. A Starship's paint lives outside the
/// entry, in CharacterCustomisationData (l:j) at the Ship_01..12 slot; empty
/// ship slots hold the default there, which is what a template without
/// captured paint gets.
/// </summary>
public static class NewFromTemplateService
{
    private static readonly string[] CustomisationPath = { "vLc", "6f=", "l:j" };

    /// <summary>The first unused slot, or null when every slot is taken.</summary>
    public static int? FirstEmptyMultiToolSlot() => FirstEmpty(NmsInventoryContainer.MultiToolArrayPath);

    public static int? FirstEmptyShipSlot() => FirstEmpty(NmsInventoryContainer.ShipArrayPath);

    public static int MultiToolSlotCount => (SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolArrayPath) as JArray)?.Count ?? 0;

    public static int ShipSlotCount => (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath) as JArray)?.Count ?? 0;

    private static int? FirstEmpty(string[] arrayPath)
    {
        if (SaveSessionManager.GetValue(arrayPath) is not JArray array) return null;
        for (int i = 0; i < array.Count; i++)
            if (string.IsNullOrEmpty(array[i]?["NTx"]?["93M"]?.Value<string>())) return i;
        return null;
    }

    // ---------------------------------------------------------------- capture

    /// <summary>Fills a Multi-Tool Full Build template's extra fields from the tool.</summary>
    public static void CaptureMultiTool(NmsLoadoutTemplate template, int toolIndex)
    {
        if (SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolArrayPath.Append(toolIndex.ToString()).ToArray()) is not JObject tool) return;
        template.Seed = tool["@EL"]?[1]?.Value<string>();
        template.IsLarge = tool["OGV"]?.Value<bool>();
        template.PrimaryMode = tool["qVG"]?.Value<int>();
        template.SecondaryMode = tool["jl;"]?.Value<int>();
        template.UseLegacyColours = tool["J<o"]?.Value<bool>();
        template.Customisation = tool["7DH"]?.ToString(Newtonsoft.Json.Formatting.None);
        template.SuperchargedPositions = Positions(tool["OsQ"]?["MMm"]);
        // Every unlocked slot, not just the ones holding tech (what a Tech
        // Stack template keeps, so it never unlocks extra slots on a target).
        if (Positions(tool["OsQ"]?["hl?"]) is { Count: > 0 } unlocked) template.UnlockedPositions = unlocked;
    }

    /// <summary>Fills a Starship Full Build template's extra fields from the ship.</summary>
    public static void CaptureShip(NmsLoadoutTemplate template, int shipIndex)
    {
        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath.Append(shipIndex.ToString()).ToArray()) is not JObject ship) return;
        template.SuperchargedPositions = Positions(ship["PMT"]?["MMm"]);
        if (Positions(ship["PMT"]?["hl?"]) is { Count: > 0 } unlocked) template.UnlockedPositions = unlocked;
        template.CargoUnlockedPositions = Positions(ship[";l5"]?["hl?"]);
        template.Customisation = SaveSessionManager.GetValue(ShipCustomisationPath(shipIndex))?.ToString(Newtonsoft.Json.Formatting.None);
    }

    // ---------------------------------------------------------------- add

    /// <summary>Stages a new Multi-Tool built from the template in the first
    /// empty slot, using an owned tool as the skeleton. Returns the new slot,
    /// or null when there's no room.</summary>
    public static int? AddMultiTool(NmsLoadoutTemplate t, int skeletonIndex)
    {
        if (FirstEmptyMultiToolSlot() is not int slot) return null;
        if (SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolArrayPath.Append(skeletonIndex.ToString()).ToArray()) is not JObject skeleton) return null;

        var tool = (JObject)skeleton.DeepClone();
        tool["NKm"] = t.Name;
        if (t.TypeScenePath is { Length: > 0 } scene) tool["NTx"]!["93M"] = scene;
        tool["@EL"] = new JArray(true, t.Seed is { Length: > 0 } seed ? seed : tool["@EL"]?[1]?.Value<string>() ?? "0x0");
        if (t.IsLarge is bool large) tool["OGV"] = large;
        if (t.PrimaryMode is int primary) tool["qVG"] = primary;
        if (t.SecondaryMode is int secondary) tool["jl;"] = secondary;
        if (t.UseLegacyColours is bool legacy) tool["J<o"] = legacy;
        tool["7DH"] = ParseObject(t.Customisation) ?? DefaultToolCustomisation(slot) ?? tool["7DH"];

        FillTechInventory((JObject)tool["OsQ"]!, t);

        SaveSessionManager.StageEdit(tool, NmsInventoryContainer.MultiToolArrayPath.Append(slot.ToString()).ToArray());
        return slot;
    }

    /// <summary>Stages a new Starship built from the template in the first
    /// empty slot, using an owned ship as the skeleton. Its cargo starts empty.
    /// Returns the new slot, or null when there's no room.</summary>
    public static int? AddShip(NmsLoadoutTemplate t, int skeletonIndex)
    {
        if (FirstEmptyShipSlot() is not int slot) return null;
        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath.Append(skeletonIndex.ToString()).ToArray()) is not JObject skeleton) return null;

        var ship = (JObject)skeleton.DeepClone();
        ship["NKm"] = t.Name;
        if (t.TypeScenePath is { Length: > 0 } scene) ship["NTx"]!["93M"] = scene;
        ship["NTx"]!["@EL"] = new JArray(true, t.Seed is { Length: > 0 } seed ? seed : ship["NTx"]?["@EL"]?[1]?.Value<string>() ?? "0x0");

        FillTechInventory((JObject)ship["PMT"]!, t);

        // Cargo: same class and stats as the tech (the game reads class from
        // here - see ShipsPage.ReadShipClass), the template's unlocked slots,
        // and nothing in it.
        var cargo = (JObject)ship[";l5"]!;
        cargo[":No"] = new JArray();
        if (t.CargoUnlockedPositions is { } cargoSlots) cargo["hl?"] = PositionsArray(cargoSlots);
        FitGrid(cargo);
        if (t.SourceClass is { Length: > 0 } cls) cargo["B@N"] = new JObject { ["1o6"] = cls };
        if (t.Stats.Count > 0) cargo["@bB"] = StatsArray(t.Stats);
        if (ship["gan"] is JObject legacyCargo) legacyCargo[":No"] = new JArray();
        if (ship["FA8"] is JArray) ship["FA8"] = new JArray();

        SaveSessionManager.StageEdit(ship, NmsInventoryContainer.ShipArrayPath.Append(slot.ToString()).ToArray());

        // Paint and parts: the template's, else the default an empty slot has.
        var paintPath = ShipCustomisationPath(slot);
        if (ParseObject(t.Customisation) is JObject paint) SaveSessionManager.StageEdit(paint, paintPath);
        else SaveSessionManager.StageEdit(DefaultCustomisationEntry(), paintPath);
        return slot;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Tech, unlocked and supercharged slots, class and stats - the
    /// whole Technology inventory as the template describes it.</summary>
    private static void FillTechInventory(JObject store, NmsLoadoutTemplate t)
    {
        store[":No"] = ItemsArray(t.TechItems);
        store["hl?"] = PositionsArray(t.UnlockedPositions);
        if (t.SuperchargedPositions is { } supercharged) store["MMm"] = SuperchargedArray(supercharged);
        if (t.SourceClass is { Length: > 0 } cls) store["B@N"] = new JObject { ["1o6"] = cls };
        if (t.Stats.Count > 0) store["@bB"] = StatsArray(t.Stats);
        FitGrid(store);
    }

    public static JArray SuperchargedArray(IEnumerable<NmsLoadoutPosition> positions) =>
        new(positions.Select(p => new JObject
        {
            ["Vn8"] = new JObject { ["QA1"] = "TechBonus" },
            ["3ZH"] = new JObject { [">Qh"] = p.X, ["XJ>"] = p.Y },
        }));

    /// <summary>Widens an inventory's grid (=Tb width, N9> height) to fit
    /// every unlocked slot and item - an empty freighter's inventories start
    /// as small as 7x3, smaller than a template's slots may need.</summary>
    public static void FitGrid(JObject store)
    {
        var cells = (store["hl?"] as JArray ?? new JArray()).Concat((store[":No"] as JArray ?? new JArray()).Select(i => i["3ZH"]))
            .Where(p => p?[">Qh"] is not null && p["XJ>"] is not null).ToList();
        if (cells.Count == 0) return;
        int width = cells.Max(p => p![">Qh"]!.Value<int>()) + 1, height = cells.Max(p => p!["XJ>"]!.Value<int>()) + 1;
        if ((store["=Tb"]?.Value<int>() ?? 0) < width) store["=Tb"] = width;
        if ((store["N9>"]?.Value<int>() ?? 0) < height) store["N9>"] = height;
    }

    /// <summary>Same item shape InventoryGridViewModel writes.</summary>
    public static JArray ItemsArray(IEnumerable<NmsLoadoutTechItem> items) =>
        new(items.Select(e => new JObject
        {
            ["Vn8"] = new JObject { ["elv"] = e.CategoryLabel },
            ["b2n"] = e.ItemId,
            ["1o9"] = e.Amount,
            ["F9q"] = e.MaxAmount,
            ["eVk"] = e.MalfunctionSeverity,
            ["b76"] = e.IsFunctional,
            ["5tH"] = false,
            ["3ZH"] = new JObject { [">Qh"] = e.X, ["XJ>"] = e.Y },
        }));

    public static JArray StatsArray(IEnumerable<NmsLoadoutStat> stats) =>
        new(stats.Select(s => new JObject { ["QL1"] = s.Key, [">MX"] = s.Value }));

    public static JArray PositionsArray(IEnumerable<NmsLoadoutPosition> positions) =>
        new(positions.Select(p => new JObject { [">Qh"] = p.X, ["XJ>"] = p.Y }));

    private static List<NmsLoadoutPosition>? Positions(JToken? array) =>
        array is JArray a
            ? a.Select(e => e["3ZH"] ?? e)
                .Where(p => p[">Qh"] is not null && p["XJ>"] is not null)
                .Select(p => new NmsLoadoutPosition { X = p[">Qh"]!.Value<int>(), Y = p["XJ>"]!.Value<int>() })
                .ToList()
            : null;

    private static JObject? ParseObject(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JObject.Parse(json); } catch { return null; }
    }

    /// <summary>An empty slot's own customisation is the game's default.</summary>
    private static JToken? DefaultToolCustomisation(int emptySlot) =>
        SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolArrayPath.Append(emptySlot.ToString()).Append("7DH").ToArray())?.DeepClone();

    /// <summary>The CharacterCustomisationData entry every unused ship slot
    /// holds (checked 2026-10-12 on the main save's two empty slots).</summary>
    private static JObject DefaultCustomisationEntry() => new()
    {
        ["VFd"] = "^",
        ["wnR"] = new JObject
        {
            ["SMP"] = new JArray(), ["FCx"] = "^", ["Aak"] = new JArray(),
            ["T>1"] = new JArray(), ["gsg"] = new JArray(), ["unY"] = 1.0,
        },
    };

    /// <summary>CharacterCustomisationData index for a ship slot: Ship_01..06
    /// are enum values 3-8, Ship_07..12 are 17-22 (GcCustomisationComponentData).</summary>
    public static int ShipCustomisationIndex(int shipIndex) => shipIndex < 6 ? 3 + shipIndex : 17 + (shipIndex - 6);

    private static string[] ShipCustomisationPath(int shipIndex) =>
        CustomisationPath.Append(ShipCustomisationIndex(shipIndex).ToString()).ToArray();
}
