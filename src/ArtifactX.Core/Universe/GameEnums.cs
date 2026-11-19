namespace ArtifactX.Core.Universe;

// The game's enum values Core needs, named exactly as libMBIN names them (Core doesn't reference libMBIN). They are
// structural, not tunable data, but a game update can insert or reorder entries: GameData.Checks --enum-check compares
// every name and value here with libMBIN, so that shows up as a failing check instead of a silent wrong answer.
// One copy for the whole app; generators keep their short local names pointing here.

/// <summary>GcBiomeType.BiomeEnum.</summary>
public static class BiomeType
{
    public const int Lush = 0, Toxic = 1, Scorched = 2, Radioactive = 3, Frozen = 4, Barren = 5, Dead = 6, Weird = 7,
        Red = 8, Green = 9, Blue = 10, Test = 11, Swamp = 12, Lava = 13, Waterworld = 14, GasGiant = 15, All = 16;
}

/// <summary>GcBiomeSubType.BiomeSubTypeEnum (the entries Core uses).</summary>
public static class BiomeSubType
{
    public const int Structure = 3, Infested = 24, Swamp = 25, Lava = 26;
}

/// <summary>GcBuildingClassification.BuildingClassEnum (the entries the app uses).</summary>
public static class BuildingClass
{
    public const int Shelter = 2, Shop = 5, Portal = 16, DistressSignal = 20, GameStartSpawn = 28,
        WaterDistressSignal = 32, SentinelDistressSignal = 52;
}
