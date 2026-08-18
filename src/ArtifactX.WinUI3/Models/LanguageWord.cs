namespace ArtifactX.WinUI3.Models;

/// <summary>One row from CatalogService.GetAllLanguageWordsAsync - a single
/// possible alien-vocabulary word (GameId is the raw save-file id, e.g.
/// "TRA_ATTACK"), independent of whether the currently loaded save actually
/// knows it. Race is the display race name ("Gek"/"Vy'keen"/"Korvax"/
/// "Autophage") derived from the id's own prefix at catalog-build time.</summary>
public sealed class LanguageWord
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }
    public required string Race { get; init; }
}
