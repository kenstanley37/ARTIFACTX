namespace ArtifactX.Core.Squadron;

/// <summary>
/// What a squadron pilot's stat card needs from the game files (catalog format 110, row SQUADRONFACTS): the
/// per-rank grade ranges (GcAISpaceshipGlobals PlayerSquadronConfig.PilotRankTraitRanges) and the English (US over UK)
/// text of every UI_SQUADRON_FACT_* and UI_SQUADRON_SHIP_FACT* key, with the game's colour tags kept
/// (&lt;TRADEABLE&gt;Good&lt;&gt;). Format 111 adds the pilot name: <see cref="SceneRaces"/> (each
/// GcPlayerSquadronConfig.RandomPilotNPCResources scene's GcNPCComponentData.Race, raw GcAlienRace value) and, in
/// <see cref="Words"/>, the race title lists (WAR_TITLE_1...) and NPC_NAME_LAYOUT.
/// </summary>
public sealed record SquadronFactRules(float[] RangeLow, float[] RangeHigh, Dictionary<string, string> Words,
    Dictionary<string, int>? SceneRaces = null)
{
    /// <summary>The GcAlienRace raw value of a pilot's NPC scene; null when the scene isn't one of the game's pilot scenes.</summary>
    public int? Race(string scene) =>
        SceneRaces is not null && SceneRaces.TryGetValue(scene.Replace('\\', '/').ToUpperInvariant(), out int race) ? race : null;

    /// <summary>The pilot's full name as the game shows it ("Liquidator Uduoh"); "" without the format 111 data.</summary>
    public string PilotName(ulong nameSeed, string scene)
    {
        if (Race(scene) is not int race || ArtifactX.Core.Universe.NpcNames.Generate(nameSeed, race) is not { } name) return "";
        string layout = Words.GetValueOrDefault("NPC_NAME_LAYOUT", "%TITLE% %NAME%");
        return name.Format(layout, name.TitleKey.Length > 0 ? Word(name.TitleKey).Text : "");
    }

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
