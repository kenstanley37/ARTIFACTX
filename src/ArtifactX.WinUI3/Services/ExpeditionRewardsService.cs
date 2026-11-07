using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ArtifactX.Core.Unlocks;
using Microsoft.Data.Sqlite;

namespace ArtifactX.WinUI3.Services;

/// <summary>Reads the catalog's expedition-reward table (CatalogSteps.AddExpeditionRewards)
/// off the UI thread, keyed by item id. Empty for a catalog built before format 60,
/// so callers fall back to the EXPD_ banner/decal/title naming.</summary>
internal static class ExpeditionRewardsService
{
    public static Task<Dictionary<string, ExpeditionReward>> LoadAsync() => Task.Run(() =>
    {
        var result = new Dictionary<string, ExpeditionReward>(StringComparer.OrdinalIgnoreCase);
        if (CatalogService.ResolveDbPath() is not string path) return result;
        using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        db.Open();
        using var query = db.CreateCommand();
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='EXPEDITIONREWARDS'";
        if (query.ExecuteScalar() is not string text) return result;
        foreach (var reward in JsonSerializer.Deserialize<ExpeditionReward[]>(text) ?? [])
            result.TryAdd(reward.ItemId, reward);
        return result;
    });
}
