using ArtifactX.Core.Universe;

internal static class CreatureInfoChecks
{
    /// <summary>A creature's weight and height read like the game's "%0.1fkg / %0.1fm": printf rounds the float's
    /// exact value, so 146.95f (146.9499...) is 146.9 - the case a captured C. Gallegwium showed (2026-11-12).</summary>
    public static void Check()
    {
        var info = new PredictedCreatureInfo(3.15f, 146.95f, 3.3f, 121.5f, 2, 1, 2, false);
        if (info.Size != "146.9kg / 3.2m") throw new InvalidOperationException($"Creature size {info.Size}, expected 146.9kg / 3.2m.");
        if (info.Size2 != "121.5kg / 3.3m") throw new InvalidOperationException($"Creature size 2 {info.Size2}, expected 121.5kg / 3.3m.");
        Console.WriteLine("PASS: creature weight and height read like the game's (printf rounding).");
    }
}
