using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// A corvette's build - cockpit, habitat, wings, engines, decorations - for
/// Starship Full Build templates. The ship entry (@Cs) only holds the corvette
/// model (BIGGS.SCENE.MBIN) and seed; what the player built lives in its own
/// PlayerShipBase entry in the base list (F?0), linked by that base's
/// UserData (CVX) = the ship's slot in @Cs. Checked 2026-10-12: all 18
/// corvettes across every save and expedition context had exactly one such
/// base, 13-163 parts each, all within ~30 units of the ship (local space).
/// Without it a copied corvette came out as the plain default one (reported
/// the same day: Slot 1's corvette copied to Slot 7 with none of its build).
///
/// The template keeps the parts (InteriorObjects, same shape as a freighter's
/// interior) and the rest of the base entry as JSON (ShipBase) with the parts
/// list emptied and the owner blanked, so the game's own key order is kept
/// when it's written back for the new ship's slot.
/// </summary>
public static class ShipBaseService
{
    private const string ShipBaseType = "PlayerShipBase";

    private static int? IndexFor(JArray bases, int shipIndex)
    {
        for (int i = 0; i < bases.Count; i++)
            if (bases[i] is JObject b
                && string.Equals(b["peI"]?["DPp"]?.Value<string>(), ShipBaseType, StringComparison.Ordinal)
                && b["CVX"]?.Type == JTokenType.Integer && b["CVX"]!.Value<long>() == shipIndex)
                return i;
        return null;
    }

    /// <summary>Records the ship's build, if it has one (corvettes).</summary>
    public static void Capture(NmsLoadoutTemplate t, int shipIndex)
    {
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases || IndexFor(bases, shipIndex) is not int index) return;
        var shipBase = (JObject)bases[index].DeepClone();
        t.InteriorObjects = FreighterBuildService.ReadParts(shipBase["@ZJ"]);
        shipBase["@ZJ"] = new JArray();
        shipBase["3?K"] = new JObject();
        t.ShipBase = shipBase.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>Ship slots that have a corvette build, with its part count -
    /// read once for a whole list (the base list is large).</summary>
    public static System.Collections.Generic.Dictionary<int, int> BuildsBySlot()
    {
        var builds = new System.Collections.Generic.Dictionary<int, int>();
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases) return builds;
        foreach (var b in bases.OfType<JObject>())
            if (string.Equals(b["peI"]?["DPp"]?.Value<string>(), ShipBaseType, StringComparison.Ordinal)
                && b["CVX"]?.Type == JTokenType.Integer)
                builds[(int)b["CVX"]!.Value<long>()] = (b["@ZJ"] as JArray)?.Count ?? 0;
        return builds;
    }

    /// <summary>Corvettes use this model; a corvette can exist without a build
    /// yet (the game makes the default one).</summary>
    public static bool IsCorvetteModel(string? scenePath) =>
        scenePath?.Contains("/BIGGS/", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>How many parts the corvette build in this ship slot has (0 = none).</summary>
    public static int PartCount(int shipIndex) =>
        SaveSessionManager.GetValue(S.BasesPath) is JArray bases && IndexFor(bases, shipIndex) is int index
            ? (bases[index]["@ZJ"] as JArray)?.Count ?? 0 : 0;

    /// <summary>Removes the build linked to a ship slot (when the ship is deleted).</summary>
    public static void Remove(int shipIndex)
    {
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases || IndexFor(bases, shipIndex) is not int index) return;
        var remaining = (JArray)bases.DeepClone();
        remaining.RemoveAt(index);
        SaveSessionManager.StageEdit(remaining, S.BasesPath);
    }

    public static bool HasBuild(NmsLoadoutTemplate t) => t.ShipBase is { Length: > 0 } && t.InteriorObjects is { Count: > 0 };

    /// <summary>Writes the template's build for the ship in `shipIndex`,
    /// replacing any build already linked to that slot.</summary>
    public static void Write(NmsLoadoutTemplate t, int shipIndex)
    {
        if (!HasBuild(t) || SaveSessionManager.GetValue(S.BasesPath) is not JArray bases) return;
        JObject shipBase;
        try { shipBase = JObject.Parse(t.ShipBase!); }
        catch (Newtonsoft.Json.JsonException) { return; }

        shipBase["peI"] = new JObject { ["DPp"] = ShipBaseType };
        shipBase["CVX"] = shipIndex;
        shipBase["3?K"] = FreighterBuildService.OwnerBlock();
        shipBase["wx7"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        shipBase["@ZJ"] = FreighterBuildService.InteriorArray(t);
        // Address: the system the player is in, like a corvette built here.
        if (S.ReadSystemAddress(SaveSessionManager.GetValue(S.CurrentUniverseAddressPath)) is ulong system)
            shipBase["oZw"] = system;

        int target = IndexFor(bases, shipIndex) ?? bases.Count;
        SaveSessionManager.StageEdit(shipBase, S.BasesPath.Append(target.ToString(CultureInfo.InvariantCulture)).ToArray());
    }
}
