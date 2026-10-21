using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// A Multi-Tool in the save (vLc.6f=.SuJ[i]): its class, how many tech slots that gives it, its kind and seed, and which tech and upgrade modules it takes. Used by Multi-Tool.
/// </summary>
public static class MultiToolService
{
    public static int TechSlots(Dictionary<string, int> capacity, string? classLetter) =>
        capacity.TryGetValue(MultiToolCapacity.TechCapacityKey(classLetter), out int slots)
            ? slots : MultiToolCapacity.FallbackTechSlots;

    /// <summary>Reads a tool's Class letter directly from the save, BEFORE any
    /// InventoryGridViewModel exists for it - needed up front to size the grid
    /// correctly, since capacity depends on class (MultiToolCapacity).</summary>
    public static string? ClassLetter(int toolIndex) =>
        SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolTechnologyPath(toolIndex).Append("B@N").Append("1o6").ToArray())?.Value<string>();

    /// <summary>Multi-tool module families, from the 94 tools in the real saves
    /// (2026-10-13): each tops out at 3 per tool. UP_SENGUN is a one-off
    /// salvaged find, so it isn't offered.</summary>
    public static readonly string[] ModuleFamilies =
        { "UP_LASER", "UP_SCAN", "UP_BOLT", "UP_SMG", "UP_SHOT", "UP_CANN", "UP_RAIL", "UP_GREN", "UP_TGREN" };

    public static readonly string[] TechGroupOrder = { "Mining", "Scanning", "Weapons", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats), same as Starships.</summary>
    public static string TechGroup(CatalogPart tech)
    {
        string stat = tech.Stat ?? "";
        if (stat is "Weapon_Laser" or "Weapon_SunLaser") return "Mining";
        if (stat.StartsWith("Weapon_Scan")) return "Scanning";
        if (stat is "Weapon_Projectile" or "Weapon_Burst" or "Weapon_Shotgun" or "Weapon_Cannon" or "Weapon_Rail"
            or "Weapon_Grenade" or "Weapon_MineGrenade" or "Weapon_StunGrenade" or "Weapon_Flame" or "Weapon_Stun"
            or "Weapon_Gravity" or "Weapon_SoulLaser") return "Weapons";
        return "Utilities";
    }

    public static async Task<string?> KindOfAsync(int index) =>
        SeedOf(index) is ulong seed ? await MultiToolKinds.KindAsync(seed) : null;

    /// <summary>A tool's own seed (SuJ[i].@EL[1]), which picks its look, kind and name.</summary>
    public static ulong? SeedOf(int index)
    {
        string? seedText = index < 0 ? null
            : SaveSessionManager.GetValue("vLc", "6f=", "SuJ", index.ToString(), "@EL", "1")?.Value<string>();
        if (seedText is not { Length: > 2 }) return null;
        string hex = seedText.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? seedText[2..] : seedText;
        return ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong seed)
            ? seed : null;
    }
}
