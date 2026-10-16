namespace ArtifactX.Core.NmsModels;

/// <summary>
/// A save holds two complete player states side by side: the main game
/// ("vLc", BaseContext) and the expedition ("2YS", ExpeditionContext), each
/// with the same "6f=" child layout. "XTp" (ActiveContext) says which one the
/// game is actually playing - "Main" or "Season" (the game's own
/// GcSaveContextQuery names; expeditions are "seasons" internally).
///
/// CONFIRMED 2026-10-09 across 8 real saves: every one read XTp == "Main",
/// including Slot 1, whose 2YS still holds a finished expedition's full
/// state (inventories included). So 2YS existing - or even being populated -
/// does NOT mean an expedition is active; only XTp does.
/// </summary>
public static class NmsSaveContext
{
    public const string ActiveContextKey = "XTp";
    public const string ExpeditionContextValue = "Season";

    public const string MainRootKey = "vLc";
    public const string ExpeditionRootKey = "2YS";
}
