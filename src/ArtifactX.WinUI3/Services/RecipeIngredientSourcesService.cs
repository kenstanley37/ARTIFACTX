using Microsoft.Data.Sqlite;
using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Helpers.Recipes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace ArtifactX.WinUI3.Services;

/// <summary>Reads the game's item descriptions for terminal recipe requirements.
/// Descriptions are catalog data, not inferred crafting recipes or invented acquisition rules.</summary>
internal static class RecipeIngredientSourcesService
{
    /// <summary>Which creature gives which item when hunted or fed (catalog CREATUREPRODUCTS, format 56); empty
    /// without it.</summary>
    public static Task<CreatureProductSource[]> CreatureSourcesAsync()
    {
        string? path = CatalogService.ResolveDbPath();
        return Task.Run(() =>
        {
            if (path is null) return Array.Empty<CreatureProductSource>();
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='CREATUREPRODUCTS'";
            return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<CreatureProductSource[]>(json) ?? [] : [];
        });
    }

    public static Task<Dictionary<string, string>> LoadAsync(IEnumerable<string> itemIds, CancellationToken token)
    {
        string? path = CatalogService.ResolveDbPath();
        string[] ids = itemIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.Run(() =>
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (path is null || ids.Length == 0) return result;
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            var parameters = ids.Select((id, i) => { string key = $"$id{i}"; command.Parameters.AddWithValue(key, id); return key; }).ToArray();
            command.CommandText = $"SELECT i.GameId, i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id=i.CategoryId WHERE i.GameId IN ({string.Join(",", parameters)}) AND c.TemplateType IN ('GcProductTable','GcSubstanceTable','GcTechnologyTable')";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                if (!reader.IsDBNull(1)) result[reader.GetString(0)] = Regex.Replace(reader.GetString(1), "<[^>]*>", "").Trim();
            }
            reader.Close();
            command.Parameters.Clear();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='CREATUREPRODUCTS'";
            if (command.ExecuteScalar() is string json)
            {
                var wanted = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var group in (JsonSerializer.Deserialize<CreatureProductSource[]>(json) ?? [])
                    .Where(s => wanted.Contains(s.ItemId)).GroupBy(s => s.ItemId, StringComparer.OrdinalIgnoreCase))
                {
                    token.ThrowIfCancellationRequested();
                    result[group.Key] = CreatureSourceText.Describe(group) + "\n\n" + result.GetValueOrDefault(group.Key);
                }
            }
            return result;
        }, token);
    }
}
