using ArtifactX.Core.Procedural;
using System.Text.Json;

namespace ArtifactX.Core.Universe;

public sealed record MultiToolModelInventoryComponent(string SizeTag, bool HasDesiredTechnologies, bool Cool);
public sealed record MultiToolModelNode(string Name, bool Selectable, bool Unsupported,
    MultiToolModelInventoryComponent[] InventoryComponents, MultiToolModelNode[] Children);
public sealed record MultiToolModelTables(string ModelPath, string DescriptorJson, MultiToolModelNode Root);

/// <summary>Resolves ordinary main-model inventory facts from the selected scene branch.</summary>
public static class MultiToolModelGeneration
{
    /// <summary>
    /// Applies descriptor selection before walking scene inventory components. Supports
    /// unambiguous ordinary Pistol/Rifle tags only; references, cool variants and unknown
    /// selected component metadata are unsupported. Four reference tool seeds resolve
    /// their actual kind/size and retain all complete inventory/stat reference checks.
    /// </summary>
    public static ResolvedStandardMultiTool? Resolve(ulong modelSeed, MultiToolModelTables? tables)
    {
        if (tables is null || tables.Root is null
            || !string.Equals(tables.ModelPath, SystemMultiToolGenerator.MainModel, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(tables.DescriptorJson)) return null;
        IReadOnlyList<PartGroup> tree;
        try { tree = ShipParts.ParseTree(tables.DescriptorJson); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { return null; }
        bool unresolvedReference = false;
        string descriptor = ShipParts.DescriptorFor(tables.ModelPath);
        IReadOnlyList<PartGroup>? Load(string path)
        {
            if (string.Equals(path, descriptor, StringComparison.OrdinalIgnoreCase)) return tree;
            unresolvedReference = true;
            return null;
        }
        var selected = ShipParts.Generate(tables.ModelPath, modelSeed, Load)
            .Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (unresolvedReference || !selected.Contains("_MULTITOOL_NORMAL")) return null;
        var components = new List<MultiToolModelInventoryComponent>();
        bool Walk(MultiToolModelNode node, int depth)
        {
            if (node.Selectable && !selected.Contains(node.Name)) return true;
            if (depth > 32 || string.IsNullOrWhiteSpace(node.Name) || node.Unsupported
                || node.InventoryComponents is null || node.Children is null) return false;
            components.AddRange(node.InventoryComponents);
            return node.Children.All(child => child is not null && Walk(child, depth + 1));
        }
        if (!Walk(tables.Root, 0) || components.Count == 0
            || components.Any(c => c is null || c.Cool || string.IsNullOrWhiteSpace(c.SizeTag))) return null;
        var tags = components.Select(c => c.SizeTag).Where(t => t != "WEAPON_DEFAULT")
            .Distinct(StringComparer.Ordinal).ToArray();
        if (tags.Length != 1) return null;
        string? kind = tags[0] switch { "WEAPON_SMALL" => "Pistol", "WEAPON_MEDIUM" or "WEAPON_LARGE" => "Rifle", _ => null };
        return kind is null ? null : new ResolvedStandardMultiTool(modelSeed, tables.ModelPath, kind, tags[0],
            components.Any(c => c.HasDesiredTechnologies));
    }
}
