namespace ArtifactX.Core.Squadron;

/// <summary>
/// What a squadron pilot's stat card needs from the game files (catalog format 110, row SQUADRONFACTS): the
/// per-rank grade ranges (GcAISpaceshipGlobals PlayerSquadronConfig.PilotRankTraitRanges) and the English (US over UK)
/// text of every UI_SQUADRON_FACT_* and UI_SQUADRON_SHIP_FACT* key, with the game's colour tags kept
/// (&lt;TRADEABLE&gt;Good&lt;&gt;).
/// </summary>
public sealed record SquadronFactRules(float[] RangeLow, float[] RangeHigh, Dictionary<string, string> Words)
{
    public IReadOnlyList<(float Low, float High)> Ranges =>
        RangeLow.Zip(RangeHigh, (low, high) => (low, high)).ToList();

    /// <summary>A key's text without colour tags, and the tag that colours it ("TRADEABLE", "COMMODITY"; "" for none).</summary>
    public (string Text, string Tag) Word(string key)
    {
        string text = Words.GetValueOrDefault(key, "");
        string tag = "";
        int open = text.IndexOf('<');
        if (open >= 0 && text.IndexOf('>', open) is int close and > 0 && close > open + 1)
            tag = text[(open + 1)..close];
        return (System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "").Trim(), tag);
    }
}
