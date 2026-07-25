namespace NMS.Almanac.MultiTool;

/// <summary>
/// One confirmed Multi-Tool "Type" (base model). Originally modeled as a
/// (ScenePath, jl;) pair, matching what NomNom writes together for a Type
/// change - but a real in-game test proved jl; isn't required for the visual
/// swap to work correctly (writing ScenePath alone rendered a fully distinct,
/// correct model with no missing mesh or crash). Dropped here accordingly;
/// only the model path actually matters.
/// </summary>
public sealed record MultiToolTypeInfo(string DisplayName, string ScenePath);