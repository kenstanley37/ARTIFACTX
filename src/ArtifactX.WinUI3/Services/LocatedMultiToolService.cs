using ArtifactX.Core.Universe;
using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Loads catalog inputs and composes a fully resolved cabinet item off the UI thread.
/// Resolves standard model components from the catalog by default. Callers must supply
/// verified site/reload selection and runtime generation context. Planet Details must not replace missing inputs with
/// assumed values or expose candidate pools as located equipment.
/// </summary>
public static class LocatedMultiToolService
{
    /// <summary>Supported stock after an ordinary reload of the supplied disk-save snapshot.</summary>
    public static async Task<IReadOnlyList<LocatedMultiTool>> GenerateSavedReloadStationAsync(
        ulong targetSystem, JObject? savedInputs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (savedInputs is null) return Array.Empty<LocatedMultiTool>();
        return await Task.Run(async () =>
        {
            var stamp = GameDataService.Info?.BuiltAtUtc;
            if (stamp is null || GameDataService.IsBuilding) return Array.Empty<LocatedMultiTool>();
            var tables = await CatalogService.GetStationEquipmentTablesAsync().WaitAsync(cancellationToken);
            if (tables is null || GameDataService.IsBuilding || GameDataService.Info?.BuiltAtUtc != stamp)
                return Array.Empty<LocatedMultiTool>();
            var request = NmsMultiToolGenerationPaths.ReadSavedStationRequest(savedInputs, tables);
            if (request is null || request.Reload.SystemAddress != targetSystem) return Array.Empty<LocatedMultiTool>();
            if (MultiToolPreviewRuntimeService.ReadOverheatDuration() is { } duration)
                request = request with { Runtime = request.Runtime with { PreviewOverheatDuration = duration } };
            request = request with { Runtime = request.Runtime with
            {
                HasVerifiedStandardIllegalModuleRoll = request.Runtime.HasVerifiedStandardIllegalModuleRoll
                    || MultiToolPreviewRuntimeService.HasStandardIllegalModuleRoll()
            } };
            var result = await GenerateStationAsync(request, cancellationToken);
            return !GameDataService.IsBuilding && GameDataService.Info?.BuiltAtUtc == stamp
                ? result : Array.Empty<LocatedMultiTool>();
        }, cancellationToken);
    }

    /// <summary>Generates supported station records for an explicitly established reload/runtime scenario.</summary>
    public static async Task<IReadOnlyList<LocatedMultiTool>> GenerateStationAsync(
        StationMultiToolGenerationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Runtime.KnownTechnologies is null || GameDataService.IsBuilding) return Array.Empty<LocatedMultiTool>();
        var snapshot = request with { Runtime = request.Runtime with
            { KnownTechnologies = new HashSet<string>(request.Runtime.KnownTechnologies, StringComparer.Ordinal) } };
        try
        {
            return await Task.Run(async () =>
            {
                var stamp = GameDataService.Info?.BuiltAtUtc;
                if (stamp is null || GameDataService.IsBuilding) return (IReadOnlyList<LocatedMultiTool>)Array.Empty<LocatedMultiTool>();
                var stations = CatalogService.GetStationEquipmentTablesAsync();
                var inventory = CatalogService.GetInventoryGenerationTablesAsync();
                var technology = CatalogService.GetMultiToolTechnologyTablesAsync();
                var models = CatalogService.GetMultiToolModelTablesAsync();
                var words = CatalogService.GetProceduralNameWordsAsync();
                await Task.WhenAll(stations, inventory, technology, models, words).WaitAsync(cancellationToken);
                if (GameDataService.IsBuilding || GameDataService.Info?.BuiltAtUtc != stamp
                    || stations.Result is not { } stationTables || inventory.Result is not { } inventoryTables
                    || technology.Result is not { } technologyTables || models.Result is not { } modelTables)
                    return Array.Empty<LocatedMultiTool>();
                cancellationToken.ThrowIfCancellationRequested();
                var result = StationMultiToolGeneration.Generate(snapshot, stationTables, inventoryTables,
                    technologyTables, modelTables, key => words.Result.GetValueOrDefault(key));
                cancellationToken.ThrowIfCancellationRequested();
                return !GameDataService.IsBuilding && GameDataService.Info?.BuiltAtUtc == stamp
                    ? result : Array.Empty<LocatedMultiTool>();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            Debug.WriteLine($"[LocatedMultiToolService] Station generation unavailable: {error.Message}");
            return Array.Empty<LocatedMultiTool>();
        }
    }

    /// <summary>Reads progression, known tech and the food-effect guard from the selected saved state.</summary>
    public static Task<LocatedMultiTool?> GenerateFromStandardSaveStateAsync(StationMultiToolContents contents,
        JObject? playerStateSnapshot, int nativeWealth, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = NmsMultiToolGenerationPaths.ReadStandardGameState(playerStateSnapshot, nativeWealth);
        return runtime is null ? Task.FromResult<LocatedMultiTool?>(null) : GenerateAsync(contents, runtime, cancellationToken);
    }

    /// <summary>Reads progression/known tech from an active-state snapshot and uses verified normal cycling.</summary>
    public static Task<LocatedMultiTool?> GenerateFromStandardSaveStateAsync(StationMultiToolContents contents,
        JObject? playerStateSnapshot, int nativeWealth, bool hasActiveSpecialTechnology,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = NmsMultiToolGenerationPaths.ReadStandardGameState(playerStateSnapshot, nativeWealth, hasActiveSpecialTechnology);
        return runtime is null ? Task.FromResult<LocatedMultiTool?>(null) : GenerateAsync(contents, runtime, cancellationToken);
    }

    /// <summary>Resolves selected model components from the catalog before composing the record.</summary>
    public static Task<LocatedMultiTool?> GenerateAsync(StationMultiToolContents contents,
        MultiToolGenerationRuntime runtime, CancellationToken cancellationToken = default)
        => GenerateCoreAsync(contents, null, runtime, cancellationToken);

    public static Task<LocatedMultiTool?> GenerateAsync(StationMultiToolContents contents,
        ResolvedStandardMultiTool model, MultiToolGenerationRuntime runtime,
        CancellationToken cancellationToken = default)
        => GenerateCoreAsync(contents, model, runtime, cancellationToken);

    private static async Task<LocatedMultiTool?> GenerateCoreAsync(StationMultiToolContents contents,
        ResolvedStandardMultiTool? model, MultiToolGenerationRuntime runtime, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (runtime.KnownTechnologies is null || GameDataService.IsBuilding) return null;
        var context = runtime with { KnownTechnologies = new HashSet<string>(runtime.KnownTechnologies, StringComparer.Ordinal) };
        try
        {
            return await Task.Run(async () =>
            {
                var catalogStamp = GameDataService.Info?.BuiltAtUtc;
                if (catalogStamp is null || GameDataService.IsBuilding) return null;
                var inventoryTask = CatalogService.GetInventoryGenerationTablesAsync();
                var technologyTask = CatalogService.GetMultiToolTechnologyTablesAsync();
                var modelTask = model is null ? CatalogService.GetMultiToolModelTablesAsync()
                    : Task.FromResult<MultiToolModelTables?>(null);
                await Task.WhenAll(inventoryTask, technologyTask, modelTask).WaitAsync(cancellationToken);
                if (GameDataService.IsBuilding || GameDataService.Info?.BuiltAtUtc != catalogStamp) return null;
                if (inventoryTask.Result is not { } inventory || technologyTask.Result is not { } technology) return null;
                cancellationToken.ThrowIfCancellationRequested();
                var resolvedModel = model ?? MultiToolModelGeneration.Resolve(contents.Tool.Seed, modelTask.Result);
                if (resolvedModel is null) return null;
                var result = LocatedMultiToolGeneration.Generate(contents, resolvedModel, context, inventory, technology);
                cancellationToken.ThrowIfCancellationRequested();
                return !GameDataService.IsBuilding && GameDataService.Info?.BuiltAtUtc == catalogStamp ? result : null;
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            Debug.WriteLine($"[LocatedMultiToolService] Exact cabinet generation unavailable: {error.Message}");
            return null;
        }
    }
}
