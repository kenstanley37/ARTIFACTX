namespace ArtifactX.WinUI3.Models;

/// <summary>One owned starship (vLc.6f=.@Cs[Index]). Name is the strip label: the player's name, else the one the game
/// makes up for an unnamed ship (GameName, StarshipService.ShipNameAsync), else "Starship N". IsGameActive: the ship
/// being flown (aBE). IsCorvette: a corvette model or one with a build of its own; BuildParts counts that build's parts
/// (ShipBaseService.BuildsBySlot).</summary>
public sealed record OwnedShip(int Index, string Name, bool IsGameActive, bool IsCorvette, int BuildParts,
    bool IsUnnamed = false, string? GameName = null)
{
    public override string ToString() => Name;
}
