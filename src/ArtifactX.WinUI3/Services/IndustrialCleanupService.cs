using System.Text.Json;
using System.Threading.Tasks;
using ArtifactX.Core.IndustrialCleanup;
using ArtifactX.Core.Unlocks;
using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;

namespace ArtifactX.WinUI3.Services;

/// <summary>Reads catalog-projected cleanup guidance off the UI thread.
/// Does not start missions, write saves or infer reward ownership.</summary>
internal sealed class IndustrialCleanupService
{
    public Task<IndustrialCleanupCatalog?> LoadAsync() => Task.Run(() =>
    {
        if (CatalogService.ResolveDbPath() is not string path) return null;
        using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        db.Open();
        T? Read<T>(string id)
        {
            using var query = db.CreateCommand();
            query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
            query.Parameters.AddWithValue("$id", id);
            return query.ExecuteScalar() is string text ? JsonSerializer.Deserialize<T>(text) : default;
        }
        var guide = Read<IndustrialCleanupDefinition>("INDUSTRIALCLEANUP");
        return guide is null ? null : new IndustrialCleanupCatalog(guide,
            Read<TitleUnlockRule[]>("TITLEUNLOCKS") ?? [], Read<VehicleCustomisationOption[]>("VEHICLECUSTOMISATION") ?? []);
    });
}
