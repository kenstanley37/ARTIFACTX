namespace ArtifactX.GameData.Models;

/// <summary>Unselected asset definitions, not generated buildings or located tools.</summary>
public sealed record PlanetShopWeaponSurvey(ShopRuleWeaponReference[] References, string[] UnresolvedPaths,
    string? SourceFingerprint = null);

public sealed record ShopRuleWeaponReference(string Race, string SystemType, string LSystemPath,
    string RuleName, string RuleModelPath, string LocatorType, string ModelPath, float Probability,
    int RestrictionCount, SceneWeaponTerminal Terminal);

public sealed record SceneWeaponTerminal(string ScenePath, string NodePath, string AttachmentPath,
    float[] PositionInEntryModel, bool ReseedAfterRewardSuccess, string? SpecificDataType,
    int? RespawnPeriodInSeconds, bool? UseSentinelWeapon);
