using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// A starship in the save (vLc.6f=.@Cs[i]): its type and class, how many slots that
/// gives it, and which technology and upgrade modules it takes. Used by Starships.
/// </summary>
public static class StarshipService
{
    /// <summary>Reads a ship's model scene path (to detect its real ship type -
    /// Hauler/Fighter/Explorer/etc.) and its current Class letter, BEFORE any
    /// InventoryGridViewModel exists for it - both are needed up front to size
    /// the grids correctly, since capacity genuinely differs by type+class
    /// (a Hauler has more Cargo than a Fighter - confirmed in StarshipCapacity).</summary>
    public static (StarshipCapacity.ShipType? Type, string? ClassLetter) TypeAndClass(int shipIndex)
    {
        string? scenePath = SaveSessionManager.GetValue(
            "vLc", "6f=", "@Cs", shipIndex.ToString(), "NTx", "93M")?.Value<string>();
        var type = StarshipCapacity.DetectShipType(scenePath);

        return (type, ReadClass(shipIndex));
    }

    /// <summary>A ship stores its Class letter on BOTH inventories (PMT and
    /// ;l5, each B@N.1o6) and the game reads the Cargo one: found 2026-10-10
    /// when an S-class edit written only to Technology still showed C in-game
    /// (save had PMT=S, ;l5=C; every untouched ship has the two matching).
    /// Every class-changing action here writes Technology via its grid view
    /// model, so this copies that letter onto Cargo afterwards. Only called
    /// from those actions, never on load, so it never stages an unasked edit.
    /// ReadClass shows the Cargo copy (Technology only as a fallback) so
    /// the page matches what the game displays.</summary>
    public static string? ReadClass(int shipIndex)
    {
        if (shipIndex < 0) return null;
        return SaveSessionManager.GetValue(NmsInventoryContainer.ShipCargoPath(shipIndex).Append("B@N").Append("1o6").ToArray())?.Value<string>()
            ?? SaveSessionManager.GetValue(NmsInventoryContainer.ShipTechnologyPath(shipIndex).Append("B@N").Append("1o6").ToArray())?.Value<string>();
    }

    public static void SyncCargoClass(int shipIndex)
    {
        if (shipIndex < 0) return;
        string? techClass = SaveSessionManager.GetValue(
            NmsInventoryContainer.ShipTechnologyPath(shipIndex).Append("B@N").Append("1o6").ToArray())?.Value<string>();
        var cargoClassPath = NmsInventoryContainer.ShipCargoPath(shipIndex).Append("B@N").Append("1o6").ToArray();
        if (string.IsNullOrEmpty(techClass) || SaveSessionManager.GetValue(cargoClassPath)?.Value<string>() == techClass) return;
        SaveSessionManager.StageEdit(techClass, cargoClassPath);
    }

    public static int CargoSlots(Dictionary<string, int> capacity, StarshipCapacity.ShipType? type, string? classLetter) =>
        type.HasValue && capacity.TryGetValue(StarshipCapacity.CargoCapacityKey(type.Value, classLetter), out int slots)
            ? slots : StarshipCapacity.FallbackCargoSlots;

    public static int TechSlots(Dictionary<string, int> capacity, StarshipCapacity.ShipType? type, string? classLetter) =>
        type.HasValue && capacity.TryGetValue(StarshipCapacity.TechCapacityKey(type.Value, classLetter), out int slots)
            ? slots : StarshipCapacity.FallbackTechSlots;

    /// <summary>Which technology a ship takes, by its model folder. Worked out
    /// 2026-10-13 from every ship in the real saves: regular ships (and
    /// corvettes) take AllShips + AllShipsExceptAlien + Ship tech, living ships
    /// (S-CLASS/BIOPARTS) AllShips + AlienShip, Sentinel ships AllShips +
    /// AllShipsExceptAlien + RobotShip (so does the Vintage Interceptor). The catalog's "Corvette" category is
    /// only generic upgrade-module templates, so corvettes use the regular set.
    /// The old grid rule ("Ship"/"All") missed AllShips and AllShipsExceptAlien
    /// tech entirely and offered nothing for living or Sentinel ships.</summary>
    public static (string[] UsageCategories, string Header) TechRuleFor(string? scenePath)
    {
        string path = (scenePath ?? "").ToUpperInvariant();
        if (path.Contains("/BIOPARTS/")) return (new[] { "AllShips", "AlienShip" }, "LIVING SHIP TECHNOLOGY");
        // The Vintage Interceptor reward is a Sentinel ship in a fighter's
        // folder: all 8 in the real saves carry only Sentinel tech.
        if (path.Contains("/SENTINELSHIP/") || path.Contains("/VINTAGEINTERCEPTOR.")) return (new[] { "AllShips", "AllShipsExceptAlien", "RobotShip" }, "SENTINEL SHIP TECHNOLOGY");
        string[] regular = { "AllShips", "AllShipsExceptAlien", "Ship" };
        return ShipBaseService.IsCorvetteModel(scenePath ?? "") ? (regular, "CORVETTE TECHNOLOGY") : (regular, "STARSHIP TECHNOLOGY");
    }

    /// <summary>The tech list's groups, in the order they're shown: by the
    /// main stat each tech works on (the game's own BaseStat, via the catalog's
    /// TechStats), so all the drives sit together, each weapon beside its
    /// upgrades, and every upgrade module with the tech it upgrades (user
    /// request 2026-10-13). Figurines and trails also use the scanner stat,
    /// so they're told apart by name.</summary>
    public static readonly string[] TechGroupOrder =
        { "Hyperdrive", "Pulse engine", "Launch thrusters", "Shields", "Weapons", "Utilities", "Figurines", "Starship trails" };

    public static string TechGroup(CatalogPart tech)
    {
        if (tech.Name.EndsWith("FIGURINE", StringComparison.OrdinalIgnoreCase)) return "Figurines";
        if (tech.Name.Contains("STARSHIP TRAIL", StringComparison.OrdinalIgnoreCase)) return "Starship trails";
        string stat = tech.Stat ?? "";
        if (stat.StartsWith("Ship_Hyperdrive")) return "Hyperdrive";
        if (stat.StartsWith("Ship_PulseDrive")) return "Pulse engine";
        if (stat.StartsWith("Ship_Launcher")) return "Launch thrusters";
        if (stat.StartsWith("Ship_Armour")) return "Shields";
        if (stat.StartsWith("Ship_Weapons")) return "Weapons";
        return "Utilities";
    }

    /// <summary>Group order, then each weapon type together (by stat), the
    /// main item before its add-ons, upgrade modules after the tech, A to Z.</summary>
    public static List<CatalogPart> SortTech(IEnumerable<CatalogPart> tech) =>
        ItemListPanel.SortByGroups(tech, TechGroup, TechGroupOrder);

    /// <summary>Upgrade module families each kind of ship takes, from every
    /// ship in the real saves (2026-10-13): regular and Sentinel ships take the
    /// station-bought UP_ modules, living ships their own UA_ ones. Corvettes'
    /// modules (CV_) are all added by their built parts, so none are offered.</summary>
    public static string[] ModuleFamiliesFor(string header) => header switch
    {
        "LIVING SHIP TECHNOLOGY" => new[] { "UA_HYP", "UA_LAUN", "UA_PULSE", "UA_SGUN", "UA_SLASR", "UA_S_SHL" },
        "CORVETTE TECHNOLOGY" => Array.Empty<string>(),
        _ => new[] { "UP_HYP", "UP_LAUN", "UP_PULSE", "UP_SGUN", "UP_SLASR", "UP_SMINI", "UP_SSHOT", "UP_S_SHL", "UP_SBLOB" },
    };
}
