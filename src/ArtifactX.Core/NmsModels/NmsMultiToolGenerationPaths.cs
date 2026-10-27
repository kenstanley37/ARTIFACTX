using System.Globalization;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>Generation inputs from exactly the save's selected Main or Season context.</summary>
public sealed record SavedMultiToolGenerationState(string ActiveContext, ulong ReloadUniverseAddress,
    MultiToolGenerationRuntime Runtime);

/// <summary>
/// Read-only generation inputs from an already selected GcPlayerStateData snapshot.
/// Build 180836 loads ProgressionLevel (raw +0x866d4) into player +0x1a9d0,
/// read by technology selection as manager +0x26310. Five native load cases matched.
/// ProgressionLevel maps to DtI in MBINCompiler's mapping; read-only Slot8 inspection
/// on 2026-10-25 found integer 1 and 156 KnownTech entries at the established 4kj key.
/// Callers select the active normal/expedition state before calling; no save is edited.
/// </summary>
public static class NmsMultiToolGenerationPaths
{
    /// <summary>Copies only saved generation inputs, retaining no mutable full-save subtree.</summary>
    public static JObject? CopySavedInputs(JObject? saveRoot)
    {
        string? context = saveRoot?[NmsSaveContext.ActiveContextKey]?.Type == JTokenType.String
            ? saveRoot[NmsSaveContext.ActiveContextKey]!.Value<string>() : null;
        string? rootKey = context switch { "Main" => NmsSaveContext.MainRootKey,
            NmsSaveContext.ExpeditionContextValue => NmsSaveContext.ExpeditionRootKey, _ => null };
        if (rootKey is null || saveRoot![rootKey] is not JObject universe || universe["6f="] is not JObject player) return null;
        var selected = new JObject();
        foreach (string key in new[] { "yhJ", ProgressionLevelKey, "4kj", FoodUnitItemKey })
            if (player[key] is { } value) selected[key] = value.DeepClone();
        var season = new JObject();
        if (saveRoot["<h0"] is JObject common && common["Rol"] is JObject sourceSeason
            && sourceSeason["EqZ"] is { } flag) season["EqZ"] = flag.DeepClone();
        return new JObject { [NmsSaveContext.ActiveContextKey] = context,
            [rootKey] = new JObject { ["6f="] = selected },
            ["<h0"] = new JObject { ["Rol"] = season } };
    }

    public const string ProgressionLevelKey = "DtI";
    public const string FoodUnitItemKey = "nsH";
    public static readonly string[] ProgressionLevelPath = { "vLc", "6f=", ProgressionLevelKey };

    /// <summary>
    /// Resolves the legacy-station rule for an ordinary load using saved SeasonData.
    /// CommonStateData/SeasonData is &lt;h0/Rol; native load 0x1404961E9 and
    /// 0x140496823 pass common+0x50 to 0x1405DE8B0. Field EqZ occupies named
    /// schema index 128, between Sks (AlwaysUseSeasonalStationTradeData) and ;wI
    /// (UseLegacyStationsRadius), matching read-only Slot8 field order on 2026-10-26.
    /// Native named decoding confirms UseLegacyStationsNearStartUA at +0x6B92.
    /// A false flag proves the early return regardless of season activity; true
    /// remains unknown without address/distance checks. Live/network season-data
    /// replacement is outside this saved-reload scenario.
    /// </summary>
    public static bool? ReadSavedLegacyStationRule(JObject? saveRoot, StationEquipmentTables tables)
    {
        bool? nearStart = saveRoot?["<h0"] is JObject common && common["Rol"] is JObject season
            && season["EqZ"] is { Type: JTokenType.Boolean } flag ? flag.Value<bool>() : null;
        return StationRootSelection.ResolveLegacy(tables, null, nearStart);
    }

    /// <summary>
    /// Builds a request for an ordinary reload of the selected saved context, using
    /// generated system attributes and the supported saved seasonal early-return rule.
    /// This is a reload scenario, not detection of the running game's current stock.
    /// </summary>
    public static StationMultiToolGenerationRequest? ReadSavedStationRequest(JObject? saveRoot,
        StationEquipmentTables tables)
    {
        var saved = ReadActiveSavedState(saveRoot, 0);
        if (saved is null) return null;
        ulong address = saved.ReloadUniverseAddress & ~(0xFUL << 52);
        ulong portal = ((address >> 40) & 0xFFF) << 32 | (address & 0xFFFFFFFF);
        var attributes = SystemGenerator.Attributes(portal, (int)((address >> 32) & 255));
        if (attributes.Uncharted || attributes.Abandoned
            || !attributes.Pirate && (attributes.Wealth is < 1 or > 3
                || ReadSavedLegacyStationRule(saveRoot, tables) is not false)) return null;
        var reload = new MultiToolReloadContext(address, saved.ReloadUniverseAddress,
            attributes.PlanetCount + attributes.PrimePlanetCount);
        return !reload.IsValid ? null : new StationMultiToolGenerationRequest(reload,
            saved.Runtime with { NativeWealth = attributes.Pirate ? 3 : attributes.Wealth - 1 },
            true, false, attributes.Pirate, attributes.Pirate ? null : false);
    }

    /// <summary>
    /// Selects the saved active player state using XTp, without falling back between
    /// Main and Season. A populated inactive expedition does not establish activity.
    /// This resolves snapshot ownership only; seasonal station-scene rules and live
    /// reload state must still be established separately.
    /// High-grade module rolls (0x140ec4602) take the override chance only when the
    /// active game mode (image+0x6E8D714) is Unspecified or Seasonal. Save load
    /// 0x1404967F0..0x1404968B4 switches to Seasonal only for active season data, so
    /// the Main context establishes the standard roll; Season remains unestablished.
    /// </summary>
    public static SavedMultiToolGenerationState? ReadActiveSavedState(JObject? saveRoot, int nativeWealth)
    {
        if (saveRoot?[NmsSaveContext.ActiveContextKey] is not { Type: JTokenType.String } token) return null;
        string? context = token.Value<string>();
        string? rootKey = context switch
        {
            "Main" => NmsSaveContext.MainRootKey,
            NmsSaveContext.ExpeditionContextValue => NmsSaveContext.ExpeditionRootKey,
            _ => null,
        };
        if (rootKey is null || saveRoot![rootKey] is not JObject universe
            || universe["6f="] is not JObject player
            || ReadSavedReloadAddress(player) is not { } address
            || ReadStandardGameState(player, nativeWealth) is not { } runtime) return null;
        return new SavedMultiToolGenerationState(context!, address,
            runtime with { HasVerifiedStandardIllegalModuleRoll = context == "Main" });
    }

    /// <summary>
    /// Reads the full address for a proposed ordinary reload of this saved player state.
    /// Native load 0x140573CAA copies raw UniverseAddress (+0x86658) to player +0x860;
    /// preparation 0x1413B924E packs it via 0x140583A90 before building the pool.
    /// Unlike ReadSystemAddress, this preserves PlanetIndex and requires every component.
    /// It does not identify the pool already loaded in a running session. Network/local
    /// load-address overrides and later address changes are outside this method's scope.
    /// </summary>
    public static ulong? ReadSavedReloadAddress(JObject? playerState)
    {
        if (playerState?[NmsSpaceStationPaths.CurrentUniverseAddressPath[^1]] is not JObject address
            || address["oZw"] is not JObject coordinates
            || Integer(address["Iis"], 0, 255) is not { } galaxy
            || Integer(coordinates["dZj"], -2047, 2047) is not { } x
            || Integer(coordinates["IyE"], -127, 127) is not { } y
            || Integer(coordinates["uXE"], -2047, 2047) is not { } z
            || Integer(coordinates["vby"], 0, 4095) is not { } system
            || Integer(coordinates["jsv"], 0, 15) is not { } body) return null;
        return NmsSpaceStationPaths.PackAddress(galaxy, x, y, z, system, body);
    }

    private static int? Integer(JToken? token, int min, int max) =>
        token is { Type: JTokenType.Integer }
        && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
        && value >= min && value <= max ? value : null;

    /// <summary>
    /// Resolves the extra-stat guard from saved FoodUnitItem (nsH). Native load
    /// 0x140573AF9 copies raw +0x83C90 to player +0x18820; stat adjustment
    /// 0x14057A9C7 looks up that ID for an additional bonus. Empty means absent
    /// (the save encodes an empty game ID as "^", also observed in Slot8);
    /// Nonempty IDs retain the guard and the product identity; display generation can
    /// resolve the catalog's FoodBonusStat before deciding whether its effects matter.
    /// Field mapping and read-only Slot8 snapshot checked 2026-10-26. A saved
    /// snapshot does not establish the current consumable state of a running game.
    /// </summary>
    public static MultiToolGenerationRuntime? ReadStandardGameState(JObject? playerState, int nativeWealth)
    {
        if (playerState?[FoodUnitItemKey] is not { Type: JTokenType.String } token
            || token.Value<string>() is not { } id || id.Any(char.IsWhiteSpace)) return null;
        if (id.StartsWith('^')) id = id[1..];
        return ReadStandardGameState(playerState, nativeWealth, id.Length != 0)
            is { } runtime ? runtime with { ActiveFoodUnitItemId = id.Length == 0 ? null : id } : null;
    }

    public static MultiToolGenerationRuntime? ReadStandardGameState(JObject? playerState, int nativeWealth,
        bool hasActiveSpecialTechnology)
    {
        if (playerState is null || nativeWealth is < 0 or > 3 || playerState[ProgressionLevelKey] is not { Type: JTokenType.Integer } levelToken
            || !int.TryParse(levelToken.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
            || level < 0 || playerState[NmsCataloguePaths.KnownTechnologyArrayPath[^1]] is not JArray known) return null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in known)
        {
            if (token.Type != JTokenType.String || token.Value<string>() is not { } id) return null;
            if (id.StartsWith('^')) id = id[1..];
            if (id.Length == 0 || id.Any(char.IsWhiteSpace)) return null;
            ids.Add(id);
        }
        return MultiToolGenerationRuntime.ForStandardGameState(nativeWealth, level, ids, hasActiveSpecialTechnology);
    }
}
