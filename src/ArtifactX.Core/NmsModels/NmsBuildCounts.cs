using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>One of the player's bases and how many parts are placed in it.</summary>
public sealed record BuildCount(string Type, string Name, int Parts, JObject Base);

/// <summary>
/// How many building parts the player has placed - the game caps them per base and across
/// everything, and only says so when the cap is hit (user, 2026-10-18). Two places hold them:
/// each base in F?0 lists its parts in @ZJ (planet bases, the freighter, claimed space
/// stations - about 260 parts each - space and ship bases), and c5s lists the loose
/// objects placed anywhere else (beacons, signal boosters, save points, garages, decor:
/// 161 on the test save). Other players' bases and stations the save has cached (ExternalPlanetBase, ExternalSpaceStationBase)
/// aren't the player's and don't count. The caps themselves aren't in the game's data
/// files, so only the counts are known.
/// </summary>
public static class NmsBuildCounts
{
    public static readonly string[] LooseObjectsPath = { "vLc", "6f=", "c5s" };

    /// <summary>The player's own bases with their part counts, biggest first.</summary>
    public static List<BuildCount> PerBase(JArray? bases) =>
        (bases ?? new JArray()).OfType<JObject>()
            .Select(b => (Type: b["peI"]?["DPp"]?.Value<string>() ?? "", Base: b))
            .Where(b => b.Type.Length > 0 && !b.Type.StartsWith("External", StringComparison.Ordinal))
            .Select(b => new BuildCount(b.Type, b.Base["NKm"]?.Value<string>() ?? "", Parts(b.Base["@ZJ"] as JArray), b.Base))
            .OrderByDescending(b => b.Parts).ToList();

    /// <summary>A base's parts as the game counts them: all but the base computer (^BASE_FLAG) -
    /// the build menu read 3,031 where the save held 3,032, one more both before and after a
    /// save point was placed, and the base computer is the base's one part that isn't built.</summary>
    private static int Parts(JArray? objects) =>
        objects?.Count(o => o["r<7"]?.Value<string>() != "^BASE_FLAG") ?? 0;

    /// <summary>Everything placed: every base's parts plus the loose objects.</summary>
    public static int Total(JArray? bases, JArray? looseObjects) => PerBase(bases).Sum(b => b.Parts) + (looseObjects?.Count ?? 0);

    /// <summary>A planet base with more parts than this can't be uploaded for other players to
    /// see - the game's build menu says "Base Not Uploadable (3031/3000)", and its count is
    /// @ZJ's less the base computer (First Base on the test save, 2026-10-18). Not the
    /// building cap: the game let parts be placed past it.</summary>
    public const int UploadLimit = 3000;

    /// <summary>"2,400 / 3,000 parts · 600 left to upload" or "3,032 / 3,000 parts · too many to upload".</summary>
    public static string UploadLabel(int parts) => parts > UploadLimit
        ? $"{parts:N0} / {UploadLimit:N0} parts · too many to upload"
        : $"{parts:N0} / {UploadLimit:N0} parts · {UploadLimit - parts:N0} left to upload";

    /// <summary>0 = plenty of room, 1 = close (80% or more), 2 = over the upload limit.</summary>
    public static int UploadLevel(int parts) => parts > UploadLimit ? 2 : parts >= UploadLimit * 8 / 10 ? 1 : 0;

    /// <summary>"Planet bases", "Claimed space stations"... for a base type.</summary>
    public static string TypeLabel(string type) => type switch
    {
        "HomePlanetBase" => "Planet bases",
        "FreighterBase" => "Freighter",
        "PlayerSpaceStationBase" => "Claimed space stations",
        "PlayerSpaceBase" => "Space bases",
        "PlayerShipBase" => "Ship bases",
        _ => type,
    };

    /// <summary>"10,301 in 36 claimed space stations · 1,532 in 6 planet bases · ... · 161 loose objects".</summary>
    public static string Breakdown(JArray? bases, JArray? looseObjects)
    {
        var parts = PerBase(bases).GroupBy(b => b.Type).OrderByDescending(g => g.Sum(b => b.Parts))
            .Select(g => $"{g.Sum(b => b.Parts):N0} in {TypeLabel(g.Key).ToLowerInvariant()} ({g.Count()})").ToList();
        if (looseObjects?.Count is int loose and > 0) parts.Add($"{loose:N0} loose objects (beacons, save points, decor...)");
        return string.Join(" · ", parts);
    }
}
