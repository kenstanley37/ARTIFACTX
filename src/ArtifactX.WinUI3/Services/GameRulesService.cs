using ArtifactX.Core.Universe;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Game numbers Core generators read from a shared table rather than a parameter on every call (SystemGenerator has
/// 40+ callers): loaded from the catalog at start-up and again after a game data rebuild, so a game update that
/// changes them flows through without a code change. Until loaded, Core uses its documented 7.14 values.
/// </summary>
public static class GameRulesService
{
    public static void Start()
    {
        CatalogService.Reloaded += () => _ = LoadAsync();
        _ = LoadAsync();
    }

    private static async Task LoadAsync()
    {
        if (await CatalogService.GetSystemChanceTablesAsync() is { } chances) SystemGenerator.UseChances(chances);
    }
}
