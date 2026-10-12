using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// A freighter's interior (rooms and furnishings) for Full Build templates, and
/// "Add freighter" for a save that has none.
///
/// Worked out from a controlled in-game test on 2026-10-12 (Slot 4 saved just
/// before and just after getting a freighter). Getting one writes:
///   bIR  hull model + seed (CurrentFreighter)
///   Sjw  captain NPC model + seed (CurrentFreighterNPC)
///   kYq  home seed [true, packed address of the system it was got in]
///   RB7  the system it's parked in (a copy of the player's yhJ)
///   9wg / fUl / lpm  its facing (at / up) and position in that system
///   0wS / 8ZP  Technology and Cargo (pre-allocated before, filled in)
///   4kj += ^F_HYPERDRIVE (known tech), eZ&lt; += ^BASECAPSULE
///   F?0 += one FreighterBase entry: the interior, 24 default parts.
/// There's no separate "owns a freighter" flag; the hull model is the signal
/// (FreighterPage.HasFreighter). The rest of that save's changes were the
/// freighter tutorial missions and ordinary play.
///
/// Interior parts sit in the base's own local space (about +/-60 units on
/// every freighter checked), so they move between freighters unchanged. The
/// base's anchor (wMC) and forward (oHw, always the negative of 9wg on every
/// save checked) differ per freighter, so they travel with the hull model and
/// seed they were captured with.
/// </summary>
public static class FreighterBuildService
{
    private static readonly string[] HullPath = { "vLc", "6f=", "bIR" };
    private static readonly string[] CaptainPath = { "vLc", "6f=", "Sjw" };
    private static readonly string[] HomeSeedPath = { "vLc", "6f=", "kYq" };
    private static readonly string[] ParkedAddressPath = { "vLc", "6f=", "RB7" };
    private static readonly string[] AtPath = { "vLc", "6f=", "9wg" };
    private static readonly string[] UpPath = { "vLc", "6f=", "fUl" };
    private static readonly string[] PositionPath = { "vLc", "6f=", "lpm" };
    /// <summary>The current system's planet positions (16 slots, unused ones zero).</summary>
    private static readonly string[] PlanetPositionsPath = { "vLc", "6f=", "aHM" };

    private const string FreighterBaseType = "FreighterBase";
    private const string DefaultHull = "MODELS/COMMON/SPACECRAFT/INDUSTRIAL/FREIGHTER_PROC.SCENE.MBIN";
    private const string DefaultCaptain = "MODELS/COMMON/PLAYER/PLAYERCHARACTER/NPCGEK.SCENE.MBIN";

    // The freighter the game gave in the 2026-10-12 test: its facing and
    // interior anchor, used with the default interior below.
    private static readonly double[] DefaultAt = { -0.49615478515625, -0.7123500108718872, 0.4963747262954712 };
    private static readonly double[] DefaultUp = { -0.5035958290100098, 0.7018243074417114, 0.5038191080093384 };
    private static readonly double[] DefaultInteriorPosition = { -169.87718200683594, 175.19149780273438, 25.698394775390625 };

    /// <summary>The 24-part interior every new freighter starts with (same test).</summary>
    private static readonly (string Id, long UserData, double[] Position, double[] Up, double[] At)[] DefaultInterior =
    {
        new("^FRE_CORR_A", 62, new[] { 3.099999958067201e-05, 4.0, 19.500080108642578 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }),
        new("^FRE_CORR_A", 4278190142, new[] { 3.099999958067201e-05, 4.0, 27.500080108642578 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }),
        new("^FRE_CORR_A", 62, new[] { 3.099999958067201e-05, 4.0, 35.50008010864258 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }),
        new("^AIRLCKCONNECTOR", 4294967296000, new[] { 3.099999958067201e-05, 4.0, 15.500080108642578 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, -1.0 }),
        new("^BRIDGECONNECTOR", 0, new[] { 3.099999958067201e-05, 4.0, 39.50008010864258 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }),
        new("^FRE_FACE_DOOR_A", 62, new[] { 4.000033855438232, 3.9999849796295166, 35.50006103515625 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { -1.0, -0.0, -0.0 }),
        new("^FRE_FACE_DOOR_A", 62, new[] { -3.9999709129333496, 3.9999849796295166, 35.50006103515625 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { 1.0, 0.0, -0.0 }),
        new("^FRE_EXT_WALKWAY", 62, new[] { -7.999961853027344, 4.0, 35.50006103515625 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { 1.0, 0.0, -0.0 }),
        new("^FRE_EXT_WALKWAY", 62, new[] { -15.999961853027344, 4.0, 35.50006103515625 }, new[] { -0.0, 1.0000009536743164, -0.0 }, new[] { 1.0, 0.0, -0.0 }),
        new("^FRE_EXT_WALKWAY", 62, new[] { 8.000036239624023, 3.999969005584717, 35.50006103515625 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { -1.0, -0.0, -0.0 }),
        new("^FRE_EXT_WALKWAY", 62, new[] { 16.00004005432129, 3.999969005584717, 35.50006103515625 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { -1.0, -0.0, -0.0 }),
        new("^FRE_EXT_PLATFOR", 62, new[] { 16.00005531311035, 3.99993896484375, 27.5 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { -1.0, -0.0, -0.0 }),
        new("^FRE_EXT_PLATFOR", 62, new[] { -15.999956130981445, 3.999969005584717, 27.500030517578125 }, new[] { 0.0, 1.0000009536743164, -0.0 }, new[] { 1.0, -0.0, 0.0 }),
        new("^FRE_ROOM_STORE0", 62, new[] { -23.999958038330078, 3.999969005584717, 27.50006103515625 }, new[] { -0.0, 1.0000009536743164, -0.0 }, new[] { 1.0, 0.0, 0.0 }),
        new("^FRE_FACE_DOOR_A", 62, new[] { -19.99995231628418, 3.9999539852142334, 27.50006103515625 }, new[] { -0.0, 1.0000009536743164, -0.0 }, new[] { -1.0, -0.0, -0.0 }),
        new("^FRE_ROOM_EXTR", 1546188226560062, new[] { -31.99995994567871, 3.999969005584717, 27.50006103515625 }, new[] { -0.0, 1.0000009536743164, -0.0 }, new[] { 1.0, 0.0, 0.0 }),
        new("^FRE_FACE_WINDOW", 51, new[] { -35.99996566772461, 3.9999849796295166, 27.50006103515625 }, new[] { -0.0, 1.0000009536743164, -0.0 }, new[] { 1.0, 0.0, -0.0 }),
        new("^FRE_ROOM_VEHICL", 62, new[] { 24.00006866455078, 3.999907970428467, 27.5 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { -0.0, -0.0, 1.0 }),
        new("^FRE_ROOM_EXTR", 1546188226560062, new[] { 32.00007629394531, 3.9998929500579834, 27.5 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { -1.0, -0.0, -0.0 }),
        new("^FRE_FACE_DOOR_A", 62, new[] { 20.00006675720215, 3.999907970428467, 27.5 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { 1.0, 0.0, 0.0 }),
        new("^FRE_FACE_WINDOW", 51, new[] { 24.000072479248047, 3.9999239444732666, 31.5 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { 0.0, 0.0, -1.0 }),
        new("^FRE_FACE_WINDOW", 51, new[] { 36.00008773803711, 3.9998779296875, 27.5 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { -1.0, -0.0, -0.0 }),
        new("^FRE_FACE_WINDOW", 51, new[] { 24.00006866455078, 3.9998929500579834, 23.5 }, new[] { -0.0, 1.0000009536743164, 0.0 }, new[] { -0.0, -0.0, 1.0 }),
        new("^U_PARAGON", 4294967296000000, new[] { 0.0, 0.0, 0.0 }, new[] { -2.6469779601696886e-23, 1.0, 8.881784197001252e-16 }, new[] { -2.9802322387695312e-08, -8.881784197001252e-16, 1.0 }),
    };

    public static bool HasInterior(NmsLoadoutTemplate t) => t.InteriorObjects is { Count: > 0 };

    /// <summary>Index of the freighter's base in F?0, or null.</summary>
    private static int? FreighterBaseIndex(JArray bases)
    {
        for (int i = 0; i < bases.Count; i++)
            if (string.Equals(bases[i]?["peI"]?["DPp"]?.Value<string>(), FreighterBaseType, StringComparison.Ordinal)) return i;
        return null;
    }

    // ---------------------------------------------------------------- capture

    /// <summary>Fills a Freighter Full Build template with the interior,
    /// facing, cargo slots and supercharged slots.</summary>
    public static void Capture(NmsLoadoutTemplate t)
    {
        t.CargoUnlockedPositions = Positions(SaveSessionManager.GetValue(NmsInventoryContainer.FreighterCargoPath.Append("hl?").ToArray()));
        t.SuperchargedPositions = Positions(SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTechnologyPath.Append("MMm").ToArray()));
        t.FreighterAt = Vector(SaveSessionManager.GetValue(AtPath));
        t.FreighterUp = Vector(SaveSessionManager.GetValue(UpPath));

        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases || FreighterBaseIndex(bases) is not int index) return;
        var freighterBase = bases[index];
        t.InteriorPosition = Vector(freighterBase["wMC"]);
        t.InteriorObjects = ReadParts(freighterBase["@ZJ"]);
    }

    // ---------------------------------------------------------------- apply

    /// <summary>Replaces the freighter's interior with the template's, along
    /// with the anchor and facing it was built against (the hull model and
    /// seed are applied by the page). False when the save has no freighter base.</summary>
    public static bool ApplyInterior(NmsLoadoutTemplate t)
    {
        if (!HasInterior(t)) return false;
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases || FreighterBaseIndex(bases) is not int index) return false;

        var basePath = S.BasesPath.Append(index.ToString(CultureInfo.InvariantCulture)).ToArray();
        SaveSessionManager.StageEdit(InteriorArray(t), basePath.Append("@ZJ").ToArray());
        if (t.InteriorPosition is { } anchor && t.FreighterAt is { } at && t.FreighterUp is { } up)
        {
            SaveSessionManager.StageEdit(new JArray(anchor), basePath.Append("wMC").ToArray());
            SaveSessionManager.StageEdit(new JArray(at.Select(v => -v)), basePath.Append("oHw").ToArray());
            SaveSessionManager.StageEdit(new JArray(at), AtPath);
            SaveSessionManager.StageEdit(new JArray(up), UpPath);
        }
        SaveSessionManager.StageEdit(Now(), basePath.Append("wx7").ToArray());
        return true;
    }

    // ---------------------------------------------------------------- add

    /// <summary>Stages a whole freighter built from the template, parked in
    /// the system the player is in. Returns why it couldn't, or null.</summary>
    public static string? AddFreighter(NmsLoadoutTemplate t)
    {
        if (SaveSessionManager.GetValue(S.CurrentUniverseAddressPath) is not JObject here
            || S.ReadSystemAddress(here) is not ulong system)
            return "this save doesn't say which system you're in.";
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases) return "this save has no base list.";
        if (FreighterBaseIndex(bases) is not null) return "this save already has a freighter base.";
        if (SaveSessionManager.GetValue(HullPath) is not JObject hull
            || SaveSessionManager.GetValue(CaptainPath) is not JObject captain
            || SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTechnologyPath) is not JObject tech
            || SaveSessionManager.GetValue(NmsInventoryContainer.FreighterCargoPath) is not JObject cargo)
            return "this save's freighter data isn't where ArtifactX expects it.";

        bool ownInterior = HasInterior(t) && t.InteriorPosition is not null && t.FreighterAt is not null && t.FreighterUp is not null;
        var at = ownInterior ? t.FreighterAt! : DefaultAt;
        var up = ownInterior ? t.FreighterUp! : DefaultUp;
        var anchor = ownInterior ? t.InteriorPosition! : DefaultInteriorPosition;
        string systemHex = "0x" + system.ToString("X", CultureInfo.InvariantCulture);

        // Hull, captain, home and where it's parked.
        var newHull = (JObject)hull.DeepClone();
        newHull["93M"] = t.TypeScenePath is { Length: > 0 } scene ? scene : DefaultHull;
        newHull["@EL"] = new JArray(true, t.Seed is { Length: > 0 } seed ? seed : NmsSeedGenerator.GenerateRandom());
        SaveSessionManager.StageEdit(newHull, HullPath);

        var newCaptain = (JObject)captain.DeepClone();
        newCaptain["93M"] = DefaultCaptain;
        newCaptain["@EL"] = new JArray(true, NmsSeedGenerator.GenerateRandom());
        SaveSessionManager.StageEdit(newCaptain, CaptainPath);

        SaveSessionManager.StageEdit(new JArray(true, systemHex), HomeSeedPath);
        SaveSessionManager.StageEdit(here.DeepClone(), ParkedAddressPath);
        SaveSessionManager.StageEdit(new JArray(at), AtPath);
        SaveSessionManager.StageEdit(new JArray(up), UpPath);
        SaveSessionManager.StageEdit(new JArray(ParkingSpot()), PositionPath);

        // Technology and Cargo: the template's, cargo empty.
        var newTech = (JObject)tech.DeepClone();
        newTech[":No"] = NewFromTemplateService.ItemsArray(t.TechItems);
        newTech["hl?"] = NewFromTemplateService.PositionsArray(t.UnlockedPositions);
        if (t.SuperchargedPositions is { } supercharged) newTech["MMm"] = NewFromTemplateService.SuperchargedArray(supercharged);
        var newCargo = (JObject)cargo.DeepClone();
        newCargo[":No"] = new JArray();
        if (t.CargoUnlockedPositions is { Count: > 0 } cargoSlots) newCargo["hl?"] = NewFromTemplateService.PositionsArray(cargoSlots);
        foreach (var store in new[] { newTech, newCargo })
        {
            if (t.SourceClass is { Length: > 0 } cls) store["B@N"] = new JObject { ["1o6"] = cls };
            if (t.Stats.Count > 0) store["@bB"] = NewFromTemplateService.StatsArray(t.Stats);
            NewFromTemplateService.FitGrid(store);
        }
        SaveSessionManager.StageEdit(newTech, NmsInventoryContainer.FreighterTechnologyPath);
        SaveSessionManager.StageEdit(newCargo, NmsInventoryContainer.FreighterCargoPath);

        // What the game learns along with its first freighter.
        AddIfMissing(NmsCataloguePaths.KnownTechnologyArrayPath, "^F_HYPERDRIVE");
        AddIfMissing(NmsFreighterUpgradePaths.BlueprintArrayPath, "^BASECAPSULE");

        // The interior.
        var newBase = new JObject
        {
            ["h4X"] = 8,
            ["BpT"] = 5,
            ["oZw"] = system,
            ["wMC"] = new JArray(anchor),
            ["oHw"] = new JArray(at.Select(v => -v)),
            ["CVX"] = 0,
            ["wx7"] = Now(),
            ["@ZJ"] = ownInterior ? InteriorArray(t) : DefaultInteriorArray(),
            ["B2h"] = "",
            ["3?K"] = OwnerBlock(),
            ["NKm"] = "",
            ["peI"] = new JObject { ["DPp"] = FreighterBaseType },
            ["J=S"] = "",
            ["vyN"] = "",
            ["D9@"] = new JArray(0.0, 0.0, 0.0),
            ["rIR"] = new JArray(0.0, 0.0, 0.0),
            ["idA"] = new JObject { ["pwt"] = "Normal" },
            ["6nS"] = new JObject { ["E7u"] = new JObject { ["7ND"] = "Normal" }, ["?<y"] = 0 },
            ["C0j"] = "",
            ["i4g"] = false,
            ["tww"] = false,
            ["6Ts"] = new JObject { ["cI>"] = "UseDefault" },
        };
        SaveSessionManager.StageEdit(newBase, S.BasesPath.Append(bases.Count.ToString(CultureInfo.InvariantCulture)).ToArray());
        return null;
    }

    // ---------------------------------------------------------------- helpers

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>A base's placed parts (@ZJ) as template objects. Also used for
    /// corvettes (ShipBaseService).</summary>
    internal static List<NmsLoadoutBaseObject>? ReadParts(JToken? parts) =>
        (parts as JArray)?.OfType<JObject>()
            .Where(o => o["r<7"]?.Value<string>() is { Length: > 0 })
            .Select(o => new NmsLoadoutBaseObject
            {
                Id = o["r<7"]!.Value<string>()!,
                UserData = o["CVX"]?.Value<long>() ?? 0,
                Position = Vector(o["wMC"]) ?? new double[3],
                Up = Vector(o["wJ0"]) ?? new double[] { 0, 1, 0 },
                At = Vector(o["aNu"]) ?? new double[] { 0, 0, 1 },
            })
            .ToList();

    internal static JArray InteriorArray(NmsLoadoutTemplate t)
    {
        long now = Now();
        return new JArray(t.InteriorObjects!.Select(o => BaseObject(now, o.Id, o.UserData, o.Position, o.Up, o.At)));
    }

    private static JArray DefaultInteriorArray()
    {
        long now = Now();
        return new JArray(DefaultInterior.Select(o => BaseObject(now, o.Id, o.UserData, o.Position, o.Up, o.At)));
    }

    private static JObject BaseObject(long timestamp, string id, long userData, double[] position, double[] up, double[] at) => new()
    {
        ["b1:"] = timestamp,
        ["r<7"] = id,
        ["CVX"] = userData,
        ["wMC"] = new JArray(position),
        ["wJ0"] = new JArray(up),
        ["aNu"] = new JArray(at),
    };

    /// <summary>A spot about as far from the system's centre as the game's own
    /// freighter was in the test (~36,000 units), in whichever direction keeps
    /// it furthest from every planet. Summoning the freighter moves it anyway.</summary>
    private static double[] ParkingSpot()
    {
        var planets = (SaveSessionManager.GetValue(PlanetPositionsPath) as JArray)?
            .Select(Vector).Where(v => v is not null && v.Any(c => c != 0)).Select(v => v!).ToList() ?? new();
        const double radius = 36_000;
        double[] best = { radius, 0, 0 };
        double bestGap = -1;
        // 26 directions: every combination of -1/0/1 per axis except 0,0,0.
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            if (dx == 0 && dy == 0 && dz == 0) continue;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            var spot = new[] { dx / length * radius, dy / length * radius, dz / length * radius };
            double gap = planets.Count == 0 ? 0 : planets.Min(p => Math.Sqrt(Math.Pow(p[0] - spot[0], 2) + Math.Pow(p[1] - spot[1], 2) + Math.Pow(p[2] - spot[2], 2)));
            if (gap > bestGap) { bestGap = gap; best = spot; }
        }
        return best;
    }

    /// <summary>The player's owner block (platform id, name): from one of
    /// their bases, else their newest own discovery.</summary>
    internal static JObject OwnerBlock()
    {
        if (SaveSessionManager.GetValue(S.BasesPath) is JArray bases
            && bases.OfType<JObject>().Select(b => b["3?K"] as JObject)
                .FirstOrDefault(o => !string.IsNullOrEmpty(o?["f5Q"]?.Value<string>())) is { } fromBase)
            return (JObject)fromBase.DeepClone();
        if (SaveSessionManager.GetValue(S.DiscoveriesPath) is JArray discoveries
            && discoveries.OfType<JObject>().Select(d => d["ksu"] as JObject)
                .Where(o => !string.IsNullOrEmpty(o?["f5Q"]?.Value<string>()))
                .OrderByDescending(o => o!["3I1"]?.Value<long>() ?? 0)
                .FirstOrDefault() is { } fromDiscovery)
            return new JObject
            {
                ["f5Q"] = fromDiscovery["f5Q"], ["K7E"] = fromDiscovery["K7E"] ?? fromDiscovery["f5Q"],
                ["V?:"] = fromDiscovery["V?:"] ?? "", ["D6b"] = fromDiscovery["D6b"] ?? "", ["3I1"] = fromDiscovery["3I1"] ?? 0,
            };
        return new JObject { ["f5Q"] = "", ["K7E"] = "", ["V?:"] = "", ["D6b"] = "", ["3I1"] = 0 };
    }

    private static void AddIfMissing(string[] path, string id)
    {
        if (SaveSessionManager.GetValue(path) is not JArray list) return;
        if (list.Any(v => string.Equals(v.Value<string>(), id, StringComparison.OrdinalIgnoreCase))) return;
        var updated = (JArray)list.DeepClone();
        updated.Add(id);
        SaveSessionManager.StageEdit(updated, path);
    }

    private static double[]? Vector(JToken? token) =>
        token is JArray a && a.Count >= 3 && a.Take(3).All(v => v.Type is JTokenType.Float or JTokenType.Integer)
            ? a.Take(3).Select(v => v.Value<double>()).ToArray()
            : null;

    private static List<NmsLoadoutPosition>? Positions(JToken? array) =>
        array is JArray a
            ? a.Select(e => e["3ZH"] ?? e)
                .Where(p => p[">Qh"] is not null && p["XJ>"] is not null)
                .Select(p => new NmsLoadoutPosition { X = p[">Qh"]!.Value<int>(), Y = p["XJ>"]!.Value<int>() })
                .ToList()
            : null;
}
