using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArtifactX.Core.Unlocks;
using Microsoft.Data.Sqlite;

namespace ArtifactX.WinUI3.Services;

/// <summary>Reads the in-game Catalog's sections (CatalogSteps.AddCatalogueSections,
/// catalog format 62) and the names to show for their items, off the UI thread.
/// Names use the game's own mixed-case form ("Titanic Skull", NameLowerEnglish) as its
/// Catalog screen does, title-casing the capitals only when that's missing. Empty for
/// an older catalog.</summary>
internal static class CatalogueSectionsService
{
    public sealed record Catalogue(CatalogueSection[] Sections, Dictionary<string, string> Names);

    public static Task<Catalogue> LoadAsync() => Task.Run(() =>
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (CatalogService.ResolveDbPath() is not string path) return new Catalogue([], names);
        using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        db.Open();
        using var query = db.CreateCommand();
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='CATALOGUESECTIONS'";
        if (query.ExecuteScalar() is not string text) return new Catalogue([], names);
        var sections = JsonSerializer.Deserialize<CatalogueSection[]>(text) ?? [];

        var wanted = sections.SelectMany(s => s.Categories).SelectMany(c => c.Items).ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var nameQuery = db.CreateCommand();
        nameQuery.CommandText = "SELECT GameId, NameLowerEnglish, NameEnglish FROM Items WHERE NameLowerEnglish IS NOT NULL OR NameEnglish IS NOT NULL";
        using var reader = nameQuery.ExecuteReader();
        while (reader.Read())
        {
            string id = reader.GetString(0);
            if (!wanted.Contains(id) || names.ContainsKey(id)) continue;
            string? lower = reader.IsDBNull(1) ? null : reader.GetString(1);
            string? upper = reader.IsDBNull(2) ? null : reader.GetString(2);
            names[id] = lower is { Length: > 0 } && lower.Any(char.IsUpper) ? lower
                : CultureInfo.CurrentCulture.TextInfo.ToTitleCase((upper ?? lower ?? id).ToLowerInvariant());
        }
        return new Catalogue(sections, names);
    });
}
