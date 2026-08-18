namespace ArtifactX.WinUI3.Models;

/// <summary>One row from CatalogService.GetGuideTopicsAsync - a real in-game
/// Guide topic, GameId/DisplayName/Category all sourced directly from the
/// game's own metadata/reality/wiki.mbin (a GcWiki, extracted into the
/// catalog DB by DataCataloger's "add-guide-topics" command). Category is
/// the resolved section name (e.g. "Survival Basics") a topic's own
/// GcWikiCategory assigns it, or "Uncategorized" for the handful of real
/// topic ids the game itself never assigns to any category.</summary>
public sealed class GuideTopicCatalogRow
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
}
