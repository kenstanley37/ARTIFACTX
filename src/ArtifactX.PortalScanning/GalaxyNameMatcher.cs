namespace ArtifactX.PortalScanning;

/// <summary>Matches a complete OCR line to known galaxy names. Never guesses a default or uses fuzzy matches.</summary>
public sealed class GalaxyNameMatcher(IReadOnlyDictionary<int, string> names)
{
    public int? Match(IEnumerable<string> lines)
    {
        var found = new HashSet<int>();
        foreach (string line in lines)
        {
            string text = Normalize(line);
            foreach (var (number, name) in names)
            {
                string expected = Normalize(name);
                if (text == expected || text == expected + "GALAXY") found.Add(number);
            }
        }
        return found.Count == 1 ? found.Single() : null;
    }

    private static string Normalize(string text) => string.Concat(text.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
