namespace ArtifactX.WinUI3.Helpers.Text;

/// <summary>Capitals for card and band headings that show a name.</summary>
public static class HeadingText
{
    /// <summary>
    /// Capitalises Latin letters only. Game names carry Greek symbols (Sentinel ships: "μ-Flameborn",
    /// "Crimson-σ"), and their capitals look like other letters (Μ reads as "M-FLAMEBORN"), so those stay
    /// as the game writes them: "μ-FLAMEBORN".
    /// </summary>
    public static string Upper(string text) =>
        string.Create(text.Length, text, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
                span[i] = source[i] <= 'ɏ' ? char.ToUpperInvariant(source[i]) : source[i];
        });
}
