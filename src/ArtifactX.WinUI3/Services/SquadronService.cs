using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The four Squadron pilot slots (vLc/6f=/S5O - see NmsSquadronPaths for the confirmed field map): reading a pilot
/// for the Squadron page and staging its edits.
/// </summary>
public static class SquadronService
{
    public static int SlotCount() => (SaveSessionManager.GetValue(NmsSquadronPaths.SquadronArrayPath) as JArray)?.Count ?? 0;

    public static bool IsUnlocked(int slot) => SaveSessionManager.GetValue(NmsSquadronPaths.UnlockedPath(slot))?.Value<bool>() ?? true;
    public static string NpcScene(int slot) => Text(NmsSquadronPaths.NpcFilenamePath(slot));
    public static string NpcSeed(int slot) => Text(NmsSquadronPaths.NpcSeedPath(slot));
    public static string ShipScene(int slot) => Text(NmsSquadronPaths.ShipFilenamePath(slot));
    public static string ShipSeed(int slot) => Text(NmsSquadronPaths.ShipSeedPath(slot));
    /// <summary>The traits seed (=bJ: a plain ulong, not a GcSeed pair - see NmsSquadronPaths).</summary>
    public static string TraitsSeed(int slot) => SaveSessionManager.GetValue(NmsSquadronPaths.TraitsSeedPath(slot))?.ToString() ?? "";
    public static double Rank(int slot) => SaveSessionManager.GetValue(NmsSquadronPaths.PilotRankPath(slot))?.Value<double>() ?? 0;

    public static void SetUnlocked(int slot, bool unlocked) => SaveSessionManager.StageEdit(unlocked, NmsSquadronPaths.UnlockedPath(slot));
    public static void SetRank(int slot, int rank) => SaveSessionManager.StageEdit(rank, NmsSquadronPaths.PilotRankPath(slot));

    /// <summary>Stages a pilot race (its NPC scene) when it changed. True when staged.</summary>
    public static bool SetNpcScene(int slot, string scene) => SetText(NmsSquadronPaths.NpcFilenamePath(slot), scene);

    /// <summary>Stages a ship model when it changed. True when staged.</summary>
    public static bool SetShipScene(int slot, string scene) => SetText(NmsSquadronPaths.ShipFilenamePath(slot), scene);

    public static bool HasEdits(int slot) => SaveSessionManager.HasStagedEditsUnder(NmsSquadronPaths.PilotPath(slot));
    public static void RevertEdits(int slot) => SaveSessionManager.RevertEditsUnder(NmsSquadronPaths.PilotPath(slot));

    private static string Text(string[] path) => SaveSessionManager.GetValue(path)?.Value<string>() ?? "";

    private static bool SetText(string[] path, string value)
    {
        if (string.Equals(Text(path), value, System.StringComparison.OrdinalIgnoreCase)) return false;
        SaveSessionManager.StageEdit(value, path);
        return true;
    }
}
