using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>The lists that follow the game while it plays (LiveItemsService).</summary>
public enum LiveCollection { MultiTools, Ships, Frigates }

/// <summary>Something the game no longer has: sold, scrapped or traded in.</summary>
public sealed record GoneItem(string Name);

/// <summary>
/// New multi-tools, starships and frigates appear while the game plays, without the
/// page reloading (user design 2026-10-16). Each time the game saves the
/// loaded slot (SaveSessionManager.GameSavedLoadedSlot) only these lists are
/// compared, by what each entry is (model and seed), not by slot - the game
/// closes up the gap when something is sold, so the rest move down a slot
/// (seen 2026-10-16: a Voltaic Staff went from slot 6 to slot 1).
/// - something the game has that the loaded save doesn't is new: its page
///   slides its button in with a "New" pill until it's clicked;
/// - something the loaded save has that the game doesn't is gone: the page
///   shows it faded instead of it vanishing under the cursor.
/// The game's list is then copied into the loaded save, so slots line up
/// and a later save from ArtifactX can't bring a sold tool back. A list with
/// unsaved edits is left alone - those edits point at slots, and the player's
/// edits always win. A move between the expedition and the main game is
/// MainWindow's to handle (it reloads the whole save), and opening another
/// save clears everything.
/// Frigates update quietly: no New/Gone notices or animations. Their seed
/// identities preserve the selected ship across roster moves, and changed
/// stats or expedition counts refresh existing cards without replacing them.
/// </summary>
public static class LiveItemsService
{
    private sealed class State(string[] arrayPath, string noun)
    {
        public readonly string[] ArrayPath = arrayPath;
        public readonly string Noun = noun;
        // Keys (Key below): identity plus which copy it is, so two of the same
        // tool - bought twice from one merchant, same seed - are new, counted
        // and clicked separately (seen 2026-10-16).
        public readonly HashSet<string> New = new();
        public readonly HashSet<string> ToAnimate = new();
        public readonly List<GoneItem> Gone = new();
    }

    private static readonly Dictionary<LiveCollection, State> States = new()
    {
        [LiveCollection.MultiTools] = new State(NmsInventoryContainer.MultiToolArrayPath, "multi-tool"),
        [LiveCollection.Ships] = new State(NmsInventoryContainer.ShipArrayPath, "starship"),
        [LiveCollection.Frigates] = new State(NmsFrigatePaths.FrigateArrayPath, "frigate"),
    };

    private static bool _started;

    /// <summary>Raised (UI thread) when a list gained or lost items, or an item
    /// stopped being new.</summary>
    public static event Action<LiveCollection>? Changed;

    public static void Start()
    {
        if (_started) return;
        _started = true;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSaved;
        SaveSessionManager.ActiveSessionChanged += (_, _) => Clear();
    }

    public static bool IsNew(LiveCollection list, int index) =>
        KeyAt(list, index) is { } key && States[list].New.Contains(key);

    public static int NewCount(LiveCollection list) => States[list].New.Count;
    public static IReadOnlyList<GoneItem> Gone(LiveCollection list) => States[list].Gone;

    /// <summary>True once per new item: the first time its page shows it, so the
    /// slide-in plays when the player sees it, not while they're elsewhere.</summary>
    public static bool TakeAnimation(LiveCollection list, int index) =>
        KeyAt(list, index) is { } key && States[list].ToAnimate.Remove(key);

    /// <summary>The player clicked a new item: it's not new any more.</summary>
    public static void Seen(LiveCollection list, int index)
    {
        if (KeyAt(list, index) is { } key && States[list].New.Remove(key)) Changed?.Invoke(list);
    }

    private static string? KeyAt(LiveCollection list, int index) =>
        SaveSessionManager.GetValue(States[list].ArrayPath) is JArray array && index >= 0 && index < array.Count
            ? Keys(array.Select(item => Identity(list, item)).ToList())[index] : null;

    /// <summary>Each slot's key: identity and which copy of it this is
    /// ("...#0", "...#1"), counted in list order - the game keeps that order
    /// when it closes up a gap. Null for an empty slot.</summary>
    private static List<string?> Keys(List<string?> identities)
    {
        var seen = new Dictionary<string, int>();
        return identities.Select(id =>
        {
            if (id is null) return null;
            int copy = seen[id] = seen.GetValueOrDefault(id, -1) + 1;
            return $"{id}#{copy}";
        }).ToList();
    }

    private static void Clear()
    {
        foreach (var (list, state) in States)
        {
            if (state.New.Count == 0 && state.Gone.Count == 0 && state.ToAnimate.Count == 0) continue;
            state.New.Clear();
            state.ToAnimate.Clear();
            state.Gone.Clear();
            Changed?.Invoke(list);
        }
    }

    private static void OnGameSaved(JObject disk)
    {
        // A switch between expedition and main game reloads the whole save (MainWindow).
        if (SaveSessionManager.IsMidExpedition(disk) != SaveSessionManager.IsExpeditionActive) return;

        if (Compare(LiveCollection.MultiTools, disk))
            SaveSessionManager.AdoptFromGame(disk, NmsInventoryContainer.MultiToolActivePath); // the tool in hand
        if (Compare(LiveCollection.Ships, disk))
            SaveSessionManager.AdoptFromGame(disk, NmsInventoryContainer.ActiveShipIndexPath); // the ship being flown

        SaveSessionManager.AdoptFromGame(disk, NmsInventoryContainer.FreighterTypePath);
        Compare(LiveCollection.Frigates, disk);
    }

    /// <summary>Compares one list with the game's copy and takes the game's;
    /// true when it changed.</summary>
    private static bool Compare(LiveCollection list, JObject disk)
    {
        var state = States[list];
        JToken? theirs = disk;
        foreach (var key in SaveSessionManager.MapPath(state.ArrayPath)) theirs = theirs?[key];
        if (theirs is not JArray gameArray || SaveSessionManager.GetValue(state.ArrayPath) is not JArray loadedArray) return false;

        var game = gameArray.Select(item => Identity(list, item)).ToList();
        var loaded = loadedArray.Select(item => Identity(list, item)).ToList();
        if (game.SequenceEqual(loaded) && (list != LiveCollection.Frigates || JToken.DeepEquals(gameArray, loadedArray))) return false;

        // Fleet updates are quiet: refresh in place, without New/Gone notices.
        // AdoptFromGame protects staged roster edits, just as for ships and tools.
        if (list == LiveCollection.Frigates)
        {
            if (!SaveSessionManager.AdoptFromGame(disk, state.ArrayPath)) return false;
            Changed?.Invoke(list);
            return true;
        }

        // Counted, not just looked up: the same tool can be owned twice. The
        // copies already owned come first; any extra copy is the new one.
        var unmatched = loaded.Where(id => id is not null).GroupBy(id => id!).ToDictionary(g => g.Key, g => g.Count());
        var gameKeys = Keys(game);
        var added = new List<string>();
        for (int i = 0; i < game.Count; i++)
        {
            if (game[i] is not { } id) continue;
            if (unmatched.TryGetValue(id, out int left) && left > 0) unmatched[id] = left - 1;
            else added.Add(gameKeys[i]!);
        }
        var gone = new List<GoneItem>();
        for (int i = 0; i < loaded.Count; i++)
        {
            if (loaded[i] is not { } id || !unmatched.TryGetValue(id, out int left) || left == 0) continue;
            unmatched[id] = left - 1;
            gone.Add(new GoneItem(loadedArray[i]?["NKm"]?.Value<string>() is { Length: > 0 } n ? n : $"Unnamed {state.Noun}"));
        }

        if (!SaveSessionManager.AdoptFromGame(disk, state.ArrayPath)) return false; // unsaved edits - keep them

        foreach (var key in added)
        {
            state.New.Add(key);
            state.ToAnimate.Add(key);
        }
        state.Gone.AddRange(gone);
        var present = gameKeys.Where(k => k is not null).ToHashSet();
        state.New.IntersectWith(present!);       // a new one sold again isn't new
        state.ToAnimate.IntersectWith(present!);
        Changed?.Invoke(list);
        return true;
    }

    /// <summary>What's in a slot: its model and seeds, or null when it's empty.
    /// A multi-tool's seed is on the slot (SuJ[i].@EL), a ship's on its model
    /// (@Cs[i].NTx.@EL) - both are taken, so either list compares the same way.</summary>
    private static string? Identity(LiveCollection list, JToken? slot)
    {
        if (list == LiveCollection.Frigates) return FrigateService.Identity(slot);
        string scene = slot?["NTx"]?["93M"]?.Value<string>() ?? "";
        if (string.IsNullOrEmpty(scene)) return null;
        return $"{scene}|{slot?["@EL"]?[1]}|{slot?["NTx"]?["@EL"]?[1]}";
    }
}
