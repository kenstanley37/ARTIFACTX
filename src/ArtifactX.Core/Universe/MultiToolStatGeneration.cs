namespace ArtifactX.Core.Universe;

/// <summary>Supported structural combination modes in the native stat evaluator.</summary>
public enum MultiToolStatCombination { Add, Multiply, Maximum }

/// <summary>Equipped technology values for intact generated inventories and default runtime flags.</summary>
public static class MultiToolStatGeneration
{
    /// <summary>
    /// Evaluates the caller's structurally resolved additive, multiplicative or maximum stat mode.
    /// Does not infer a combination mode, intrinsic adjustment or active weapon state.
    /// Checked against 600 native laser/scanner evaluations, including module rolls,
    /// adjacency, charged cells and the laser base-stat filter; another 750 Bolt checks
    /// cover its damage, rate, clip, reload and maximum extra-shot values.
    /// </summary>
    public static float? Evaluate(GeneratedMultiToolInventory inventory, MultiToolTechnologyTables tables,
        int statId, MultiToolStatCombination combination, int baseStatFilter = 0,
        bool hasActiveSpecialTechnology = false)
    {
        if (hasActiveSpecialTechnology || tables.ScannerRules is not { } rules
            || tables.Definitions is not { } definitions || tables.StatParents is not { } parents
            || !parents.ContainsKey(statId) || baseStatFilter < 0
            || combination is not (MultiToolStatCombination.Add or MultiToolStatCombination.Multiply or MultiToolStatCombination.Maximum)
            || !float.IsFinite(rules.SpecialSlotBonus) || rules.SpecialSlotBonus <= 0f
            || rules.MaximumGroupTechnologies < 1) return null;
        bool multiply = combination == MultiToolStatCombination.Multiply;
        bool maximum = combination == MultiToolStatCombination.Maximum;
        float? sameBonus = maximum ? 0f : multiply ? rules.SameTypeMultiplier : rules.SameTypeAdd;
        float? childBonus = maximum ? 0f : multiply ? rules.ChildTypeMultiplier : rules.ChildTypeAdd;
        if (sameBonus is not { } sameCoefficient || childBonus is not { } childCoefficient
            || !float.IsFinite(sameCoefficient) || !float.IsFinite(childCoefficient)) return null;
        var resolved = new List<(GeneratedInstalledTechnology Installed, TechnologySelectionDefinition Definition,
            IReadOnlyList<GeneratedModuleStat> Stats)>();
        var occupied = new HashSet<GeneratedInventoryPosition>();
        foreach (var installed in inventory.Technologies)
        {
            if (!occupied.Add(installed.Position) || !inventory.Layout.UnlockedSlots.Contains(installed.Position)
                || installed.Position.X < 0 || installed.Position.X >= inventory.Layout.Width
                || installed.Position.Y < 0 || installed.Position.Y >= inventory.Layout.Height) return null;
            string key = installed.ModuleStats is null ? installed.Id : installed.Id.Split('#')[0];
            var matches = definitions.Where(d => d.Id == key).ToArray();
            if (matches.Length != 1 || matches[0].Group is null) return null;
            IReadOnlyList<GeneratedModuleStat>? stats = installed.ModuleStats ?? matches[0].StatBonuses;
            if (stats is null || stats.Any(s => !float.IsFinite(s.Value) || !parents.ContainsKey(s.Stat))) return null;
            resolved.Add((installed, matches[0], stats));
        }
        if (resolved.Where(t => t.Definition.Group!.Length > 0).GroupBy(t => t.Definition.Group)
            .Any(g => g.Count() > rules.MaximumGroupTechnologies)) return null;
        float result = multiply ? 1f : 0f;
        foreach (var source in resolved)
        {
            if (baseStatFilter != 0 && source.Definition.BaseStat != baseStatFilter) continue;
            foreach (var stat in source.Stats.Where(s => s.Stat == statId))
            {
                float value = stat.Value;
                if (inventory.SuperchargedSlots.Contains(source.Installed.Position))
                    value = multiply && value < 1f ? value * (1f / rules.SpecialSlotBonus) : value * rules.SpecialSlotBonus;
                if (maximum) { result = Math.Max(result, value); continue; }
                int same = 0, child = 0;
                foreach (var neighbor in resolved)
                {
                    if (Math.Abs(source.Installed.Position.X - neighbor.Installed.Position.X)
                        + Math.Abs(source.Installed.Position.Y - neighbor.Installed.Position.Y) != 1) continue;
                    if (source.Definition.BaseStat != 0 && source.Definition.BaseStat != neighbor.Definition.BaseStat) continue;
                    bool sourceIsChild = source.Stats.Any(a => neighbor.Stats.Any(b => parents[a.Stat] == b.Stat));
                    bool neighborIsChild = neighbor.Stats.Any(a => source.Stats.Any(b => parents[a.Stat] == b.Stat));
                    if (sourceIsChild) child++;
                    if (!sourceIsChild && !neighborIsChild
                        && source.Stats.Any(a => neighbor.Stats.Any(b => parents[a.Stat] == parents[b.Stat]))) same++;
                }
                if (value > (multiply ? 1f : 0f))
                {
                    float withSame = ((value * sameCoefficient) * same) + value;
                    value = withSame + ((value * childCoefficient) * child);
                }
                result = multiply ? value * result : value + result;
            }
        }
        return float.IsFinite(result) ? result : null;
    }
}
