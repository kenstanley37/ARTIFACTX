namespace ArtifactX.Core.Universe;

/// <summary>Ordered eligible IDs from the technology and procedural-technology tables.</summary>
public sealed record MultiToolTechnologyTables(string[] TechnologyIds, string[] ProceduralIds,
    TechnologySelectionDefinition[]? Definitions = null, float[]? RarityWeights = null,
    Dictionary<int, int>? StatParents = null, ProceduralModuleDefinition[]? ProceduralModules = null,
    int[]? WeightingCurves = null, MultiToolScannerRules? ScannerRules = null,
    MultiToolLaserRules? StandardLaserRules = null, MultiToolProjectileRules? BoltRules = null,
    MultiToolDamageRules? DamageRules = null, int? IllegalModuleHighGradeChance = null);

/// <summary>Selection metadata from one technology, or a procedural entry's resolved template.</summary>
public sealed record TechnologySelectionDefinition(string Id, string TechnologyId, int RarityIndex,
    int RequiredLevel, string RequiredTech, bool PrimaryItem, int BaseStat, int[] StatTypes,
    GeneratedModuleStat[]? StatBonuses = null, string? Group = null);

/// <summary>Explicit native runtime inputs; wealth is Poor=0, Average=1, Wealthy=2, Pirate=3.</summary>
public sealed record TechnologySelectionContext(int RequestedSlots, int NativeWealth, int RequiredLevel,
    int AvailableSlots, IReadOnlySet<string> KnownTechnologies);

/// <summary>A technology's initial random ranking input, before rarity and prerequisite rules.</summary>
public sealed record MultiToolTechnologyCandidate(string Id, bool Procedural, float RandomScore);

/// <summary>Selected installed ID; procedural modules retain their generated stat bonuses.</summary>
public sealed record GeneratedTechnologySelection(string Id, IReadOnlyList<GeneratedModuleStat>? ModuleStats = null);

/// <summary>Candidate construction and supported default technology loadouts for ordinary multi-tools.</summary>
public static class MultiToolTechnologyGeneration
{
    /// <summary>Combines supported regular/module selection and placement, retaining module bonuses.</summary>
    public static IReadOnlyList<GeneratedInstalledTechnology>? SelectAndPlaceLoadout(ulong inventorySeed,
        MultiToolTechnologyTables? tables, GeneratedMultiToolLayout layout, int nativeWealth, int requiredLevel,
        IReadOnlySet<string> knownTechnologies, IReadOnlyCollection<GeneratedInventoryPosition>? brokenPositions = null)
    {
        var broken = brokenPositions?.ToHashSet() ?? [];
        int available = layout.UnlockedSlots.Distinct().Count(p => p.X >= 0 && p.X < layout.Width
            && p.Y >= 0 && p.Y < layout.Height && !broken.Contains(p));
        var context = new TechnologySelectionContext(layout.RequestedSlots, nativeWealth, requiredLevel, available, knownTechnologies);
        var loadout = SelectLoadout(inventorySeed, tables, context);
        if (loadout is null) return null;
        var placed = MultiToolInventoryGeneration.PlaceTechnologies(layout, loadout.Select(t => t.Id).ToArray(), brokenPositions);
        return placed?.Select((t, i) => t with { ModuleStats = loadout[i].ModuleStats }).ToArray();
    }

    /// <summary>Combines exact regular selection with automatic placement; procedural winners yield no inventory.</summary>
    public static IReadOnlyList<GeneratedInstalledTechnology>? SelectAndPlaceRegularLoadout(ulong inventorySeed,
        MultiToolTechnologyTables? tables, GeneratedMultiToolLayout layout, int nativeWealth, int requiredLevel,
        IReadOnlySet<string> knownTechnologies, IReadOnlyCollection<GeneratedInventoryPosition>? brokenPositions = null)
    {
        var broken = brokenPositions?.ToHashSet() ?? [];
        int available = layout.UnlockedSlots.Distinct().Count(p => p.X >= 0 && p.X < layout.Width
            && p.Y >= 0 && p.Y < layout.Height && !broken.Contains(p));
        var context = new TechnologySelectionContext(layout.RequestedSlots, nativeWealth, requiredLevel, available, knownTechnologies);
        var loadout = SelectRegularLoadout(inventorySeed, tables, context);
        return loadout is null ? null : MultiToolInventoryGeneration.PlaceTechnologies(layout, loadout, brokenPositions);
    }

    /// <summary>
    /// Default ordinary-weapon ranking at 0x1404cf569 onward, with no caller overrides.
    /// Returns null if a procedural entry wins: its generated stats must be materialized
    /// before subsequent ranking can be exact. Never substitutes its template as a module.
    /// A successful result is an ordered regular-technology loadout, ready for placement.
    /// </summary>
    public static IReadOnlyList<string>? SelectRegularLoadout(ulong inventorySeed, MultiToolTechnologyTables? tables,
        TechnologySelectionContext context)
        => SelectLoadoutCore(inventorySeed, tables, context, false)?.Select(t => t.Id).ToArray();

    /// <summary>
    /// Default selection including ordinary procedural modules. Special-quality modules,
    /// missing roll metadata and runtime overrides are unsupported and yield no result.
    /// Checked against 400 native ranking cases with resolved module stat records:
    /// 343 supported loadouts matched; 57 stopped at unsupported module types.
    /// </summary>
    public static IReadOnlyList<GeneratedTechnologySelection>? SelectLoadout(ulong inventorySeed,
        MultiToolTechnologyTables? tables, TechnologySelectionContext context)
        => SelectLoadoutCore(inventorySeed, tables, context, true);

    private static IReadOnlyList<GeneratedTechnologySelection>? SelectLoadoutCore(ulong inventorySeed,
        MultiToolTechnologyTables? tables, TechnologySelectionContext context, bool includeProcedural)
    {
        if (tables?.Definitions is not { } definitions || tables.RarityWeights is not { Length: >= 7 } weights
            || tables.StatParents is not { } parents || context.NativeWealth is < 0 or > 3
            || context.RequestedSlots < 0 || context.AvailableSlots < 0
            || weights.Any(w => !float.IsFinite(w) || w < 0f)) return null;
        if (definitions.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != definitions.Length) return null;
        var byId = definitions.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var candidates = Candidates(inventorySeed, tables).ToList();
        if (candidates.Any(c => !byId.TryGetValue(c.Id, out var d) || d.RarityIndex < 0
            || d.RarityIndex >= weights.Length || d.StatTypes.Any(s => !parents.ContainsKey(s)))) return null;
        // Native fixed code multipliers at 0x144b32100, not game-content defaults.
        float wealthFactor = context.NativeWealth switch { 0 => .3f, 1 => .6f, 2 => 1f, _ => .6f };
        int maximum = Math.Clamp((int)((context.RequestedSlots / 5) * wealthFactor + .5f), 1, 10);
        int budget = 1 + (int)SolarLayoutGeneration.RandomFor(inventorySeed).Next((uint)maximum);
        int consumed = 0;
        var installed = new List<GeneratedTechnologySelection>();
        var usedStats = new HashSet<int>();
        while (consumed < budget && installed.Count < context.AvailableSlots)
        {
            int bestIndex = -1;
            float bestScore = -1f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var definition = byId[candidate.Id];
                float score = weights[definition.RarityIndex] * candidate.RandomScore;
                if (definition.RarityIndex == 6) score = 9999999f; // Always
                else if (definition.RarityIndex == 5) score = -1f; // Impossible
                if (definition.RequiredLevel > context.RequiredLevel) score = -1f;
                if (definition.RequiredTech.Length > 0
                    && (!context.KnownTechnologies.Contains(definition.RequiredTech)
                        || (!definition.PrimaryItem && !installed.Any(t => t.Id == definition.RequiredTech)))) score = -1f;
                if (score > 0f)
                {
                    bool missingParent = candidate.Procedural
                        ? !usedStats.Contains(definition.BaseStat)
                        : !definition.StatTypes.Any(s => parents[s] == s)
                            && definition.StatTypes.Any(s => !usedStats.Contains(parents[s]));
                    if (missingParent) score *= .1f;
                }
                if (score > bestScore) { bestScore = score; bestIndex = i; }
            }
            if (bestIndex < 0 || bestScore <= 0f) break;
            var chosen = candidates[bestIndex];
            var selected = byId[chosen.Id];
            IReadOnlyList<GeneratedModuleStat>? moduleStats = null;
            string installedId = selected.TechnologyId;
            if (chosen.Procedural)
            {
                if (!includeProcedural || tables.ProceduralModules is not { } modules
                    || tables.WeightingCurves is not { } curves) return null;
                var matches = modules.Where(m => m.Id == chosen.Id).ToArray();
                if (matches.Length != 1) return null;
                string? generatedId = MultiToolProceduralModuleGeneration.InitialId(chosen.Id, inventorySeed);
                if (generatedId is null) return null;
                uint suffix = SolarLayoutGeneration.RandomFor(inventorySeed).Next(100000);
                moduleStats = MultiToolProceduralModuleGeneration.RollOrdinaryStats(suffix, matches[0], curves,
                    tables.IllegalModuleHighGradeChance);
                if (moduleStats is null || moduleStats.Any(s => !parents.ContainsKey(s.Stat))) return null;
                installedId = generatedId;
            }
            installed.Add(new GeneratedTechnologySelection(installedId, moduleStats));
            usedStats.Add(selected.BaseStat);
            usedStats.UnionWith(moduleStats is null ? selected.StatTypes : moduleStats.Select(s => s.Stat));
            if (selected.RarityIndex != 6) consumed++;
            candidates.RemoveAt(bestIndex);
        }
        return installed;
    }

    /// <summary>
    /// NMS build 180836, 0x1404cefe0: the technology table and procedural table each
    /// start an independent stream from the inventory/site seed. Draw once per eligible
    /// entry in file order. Impossible/Always rarity still consumes its candidate draw.
    /// Later selection must apply rarity, prerequisites, level, overrides and capacity.
    /// </summary>
    public static IReadOnlyList<MultiToolTechnologyCandidate> Candidates(ulong inventorySeed, MultiToolTechnologyTables? tables)
    {
        if (tables is null) return [];
        var result = new List<MultiToolTechnologyCandidate>();
        Add(tables.TechnologyIds, false);
        Add(tables.ProceduralIds, true);
        return result;

        void Add(IEnumerable<string> ids, bool procedural)
        {
            var rng = SolarLayoutGeneration.RandomFor(inventorySeed);
            foreach (string id in ids)
                result.Add(new MultiToolTechnologyCandidate(id, procedural,
                    (float)(rng.NextWord() * 2.3283064370807974e-10)));
        }
    }
}
