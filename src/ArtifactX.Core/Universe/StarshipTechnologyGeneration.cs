namespace ArtifactX.Core.Universe;

/// <summary>Composes the shared technology selector and placement for a normal ship inventory.
/// Its caller supplies file-ordered candidates filtered by the native ship category, including
/// the Solar-only guard. This does not accept a weapon candidate table as a ship table.
/// Ship budget arithmetic uses nine requested slots per unit (0x1404D83D0), versus five for weapons.
/// Component technology overrides must be resolved separately; unsupported input yields no loadout.</summary>
public static class StarshipTechnologyGeneration
{
    /// <summary>Native selector 0x1404D7FCB admits SOLAR_SAIL only for Sail (8).
    /// Catalog definitions retain source order, including this otherwise skipped row.</summary>
    public static MultiToolTechnologyTables? ForShipClass(int shipClass, MultiToolTechnologyTables tables)
    {
        if (shipClass is 1 or 2 or 3 or 4 or 6) return tables;
        if (shipClass != 8 || tables.Definitions is not { } definitions
            || definitions.Count(d => d.Id == "SOLAR_SAIL") != 1) return null;
        var eligible = tables.TechnologyIds.ToHashSet(StringComparer.Ordinal);
        var ids = definitions.Where(d => eligible.Contains(d.Id) || d.Id == "SOLAR_SAIL")
            .Select(d => d.Id).ToArray();
        return ids.Length == eligible.Count + 1 ? tables with { TechnologyIds = ids } : null;
    }

    public static IReadOnlyList<GeneratedInstalledTechnology>? SelectNormalLoadout(ulong siteKey,
        MultiToolTechnologyTables? shipTechnologyTables, GeneratedMultiToolLayout layout,
        int nativeWealth, int requiredLevel, IReadOnlySet<string> knownTechnologies,
        bool hasTechnologyOverrides)
    {
        if (hasTechnologyOverrides || shipTechnologyTables?.Definitions is not { } definitions
            || !definitions.Any(d => d.BaseStat == 155) // native Ship_PulseDrive root
            || layout.UnlockedSlots.Any(p => p.X < 0 || p.X >= layout.Width || p.Y < 0 || p.Y >= layout.Height))
            return null;
        int available = layout.UnlockedSlots.Distinct().Count();
        var selected = MultiToolTechnologyGeneration.SelectLoadout(siteKey, shipTechnologyTables,
            new(layout.RequestedSlots, nativeWealth, requiredLevel, available, knownTechnologies, SlotsPerBudget: 9));
        if (selected is null) return null;
        var placed = MultiToolInventoryGeneration.PlaceTechnologies(layout, selected.Select(t => t.Id).ToArray());
        return placed?.Select((t, i) => t with { ModuleStats = selected[i].ModuleStats }).ToArray();
    }
}
