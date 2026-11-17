using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Edits this save slot's fishing-record checklist (NmsFishingPaths, "vLc/6f=/bTf"): three PARALLEL
/// fixed 256-slot arrays (ProductList/ProductCountList/LargestCatchList). A caught species is present
/// (non-"^") in ProductList. Every edit keeps the three arrays index-aligned and exactly
/// <see cref="NmsFishingPaths.SlotCount"/> long, matching the shape every real save uses (see
/// NmsFishingPaths' doc comment), and stages the whole arrays through SaveSessionManager.
/// Shared by the Fishing Records page and the Catalog's Fish wonders so both write the same shape.
/// </summary>
public sealed class FishingRecordEditor
{
    private readonly List<string> _productIds;
    private readonly List<uint> _productCounts;
    private readonly List<float> _largestCatches;
    private readonly HashSet<string> _caughtIds;

    private FishingRecordEditor(List<string> productIds, List<uint> productCounts, List<float> largestCatches)
    {
        _productIds = productIds;
        _productCounts = productCounts;
        _largestCatches = largestCatches;
        _caughtIds = new HashSet<string>(
            productIds.Where(id => id.Length > 0 && id != "^").Select(CatalogService.NormalizeId), StringComparer.Ordinal);
    }

    /// <summary>Reads all three arrays fresh from the active session, defaulting to 256 empty slots
    /// if this save has never fished at all (the field may not exist yet in that case).</summary>
    public static FishingRecordEditor Load()
    {
        var ids = (SaveSessionManager.GetValue(NmsFishingPaths.ProductListPath) as JArray)?.Select(t => t.Value<string>() ?? "^").ToList()
            ?? Enumerable.Repeat("^", NmsFishingPaths.SlotCount).ToList();
        var counts = (SaveSessionManager.GetValue(NmsFishingPaths.ProductCountListPath) as JArray)?.Select(t => t.Value<uint>()).ToList()
            ?? Enumerable.Repeat(0u, NmsFishingPaths.SlotCount).ToList();
        var largest = (SaveSessionManager.GetValue(NmsFishingPaths.LargestCatchListPath) as JArray)?.Select(t => t.Value<float>()).ToList()
            ?? Enumerable.Repeat(0f, NmsFishingPaths.SlotCount).ToList();
        while (ids.Count < NmsFishingPaths.SlotCount) ids.Add("^");
        while (counts.Count < NmsFishingPaths.SlotCount) counts.Add(0u);
        while (largest.Count < NmsFishingPaths.SlotCount) largest.Add(0f);
        return new FishingRecordEditor(ids, counts, largest);
    }

    public bool IsCaught(string gameId) => _caughtIds.Contains(CatalogService.NormalizeId(gameId));

    /// <summary>
    /// Marking caught fills the first open ("^") slot with a plausible-but-not-authentic count/size
    /// (1 catch, 0.0 largest); marking uncaught removes that slot and re-appends an empty one at the
    /// tail, keeping every other entry's index (and the game's own observed "packed from index 0, no
    /// gaps" shape) intact. Returns false when nothing changed (or all 256 slots are already full).
    /// </summary>
    public bool SetCaught(string gameId, bool caught)
    {
        string id = CatalogService.NormalizeId(gameId);
        if (caught)
        {
            if (_caughtIds.Contains(id)) return false;
            int emptyIndex = _productIds.FindIndex(p => p.Length == 0 || p == "^");
            if (emptyIndex < 0) return false; // all 256 slots already full - don't corrupt the array
            _caughtIds.Add(id);
            _productIds[emptyIndex] = "^" + id;
            _productCounts[emptyIndex] = 1;
            _largestCatches[emptyIndex] = 0f;
            return true;
        }

        if (!_caughtIds.Remove(id)) return false;
        int idx = _productIds.FindIndex(p => CatalogService.NormalizeId(p) == id);
        if (idx < 0) return false;
        _productIds.RemoveAt(idx);
        _productCounts.RemoveAt(idx);
        _largestCatches.RemoveAt(idx);
        _productIds.Add("^");
        _productCounts.Add(0u);
        _largestCatches.Add(0f);
        return true;
    }

    /// <summary>Stages all three arrays together; the game reads them index by index.</summary>
    public void Stage()
    {
        SaveSessionManager.StageEdit(new JArray(_productIds), NmsFishingPaths.ProductListPath);
        SaveSessionManager.StageEdit(new JArray(_productCounts), NmsFishingPaths.ProductCountListPath);
        SaveSessionManager.StageEdit(new JArray(_largestCatches), NmsFishingPaths.LargestCatchListPath);
    }

    /// <summary>Everything a fishing-record edit stages lives under this container.</summary>
    public static bool HasStagedEdits => SaveSessionManager.HasStagedEditsUnder(NmsFishingPaths.RecordContainerPath);

    public static void RevertEdits() => SaveSessionManager.RevertEditsUnder(NmsFishingPaths.RecordContainerPath);
}
