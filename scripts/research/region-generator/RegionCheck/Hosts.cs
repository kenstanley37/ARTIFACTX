using ArtifactX.Core.Universe;

/// <summary>"hosts address...": the generator's planet/moon layout (SystemGenerator.Bodies Hosts) for system addresses
/// in save hex form (planet digit ignored), to compare with the save's planet centres.</summary>
static class Hosts
{
    public static void Show(IEnumerable<string> addresses)
    {
        foreach (string text in addresses)
        {
            ulong address = Convert.ToUInt64(text, 16);
            ulong code = (((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF);
            var bodies = SystemGenerator.Bodies(code, (int)((address >> 32) & 0xFF));
            Console.WriteLine($"{text}: {bodies.Hosts.Length} bodies, hosts [{string.Join(", ", bodies.Hosts)}]");
        }
    }
}
