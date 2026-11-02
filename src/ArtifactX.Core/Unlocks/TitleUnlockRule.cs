namespace ArtifactX.Core.Unlocks;

/// <summary>A title's declared requirements and associated special rewards.
/// These are eligibility rules, not evidence that the game granted a reward.
/// Inspected in build 184271 on 2026-11-02; the owner's recycling titles are
/// present while several linked Colossus rewards remain absent.</summary>
public sealed record TitleUnlockRule(string TitleId, string Title, string Requirement,
    string CompletedDescription, string StatId, float StatValue, int StatRank,
    string MissionId, string ProductId, string TrophyId, string Interaction,
    int InteractionIndex, string InteractionRace, bool InteractionOnlyMainRaces, string RevealedBy,
    int[] BlockedInSeasons, string[] SpecialIds);
