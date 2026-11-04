namespace ArtifactX.Core.Unlocks;

/// <summary>A product's own game type (GcProductData.Type, GcProductCategory's
/// enum name: BuildingPart, CustomisationPart, CreatureEgg...). ShipBuilderPart
/// marks rows of the modular-customisation table, the Ship Designer's parts.
/// Build 184271, inspected 2026-11-03: every one of the 1,215 products with no
/// Catalogue group (WikiCategory NotEnabled) has a type.</summary>
public sealed record ProductTypeEntry(string ItemId, string Type, bool ShipBuilderPart);
