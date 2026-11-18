using System.Text.Json;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;

/// <summary>The installed game's abandoned / empty / pirate chances (catalog row SYSTEMCHANCES) against the 7.14 values
/// SystemGenerator starts with. A difference is not an app bug (the app uses the catalog's); it means the game changed
/// them, so the defaults and any research built on them need a look.</summary>
internal static class SystemChanceChecks
{
    public static void Run(string catalog)
    {
        using var connection = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='SYSTEMCHANCES'";
        if (command.ExecuteScalar() is not string json)
            throw new InvalidOperationException("The catalog has no SYSTEMCHANCES row (built before format 106).");
        var game = JsonSerializer.Deserialize<SystemChanceTables>(json)!;
        var defaults = SystemGenerator.Defaults;
        static bool Same(float[] a, float[] b) => a.Length == b.Length
            && a.Zip(b).All(x => BitConverter.SingleToInt32Bits(x.First) == BitConverter.SingleToInt32Bits(x.Second));
        if (!game.IsValid || !Same(game.Abandoned, defaults.Abandoned) || !Same(game.Empty, defaults.Empty) || !Same(game.Pirate, defaults.Pirate))
            throw new InvalidOperationException($"The game's system chances differ from SystemGenerator.Defaults: {json}");
        Console.WriteLine("PASS: the installed game's abandoned / empty / pirate chances equal SystemGenerator.Defaults.");
    }
}
