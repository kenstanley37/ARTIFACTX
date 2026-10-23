using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A ship type, on when it was seen in the system.</summary>
public sealed partial class ShipSeen(string type) : ObservableObject
{
    public string Type { get; } = type;
    [ObservableProperty] public partial bool IsOn { get; set; }
}

/// <summary>
/// "Your notes on this system" on Current System: facts about the whole system as the
/// Galaxy Map or the system's info panel shows them (SystemNotesService - ArtifactX's own
/// file, never the save). Saved as they change; collapsible to one summary line.
/// </summary>
public sealed partial class SystemNotesViewModel : ObservableObject
{
    private const string NotRecorded = "Not recorded";
    private ulong? _system;
    private bool _loading;

    public SystemNotesViewModel()
    {
        foreach (var picker in new[] { Star, Economy, Wealth, Conflict, Race }) picker.Changed += Save;
        Moons.Changed += () => { Save(); MoonsChanged?.Invoke(); };
        foreach (string type in SystemNotesService.ShipTypes)
        {
            var ship = new ShipSeen(type);
            ship.PropertyChanged += (_, _) => Save();
            Ships.Add(ship);
        }
    }

    public ChoiceFilter Star { get; } = new("Star colour", 170);
    public ChoiceFilter Economy { get; } = new("Economy", 250);
    public ChoiceFilter Wealth { get; } = new("Wealth", 230);
    public ChoiceFilter Conflict { get; } = new("Conflict", 200);
    public ChoiceFilter Race { get; } = new("Dominant race", 170);
    public ChoiceFilter Moons { get; } = new("Moons", 140);

    public ObservableCollection<ShipSeen> Ships { get; } = new();
    public ObservableCollection<string> Notes { get; } = new();

    /// <summary>The system's planets, for moving a note that's about one of them.</summary>
    public IReadOnlyList<KnownPlanet> Planets { get; private set; } = [];

    [ObservableProperty] public partial bool HasSystem { get; private set; }
    [ObservableProperty] public partial string SystemName { get; set; } = "";
    [ObservableProperty] public partial string SystemNamePlaceholder { get; private set; } = "Learned when you save here";
    [ObservableProperty] public partial string StarClass { get; set; } = "";
    [ObservableProperty] public partial bool Water { get; set; }
    [ObservableProperty] public partial bool Dissonant { get; set; }
    [ObservableProperty] public partial string NewNote { get; set; } = "";
    [ObservableProperty] public partial string Summary { get; private set; } = "";

    partial void OnWaterChanged(bool value) => Save();
    partial void OnDissonantChanged(bool value) => Save();

    /// <summary>Typing a star class fills in its colour (as the Galaxy Map shows "G7": G is yellow).</summary>
    partial void OnStarClassChanged(string value)
    {
        if (_loading || SystemNotesService.ColourForClass(value) is not string colour || Star.Value == colour) return;
        Star.Select(colour);
        Save();
    }

    /// <summary>The moon count changed: the drawing and which cards say "moon" change with it.</summary>
    public event Action? MoonsChanged;

    /// <summary>Fills the pickers once: economy, wealth and conflict list the game's own words
    /// with their category ("Mathematical (Scientific)").</summary>
    public void FillChoices(IReadOnlyList<(string Word, string Group, string Tier)> descriptors)
    {
        IEnumerable<ChoiceItem> Words(string group) => descriptors.Where(d => d.Group == group)
            .Select(d => new ChoiceItem($"{d.Word} ({SystemNotesService.TierName(group, d.Tier)})", d.Word))
            .OrderBy(c => c.Label, StringComparer.CurrentCultureIgnoreCase);
        static IEnumerable<ChoiceItem> Plain(IEnumerable<string> values) => values.Select(v => new ChoiceItem(v, v));
        _loading = true;
        Star.Refill(NotRecorded, Plain(SystemNotesService.StarColours));
        Economy.Refill(NotRecorded, Words("Economy").Concat(Plain(SystemNotesService.EconomyExtras)));
        Wealth.Refill(NotRecorded, Words("Wealth"));
        Conflict.Refill(NotRecorded, Words("Conflict"));
        Race.Refill(NotRecorded, Plain(SystemNotesService.Races));
        Moons.Refill(NotRecorded, Plain(["0", "1", "2", "3"]));
        _loading = false;
    }

    /// <summary>Shows a system's notes (null: no system - you're somewhere the save doesn't place).</summary>
    public void Load(ulong? system, IReadOnlyList<KnownPlanet> planets)
    {
        _system = system;
        Planets = planets;
        HasSystem = system is not null;
        var note = system is ulong s ? SystemNotesService.Get(s) : null;
        _loading = true;
        if (system is ulong address) SystemNamePlaceholder = $"{UniverseInfo.For(address).GeneratedName} (the game's name)";
        Star.Select(note?.StarColour ?? "");
        Economy.Select(note?.Economy ?? "");
        Wealth.Select(note?.Wealth ?? "");
        Conflict.Select(note?.Conflict ?? "");
        Race.Select(note?.Race ?? "");
        Moons.Select(note?.Moons?.ToString(CultureInfo.InvariantCulture) ?? "");
        SystemName = note?.SystemName ?? "";
        StarClass = note?.StarClass ?? "";
        Water = note?.Water == true;
        Dissonant = note?.Dissonant == true;
        foreach (var ship in Ships) ship.IsOn = note?.Ships.Contains(ship.Type, StringComparer.OrdinalIgnoreCase) == true;
        Notes.Clear();
        foreach (string text in note?.Notes ?? []) Notes.Add(text);
        _loading = false;
        Summary = PlanetText.NoteSummary(note);
    }

    /// <summary>Adds what's typed in the new-note box; false when it's empty.</summary>
    public bool AddNote()
    {
        if (NewNote.Trim() is not { Length: > 0 } text) return false;
        Notes.Add(text);
        NewNote = "";
        Save();
        return true;
    }

    public void RemoveNote(string text)
    {
        if (Notes.Remove(text)) Save();
    }

    /// <summary>"Move to planet" for a note about one planet (typed here before planets had
    /// their own details - 2026-10-13: moved, not copied).</summary>
    public void MoveNote(string text, KnownPlanet planet)
    {
        if (_system is not ulong system || !Notes.Contains(text)) return;
        var details = PlanetDetailsService.Get(system, planet.PlanetIndex)?.Copy() ?? new PlanetDetails();
        details.Notes.Add(text);
        PlanetDetailsService.Save(system, planet.PlanetIndex, details);
        Notes.Remove(text);
        Save();
    }

    public void Save()
    {
        if (_loading || _system is not ulong system) return;
        var existing = SystemNotesService.Get(system);
        var note = new SystemNote
        {
            SystemName = SystemName.Trim(),
            StationName = existing?.StationName ?? "",
            PlanetNames = existing?.PlanetNames ?? new(),
            StarColour = Star.Value,
            Economy = Economy.Value,
            Wealth = Wealth.Value,
            Conflict = Conflict.Value,
            Race = Race.Value,
            Moons = int.TryParse(Moons.Value, out int moons) ? moons : null,
            Notes = Notes.ToList(),
            StarClass = StarClass.Trim(),
            Water = Water,
            Dissonant = Dissonant,
            Ships = Ships.Where(s => s.IsOn).Select(s => s.Type).ToList(),
        };
        SystemNotesService.Save(system, note);
        Summary = PlanetText.NoteSummary(note);
    }
}
