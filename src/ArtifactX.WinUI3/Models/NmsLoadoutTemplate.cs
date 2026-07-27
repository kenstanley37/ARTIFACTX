using System;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.Models;

/// <summary>
/// A saved tech loadout, independent of any specific owned tool - the point is
/// to survive past whichever tool it was originally captured from, so a good
/// build can be re-applied to a brand-new tool later instead of re-buying
/// every piece of tech from scratch across a dozen space stations again.
/// </summary>
public sealed class NmsLoadoutTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Informational only - which tool/ship this was captured from,
    /// shown in the picker so old templates are easier to recognize later.</summary>
    public string? SourceToolName { get; set; }

    /// <summary>Which equipment kind this template belongs to ("MultiTool" or
    /// "Ship") - templates are stored in one shared pool on disk, but Multi-Tool
    /// and Ship Technology use different usage categories (Weapon vs Ship), so
    /// a template captured from one would contain items that don't belong in
    /// the other's grid. Each page filters its own list to this kind. Defaults
    /// to "MultiTool" for templates saved before this field existed - the only
    /// source that could have created them.</summary>
    public string SourceKind { get; set; } = "MultiTool";

    /// <summary>The class (S/A/B/C) the source tool had when this was captured -
    /// null if unknown. Applying a template only changes the target's class if
    /// the user opts in, same as tool-to-tool copy.</summary>
    public string? SourceClass { get; set; }

    public List<NmsLoadoutTechItem> TechItems { get; set; } = new();
    public List<NmsLoadoutPosition> UnlockedPositions { get; set; } = new();
}

public sealed class NmsLoadoutTechItem
{
    public int X { get; set; }
    public int Y { get; set; }
    public string? ItemId { get; set; }
    public int Amount { get; set; }
    public int MaxAmount { get; set; }
    public string? CategoryLabel { get; set; }
    public bool IsFunctional { get; set; } = true;
    public double MalfunctionSeverity { get; set; }
}

public sealed class NmsLoadoutPosition
{
    public int X { get; set; }
    public int Y { get; set; }
}