using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>One frigate as the Frigates page lists it.</summary>
public sealed record FrigateSummary(int Index, string CustomName, string Role, string Rating, string Race, ulong ResourceSeed, int[] Stats, int Expeditions, string Key);

/// <summary>
/// The player's frigate fleet (;Du - see NmsFrigatePaths for the confirmed field map): reading
/// it for the page and staging edits. Arrays (stats, traits) are staged whole, since a deeper
/// leaf-only stage isn't seen by SaveSessionManager's lookup at the array path.
/// </summary>
public static class FrigateService
{
    /// <summary>The 11 stat slots in GcFrigateStatType order, in the game's words where it shows
    /// them - the frigate screen reads Combat, Exploration, Industrial, Trade (enum Mining and
    /// Diplomatic; checked against SS-2 Ujimitam on Slot 8, 2026-10-22: 21 / 1 / 2 / 2).</summary>
    public static readonly string[] StatNames =
        { "Combat", "Exploration", "Industrial", "Trade", "Fuel Burn Rate", "Fuel Capacity", "Speed", "Extra Loot", "Repair", "Invulnerable", "Stealth" };

    public static bool HasFreighter() =>
        !string.IsNullOrEmpty(SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>());

    public static List<FrigateSummary> ReadAll()
    {
        var list = new List<FrigateSummary>();
        var copies = new Dictionary<string, int>();
        if (SaveSessionManager.GetValue(NmsFrigatePaths.FrigateArrayPath) is not JArray array) return list;
        for (int i = 0; i < array.Count; i++)
        {
            var f = array[i];
            string identity = Identity(f) ?? $"slot:{i}";
            int copy = copies[identity] = copies.GetValueOrDefault(identity, -1) + 1;
            list.Add(new FrigateSummary(i,
                f?["fH8"]?.Value<string>() ?? "",
                f?["uw7"]?["uw7"]?.Value<string>() ?? "",
                f?["1o6"]?["1o6"]?.Value<string>() ?? "",
                f?["SS2"]?["0Hi"]?.Value<string>() ?? "",
                ShipPartsService.TryParseSeed(f?["SLc"]?[1]?.Value<string>(), out ulong resourceSeed) ? resourceSeed : 0,
                (f?["gUR"] as JArray)?.Select(v => v.Type == JTokenType.Integer ? v.Value<int>() : 0).ToArray() ?? [],
                f?["5es"]?.Value<int>() ?? 0,
                $"{identity}#{copy}"));
        }
        return list;
    }

    /// <summary>A frigate's identity across roster moves. Class, name and stats may
    /// change; resource and home seeds identify it. Duplicate copies are counted
    /// separately by the roster reader and LiveItemsService.</summary>
    internal static string? Identity(JToken? frigate)
    {
        if (!ShipPartsService.TryParseSeed(frigate?["SLc"]?[1]?.Value<string>(), out ulong resource)) return null;
        ShipPartsService.TryParseSeed(frigate?["@ui"]?[1]?.Value<string>(), out ulong home);
        return $"{resource:X16}|{home:X16}";
    }

    public static string Text(string[] path) => SaveSessionManager.GetValue(path)?.Value<string>() ?? "";

    /// <summary>Stages a text field when it changed. True when staged.</summary>
    public static bool SetText(string[] path, string value)
    {
        if (value == Text(path)) return false;
        SaveSessionManager.StageEdit(value, path);
        return true;
    }

    public static void SetInt(string[] path, double value)
    {
        if (!double.IsNaN(value)) SaveSessionManager.StageEdit((int)value, path);
    }

    /// <summary>Stages one stat by rebuilding the whole 11-slot array.</summary>
    public static void SetStat(int frigate, int stat, double value)
    {
        var path = NmsFrigatePaths.StatsPath(frigate);
        if (double.IsNaN(value) || SaveSessionManager.GetValue(path) is not JArray stats || stat >= stats.Count) return;
        var updated = new JArray(stats.Select(s => s.DeepClone())) { [stat] = (int)value };
        SaveSessionManager.StageEdit(updated, path);
    }

    /// <summary>The trait ids in a frigate's slots, "^" stripped ("" = empty slot).</summary>
    public static List<string> Traits(int frigate) =>
        (SaveSessionManager.GetValue(NmsFrigatePaths.TraitIDsPath(frigate)) as JArray)?
            .Select(t => (t.Value<string>() ?? "").TrimStart('^')).ToList() ?? [];

    /// <summary>Stages one trait slot by rebuilding the whole array; "" empties it ("^").</summary>
    public static void SetTrait(int frigate, int slot, string traitId)
    {
        var path = NmsFrigatePaths.TraitIDsPath(frigate);
        if (SaveSessionManager.GetValue(path) is not JArray traits || slot >= traits.Count) return;
        if (string.Equals((traits[slot].Value<string>() ?? "").TrimStart('^'), traitId, System.StringComparison.OrdinalIgnoreCase)) return;
        var updated = new JArray(traits.Select(t => t.DeepClone())) { [slot] = "^" + traitId };
        SaveSessionManager.StageEdit(updated, path);
    }

    public static bool HasEdits(int frigate) => SaveSessionManager.HasStagedEditsUnder(NmsFrigatePaths.FrigatePath(frigate));
    public static void RevertEdits(int frigate) => SaveSessionManager.RevertEditsUnder(NmsFrigatePaths.FrigatePath(frigate));
}
