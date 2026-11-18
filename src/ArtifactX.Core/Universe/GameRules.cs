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
    /// <summary>This file's layout; a phone refuses a newer one rather than misread it.</summary>
    public const int CurrentFormat = 1;

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

    // Parsed tables, once each (PLANETPROFILE alone is ~650 KB of JSON).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object?> _parsed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One table, read the way the desktop reads it (System.Text.Json defaults) and kept; null when absent.
    /// Callers treat it as read-only - it is shared.</summary>
    public T? Row<T>(string id) where T : class =>
        _parsed.GetOrAdd(id, key => Rows.TryGetValue(key, out var json) && json.Length > 0 ? JsonSerializer.Deserialize<T>(json) : null) as T;

    public string? ItemName(string id) => ItemNames.TryGetValue(id, out var name) ? name : null;

    public BiomeTables? Biomes => Row<BiomeTables>("BIOMETABLES");
    public PlanetProfileTables? Profiles => Row<PlanetProfileTables>("PLANETPROFILE");
    public StationEquipmentTables? Stations => Row<StationEquipmentTables>("STATIONEQUIPMENT");
    public BuildingOverrideRule[]? BuildingOverrides => Row<BuildingOverrideRule[]>("BUILDINGOVERRIDES");
    public Recipes.ExtractionTables? Extraction => Row<Recipes.ExtractionTables>("EXTRACTIONTABLES");

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
        };
    }
}
