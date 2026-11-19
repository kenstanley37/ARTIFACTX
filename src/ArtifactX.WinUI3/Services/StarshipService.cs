using ArtifactX.Almanac.Starship;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

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

    // ------------------------------------------------------------ the Starships page

    private static string I(int index) => index.ToString(CultureInfo.InvariantCulture);

    /// <summary>A ship's whole save entry: name, seed, class, stats, tech and cargo.</summary>
    public static string[] ShipPath(int index) => NmsInventoryContainer.ShipArrayPath.Append(I(index)).ToArray();

    /// <summary>Its model scene (NTx.93M).</summary>
    public static string? Scene(int index) =>
        index < 0 ? null : SaveSessionManager.GetValue("vLc", "6f=", "@Cs", I(index), "NTx", "93M")?.Value<string>();

    // The game's names for unnamed ships, by (seed, model) - worked out once, off the list build. Emptied when the game
    // data changes: names worked out before an update (none, without the word lists) stayed cached as none afterwards
    // (2026-10-16).
    private static readonly Dictionary<(ulong, string), string?> GameNames = new();
    private static DateTime? _gameNamesStamp;
    private static bool _namingShips;

    private static (ulong Seed, string Scene) NameKey(int index)
    {
        ShipPartsService.TryParseSeed(SaveSessionManager.GetValue(NmsInventoryContainer.ShipModelSeedPath(index))?.Value<string>(), out ulong seed);
        return (seed, Scene(index) ?? "");
    }

    /// <summary>
    /// The owned ships, in save order. A slot is owned when it has a model scene (NTx.93M), NOT a name (NKm): a new
    /// save's starter ship is owned but has no name yet, and filtering on NKm dropped every ship on a fresh save (found
    /// 2026-08-10 on a save that hadn't left the starting planet). The ship being flown is a plain index (aBE,
    /// NmsInventoryContainer.ActiveShipIndexPath), confirmed against 15 real saves across 3 characters. Unnamed ships
    /// carry the game's name once WorkOutGameNamesAsync has it.
    /// </summary>
    public static IReadOnlyList<OwnedShip> ReadShips()
    {
        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath) is not JArray array) return [];
        int active = SaveSessionManager.GetValue(NmsInventoryContainer.ActiveShipIndexPath)?.Value<int>() ?? -1;
        var builds = ShipBaseService.BuildsBySlot();
        if (GameDataService.Info?.BuiltAtUtc != _gameNamesStamp) { GameNames.Clear(); _gameNamesStamp = GameDataService.Info?.BuiltAtUtc; }
        var ships = new List<OwnedShip>();
        for (int i = 0; i < array.Count; i++)
        {
            string scene = array[i]?["NTx"]?["93M"]?.Value<string>() ?? "";
            if (scene.Length == 0) continue;
            string name = array[i]?["NKm"]?.Value<string>() ?? "";
            bool unnamed = name.Length == 0;
            string? gameName = unnamed ? GameNames.GetValueOrDefault(NameKey(i)) : null;
            if (unnamed) name = gameName ?? $"Starship {i + 1}";
            int parts = builds.GetValueOrDefault(i);
            ships.Add(new OwnedShip(i, name, i == active, parts > 0 || ShipBaseService.IsCorvetteModel(scene), parts, unnamed, gameName));
        }
        return ships;
    }

    /// <summary>Works out the game's names for unnamed ships not tried yet (ShipNameAsync). True when one was found,
    /// so the list is worth reading again.</summary>
    public static async Task<bool> WorkOutGameNamesAsync(IReadOnlyList<OwnedShip> ships)
    {
        var missing = ships.Where(s => s.IsUnnamed).Select(s => NameKey(s.Index)).Where(k => !GameNames.ContainsKey(k)).Distinct().ToList();
        if (missing.Count == 0 || _namingShips) return false;
        _namingShips = true;
        try
        {
            foreach (var (seed, scene) in missing)
                GameNames[(seed, scene)] = await ShipNameAsync(seed, scene);
        }
        finally { _namingShips = false; }
        return missing.Any(m => GameNames.GetValueOrDefault(m) is not null);
    }

    /// <summary>Stages a typed name. Nothing when it's empty or what the box already showed (the player's name, or the
    /// game's for an unnamed ship), so the game's name never becomes a saved custom name by accident.</summary>
    public static bool Rename(OwnedShip ship, string? text)
    {
        string name = text?.Trim() ?? "";
        if (name.Length == 0 || name == (ship.IsUnnamed ? ship.GameName : ship.Name)) return false;
        SaveSessionManager.StageEdit(name, "vLc", "6f=", "@Cs", I(ship.Index), "NKm");
        return true;
    }

    /// <summary>Stages the model scene (NTx.93M): the hull flown in game. Added 2026-08-12 - nothing on the page could
    /// write it, so a Full Build captured from a reward ship (Golden Vector) never restored its look.</summary>
    public static void SetType(int index, string scenePath) =>
        SaveSessionManager.StageEdit(scenePath, "vLc", "6f=", "@Cs", I(index), "NTx", "93M");

    public static bool HasEdits(int index) => index >= 0 && SaveSessionManager.HasStagedEditsUnder(ShipPath(index));

    /// <summary>Undoes every staged change to one ship - its inventories and also its name, seed and stats.</summary>
    public static void RevertShip(int index)
    {
        if (index >= 0) SaveSessionManager.RevertEditsUnder(ShipPath(index));
    }

    /// <summary>Label of the stand-in Type choice for a model outside StarshipTypes.All.</summary>
    public const string UnlistedTypeLabel = "(Current - not in known list)";

    /// <summary>The Type choices for a ship (StarshipTypes.All, a small list in code) and the one it has. Real saves
    /// often hold ships outside the known list (see StarshipTypes), so an unlisted model gets a stand-in choice, first,
    /// that keeps it shown and selected without overwriting it. A new list each call, so the ComboBox always sees a
    /// change.</summary>
    public static (IReadOnlyList<StarshipTypeInfo> Options, StarshipTypeInfo? Selected) TypeOptions(int index)
    {
        var options = StarshipTypes.All.ToList();
        string? scene = Scene(index);
        var matched = options.FirstOrDefault(t => string.Equals(t.ScenePath, scene, StringComparison.OrdinalIgnoreCase));
        if (matched is null && !string.IsNullOrEmpty(scene))
        {
            matched = new StarshipTypeInfo(UnlistedTypeLabel, scene);
            options.Insert(0, matched);
        }
        return (options, matched);
    }

    /// <summary>A ship as a Full Build template (Save Current, and "save it first" before deleting).</summary>
    public static NmsLoadoutTemplate CaptureFullBuild(ViewModels.InventoryGridViewModel tech, string templateName, OwnedShip ship)
    {
        var template = tech.ExtractTemplate(templateName, ship.Name, "Ship");
        template.Scope = "FullBuild";
        template.Stats = StatBonusService.ReadAll(NmsInventoryContainer.ShipStatBonusesPath(ship.Index));
        template.Seed = SaveSessionManager.GetValue(NmsInventoryContainer.ShipModelSeedPath(ship.Index))?.Value<string>();
        template.TypeScenePath = Scene(ship.Index);
        NewFromTemplateService.CaptureShip(template, ship.Index);
        return template;
    }

    public static NmsLoadoutTemplate CaptureTechStack(ViewModels.InventoryGridViewModel tech, string templateName, OwnedShip ship) =>
        tech.ExtractTemplate(templateName, ship.Name, "Ship");

    /// <summary>What a delete takes with the ship, for the confirmation.</summary>
    public static string DeleteContents(OwnedShip ship) =>
        "its tech, everything in its cargo, its paint" + (ship.BuildParts > 0 ? $" and its corvette build ({ship.BuildParts} parts)" : "");

    /// <summary>The Details note on a corvette: its build lives apart from the ship (ShipBaseService).</summary>
    public static string CorvetteNote(OwnedShip ship) => ship.BuildParts > 0
        ? $"This is a corvette. What you built onto it - {ship.BuildParts} parts: cockpit, habitat, wings, engines and the rest - is stored apart from the ship. Full Build templates include it."
        : "This is a corvette with no build of its own yet, so the game shows the default one.";

    /// <summary>"N OF M SLOTS USED" over the strip; empty when the slot count is unknown.</summary>
    public static string SlotsUsedText()
    {
        int total = NewFromTemplateService.ShipSlotCount, free = NewFromTemplateService.FreeShipSlots;
        return total > 0 ? $"{total - free} OF {total} SLOTS USED" : "";
    }

    /// <summary>Why a Full Build can't be added as a new ship: no ship yet, or every slot in use.</summary>
    public static string NoRoomText(bool hasShip) => !hasShip
        ? "This save doesn't have a Starship yet. Get one in game first, then add builds here."
        : $"All {NewFromTemplateService.ShipSlotCount} Starship slots are in use. Sell or scrap one in game, then try again.";

    public static (string Title, string Body) AddAsNewText(NmsLoadoutTemplate template, int slot) =>
        ($"Add \"{template.Name}\" as a new Starship?",
            $"It goes in empty slot {slot + 1} of {NewFromTemplateService.ShipSlotCount} with this build's tech, class, stats and look. "
            + "Your other Starships aren't changed. Nothing is written to your save until you Save.");

    /// <summary>A ship's name as the game shows it: the player's name (NKm), else the
    /// game's generated name (ShipNameAsync), else "Starship N".</summary>
    public static async Task<string> DisplayNameAsync(int shipIndex)
    {
        string[] ship = NmsInventoryContainer.ShipArrayPath.Append(shipIndex.ToString()).ToArray();
        string name = SaveSessionManager.GetString(ship.Append("NKm").ToArray()) ?? "";
        if (name.Length > 0) return name;
        string scene = SaveSessionManager.GetValue(ship.Append("NTx").Append("93M").ToArray())?.Value<string>() ?? "";
        return ShipBuilderService.TryParseSeed(shipIndex) is ulong seed && await ShipNameAsync(seed, scene) is { } gameName
            ? gameName : $"Starship {shipIndex + 1}";
    }

    /// <summary>The name the game gives a starship nobody renamed ("Ashes of the
    /// Suitaka") - ShipNames, words from the catalog (format 12). Living Ships ("Yulin KV4")
    /// and Sentinel ships ("μ-Flameborn", catalog format 96) use their own routines. Corvettes use the ordinary
    /// routine: Slot 8's corvette (seed 0x6ADD2A2F) is "Jiyodat RR1" in game, exactly ShipNames.Generate's result
    /// (2026-11-15). Null for reward Living Ships (The Wraith, seed 0) and the Vintage Interceptor.</summary>
    public static async Task<string?> ShipNameAsync(ulong seed, string scenePath)
    {
        string path = (scenePath ?? "").ToUpperInvariant();
        bool living = path.Contains("/BIOPARTS/BIOSHIP_PROC."), sentinel = path.Contains("/SENTINELSHIP/");
        if (seed == 0 || (path.Contains("/BIOPARTS/") && !living) || path.Contains("/VINTAGEINTERCEPTOR."))
            return null;
        var words = await CatalogService.GetProceduralNameWordsAsync();
        if (words.Count == 0) return null;
        IReadOnlyList<string>? List(string list) => words.GetValueOrDefault(list);
        return living ? ShipNames.GenerateLiving(seed, List)
            : sentinel ? ShipNames.GenerateSentinel(seed, List)
            : ShipNames.Generate(seed, List);
    }
}
