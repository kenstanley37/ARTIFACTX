namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The species players know for the game's culture names (GcAlienRace): the game translates
/// Traders, Warriors and Explorers into Gek, Vy'keen and Korvax wherever it shows them -
/// confirmed by save edits (see NmsSettlementPaths: Race). Others aren't confirmed, so they
/// keep the game's word.
/// </summary>
public static class RaceNames
{
    public static string Display(string race) => race switch
    {
        "Traders" => "Gek",
        "Warriors" => "Vy'keen",
        "Explorers" => "Korvax",
        _ => race,
    };
}

/// <summary>A race in a dropdown: the save's value, shown as the species name.</summary>
public sealed record RaceChoice(string Value)
{
    public string Label => RaceNames.Display(Value);
    public override string ToString() => Label;
}
