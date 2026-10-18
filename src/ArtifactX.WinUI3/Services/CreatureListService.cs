using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>One creature as the game's Discoveries fauna list showed it.</summary>
public sealed record Creature(string Name, string Rarity, string Habitat, string Activity, string Gender, string Size, bool Picture, string SeenAt);

/// <summary>
/// Creatures on a planet, read off the game's Discoveries fauna list (2026-10-18). The save
/// keeps one record per creature, but only its seeds - no name, rarity, habitat or activity -
/// so the screen is the only source until creature generation is cracked; the readings are
/// then the answers to check it against.
///
/// The list's detail lines ("Rare / Underwater / Always Active") scroll sideways when they
/// don't fit, so a frame often shows a cut slice ("Active Common / Underwate"). Only whole,
/// properly capitalised words count: the known ones below, and any word read between two
/// slashes (it can't be cut there) - learned for the session, so land habitats come in too.
/// Frames add up: each fills what it read in full. The planet comes from the selected
/// creature's notes ("Discovered on planet Waterworld"), or from a listed creature already
/// placed. A creature listed as Underwater marks its planet as having water.
/// </summary>
public static class CreatureListService
{
    private static readonly object Lock = new();
    private static Dictionary<string, Dictionary<string, Creature>>? _byPlanet;

    private static readonly string[] Rarities = { "Common", "Uncommon", "Rare", "Extinct" };
    private static readonly HashSet<string> Words = new(StringComparer.Ordinal) { "Underwater", "Always Active", "Nocturnal", "Diurnal" };
    // Where each creature was placed this session, so a frame whose notes don't name the planet still lands.
    private static readonly Dictionary<string, (ulong System, int Planet)> PlacedThisSession = new();
    // The selected creature, and how many frames it has been selected - the picture waits for a settled one.
    private static (string Key, int Frames) _selected;
    private static string? _firstFramePicture;

    /// <summary>Raised (any thread) when a planet's creatures changed.</summary>
    public static event Action<ulong, int>? Changed;

    private static string Hex(ulong system) => system.ToString("X", CultureInfo.InvariantCulture);
    private static string PlanetKey(ulong system, int planet) => $"{Hex(system)}|{planet}";

    /// <summary>The name without its initial ("Zeronatonae" for "E. Zeronatonae") - the list sometimes loses the "E.".</summary>
    public static string NameKey(string name) => Regex.Replace(name.Trim(), @"^\p{Lu}\.\s*", "").ToUpperInvariant();

    public static string PicturePath(ulong system, int planet, string name) =>
        Path.Combine(LocalAppDataPaths.GetSubfolder("creature-pictures"), $"{Hex(system)}_{planet}_{Regex.Replace(NameKey(name), @"[^\p{L}\p{N}]+", "_")}.jpg");

    /// <summary>The creatures recorded on a planet, by name.</summary>
    public static IReadOnlyList<Creature> On(ulong system, int planet)
    {
        lock (Lock) return All.TryGetValue(PlanetKey(system, planet), out var list)
            ? list.Values.OrderBy(c => c.Rarity == "Extinct").ThenBy(c => c.Name, StringComparer.CurrentCulture).ToList() : new List<Creature>();
    }

    /// <summary>Whether a recorded creature on the planet lives underwater.</summary>
    public static bool AnyUnderwater(ulong system, int planet) => On(system, planet).Any(c => c.Habitat == "Underwater");

    /// <param name="placed">Each line with its left edge (share of the width) and height: the list
    /// is on the right, the selected creature's details on the left, at the same heights - by
    /// height alone the gender ("Prime") was taken for a creature (2026-10-18).</param>
    /// <param name="savePicture">Saves the creature part of the frame to the given path.</param>
    public static async Task<string?> TryReadAsync(IReadOnlyList<(string Text, double X, double Y)> placed, Func<string, Task<bool>>? savePicture = null)
    {
        var species = placed.FirstOrDefault(p => Regex.IsMatch(p.Text, @"Species\s+Discovered", RegexOptions.IgnoreCase));
        if (species.Text is null || !placed.Any(p => p.Text.Trim() == "Fauna")) return null;
        // The creature's menu (Rename and upload / View / Add to Wonders) covers the list - its
        // "Add Discovery to Personal Records" was read as a creature (2026-10-18).
        if (placed.Any(p => Regex.IsMatch(p.Text, @"RENAME\s+AND\s+UPLOAD|ADD\s+TO\s+WONDERS|View\s+fauna\s+data|Personal\s+Records", RegexOptions.IgnoreCase)))
            return "fauna list: menu open, skipped";
        var list = placed.Where(p => p.X > 0.55 && p.Y > species.Y + 10).ToList();
        var left = placed.Where(p => p.X < 0.5).ToList();

        // The list: each details line has its name just above it.
        var rows = new List<(string Name, string Rarity, string Habitat, string Activity)>();
        foreach (var line in list)
        {
            string t = line.Text.Trim();
            if (t.Length > 60 || !(t.Contains('/') || Rarities.Contains(t))) continue;
            var (rarity, habitat, activity) = Traits(line.Text);
            if (rarity.Length == 0 && habitat.Length == 0 && activity.Length == 0) continue;
            // The name sits right above its details line, starting at the same left edge.
            var above = list.Where(p => line.Y - p.Y > 12 && line.Y - p.Y < 45 && !p.Text.Contains('/') && Math.Abs(p.X - line.X) < 0.03)
                .OrderBy(p => line.Y - p.Y).FirstOrDefault();
            if (above.Text is null) continue;
            string name = CleanName(above.Text);
            // A creature's name: one or two words, maybe after an initial ("E. Zeronatonae").
            if (!Regex.IsMatch(name, @"^(\p{Lu}\.\s?)?\p{Lu}[\p{L}'-]{2,}(\s\p{Lu}[\p{L}'-]+)?$") || Traits(name) != ("", "", "")) continue;
            rows.Add((name, rarity, habitat, activity));
        }
        if (rows.Count == 0) return "fauna list: no creatures read";
        // The whole list on screen ("7/7 Species Discovered" and 7 creatures besides the extinct
        // ones): anything recorded for the planet that isn't in it was a misreading.
        bool complete = Regex.Match(species.Text, @"(\d+)\s*/\s*\d+") is { Success: true } count
            && int.TryParse(count.Groups[1].Value, out int discovered)
            && rows.Select(r => NameKey(r.Name)).Distinct().Count() == rows.Count
            && rows.Count(r => r.Rarity != "Extinct") == discovered;

        // The planet: named in the selected creature's notes, or known from an earlier frame.
        string text = string.Join(" ", left.Select(p => p.Text));
        (ulong System, int Planet)? where = PlanetIn(text);
        lock (Lock)
            where ??= rows.Select(r => PlacedThisSession.TryGetValue(NameKey(r.Name), out var at) ? at : ((ulong, int)?)null)
                .FirstOrDefault(at => at is not null);
        if (where is not (ulong system, int planet)) return $"fauna list: {rows.Count} creatures, planet not named on screen";

        // The selected creature (left side): its name is the big title above "Discovered ... by".
        var by = left.FirstOrDefault(p => Regex.IsMatch(p.Text, @"Discovered\s.*\bby\b|\bago\s+by\b|today\s+by\b", RegexOptions.IgnoreCase));
        string? selected = by.Text is null ? null : left.Where(p => by.Y - p.Y > 10 && by.Y - p.Y < 80).OrderBy(p => by.Y - p.Y)
            .Select(p => CleanName(p.Text)).FirstOrDefault(n => rows.Any(r => NameKey(r.Name) == NameKey(n)));
        string gender = selected is null ? "" : ValueBelow(left, "Gender");
        string size = selected is null ? "" : ValueBelow(left, "Weight / Height").Replace(" ", "").Replace("/", " / ");
        if (!Regex.IsMatch(size, @"^[\d.]+\s*kg\s*/\s*[\d.]+\s*m$", RegexOptions.IgnoreCase)) size = "";
        if (gender.Count(char.IsLetter) < 3 || gender.Contains('/')) gender = "";

        bool picture = false;
        if (selected is not null)
        {
            string key = NameKey(selected);
            lock (Lock) _selected = _selected.Key == key ? (key, _selected.Frames + 1) : (key, 1);
            // Taken on the first frame (a list that doesn't scroll may give no second one - the
            // recorder skips frames with the same text), then replaced by the second, when the
            // model has finished appearing.
            bool hasPicture = File.Exists(PicturePath(system, planet, selected));
            bool retake;
            lock (Lock) retake = _selected.Frames == 2 && _firstFramePicture == key;
            if (savePicture is not null && ((_selected.Frames == 1 && !hasPicture) || retake))
            {
                picture = await savePicture(PicturePath(system, planet, selected));
                lock (Lock) _firstFramePicture = picture && _selected.Frames == 1 ? key : null;
            }
        }

        var (added, changed) = Merge(system, planet, rows, selected, gender, size, picture, complete);
        if (rows.Any(r => r.Habitat == "Underwater") && PlanetWaterService.Get(system, planet) is null)
            PlanetWaterService.Set(system, planet, true, source: "captured");
        if (changed > 0) Changed?.Invoke(system, planet);
        return $"fauna list {UniverseInfo.PlanetName(system, planet)}: {string.Join(", ", rows.Select(r => $"{r.Name} ({string.Join(" / ", new[] { r.Rarity, r.Habitat, r.Activity }.Where(s => s.Length > 0))})"))}"
            + (selected is not null ? $"; selected {selected} {gender} {size}" : "") + (picture ? "; picture saved" : "") + (added > 0 ? $"; {added} new" : "");
    }

    /// <summary>The whole words on a details line, sorted into rarity / habitat / activity.</summary>
    private static (string Rarity, string Habitat, string Activity) Traits(string line)
    {
        string rarity = "", habitat = "", activity = "";
        void Put(string word)
        {
            if (Rarities.Contains(word)) { if (rarity.Length == 0) rarity = word; }
            else if (Regex.IsMatch(word, @"Active|Nocturnal|Diurnal|Asleep|Sleep")) { if (activity.Length == 0) activity = word; }
            else if (habitat.Length == 0) habitat = word;
        }
        // Known words anywhere, matched whole and case-sensitive ("common" cut from "Uncommon" isn't "Common").
        List<string> known;
        lock (Lock) known = Words.Concat(Rarities).OrderByDescending(w => w.Length).ToList();
        string rest = line;
        foreach (var word in known)
            if (Regex.IsMatch(rest, $@"(?<![\p{{L}}]){Regex.Escape(word)}(?![\p{{L}}])"))
            {
                Put(word);
                rest = Regex.Replace(rest, $@"(?<![\p{{L}}]){Regex.Escape(word)}(?![\p{{L}}])", "/");
            }
        // A word between two slashes is whole: learn it.
        var parts = line.Split('/');
        for (int i = 1; i < parts.Length - 1; i++)
        {
            string word = parts[i].Trim();
            if (!Regex.IsMatch(word, @"^\p{Lu}\p{Ll}+(\s\p{Lu}\p{Ll}+)?$") || Rarities.Contains(word)) continue;
            lock (Lock) Words.Add(word);
            Put(word);
        }
        return (rarity, habitat, activity);
    }

    // How often each spelling of a name was read: a cut row can lose its initial ("Alderhukkeum"),
    // an icon can come out as a symbol - the spelling read most wins. ("Q. Hoydroibolima" is the
    // game's own name, the Q. isn't the icon - checked against its title, 2026-10-18.)
    private static readonly Dictionary<string, Dictionary<string, int>> NameVotes = new();
    // How many frames read each creature this session, per planet; it's kept from the second.
    private static readonly Dictionary<(string Planet, string Key), int> ReadCount = new();
    private const int Confirmations = 2;

    private static string VotedName(string key, string name)
    {
        if (!NameVotes.TryGetValue(key, out var votes)) NameVotes[key] = votes = new(StringComparer.Ordinal);
        votes[name] = votes.GetValueOrDefault(name) + 1;
        return votes.OrderByDescending(v => v.Value).ThenByDescending(v => v.Key.Length).First().Key;
    }

    private static string CleanName(string text) =>
        // Icons read as symbols before the name ("@Hoydroibolima") and dots after it ("Z. Donkeisae ::").
        Regex.Replace(Regex.Replace(Regex.Replace(text.Trim(), @"^(?:[^\p{L}]+|[Oo0@]\s+)+", ""), @"[^\p{L}.']+$", ""), @"\s+", " ").Trim().TrimEnd('.');

    private static string ValueBelow(IReadOnlyList<(string Text, double X, double Y)> placed, string label)
    {
        var at = placed.FirstOrDefault(p => p.Text.Trim().Equals(label, StringComparison.OrdinalIgnoreCase));
        if (at.Text is null) return "";
        return placed.Where(p => p.Y - at.Y > 8 && p.Y - at.Y < 45).OrderBy(p => p.Y - at.Y).Select(p => p.Text.Trim()).FirstOrDefault() ?? "";
    }

    /// <summary>"planet Waterworld" in the notes, matched to a planet of a system the player was recently in.</summary>
    private static (ulong, int)? PlanetIn(string text)
    {
        string norm = Regex.Replace(text, @"\s+", " ");
        var found = new List<(ulong, int)>();
        foreach (ulong system in PlanetPageReader.Recent())
        {
            var f = UniverseInfo.For(system);
            for (int b = 1; b <= f.PlanetCount + f.MoonCount; b++)
                foreach (var name in new[] { UniverseInfo.PlanetName(system, b), UniverseInfo.GeneratedPlanetName(system, b) }.Distinct())
                    if (name.Length > 2 && Regex.IsMatch(norm, $@"\b(planet|moon)\s+{Regex.Escape(name)}(?![\p{{L}}])", RegexOptions.IgnoreCase))
                        found.Add((system, b));
        }
        found = found.Distinct().ToList();
        return found.Count == 1 ? found[0] : null;
    }

    private static Dictionary<string, Dictionary<string, Creature>> All
    {
        get
        {
            if (_byPlanet is not null) return _byPlanet;
            _byPlanet = new(StringComparer.Ordinal);
            try
            {
                using var connection = UserDatabase.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT system_address, planet_index, name_key, name, rarity, habitat, activity, gender, size, picture, seen_at FROM planet_creatures WHERE deleted = 0";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string key = $"{r.GetString(0)}|{r.GetInt32(1)}";
                    if (!_byPlanet.TryGetValue(key, out var list)) _byPlanet[key] = list = new(StringComparer.Ordinal);
                    list[r.GetString(2)] = new Creature(r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.GetInt32(9) != 0, r.GetString(10));
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[CreatureListService] {ex.Message}"); }
            return _byPlanet;
        }
    }

    /// <summary>Fills what each reading has; never empties a field already known. Returns how many
    /// creatures are new and how many changed.</summary>
    private static (int Added, int Changed) Merge(ulong system, int planet, List<(string Name, string Rarity, string Habitat, string Activity)> rows,
        string? selected, string gender, string size, bool picture, bool complete)
    {
        string now = UserDatabase.Now(), planetKey = PlanetKey(system, planet);
        var changed = new List<(string Key, Creature Creature)>();
        var removed = new List<string>();
        int added = 0;
        lock (Lock)
        {
            if (!All.TryGetValue(planetKey, out var list)) All[planetKey] = list = new(StringComparer.Ordinal);
            // The whole list was on screen: what isn't in it was a misreading (a gender read as a name).
            if (complete)
                // Not what was confirmed this session: a frame can miss a real row (the extinct
                // one at the bottom), and removing it made it flicker (Oreniuite, 2026-10-18).
                foreach (var key in list.Keys.Where(k => !rows.Any(r => NameKey(r.Name) == k)
                    && ReadCount.GetValueOrDefault((planetKey, k)) < Confirmations).ToList())
                {
                    list.Remove(key);
                    removed.Add(key);
                }
            foreach (var row in rows)
            {
                string key = NameKey(row.Name);
                // Kept once read in two frames: a name caught mid-animation ("Alde" for Alderhukkeum)
                // or another line taken for one ("Add Records") rarely comes back (2026-10-18).
                ReadCount[(planetKey, key)] = ReadCount.GetValueOrDefault((planetKey, key)) + 1;
                if (ReadCount[(planetKey, key)] < Confirmations) continue;
                // A piece of a longer name is that name, cut off; a longer name replaces a stored piece.
                if (list.Keys.Concat(rows.Select(r => NameKey(r.Name))).Any(k => k != key && k.Length > key.Length && k.Contains(key, StringComparison.Ordinal)))
                    continue;
                foreach (var piece in list.Keys.Where(k => k != key && key.Contains(k, StringComparison.Ordinal)).ToList())
                {
                    list.Remove(piece);
                    removed.Add(piece);
                }
                PlacedThisSession[key] = (system, planet);
                bool isSelected = selected is not null && NameKey(selected) == key;
                list.TryGetValue(key, out var old);
                if (old is null) added++;
                static string Keep(string fresh, string? kept) => fresh.Length > 0 ? fresh : kept ?? "";
                var next = new Creature(
                    VotedName(key, row.Name),
                    Keep(row.Rarity, old?.Rarity), Keep(row.Habitat, old?.Habitat), Keep(row.Activity, old?.Activity),
                    isSelected ? Keep(gender, old?.Gender) : old?.Gender ?? "", isSelected ? Keep(size, old?.Size) : old?.Size ?? "",
                    (old?.Picture ?? false) || (isSelected && picture), now);
                if (old is not null && old with { SeenAt = now } == next) continue;
                list[key] = next;
                changed.Add((key, next));
            }
        }
        if (changed.Count == 0 && removed.Count == 0) return (added, 0);
        try
        {
            using var connection = UserDatabase.Open();
            using var tx = connection.BeginTransaction();
            string build = FieldSources.GameBuild;
            foreach (var key in removed)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE planet_creatures SET deleted = 1, updated_at = $now, synced_at = NULL WHERE system_address = $s AND planet_index = $p AND name_key = $k";
                cmd.Parameters.AddWithValue("$s", Hex(system));
                cmd.Parameters.AddWithValue("$p", planet);
                cmd.Parameters.AddWithValue("$k", key);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.ExecuteNonQuery();
            }
            foreach (var (key, c) in changed)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO planet_creatures (system_address, planet_index, name_key, name, rarity, habitat, activity, gender, size, picture,
                                                  seen_at, updated_at, source, game_build, synced_at, deleted, added_by)
                    VALUES ($s, $p, $k, $name, $rarity, $habitat, $activity, $gender, $size, $picture, $now, $now, 'captured', $build, NULL, 0, $by)
                    ON CONFLICT(system_address, planet_index, name_key) DO UPDATE SET name = $name, rarity = $rarity, habitat = $habitat,
                        activity = $activity, gender = $gender, size = $size, picture = $picture, seen_at = $now, updated_at = $now,
                        game_build = $build, synced_at = NULL, deleted = 0";
                cmd.Parameters.AddWithValue("$s", Hex(system));
                cmd.Parameters.AddWithValue("$p", planet);
                cmd.Parameters.AddWithValue("$k", key);
                cmd.Parameters.AddWithValue("$name", c.Name);
                cmd.Parameters.AddWithValue("$rarity", c.Rarity);
                cmd.Parameters.AddWithValue("$habitat", c.Habitat);
                cmd.Parameters.AddWithValue("$activity", c.Activity);
                cmd.Parameters.AddWithValue("$gender", c.Gender);
                cmd.Parameters.AddWithValue("$size", c.Size);
                cmd.Parameters.AddWithValue("$picture", c.Picture ? 1 : 0);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$build", build);
                cmd.Parameters.AddWithValue("$by", GameAccountsService.ActiveKey);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch (Exception ex) { Debug.WriteLine($"[CreatureListService] {ex.Message}"); }
        return (added, changed.Count + removed.Count);
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _byPlanet = null;
    }
}
