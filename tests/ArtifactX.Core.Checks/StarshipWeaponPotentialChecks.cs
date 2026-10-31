using System.Text.Json;
using ArtifactX.Core.Universe;

internal static class StarshipWeaponPotentialChecks
{
    public static void CheckNative(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        int checks = 0;
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var rules = row.GetProperty("Rules").Deserialize<StarshipWeaponPotentialRules>()!;
            var inputs = row.GetProperty("Inputs").Deserialize<StarshipWeaponPotentialInputs>()!;
            float expected = row.GetProperty("Expected").GetSingle();
            float? actual = StarshipWeaponPotentialGeneration.Evaluate(rules, inputs);
            if (actual is null || BitConverter.SingleToInt32Bits(actual.Value) != BitConverter.SingleToInt32Bits(expected))
                throw new InvalidOperationException($"Weapon potential differs at {checks}: {actual:R}, native {expected:R}");
            if (StarshipWeaponPotentialGeneration.Evaluate(rules, inputs with { Damage = float.NaN }) is not null)
                throw new InvalidOperationException("Non-finite weapon input accepted");
            checks++;
        }
        if (checks == 0) throw new InvalidOperationException("No weapon potential vectors supplied");
        Console.WriteLine($"PASS: {checks} ship weapon potentials match native floats exactly.");
    }
}
