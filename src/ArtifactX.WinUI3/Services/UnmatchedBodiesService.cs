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
///
/// A body is "accounted for" once details were read off the screen or typed for it, or its
/// name was learned (from where you saved, a discovery record, or typed). The generator's name
/// is what the game shows about 99% of the time, but not always - Ehilusti's planet 1 is
/// "Rigie IV" in game and "Nigie Major" from the generator (2026-10-20) - so a reading that
/// matches no name is only held while the system has a body not accounted for; when every
/// body is, it's other screen text ("Online Discovery Services Active") and is dropped. A held
/// reading that can only be one body is placed there by itself (Place).
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
        // Another body's details read: one fewer body a held name can belong to.
        PlanetDetailsService.Changed += (_, _) => Retry();
    }

    private static void Retry()
    {
        List<Held> held;
        lock (Lock) held = Items.ToList();
        foreach (var h in held)
        {
            if (PlanetPageReader.MatchBody(h.Name) is (ulong system, int body, _) && system == h.System)
                Assign(h, body, rememberName: false);
            else if (UnaccountedBodies(h.System).Count == 0)
                Dismiss(h); // every body is accounted for, and none is this name
            else
                Place(h);
        }
    }

    /// <summary>The bodies of a system no name or details are known for yet - the only ones a
    /// name ArtifactX doesn't know can belong to.</summary>
    public static IReadOnlyList<int> UnaccountedBodies(ulong system)
    {
        var info = UniverseInfo.For(system);
        var names = SystemNotesService.Get(system)?.PlanetNames;
        return Enumerable.Range(1, info.PlanetCount + info.MoonCount)
            .Where(b => PlanetDetailsService.Get(system, b) is null or { IsEmpty: true }
                && names?.ContainsKey(b.ToString(CultureInfo.InvariantCulture)) != true)
            .ToList();
    }

    /// <summary>Places a held reading on the one body it can be: the only body of its kind
    /// (planet or moon) not accounted for, whose generated name starts almost the same
    /// ("Rigie IV" on "Nigie Major" - one letter off; screen text never is). The name is then
    /// remembered, so later readings match. False when it could be more than one body.</summary>
    private static bool Place(Held held)
    {
        var candidates = UnaccountedBodies(held.System)
            .Where(b => held.Moon is not bool moon || BodyKindsService.IsMoon(held.System, b) == moon)
            .ToList();
        if (candidates.Count != 1) return false;
        int body = candidates[0];
        string generated = UniverseInfo.GeneratedPlanetName(held.System, body);
        if (PlanetPageReader.Distance(FirstWord(held.Name), FirstWord(generated)) > 2) return false;
        Assign(held, body);
        return true;
    }

    private static string FirstWord(string name) => PlanetPageReader.Norm(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "");

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
        if (system == 0 || UnaccountedBodies(system).Count == 0) return;
        string key = PlanetPageReader.Norm(name);
        lock (Lock)
        {
            var old = Items.FirstOrDefault(h => h.System == system && PlanetPageReader.Norm(h.Name) == key);
            if (old is not null) Items.Remove(old);
            Items.Add(new Held(system, old is not null && old.Name.Length > name.Length ? old.Name : name,
                MergeDetails(old?.Details, details), moon ?? old?.Moon));
        }
        Changed?.Invoke(system);
        Held? added;
        lock (Lock) added = Items.FirstOrDefault(h => h.System == system && PlanetPageReader.Norm(h.Name) == key);
        if (added is not null) Place(added);
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
            var merged = MergeDetails(existing, details) ?? details;
            merged.Notes = existing?.Notes.ToList() ?? new();
            PlanetDetailsService.Save(held.System, body, merged, source: "captured");
        }
        if (held.Moon is bool moon) BodyKindsService.Store(held.System, body, moon);
        Changed?.Invoke(held.System);
    }

    private static PlanetDetails? MergeDetails(PlanetDetails? oldDetails, PlanetDetails? newDetails)
    {
        if (oldDetails is null && newDetails is null) return null;
        var merged = oldDetails?.Copy() ?? new PlanetDetails();
        if (newDetails is not null)
        {
            if (!string.IsNullOrEmpty(newDetails.Descriptor)) merged.Descriptor = newDetails.Descriptor;
            if (!string.IsNullOrEmpty(newDetails.Sentinels)) merged.Sentinels = newDetails.Sentinels;
            if (!string.IsNullOrEmpty(newDetails.Weather)) merged.Weather = newDetails.Weather;
            if (!string.IsNullOrEmpty(newDetails.Flora)) merged.Flora = newDetails.Flora;
            if (!string.IsNullOrEmpty(newDetails.Fauna)) merged.Fauna = newDetails.Fauna;
            if (newDetails.Resources.Count > 0) merged.Resources = newDetails.Resources.ToList();
        }
        return merged;
    }

    public static void Dismiss(Held held)
    {
        lock (Lock) Items.Remove(held);
        Changed?.Invoke(held.System);
    }
}
