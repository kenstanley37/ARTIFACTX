namespace ArtifactX.Core.Unlocks;

/// <summary>One expedition reward from the game's own season-rewards table
/// (DefaultReality UnlockableSeasonRewardsTable, GcUnlockableSeasonReward).
/// Build 184271, inspected 2026-11-03: 293 rewards, each with exactly one season
/// id, numbered like the EXPD_BANNERnn banners (1 Pioneers ... 23). Includes
/// rewards without an EXPD_ id (SWARM_HAT, BOBBLE_ASTRO); the owner's account
/// lists them in B1h like the EXPD_ ones. Stage is the season's reward stage,
/// -1 when the table gives none.</summary>
public sealed record ExpeditionReward(string ItemId, int Expedition, int Stage);
