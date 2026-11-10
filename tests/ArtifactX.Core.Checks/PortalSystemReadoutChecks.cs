using ArtifactX.Core.Universe;

internal static class PortalSystemReadoutChecks
{
    public static void Check()
    {
        Require(!PortalSystemReadout.TryGenerate("invalid", 1, out _, out _), "Invalid address accepted.");
        Require(!PortalSystemReadout.TryGenerate("11FA016452DB", 0, out _, out _), "Invalid galaxy accepted.");
        var first = PortalSystemReadout.Generate("11FA016452DB", 1);
        var otherBody = PortalSystemReadout.Generate("21FA016452DB", 1);
        Require(first.SystemName == otherBody.SystemName, "Body glyph changed system identity.");
        Require(first.Bodies.Count == otherBody.Bodies.Count && first.Bodies.Select(b => b.Seed).SequenceEqual(otherBody.Bodies.Select(b => b.Seed)), "Body glyph changed body generation.");
        Require(first.Bodies.All(b => b.BodyPortalCode[1..] == first.PortalCode[1..]), "Body address points to another system.");
        Require(first.Bodies.Count(b => b.IsDialedBody) == 1 && otherBody.Bodies.Single(b => b.IsDialedBody).BodyIndex == 2, "Dialed body selection is incorrect.");
        Require(first.MultiToolPools.Count == 0, "Missing tables produced a fabricated multi-tool pool.");
        Require(first.Ships.All(s => s.Type != SystemStarshipType.SentinelInterceptor), "Unconfirmed dissonance produced an interceptor.");
        Require(first.Bodies.All(b => !b.SecondaryLine.Contains("Sentinels")), "Sample body annotations leaked into results.");
        Require(first.Ships.Select(s => s.Seed).SequenceEqual(PortalSystemReadout.Generate(first.PortalCode, 1).Ships.Select(s => s.Seed)), "Repeated generation changed ship seeds.");
        Console.WriteLine("PASS: portal readout validation, system identity, body addresses, deterministic seeds and missing-table boundaries.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
