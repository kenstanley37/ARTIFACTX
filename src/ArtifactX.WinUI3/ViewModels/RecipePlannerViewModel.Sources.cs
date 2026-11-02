using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

public sealed partial class RecipePlannerViewModel
{
    private string SupplyLabel(string id) => _supplyChoices.GetValueOrDefault(id) is { } source
        ? source == "stored" ? "Use stored supplies. Gather any shortfall shown in your inventory totals."
            : "Collect from " + source[5..] : "";

    private void ChooseSupply(string id, string source)
    {
        if (_supplyChoices.GetValueOrDefault(id) == source) return;
        _supplyChoices[id] = source;
        _ = RebuildAsync();
    }

    private void RestoreRecipe(string id)
    {
        if (_supplyChoices.Remove(id)) _ = RebuildAsync();
    }

    private void UpdateSupplyOptions(RecipePlannerItemViewModel item, IReadOnlyList<BaseExtractionSite> sites, long owned)
    {
        var desired = new List<(string Label, string Source)>();
        long needed = _target?.Result.Id == item.Id ? (long)Quantity : item.Required;
        if (SaveSessionManager.IsSaveLoaded && owned >= needed && needed > 0)
            desired.Add(("Use stored supplies", "stored"));
        desired.AddRange(sites.Where(s => s.ItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))
            .Select(s => ("Collect from " + s.BaseName, "base:" + s.BaseName)).Distinct());
        if (!item.SupplyOptions.Select(o => o.Label).SequenceEqual(desired.Select(o => o.Label)))
        {
            item.SupplyOptions.Clear();
            foreach (var option in desired)
            {
                string source = option.Source;
                item.SupplyOptions.Add(new(option.Label, option.Label + " for " + item.Name,
                    new RelayCommand(() => ChooseSupply(item.Id, source))));
            }
        }
        item.SupplyOptionsVisibility = desired.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
