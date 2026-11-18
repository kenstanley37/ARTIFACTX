using System.Reflection;
using ArtifactX.Core.Universe;
using libMBIN.NMS.GameComponents;

/// <summary>Core's copies of game enum values (Core/Universe/GameEnums.cs) against libMBIN: every constant's name must
/// exist in the enum with the same value. Catches a game update that inserts or reorders entries.</summary>
internal static class GameEnumChecks
{
    public static void Run()
    {
        int checkedCount = 0;
        void Compare(Type mirror, Type gameEnum)
        {
            foreach (var field in mirror.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
            {
                int mine = (int)field.GetRawConstantValue()!;
                if (!Enum.TryParse(gameEnum, field.Name, out var value))
                    throw new InvalidOperationException($"{mirror.Name}.{field.Name} isn't in {gameEnum.FullName}.");
                long game = Convert.ToInt64(value);
                if (game != mine)
                    throw new InvalidOperationException($"{mirror.Name}.{field.Name} is {mine}, the game's {gameEnum.Name} says {game}.");
                checkedCount++;
            }
        }
        Compare(typeof(BiomeType), typeof(GcBiomeType.BiomeEnum));
        Compare(typeof(BiomeSubType), typeof(GcBiomeSubType.BiomeSubTypeEnum));
        Compare(typeof(BuildingClass), typeof(GcBuildingClassification.BuildingClassEnum));
        Console.WriteLine($"PASS: {checkedCount} game enum values in Core match libMBIN.");
    }
}
