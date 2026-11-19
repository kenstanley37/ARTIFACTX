namespace ArtifactX.Core.Universe;

/// <summary>A space station cabinet's tool for one load place: which cabinet (in the station's order), its class
/// (C=0 ... S=3) and the tool with its kind.</summary>
public sealed record StationCabinetTool(int Cabinet, int ClassIndex, SystemMultiTool Tool, string Kind)
{
    public string ClassLetter => ClassIndex switch { 3 => "S", 2 => "A", 1 => "B", _ => "C" };
}

/// <summary>A planet shop's possible tool for one load place: one of the load place's list, picked by where the cabinet
/// stands.</summary>
public sealed record ShopCabinetTool(SystemMultiTool Tool, string Kind);

/// <summary>One place the game can be loaded at (0 = in space or at the station, as on arrival by warp; n = planet or
/// moon n) and what the system's cabinets then sell.</summary>
public sealed record CabinetLoadPlace(int Body, string PlaceName, IReadOnlyList<StationCabinetTool> Station,
    IReadOnlyList<ShopCabinetTool> Shops);

/// <summary>
/// What a star system's standard multi-tool cabinets sell, by the place the game was last loaded at - the rule the
/// owner confirmed in game on 2026-11-05 (Slot 7, Ihweyf, Kaim 30/19: one shop cabinet sold the planet list's tool
/// when loaded on the planet and the station list's tool after a save and reload at the station; three cabinets x
/// three load places, docs/research/MULTITOOL_GENERATION.md). Every cabinet - the station's and every planet shop's -
/// sells from the load place's list; where the cabinet stands picks its tool from that list and fixes its class.
/// Station cabinets are exact (<see cref="CabinetMultiToolSearch"/>); a planet shop cabinet's exact spot isn't
/// worked out yet (Core's shop sites are ~20 m off), so for shops only the list is given. Shared by the desktop and
/// the phone, with the steps to roll a cabinet in plain words.
/// </summary>
public static class SystemCabinets
{
    /// <summary>How rolling works, as numbered steps for the player.</summary>
    public static readonly IReadOnlyList<string> RollSteps =
    [
        "Every multi-tool cabinet in this system - the space station's and every planet shop's - sells from one list. "
            + "Which list depends on where you last loaded your game.",
        "Just warped in? Cabinets sell the space station's list.",
        "To switch to a planet's list: land on that planet, save, quit to the main menu and load that save. "
            + "Then go to any cabinet in the system.",
        "To go back to the station's list, save at the station (or in space), quit and load again.",
        "A cabinet's class (S, A, B, C) comes from where it stands, so it stays the same whichever list it sells from. "
            + "Look for an S-class cabinet, then load at the place whose list has the tool you want.",
    ];

    /// <summary>Every load place of the system with what its cabinets sell; empty for uncharted and abandoned systems
    /// (no working cabinets) or when the tables are missing. <paramref name="placeNames"/> names the bodies (index 1 =
    /// planet 1); <paramref name="useLegacyStation"/> is false for an ordinary save (the phone has no save: expedition
    /// "walk back in time" stations are older layouts and aren't supported).</summary>
    public static IReadOnlyList<CabinetLoadPlace> ForSystem(ulong systemAddress, IReadOnlyList<string> placeNames,
        StationEquipmentTables stations, InventoryGenerationTables inventory, MultiToolKindClassifier kinds,
        Func<string, IReadOnlyList<string>?>? words, bool? useLegacyStation = false)
    {
        ulong system = systemAddress & ~(0xFUL << 52);
        var (portal, galaxy) = SystemProfiles.PortalOf(system);
        var attributes = SystemGenerator.Attributes(portal, galaxy);
        if (attributes.Uncharted || attributes.Abandoned || inventory.MultiToolPools is not { Length: > 0 } pools) return [];
        var station = CabinetMultiToolSearch.InSystem(system, useLegacyStation, stations, inventory, kinds, words)
            .GroupBy(f => f.ReloadBody).ToDictionary(g => g.Key, g => g.OrderBy(f => f.Cabinet).ToArray());
        int bodies = attributes.PlanetCount + attributes.PrimePlanetCount;
        var places = new List<CabinetLoadPlace>();
        for (int body = 0; body <= bodies; body++)
        {
            string name = body == 0 ? "Space Station" : body < placeNames.Count + 1 && placeNames[body - 1].Length > 0 ? placeNames[body - 1] : $"Planet {body}";
            var shop = SystemMultiToolGenerator.GeneratePool(system | ((ulong)body << 52),
                    body == 0 ? MultiToolPoolSource.SpaceStation : MultiToolPoolSource.Planet, name, words, pools)
                .Where(t => t.PoolType == "Standard").Select(t => new ShopCabinetTool(t, kinds.KindOf(t))).ToArray();
            var cabinets = station.GetValueOrDefault(body, [])
                .Select(f => new StationCabinetTool(f.Cabinet, f.ClassIndex, f.Tool, f.Kind)).ToArray();
            if (shop.Length > 0 || cabinets.Length > 0) places.Add(new CabinetLoadPlace(body, name, cabinets, shop));
        }
        return places;
    }
}
