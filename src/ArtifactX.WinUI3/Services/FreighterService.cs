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
    /// Confirmed 2026-11-17 on Slot 8: custom colours set, Home Seed 0x3EE6031A4EAED6EC picked and saved through
    /// this rule; in game the freighter wore the yellow + red pattern the preview showed.
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

    // ------------------------------------------------------------ the Freighter page's fields

    private static string? Text(string[] path) => SaveSessionManager.GetValue(path)?.Value<string>();

    public static string Name() => Text(NmsInventoryContainer.FreighterNamePath) ?? "";
    public static string? TypeScene() => Text(NmsInventoryContainer.FreighterTypePath);
    public static string? CrewRaceScene() => Text(NmsInventoryContainer.FreighterCrewRacePath);
    public static string? ModelSeed() => Text(NmsInventoryContainer.FreighterModelSeedPath);
    public static string? HomeSeed() => Text(NmsInventoryContainer.FreighterHomeSeedPath);
    public static string? CrewSeed() => Text(NmsInventoryContainer.FreighterCrewSeedPath);
    public static string EngineFlare() => Text(NmsInventoryContainer.FreighterEngineEffectPath) ?? "";

    /// <summary>Stages a new name; false when it's empty or unchanged (a LostFocus mustn't stage the same value).</summary>
    public static bool Rename(string? name)
    {
        string trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed == Name()) return false;
        SaveSessionManager.StageEdit(trimmed, NmsInventoryContainer.FreighterNamePath);
        return true;
    }

    /// <summary>The real class lives on the Cargo container (FreighterClassPath), not Technology's.</summary>
    public static void SetClass(string letter) => SaveSessionManager.StageEdit(letter, NmsInventoryContainer.FreighterClassPath);
    public static void SetType(string scenePath) => SaveSessionManager.StageEdit(scenePath, NmsInventoryContainer.FreighterTypePath);
    public static void SetCrewRace(string scenePath) => SaveSessionManager.StageEdit(scenePath, NmsInventoryContainer.FreighterCrewRacePath);

    /// <summary>Stages an engine flare; false when it's the current one.</summary>
    public static bool SetEngineFlare(string value)
    {
        if (string.Equals(value, EngineFlare(), StringComparison.Ordinal)) return false;
        SaveSessionManager.StageEdit(value, NmsInventoryContainer.FreighterEngineEffectPath);
        return true;
    }

    /// <summary>The whole freighter as a Full Build template: tech, every unlocked slot (a Tech Stack template keeps only
    /// the ones holding tech, so it never unlocks extra slots), stats, Type, Model Seed (the hull's own look; never the
    /// Crew Seed, see NmsLoadoutTemplate.Seed) and furnished interior.</summary>
    public static NmsLoadoutTemplate CaptureFullBuild(ViewModels.InventoryGridViewModel tech, string templateName)
    {
        var template = tech.ExtractTemplate(templateName, DisplayName(), "Freighter");
        template.Scope = "FullBuild";
        template.Stats = StatBonusService.ReadAll(NmsInventoryContainer.FreighterStatBonusesPath);
        template.Seed = ModelSeed();
        template.TypeScenePath = TypeScene();
        FreighterBuildService.Capture(template);
        template.UnlockedPositions = tech.Cells.Where(c => c.State != ViewModels.InventorySlotState.Locked)
            .Select(c => new NmsLoadoutPosition { X = c.X, Y = c.Y }).ToList();
        return template;
    }

    public static NmsLoadoutTemplate CaptureTechStack(ViewModels.InventoryGridViewModel tech, string templateName) =>
        tech.ExtractTemplate(templateName, DisplayName(), "Freighter");

    /// <summary>The name a template records for the freighter ("Freighter" when it has none).</summary>
    public static string DisplayName() => Name().Trim() is { Length: > 0 } n ? n : "Freighter";

    /// <summary>Applies a Tech Stack template. The grid's own class write lands on the Technology container (inert for
    /// a freighter's class, see FreighterClassPath), so the real class is written here when asked.</summary>
    public static void ApplyTechStack(ViewModels.InventoryGridViewModel tech, NmsLoadoutTemplate template, bool alsoMatchClass)
    {
        tech.ApplyTemplate(template, alsoMatchClass);
        if (alsoMatchClass && !string.IsNullOrEmpty(template.SourceClass)) SetClass(template.SourceClass);
    }

    /// <summary>Applies a Full Build template: tech (and class when asked), interior, stats, Model Seed and Type.</summary>
    public static void ApplyFullBuild(ViewModels.InventoryGridViewModel tech, NmsLoadoutTemplate template, bool alsoMatchClass)
    {
        ApplyTechStack(tech, template, alsoMatchClass);
        FreighterBuildService.ApplyInterior(template);
        StatBonusService.WriteAll(NmsInventoryContainer.FreighterStatBonusesPath, template.Stats);
        if (!string.IsNullOrEmpty(template.Seed)) SaveSessionManager.StageEdit(template.Seed, NmsInventoryContainer.FreighterModelSeedPath);
        if (!string.IsNullOrEmpty(template.TypeScenePath)) SetType(template.TypeScenePath);
    }

    /// <summary>What deleting the freighter takes with it, for the confirmation.</summary>
    public static string DeleteSummary(int cargoStacks)
    {
        int frigates = (SaveSessionManager.GetValue(NmsFrigatePaths.FrigateArrayPath) as JArray)?.Count ?? 0;
        return "its tech, its furnished interior"
            + (cargoStacks > 0 ? $", the {cargoStacks} stacks in its cargo" : "")
            + " and its look. What you've learned (blueprints, known tech) stays"
            + (frigates > 0 ? $", and your {frigates} frigate{(frigates == 1 ? "" : "s")} stay as they are" : "");
    }

    /// <summary>The apply confirmation's summary line.</summary>
    public static string ApplySummary(string sourceLabel, bool includesStatsAndSeed, bool replacesInterior) =>
        includesStatsAndSeed && replacesInterior
            ? $"This replaces your freighter's entire tech loadout, base stats, Type, appearance seed and interior (every room and furnishing) with {sourceLabel}. "
                + "Items in your storage containers aren't affected."
            : includesStatsAndSeed
            ? $"This replaces your freighter's entire tech loadout, base stats, Type, and appearance seed with {sourceLabel}."
            : $"This replaces your freighter's entire tech loadout with {sourceLabel}.";

    /// <summary>The confirmation text for adding a freighter from a template (a starter hull when <paramref name="fresh"/>).</summary>
    public static (string Title, string Body) AddFreighterText(NmsLoadoutTemplate template)
    {
        bool fresh = template.TechItems.Count <= 2 && !FreighterBuildService.HasInterior(template);
        return fresh
            ? ($"Add a new {template.Name.ToLowerInvariant()}?",
                "A brand-new one, just as the game hands it over: C class, a hyperdrive and scanner, the starter interior and a random look, "
                + "parked in the system you're in, with a new Gek captain (change the race under Crew Race). Nothing is written to your save until you Save.")
            : ($"Add \"{template.Name}\" as your freighter?",
                "It arrives with this template's model, look, tech, class, stats and "
                + (FreighterBuildService.HasInterior(template) ? "furnished interior" : "the standard starter interior")
                + ", parked in the system you're in. Its cargo starts empty and it gets a new Gek captain (change the race under Crew Race). "
                + "Nothing is written to your save until you Save.");
    }
}
