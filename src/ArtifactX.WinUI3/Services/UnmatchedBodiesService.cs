using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Planet and moon readings whose name ArtifactX doesn't know (2026-10-17). Another player
/// may have renamed the bodies of a system they discovered ("Aeniladust Edog" for the
/// generated "Aeniladust Tosuk"); those names live on the game's servers, not in the save,
/// so the reading can't be tied to a body by name. It's held here for the system the
/// player is in, and the Current System page asks which body it is. Once answered, the
/// name is stored as that body's name (system notes), so every later reading matches.
/// Held in memory for the session only.
/// </summary>
public static class UnmatchedBodiesService
{
    public sealed record Held(ulong System, string Name, PlanetDetails? Details, bool? Moon);

    private static readonly object Lock = new();
    private static readonly List<Held> Items = new();

    public static event Action<ulong>? Changed;

    /// <summary>Landing on a planet makes the save record its name as the game shows it
    /// ("On Planet (Aeniladust Edog)"): learn it from every save, then place any held
    /// reading that now matches - the player needn't pick at all.</summary>
    public static void Start()
    {
        FishCatchRecorder.SnapshotSeen += snapshot =>
        {
            ulong? system = ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ReadSystemAddress(snapshot.Universe);
            SystemNameLearner.Learn(system, (int?)snapshot.Universe?["oZw"]?["jsv"] ?? 0, snapshot.Summary);
            // Renamed planets the save's discovery records name ("Waterworld") place held readings too.
            SystemNameLearner.LearnDiscoveryNames(snapshot.Discoveries);
            Retry();
        };
    }

    private static void Retry()
    {
        List<Held> held;
        lock (Lock) held = Items.ToList();
        foreach (var h in held)
            if (PlanetPageReader.MatchBody(h.Name) is (ulong system, int body, _) && system == h.System)
                Assign(h, body, rememberName: false);
    }

    public static IReadOnlyList<Held> For(ulong system)
    {
        lock (Lock) return Items.Where(h => h.System == system).ToList();
    }

    /// <summary>Holds a reading for the system the save puts the player in now. Details and
    /// planet/moon from several readings of the same name are merged.</summary>
    public static void Hold(string name, PlanetDetails? details, bool? moon)
    {
        // Not a name: the view's "5 PLANETS / 1 MOON" header was once taken for one (2026-10-17).
        if (name.Contains('/') || System.Text.RegularExpressions.Regex.IsMatch(name, @"\b(PLANETS?|MOONS?)\b|^\W*\d") || PlanetPageReader.LooksLikeDistance(name)) return;
        ulong system = PlanetPageReader.Recent().FirstOrDefault();
        if (system == 0) return;
        string key = PlanetPageReader.Norm(name);
        lock (Lock)
        {
            var old = Items.FirstOrDefault(h => h.System == system && PlanetPageReader.Norm(h.Name) == key);
            if (old is not null) Items.Remove(old);
            Items.Add(new Held(system, old is not null && old.Name.Length > name.Length ? old.Name : name,
                details ?? old?.Details, moon ?? old?.Moon));
        }
        Changed?.Invoke(system);
    }

    /// <summary>The player said which body it is: remember the name, then apply what was read.</summary>
    public static void Assign(Held held, int body, bool rememberName = true)
    {
        lock (Lock) Items.Remove(held);
        if (rememberName)
        {
            var note = SystemNotesService.Get(held.System) ?? new SystemNote();
            note.PlanetNames[body.ToString(CultureInfo.InvariantCulture)] = held.Name;
            SystemNotesService.Save(held.System, note, source: "user");
        }
        if (held.Details is { } details)
        {
            var existing = PlanetDetailsService.Get(held.System, body);
            details.Notes = existing?.Notes.ToList() ?? new();
            PlanetDetailsService.Save(held.System, body, details, source: "captured");
        }
        if (held.Moon is bool moon) BodyKindsService.Store(held.System, body, moon);
        Changed?.Invoke(held.System);
    }

    public static void Dismiss(Held held)
    {
        lock (Lock) Items.Remove(held);
        Changed?.Invoke(held.System);
    }
}
