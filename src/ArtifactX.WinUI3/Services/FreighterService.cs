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
    /// home seed and crew (race and seed). The interior and adding or deleting the
    /// freighter live in the bases list - the title bar's Reset covers those.</summary>
    private static readonly string[][] PageRoots =
    {
        NmsInventoryContainer.FreighterTechnologyPath,
        NmsInventoryContainer.FreighterCargoPath,
        NmsInventoryContainer.FreighterNamePath,
        new[] { "vLc", "6f=", "bIR" },
        new[] { "vLc", "6f=", "kYq" },
        new[] { "vLc", "6f=", "Sjw" },
    };

    public static bool HasPageEdits() => PageRoots.Any(p => SaveSessionManager.HasStagedEditsUnder(p));

    public static void RevertPageEdits()
    {
        foreach (var path in PageRoots) SaveSessionManager.RevertEditsUnder(path);
    }
}
