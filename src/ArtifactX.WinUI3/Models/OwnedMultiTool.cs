namespace ArtifactX.WinUI3.Models;

/// <summary>One owned Multi-Tool (vLc.6f=.SuJ[Index]). Name is the strip label: the player's name, or until the game's
/// own one is worked out "Multi-Tool N". GameName is the name the game gives an unnamed tool (MultiToolKinds.GameNameAsync),
/// shown as-is in the name box. IsGameActive: the tool in hand (MultiToolService.ReadTools).</summary>
public sealed record OwnedMultiTool(int Index, string Name, bool IsGameActive, bool IsUnnamed = false, string? GameName = null)
{
    public override string ToString() => Name;
}

/// <summary>A Multi-Tool Type choice: the model scene the save stores and the name shown.</summary>
public sealed record MultiToolTypeOption(string DisplayName, string ScenePath)
{
    public override string ToString() => DisplayName;
}
