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

    /// <summary>"TechStack" (default) captures only the Technology grid, same
    /// as this feature always worked. "FullBuild" also captures Stats + Seed
    /// below - still deliberately never Cargo, Name, or any equip/active
    /// state (see the Multi-Tool active-badge bug this distinction grew out
    /// of: two tools an identical TECH STACK made them indistinguishable to
    /// ArtifactX's own equipped-tool detection, which is a completely
    /// separate concern from what a template itself should capture). Old
    /// templates saved before this field existed deserialize as "TechStack",
    /// which is exactly what they always were - no migration needed.</summary>
    public string Scope { get; set; } = "TechStack";

    /// <summary>Only meaningful when Scope == "FullBuild" - the source's
    /// whole stat-bonus array (@bB) at save time, one entry per key (e.g.
    /// "^SHIP_DAMAGE") so applying is a plain whole-array rebuild, matching
    /// every page's own SetStatValue convention. Empty for "TechStack"
    /// templates.</summary>
    public List<NmsLoadoutStat> Stats { get; set; } = new();

    /// <summary>Only meaningful when Scope == "FullBuild" - the source's
    /// Model Seed (drives visual appearance/hull look). Deliberately never
    /// the item's Name, or Freighter's Crew Seed (ties to a specific NPC
    /// captain) - a Full Build template changes how the target performs and
    /// looks, not its identity. Null for "TechStack" templates.</summary>
    public string? Seed { get; set; }

    /// <summary>Only meaningful when Scope == "FullBuild" - the source's
    /// Type/model scene path (NTx.93M or equivalent). Added 2026-08-12 after
    /// a real reported bug: applying a Ship Full Build template captured for
    /// a reward ship (Golden Vector) restored its Tech/Stats/Seed but left
    /// the TARGET ship's own existing model completely untouched, since
    /// nothing on this class captured Type at all - Seed only varies the
    /// procedural paint/details WITHIN a model, it doesn't select WHICH
    /// model renders. Null for "TechStack" templates, or for a "FullBuild"
    /// template saved before this field existed (no migration needed - such
    /// a template just won't touch Type when applied, same as it always
    /// silently didn't).</summary>
    public string? TypeScenePath { get; set; }

    public List<NmsLoadoutTechItem> TechItems { get; set; } = new();
    public List<NmsLoadoutPosition> UnlockedPositions { get; set; } = new();

    // ---- Full Build extras, captured since 2026-10-12 so "Add as new" can
    // rebuild the whole thing in an empty slot (NewFromTemplateService). All
    // null on older templates, which then fall back to the skeleton entry's
    // own values.

    /// <summary>Supercharged Technology slots (MMm).</summary>
    public List<NmsLoadoutPosition>? SuperchargedPositions { get; set; }
    /// <summary>Starships only: unlocked Cargo slots (;l5 hl?). Never the cargo itself.</summary>
    public List<NmsLoadoutPosition>? CargoUnlockedPositions { get; set; }
    /// <summary>Paint, colours and parts, as the save's own JSON: a Multi-Tool's
    /// CustomisationData (7DH), or a Starship's CharacterCustomisationData slot
    /// (l:j entry). Checked by TemplateSharingService on import.</summary>
    public string? Customisation { get; set; }
    /// <summary>Multi-Tools only (GcMultitoolData): pistol vs rifle size (OGV) -
    /// varies even within one model, so it can't be worked out from Type.</summary>
    public bool? IsLarge { get; set; }
    /// <summary>Multi-Tools only: selected primary/secondary weapon modes (qVG / jl;).</summary>
    public int? PrimaryMode { get; set; }
    public int? SecondaryMode { get; set; }
    /// <summary>Multi-Tools only: UseLegacyColours (J&lt;o).</summary>
    public bool? UseLegacyColours { get; set; }

    /// <summary>Freighters only: every placed room and furnishing in the
    /// freighter's base (FreighterBase entry of F?0), in the base's own local
    /// space - see FreighterBuildService.</summary>
    public List<NmsLoadoutBaseObject>? InteriorObjects { get; set; }
    /// <summary>Freighters only: the interior's anchor (FreighterBase wMC) and
    /// the freighter's facing (9wg at / fUl up). Always copied together with
    /// the model and seed they were captured with.</summary>
    public double[]? InteriorPosition { get; set; }
    public double[]? FreighterAt { get; set; }
    public double[]? FreighterUp { get; set; }
}

/// <summary>One placed base part (GcPersistentBaseEntry: r&lt;7 id, CVX user
/// data such as colour, wMC position, wJ0 up, aNu at).</summary>
public sealed class NmsLoadoutBaseObject
{
    public string Id { get; set; } = "";
    public long UserData { get; set; }
    public double[] Position { get; set; } = new double[3];
    public double[] Up { get; set; } = new double[3];
    public double[] At { get; set; } = new double[3];
}

public sealed class NmsLoadoutStat
{
    public string Key { get; set; } = "";
    public double Value { get; set; }
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