using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace ArtifactX.Core.Universe;

/// <summary>
/// The mobile app's rules file: the game's numbers and text lists the generators need, with no images, models or
/// other assets (owner decision 2026-10-29, docs/AGENT_HANDOFF_NOTES.md "Mobile app constraints"). Exported on a PC
/// from a catalog built from the player's install (DataCataloger export-mobile-rules), stamped with that game build,
/// rebuilt each NMS update and kept out of git.
/// <para>
/// <see cref="Rows"/> holds the catalog's "PlanetGeneration" rows exactly as the PC stores them (the JSON text of
/// BiomeTables, PlanetProfileTables, ExtractionTables...), so a phone deserializes the same text the desktop does and
/// both give the same results. <see cref="ItemNames"/> is the products' and substances' display names by id.
/// </para>
/// </summary>
public sealed class GameRules
{
    /// <summary>This file's layout; a phone refuses a newer one rather than misread it. 2 (2026-11-18): recipes, item
    /// values and the EGGSEQUENCER row. 3: multi-tool name words. 4: fish habitats. 5: trade category and legality on item values.</summary>
    public const int CurrentFormat = 5;

    public int Format { get; init; } = CurrentFormat;

    /// <summary>The game build the tables came from ("exe version/fingerprint").</summary>
    public string GameBuild { get; init; } = "";

    /// <summary>The readable build ("NMS Forsaken 7.14 · build 185034") for the about line.</summary>
    public string GameBuildLabel { get; init; } = "";

    /// <summary>The PC catalog's format (GameDataBuilder.CatalogFormat) when exported.</summary>
    public int CatalogFormat { get; init; }

    public DateTime ExportedUtc { get; init; }

    /// <summary>Catalog row id (BIOMETABLES, PLANETPROFILE, EXTRACTIONTABLES...) to its JSON text.</summary>
    public Dictionary<string, string> Rows { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Products' and substances' display names by game id, as the catalog has them.</summary>
    public Dictionary<string, string> ItemNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every refiner and cooking recipe (the catalog's "Recipes" rows), as the recipe planner takes them.</summary>
    public List<Recipes.RequirementRecipe> Recipes { get; init; } = [];

    /// <summary>Products' and substances' base values by game id (the catalog's "ItemValues" rows): the Egg
    /// Sequencer's doses are worked out from them.</summary>
    public Dictionary<string, ItemBaseValue> ItemValues { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The multi-tool name word lists (the catalog's "MultiToolNameWords" rows) that cabinet tools are named from.</summary>
    public Dictionary<string, List<string>> NameWords { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where each fish caught outside missions lives (the catalog's "Fish" rows), by item id.</summary>
    public Dictionary<string, FishHabitat> Fish { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string>? Words(string key) => NameWords.TryGetValue(key, out var words) ? words : null;

    // Parsed tables, once each (PLANETPROFILE alone is ~650 KB of JSON).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object?> _parsed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One table, read the way the desktop reads it (System.Text.Json defaults) and kept; null when absent.
    /// Callers treat it as read-only - it is shared.</summary>
    public T? Row<T>(string id) where T : class =>
        _parsed.GetOrAdd(id, key => Rows.TryGetValue(key, out var json) && json.Length > 0 ? JsonSerializer.Deserialize<T>(json) : null) as T;

    public string? ItemName(string id) => ItemNames.TryGetValue(id, out var name) ? name : null;

    [System.Text.Json.Serialization.JsonIgnore] public BiomeTables? Biomes => Row<BiomeTables>("BIOMETABLES");
    [System.Text.Json.Serialization.JsonIgnore] public PlanetProfileTables? Profiles => Row<PlanetProfileTables>("PLANETPROFILE");
    [System.Text.Json.Serialization.JsonIgnore] public StationEquipmentTables? Stations => Row<StationEquipmentTables>("STATIONEQUIPMENT");
    [System.Text.Json.Serialization.JsonIgnore] public BuildingOverrideRule[]? BuildingOverrides => Row<BuildingOverrideRule[]>("BUILDINGOVERRIDES");
    [System.Text.Json.Serialization.JsonIgnore] public BuildingClassPlacement[]? BuildingPlacement => Row<BuildingClassPlacement[]>("BUILDINGPLACEMENT");
    [System.Text.Json.Serialization.JsonIgnore] public Recipes.CreatureProductSource[]? CreatureSources => Row<Recipes.CreatureProductSource[]>("CREATUREPRODUCTS");
    [System.Text.Json.Serialization.JsonIgnore] public GalaxyPlacementSettings? GalaxyPlacement => Row<GalaxyPlacementSettings>("GALAXYPLACEMENT");
    [System.Text.Json.Serialization.JsonIgnore] public GalaxyGenerationSettings? GalaxySettings => Row<GalaxyGenerationSettings>("GALAXYGENERATION");
    [System.Text.Json.Serialization.JsonIgnore] public Recipes.ExtractionTables? Extraction => Row<Recipes.ExtractionTables>("EXTRACTIONTABLES");
    [System.Text.Json.Serialization.JsonIgnore] public Recipes.FoodIngestorTables? Food => Row<Recipes.FoodIngestorTables>("FOODINGESTOR");
    [System.Text.Json.Serialization.JsonIgnore] public InventoryGenerationTables? Inventory => Row<InventoryGenerationTables>("INVENTORYGENERATION");
    [System.Text.Json.Serialization.JsonIgnore] public MultiToolModelTables? MultiToolModel => Row<MultiToolModelTables>("MULTITOOLMODEL");
    [System.Text.Json.Serialization.JsonIgnore] public Companions.EggSequencerRules? EggSequencer => Row<Companions.EggSequencerRules>("EGGSEQUENCER");

    /// <summary>Writes the file: gzip-compressed JSON (about a tenth of the raw ~2 MB).</summary>
    public void Write(Stream output)
    {
        using var zip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true);
        JsonSerializer.Serialize(zip, this);
    }

    /// <summary>Reads a file written by <see cref="Write"/>. Throws InvalidDataException for a format this build can't
    /// read.</summary>
    public static GameRules Read(Stream input)
    {
        using var zip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        var rules = JsonSerializer.Deserialize<GameRules>(zip) ?? throw new InvalidDataException("The rules file is empty.");
        if (rules.Format > CurrentFormat)
            throw new InvalidDataException($"The rules file is format {rules.Format}; this app reads up to {CurrentFormat}. Update the app.");
        // Deserialized dictionaries lose the case-insensitive comparer.
        return new GameRules
        {
            Format = rules.Format,
            GameBuild = rules.GameBuild,
            GameBuildLabel = rules.GameBuildLabel,
            CatalogFormat = rules.CatalogFormat,
            ExportedUtc = rules.ExportedUtc,
            Rows = new(rules.Rows, StringComparer.OrdinalIgnoreCase),
            ItemNames = new(rules.ItemNames, StringComparer.OrdinalIgnoreCase),
            Recipes = rules.Recipes,
            ItemValues = new(rules.ItemValues, StringComparer.OrdinalIgnoreCase),
            NameWords = new(rules.NameWords, StringComparer.OrdinalIgnoreCase),
            Fish = new(rules.Fish, StringComparer.OrdinalIgnoreCase),
        };
    }
}

/// <summary>An item's base value (GcProductData/GcRealitySubstanceData BaseValue), whether it is a substance, its trade
/// category (Recipes.TradeEconomies names the economy) and whether it is contraband.</summary>
public sealed record ItemBaseValue(long Value, bool IsSubstance, string TradeCategory = "None", bool Illegal = false);
