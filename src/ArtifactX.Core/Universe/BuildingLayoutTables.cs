namespace ArtifactX.Core.Universe;

/// <summary>
/// What planet building layouts (shops, shelters, observatories... assembled by the game's L-system expander) are
/// generated from, extracted from the player's own game data. Stored in catalog category <c>PlanetGeneration</c>
/// under GameId <c>BUILDINGLAYOUTS</c>; <see cref="BuildingLayoutGenerator"/> expands them.
/// </summary>
/// <param name="LSystems">Every *.LSYSTEM.MBIN the building table names, and their templates, by upper-case game path.</param>
/// <param name="Scenes">Every model those rules place, by upper-case game path.</param>
/// <param name="MinRadius">GcBuildingGlobals.MinRadius: the smallest record radius.</param>
/// <param name="ChanceOfAddingShelter">GcBuildingGlobals.ChanceOfAddingShelter: gates Shelter cluster entries on the side stream.</param>
/// <param name="ClusterLayouts">GcBuildingDefinitionTable.ClusterLayouts by upper-case layout ID.</param>
/// <param name="ClassClusterLayouts">Each building class's upper-case ClusterLayout ID when set.</param>
/// <param name="BuildingLSystems">GcBuildingDefinitionTable.BuildingFiles L-system paths indexed by [race][buildingClass][systemType].</param>
public sealed record BuildingLayoutTables(
    Dictionary<string, LSystemFile> LSystems,
    Dictionary<string, LayoutScene> Scenes,
    float MinRadius,
    float ChanceOfAddingShelter = 0.75f,
    Dictionary<string, BuildingClusterEntry[]>? ClusterLayouts = null,
    Dictionary<int, string>? ClassClusterLayouts = null,
    string[][][]? BuildingLSystems = null);

/// <summary>One entry of a GcBuildingClusterLayout: the member building class, min/max count and probability.</summary>
public sealed record BuildingClusterEntry(int BuildingClass, int Min, int Max, float Probability);

/// <summary>One TkLSystemRulesData file as stored: templates (merged first), global restrictions and rules.</summary>
public sealed record LSystemFile(string[] Templates, LSystemGlobal[] Globals, LSystemRule[] Rules);

/// <summary>A global restriction: how many of a model the whole layout may (or must) hold.</summary>
public sealed record LSystemGlobal(string Model, LSystemRestriction[] Restrictions);

/// <summary>TkLSystemRestrictionData: Type 0 NoMoreThan, 1 AtLeast, 2 AtLeastIfICan.</summary>
public sealed record LSystemRestriction(int Type, int Amount);

/// <summary>TkLSystemRule: what grows from a model's locators. IsBase = RuleType BaseRule.</summary>
public sealed record LSystemRule(string Name, string Model, bool IsBase, LSystemInnerRule[] Rules);

/// <summary>TkLSystemInnerRule: the models one locator type may hold. Merge: 0 Balance, 1 Prioritize, 2 Replace.</summary>
public sealed record LSystemInnerRule(string LocatorType, int Merge, LSystemEntry[] Entries);

/// <summary>TkLSystemLocatorEntry: a model, its weight (percent) and local restrictions.</summary>
public sealed record LSystemEntry(string Model, float Probability, LSystemRestriction[] Restrictions);

/// <summary>
/// A model scene as the expander and the bounds read it. Locators: the candidate locators in scene order (direct
/// children of the scene root, and children of an "Interior_" locator), only those whose name holds some rule's
/// locator type. BoxMin/BoxMax: the box around every MESH node's AABB in the model's own frame, referenced scenes
/// included; null when the model has none. WeaponCrate: the 12-value transform of the CRATE_WEAPON reference relative
/// to the scene root, for scenes that hold one.
/// </summary>
public sealed record LayoutScene(LayoutLocator[] Locators, double[]? BoxMin, double[]? BoxMax, double[]? WeaponCrate = null);

/// <summary>A locator's name and its transform relative to the scene root: 3x3 rotation-scale rows, then the
/// translation (12 values, column-vector convention).</summary>
public sealed record LayoutLocator(string Name, double[] Matrix);
