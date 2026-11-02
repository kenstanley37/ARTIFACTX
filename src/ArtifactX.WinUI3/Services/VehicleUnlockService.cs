using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArtifactX.Core.Unlocks;
using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>Read-only customization catalog and account observations. Account
/// membership is reported as recorded ownership, never inferred from statistics.
/// 2026-11-02: Bgb contains T_RECYCLE1..8; fyX records flatbed/weathered finish;
/// B1h/d4U record expedition cabin/legs/paint, matching owner screenshots.
/// Missing records are not diagnosed or repaired here.</summary>
internal sealed class VehicleUnlockService
{
    public Task<VehicleUnlockCatalog?> LoadAsync() => Task.Run(() =>
    {
        if (CatalogService.ResolveDbPath() is not string path) return null;
        using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        db.Open();
        using var query = db.CreateCommand();
        T[]? Read<T>(string id)
        {
            query.Parameters.Clear();
            query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
            query.Parameters.AddWithValue("$id", id);
            return query.ExecuteScalar() is string json ? JsonSerializer.Deserialize<T[]>(json) : null;
        }
        return Read<VehicleCustomisationOption>("VEHICLECUSTOMISATION") is { } options
            && Read<TitleUnlockRule>("TITLEUNLOCKS") is { } titles ? new VehicleUnlockCatalog(options, titles) : null;
    });

    public Task<VehicleUnlockAccount?> ReadAccountAsync(string? path) => Task.Run(async () =>
    {
        if (path is null) return null;
        // Read the file afresh, not the account editor's potentially staged session.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var root = JObject.Parse((await reader.ReadToEndAsync()).TrimEnd('\0'));
        var account = root["B89"];
        if (account is null) return null;
        HashSet<string> Read(params string[] fields) => fields.SelectMany(f => account[f] as JArray ?? [])
            .Select(t => CatalogService.NormalizeId(t.Value<string>() ?? "")).Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        return new VehicleUnlockAccount(Read("B1h", "fyX", "d4U"), Read("Bgb"));
    });
}

internal sealed record VehicleUnlockAccount(HashSet<string> Rewards, HashSet<string> Titles);
