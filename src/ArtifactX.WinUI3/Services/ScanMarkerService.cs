using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Markers ArtifactX adds to the save's active scan events (vLc.6f=.A1f) - the list the game's planetary charts and
/// signal boosters write to, so the game shows the marker and leads you to it. Each add or remove stages the whole
/// list (SaveSessionManager), so a marker is only written when the player saves, and the game must be closed.
/// Format evidence (2026-10-28, Slot 8): a portal marker the game saved itself (9@i 3, oZw as hex text, 5bU
/// [true, site key], TWn the system-space position) and test markers written the same way led straight to the portal
/// on 5 planets and from the app to Mephar VI's portal; an Observatory marker written with 9@i 1 and TWn at the
/// planet radius led to the building (2026-10-28).
/// </summary>
public static class ScanMarkerService
{
    private static readonly string[] ScanEventsPath = { "vLc", "6f=", "A1f" };

    /// <summary>Site keys of markers added in this session and not saved yet (they can be removed again).</summary>
    private static readonly HashSet<string> Added = new(StringComparer.OrdinalIgnoreCase);

    public static bool CanAdd => SaveSessionManager.IsSaveLoaded && !GameProcessMonitorService.IsGameRunning;

    /// <summary>Whether the save (with unsaved changes) has a marker for this site key.</summary>
    public static bool IsMarked(ulong key) => Events() is { } events && events.Any(e => Matches(e, key));

    /// <summary>Added in this session and waiting for Save.</summary>
    public static bool IsUnsaved(ulong key) =>
        Added.Contains(KeyText(key)) && SaveSessionManager.HasStagedEdit(ScanEventsPath) && IsMarked(key);

    /// <param name="table">The scan event table the event belongs to (9@i): 1 for planetary building events, 3 as the
    /// game saves portal markers.</param>
    /// <param name="eventId">The scan event (FN5), e.g. "PORTAL", "DISTRESS".</param>
    /// <param name="buildingClass">The building class name (iqv), e.g. "Portal", "DistressSignal".</param>
    public static void Add(ulong planetAddress, ulong key, double x, double y, double z, int table, string eventId, string buildingClass)
    {
        if (!CanAdd || IsMarked(key)) return;
        var events = Events() is { } current ? (JArray)current.DeepClone() : new JArray();
        events.Add(new JObject
        {
            ["9@i"] = table,
            ["FN5"] = "^" + eventId,
            ["oZw"] = $"0x{planetAddress:X}",
            ["5bU"] = new JArray(true, KeyText(key)),
            ["TWn"] = new JArray(Math.Round(x, 3), Math.Round(y, 3), Math.Round(z, 3)),
            ["iqv"] = new JObject { ["iqv"] = buildingClass },
            ["ADw"] = 0.0,
            ["jGk"] = "^",
            ["1JW"] = 0,
            ["M?f"] = new JObject { ["M?f"] = "None" },
        });
        SaveSessionManager.StageEdit(events, ScanEventsPath);
        Added.Add(KeyText(key));
    }

    /// <summary>Takes back a marker added in this session (other markers stay as they are).</summary>
    public static void Remove(ulong key)
    {
        if (!IsUnsaved(key) || Events() is not { } current) return;
        var events = new JArray(current.Where(e => !Matches(e, key)).Select(e => e.DeepClone()));
        SaveSessionManager.StageEdit(events, ScanEventsPath);
        Added.Remove(KeyText(key));
    }

    private static JArray? Events() => SaveSessionManager.GetValue(ScanEventsPath) as JArray;

    private static bool Matches(JToken e, ulong key) =>
        string.Equals(e["5bU"]?[1]?.Value<string>(), KeyText(key), StringComparison.OrdinalIgnoreCase);

    private static string KeyText(ulong key) => $"0x{key:X16}";
}
