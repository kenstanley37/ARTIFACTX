using ArtifactX.Core.Universe;

namespace ArtifactX.Maui.Services;

/// <summary>
/// The game's numbers and text lists the generators need (<see cref="GameRules"/>), shipped with the app as
/// Resources/Raw/game-rules.json.gz - exported on a PC with DataCataloger export-mobile-rules, rebuilt each NMS update,
/// never committed. Read once, off the UI thread. Null when the app was built without it or it can't be read; pages
/// then say the rules are missing instead of guessing.
/// </summary>
public static class GameRulesService
{
    public const string FileName = "game-rules.json.gz";

    private static Task<GameRules?>? _load;

    public static Task<GameRules?> GetAsync() => _load ??= Task.Run(LoadAsync);

    private static async Task<GameRules?> LoadAsync()
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(FileName);
            return GameRules.Read(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or IOException or System.Text.Json.JsonException)
        {
            System.Diagnostics.Debug.WriteLine($"[GameRules] {ex.Message}");
            return null;
        }
    }
}
