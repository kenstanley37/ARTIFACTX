namespace ArtifactX.Core.Universe;

/// <summary>
/// Solar-system layout values and verified station equipment locations extracted from the
/// player's own game data. Stored in catalog category <c>PlanetGeneration</c> under GameId
/// <c>STATIONEQUIPMENT</c>.
/// </summary>
/// <remarks>
/// <see cref="StandardTypeB"/> is null unless the current standard station root is the
/// TypeB scene whose weapon-terminal chains ArtifactX has extracted. This legacy property
/// retains branch A for older callers; use <see cref="StandardTypeBVariants"/> and
/// <see cref="StationSceneSelection"/> to select the actual branch. Pirate shapes use their own descriptor and geometry; other station roots
/// are retained without borrowed cabinet origins.
/// </remarks>
public sealed record StationEquipmentTables(
    SolarLayoutTables SolarLayout,
    StationSceneRoots StationRoots,
    StationEquipmentScene? StandardTypeB,
    string? StandardTypeBRootDescriptor = null,
    StationEquipmentScene[]? StandardTypeBVariants = null,
    bool? ForceLegacySpaceStations = null,
    string? PirateRootDescriptor = null,
    StationEquipmentScene[]? PirateVariants = null);

/// <summary>Solar-generation values and the supported debug BootMode gate for ordinary moon placement.</summary>
public sealed record SolarLayoutTables(
    float[] SolarSystemSize,
    float[] PerPlanetAngleChangeDegrees,
    float[] PerPlanetElevationChangeDegrees,
    float SolarSystemMaximumRadius,
    float SolarSystemMaximumRadiusMassive,
    int GenerateForcedNumberPlanets,
    bool GenerateMaximumSolarSystem,
    bool MassiveSolarSystems,
    float StationSpawnAvoidRadius,
    float[] SpawnPointStationToPlanetInterpRange,
    bool? StandardMoonPlacementEnabled = null,
    ShipPoolSettings? ShipPool = null);

/// <summary>GcSolarGenerationGlobals values the system starship pool and crashed ships depend on: the locator scatter
/// around each planet (whose draws come before the pool), each race's ship class weights (SpaceshipWeightings,
/// CivilianClassWeightings per GcAlienRace) and the chance a Shuttle becomes a Sail (PirateClassShipOverrideProbability,
/// and its pirate-system value). Catalogs before format 50 don't have them.</summary>
public sealed record ShipPoolSettings(int LocatorScatterMinCount, int LocatorScatterMaxCount,
    float LocatorScatterMaxDistanceFromPlanet, float[][] ClassWeightings, float SailChance, float SailChancePirateSystem);

/// <summary>The four station scene resources selected by GcSimulationGlobals.</summary>
public sealed record StationSceneRoots(
    string Standard,
    string Legacy,
    string Pirate,
    string Abandoned);

/// <summary>A station variant whose equipment interaction origins were verified from its scene graph.</summary>
public sealed record StationEquipmentScene(
    string Variant,
    string RootScene,
    StationCabinetDefinition[] Cabinets,
    string? LaunchReference = null,
    string? RootDescriptorId = null);

/// <summary>
/// One actual WeaponTerminal interaction in a station scene. <paramref name="LocalToStationMatrix"/>
/// is a System.Numerics row-vector matrix serialized in M11..M44 order; the translation is also
/// exposed directly in <paramref name="LocalPosition"/> as [x,y,z].
/// </summary>
public sealed record StationCabinetDefinition(
    string Id,
    string ScenePath,
    string NodePath,
    string AttachmentPath,
    string InteractionType,
    bool ReseedAfterRewardSuccess,
    float[] LocalPosition,
    float[] LocalToStationMatrix,
    StationSceneReference[] ReferenceChain,
    string? PoolType = null,
    int? RespawnPeriodInSeconds = null,
    bool? UseSentinelWeapon = null);

/// <summary>
/// One scene-reference hop used to reach an equipment interaction. The local Tk transform
/// and its accumulated station-space M11..M44 matrix are both retained.
/// </summary>
public sealed record StationSceneReference(
    string ScenePath,
    string NodePath,
    string ReferencedScene,
    StationSceneTransform Transform,
    float[] LocalToStationMatrix);

/// <summary>An exact TkTransformData value represented by JSON-safe scalar properties.</summary>
public sealed record StationSceneTransform(
    float TransX,
    float TransY,
    float TransZ,
    float RotX,
    float RotY,
    float RotZ,
    float ScaleX,
    float ScaleY,
    float ScaleZ);
