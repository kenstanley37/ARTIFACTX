namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A choice in a filter or form ComboBox: the words shown and the value it stands for (Fishing's Show / Time of
/// day / Rarity, Finds' Type / Class / Sort).</summary>
public sealed record TextOption(string Label, string Key)
{
    public override string ToString() => Label;
}

/// <summary>A Galaxy choice: its number (1-256) and "Euclid (1)".</summary>
public sealed record GalaxyOption(string Label, int Number)
{
    public override string ToString() => Label;
}
