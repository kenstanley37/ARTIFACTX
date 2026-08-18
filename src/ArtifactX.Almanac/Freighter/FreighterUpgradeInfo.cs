namespace ArtifactX.Almanac.Freighter;

/// <summary>Group is one of "Frigate Upgrade", "Construction Module", or
/// "Recolouring" - matches the 3 tabs on the in-game "Capital Ship Research"
/// screen (the 4th tab, base Freighter Technology like Warp Shielding, is a
/// separate GcTechnologyTable system already covered by CataloguePage's own
/// Freighter filter - deliberately not duplicated here).</summary>
public sealed record FreighterUpgradeInfo(string GameId, string DisplayName, string Group);
