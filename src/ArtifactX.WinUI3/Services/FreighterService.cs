using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The freighter in the save: whether there is one, its class, and which tech and upgrade modules it takes. Used by Freighter.
/// </summary>
public static class FreighterService
{
    public static string? ClassLetter() =>
        SaveSessionManager.GetValue(NmsInventoryContainer.FreighterClassPath)?.Value<string>();

    /// <summary>The freighter hull's model scene path (bIR.93M) is empty
    /// until the player actually owns one - confirmed 2026-08-10 against a
    /// save that hadn't left the starting planet, where it was "" even
    /// though the Tech/Cargo containers (0wS/8ZP) and their supercharged-slot
    /// layout (hl?) already existed with real, non-default data. Those
    /// containers appear to be pre-allocated by the game from the start
    /// (same pattern as Multi-Tool/Ship's padding slots), so their mere
    /// presence isn't a reliable ownership signal - the hull scene path is.</summary>
    public static bool HasFreighter() =>
        !string.IsNullOrEmpty(SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>());

    /// <summary>Freighter module families, from the 24 freighters in the real
    /// saves (2026-10-13): hyperdrive plus the six fleet ones, each topping
    /// out at 3.</summary>
    public static readonly string[] ModuleFamilies =
        { "UP_FRHYP", "UP_FRCOM", "UP_FREXP", "UP_FRFUE", "UP_FRMIN", "UP_FRSPE", "UP_FRTRA" };

    public static readonly string[] TechGroupOrder = { "Hyperdrive", "Fleet", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats), same as Starships.</summary>
    public static string TechGroup(CatalogPart tech)
    {
        string stat = tech.Stat ?? "";
        if (stat.StartsWith("Freighter_Hyperdrive")) return "Hyperdrive";
        if (stat.StartsWith("Freighter_Fleet")) return "Fleet";
        return "Utilities";
    }
    /// <summary>Everything the Freighter page's details and inventories edit: tech,
    /// cargo (which holds the class and base stats), name, hull (type and model seed),
    /// home seed and crew (race and seed) - plus FreighterBuildService's interior,
    /// facing and parking, and a pending delete.</summary>
    private static readonly string[][] PageRoots =
    {
        NmsInventoryContainer.FreighterTechnologyPath,
        NmsInventoryContainer.FreighterCargoPath,
        NmsInventoryContainer.FreighterNamePath,
        new[] { "vLc", "6f=", "bIR" },
        new[] { "vLc", "6f=", "kYq" },
        new[] { "vLc", "6f=", "Sjw" },
        NmsInventoryContainer.FreighterEngineEffectPath,
    };

    public static bool HasPageEdits() =>
        PageRoots.Concat(FreighterBuildService.PageRoots()).Any(p => SaveSessionManager.HasStagedEditsUnder(p))
        || FreighterBuildService.HasPendingRemoval();

    public static void RevertPageEdits()
    {
        // Newest first: a freighter added after a delete sits at the end of the
        // shortened list - undoing the delete first left that entry over another
        // base (2026-10-21).
        foreach (var path in PageRoots.Concat(FreighterBuildService.PageRoots().ToList())) SaveSessionManager.RevertEditsUnder(path);
        FreighterBuildService.RevertRemoval();
        SyncCustomColoursWithHomeSeed();
    }

    /// <summary>Character-customisation entry 15 (l:j) holds an ordinary freighter's picked colours
    /// (paint diff on Slot 8, 2026-10-22). A Dreadnought keeps its own entry, tagged with its model.</summary>
    private const int FreighterCustomisationIndex = 15;

    /// <summary>The customisation entry for this freighter model: the one tagged with it (a Dreadnought's),
    /// else the ordinary freighter entry. Null index when the save has neither.</summary>
    public static (JToken? Entry, int Index) Customisation(string? scene)
    {
        if (SaveSessionManager.GetValue("vLc", "6f=", "l:j") is not JArray entries) return (null, -1);
        string model = "^" + System.IO.Path.GetFileName(scene ?? "").Split('.')[0];
        for (int i = 0; i < entries.Count; i++)
            if (entries[i]["wnR"]?["T>1"] is JArray tags
                && tags.Any(t => string.Equals(t["@6c"]?.ToString(), model, StringComparison.OrdinalIgnoreCase)))
                return (entries[i], i);
        return entries.Count > FreighterCustomisationIndex
            ? (entries[FreighterCustomisationIndex], FreighterCustomisationIndex) : (null, -1);
    }

    private static readonly string[] CustomColoursPath =
        { "vLc", "6f=", "l:j", FreighterCustomisationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), "wnR", "Aak" };

    /// <summary>Colours the player picked in game for an ordinary freighter, as saved (staged edits ignored).</summary>
    public static bool HasSavedCustomColours() =>
        SaveSessionManager.GetSavedValue(CustomColoursPath) is JArray { Count: > 0 };

    /// <summary>The Home Seed differs from the one in the save.</summary>
    public static bool HomeSeedChanged() =>
        !JToken.DeepEquals(SaveSessionManager.GetValue(NmsInventoryContainer.FreighterHomeSeedPath),
            SaveSessionManager.GetSavedValue(NmsInventoryContainer.FreighterHomeSeedPath));

    /// <summary>
    /// Picked colours win over the Home Seed's paint (#69), so a new Home Seed also clears them, as the game's
    /// Paint screen "Reset Custom Colors" does: on Slot 8 (2026-11-15) that left entry 15's Aak empty, and the
    /// freighter then showed its Home Seed paint. A Home Seed back at its saved value restores the colours.
    /// Ordinary freighters only: whether a Dreadnought's paint follows the Home Seed is unknown.
    /// </summary>
    public static void SyncCustomColoursWithHomeSeed()
    {
        string? scene = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>();
        if (Customisation(scene).Index != FreighterCustomisationIndex || !HasSavedCustomColours()) return;
        bool clear = HomeSeedChanged();
        if (clear == SaveSessionManager.HasStagedEdit(CustomColoursPath)) return;
        if (clear) SaveSessionManager.StageEdit(new JArray(), CustomColoursPath);
        else SaveSessionManager.RevertEdit(CustomColoursPath);
    }
}
