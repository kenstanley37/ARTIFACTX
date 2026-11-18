using ArtifactX.Almanac.Starship;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>Where the player is, from the newest save the game wrote: system, planet (0 in space), and the coordinates on
/// that planet (NmsPlanetCoordinates). All from the same snapshot (Bug 2, 2026-11-03 - the system was the loaded save's
/// while the coordinates were the latest).</summary>
public sealed record FindsHere(ulong System, int Planet, (double Latitude, double Longitude)? Coordinates, List<KnownPlanet> Planets);

/// <summary>An item or fish picked in the Finds form; the icon arrives later for items not cached yet.</summary>
public sealed partial class FindFormItemChip(string id, string label, ImageSource? icon) : ObservableObject
{
    public string Id { get; } = id;
    public string Label { get; } = label;
    public string RemoveName => $"Remove {Label}";

    [ObservableProperty] public partial ImageSource? Icon { get; set; } = icon;
}

/// <summary>
/// The Finds page's Add / Edit form: type, title, class, kind, where to find it, items sold or fish caught, the portal
/// address and galaxy (with the glyphs and a check for another galaxy), a system name, and notes. Saves through
/// FindsService (user.db); a typed system name goes to the shared system notes.
/// </summary>
public sealed partial class FindFormViewModel : ObservableObject
{
    private List<string> _shipKinds = new();
    private List<string> _toolKinds = new();
    private List<string> _freighterKinds = new();
    private Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _fishNames = new(StringComparer.OrdinalIgnoreCase);
    private string? _editingId;
    private string _editingCreatedAt = "";
    // The last "Where to find it" text Use my current location wrote, so pressing it again after moving updates it
    // without overwriting the player's own.
    private string _autoLocation = "";

    /// <summary>Where the player is (FindsPageViewModel keeps it current); null with no save.</summary>
    public FindsHere? Here
    {
        get;
        set
        {
            field = value;
            CanUseHere = value is not null;
            UpdateGlyphs();
        }
    }

    public IReadOnlyList<string> Types { get; } = FindsService.Types;

    public IReadOnlyList<TextOption> ClassOptions { get; } =
        new[] { "", "S", "A", "B", "C" }.Select(c => new TextOption(c.Length == 0 ? "-" : c, c)).ToList();

    public IReadOnlyList<GalaxyOption> Galaxies { get; } =
        Enumerable.Range(1, 256).Select(g => new GalaxyOption(PlanetText.GalaxyLabel(g), g)).ToList();

    public ObservableCollection<FindFormItemChip> Items { get; } = new();

    [ObservableProperty] public partial bool IsOpen { get; private set; }
    [ObservableProperty] public partial string FormTitle { get; private set; } = "ADD A FIND";
    [ObservableProperty] public partial string SaveText { get; private set; } = "Save find";
    public string ToggleGlyph => IsOpen ? "" : "";

    [ObservableProperty] public partial string? SelectedType { get; set; }
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial TextOption? SelectedClass { get; set; }
    [ObservableProperty] public partial bool ShowClass { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<string> Kinds { get; private set; } = [];
    [ObservableProperty] public partial string? SelectedKind { get; set; }
    [ObservableProperty] public partial bool ShowKinds { get; private set; }
    [ObservableProperty] public partial string Location { get; set; } = "";
    [ObservableProperty] public partial string Notes { get; set; } = "";

    [ObservableProperty] public partial bool ShowItems { get; private set; }
    [ObservableProperty] public partial string ItemSearchHeader { get; private set; } = "Items sold here";
    [ObservableProperty] public partial string ItemSearchPlaceholder { get; private set; } = "Search an item, e.g. Pugneum";

    [ObservableProperty] public partial bool CanUseHere { get; private set; }
    [ObservableProperty] public partial string Code { get; set; } = "";
    [ObservableProperty] public partial GalaxyOption? SelectedGalaxy { get; set; }
    [ObservableProperty] public partial string SystemName { get; set; } = "";
    [ObservableProperty] public partial string SystemNamePlaceholder { get; private set; } = "As shown in game";
    [ObservableProperty] public partial string GlyphCode { get; private set; } = "";
    [ObservableProperty] public partial string CodeStatus { get; private set; } = "";
    [ObservableProperty] public partial string Error { get; private set; } = "";

    /// <summary>The kinds and pickers' lists from game data and the app's own type lists, not hand-typed here.</summary>
    public async Task LoadAsync(Dictionary<string, string> itemNames, Func<string, string> nameOf)
    {
        _itemNames = itemNames;
        // Reward ships can't be found in the wild, so they're left out.
        _shipKinds = StarshipTypes.All.Select(t => t.DisplayName).Where(n => !n.Contains("(Reward")).ToList();
        // The main model (MULTITOOL.SCENE, listed as "Rifle") makes pistols, rifles, experimentals and alien tools - the
        // seed picks which - so a find needs those names, not the one model name (an Alien tool had no entry,
        // 2026-10-15). The game has no names for them; these are the players'.
        _toolKinds = (await CatalogService.GetMultiToolTypesAsync())
            .SelectMany(t => t.ScenePath.EndsWith("/MULTITOOL.SCENE.MBIN", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Pistol", "Rifle", "Experimental", "Alien", "Starter" }
                : new[] { MultiToolKinds.NameFor(t.ScenePath, t.DisplayName) })
            .Distinct().ToList();
        _freighterKinds = (await CatalogService.GetFreighterTypesAsync()).Select(t => t.DisplayName)
            .Where(n => n is "Normal" or "Capital" or "Dreadnought").ToList();
        // Same fish list as the Fishing Records page (game ids, not hand-typed).
        var fish = CatalogService.FilterFishSpecies(await CatalogService.GetAllUnlockableItemsAsync());
        _fishNames = fish.GroupBy(f => f.GameId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => nameOf(g.Key), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The fish picker's ids, to warm their icons.</summary>
    public IEnumerable<string> FishIds => _fishNames.Keys;

    /// <summary>Ready to add a find, in the player's galaxy.</summary>
    public void Start()
    {
        SelectedType = Types[0];
        SelectedClass = ClassOptions[0];
        SelectGalaxy(Here is { } here ? S.GalaxyNumber(here.System) : 1);
    }

    public void Toggle() => SetOpen(!IsOpen);

    private void SetOpen(bool open)
    {
        IsOpen = open;
        OnPropertyChanged(nameof(ToggleGlyph));
    }

    partial void OnSelectedTypeChanged(string? value)
    {
        string type = value ?? "";
        ShowClass = FindsService.HasClass(type);
        var kinds = type switch
        {
            "Starship" => _shipKinds,
            "Multi-Tool" => _toolKinds,
            "Freighter" => _freighterKinds,
            "Creature" => FindsService.CreatureKinds.ToList(),
            _ => new List<string>(),
        };
        Kinds = kinds.ToList();
        ShowKinds = kinds.Count > 0;
        ShowItems = FindsService.HasItems(type);
        bool fishing = type == "Fishing spot";
        ItemSearchHeader = fishing ? "Fish caught here" : "Items sold here";
        ItemSearchPlaceholder = fishing ? "Search a fish, e.g. Mud Crab" : "Search an item, e.g. Pugneum";
        // Switching between the item and fish lists drops chips from the other list.
        foreach (var chip in Items.Where(c => fishing != _fishNames.ContainsKey(c.Id)).ToList()) Items.Remove(chip);
    }

    partial void OnCodeChanged(string value) => UpdateGlyphs();
    partial void OnSelectedGalaxyChanged(GalaxyOption? value) => UpdateGlyphs();

    private int Galaxy => SelectedGalaxy?.Number ?? 1;

    private void SelectGalaxy(int galaxy) => SelectedGalaxy = Galaxies.FirstOrDefault(g => g.Number == galaxy) ?? Galaxies[0];

    /// <summary>Shows the glyphs for the code being entered, and flags a code for a different galaxy from the player's.</summary>
    private void UpdateGlyphs()
    {
        string input = Code.Trim();
        GlyphCode = NmsPortalCode.TryParse(input, out string stripCode, out _) ? stripCode : "";
        if (input.Length == 0)
        {
            CodeStatus = "";
            return;
        }
        if (!NmsPortalCode.TryParse(input, out string code, out string error))
        {
            CodeStatus = error;
            return;
        }
        // Show the system's name (recorded, else the game's own); a typed name is only needed when a player renamed the
        // system. Never overwrite what the player typed.
        SystemNamePlaceholder = UniverseInfo.SystemName(NmsPortalCode.ToAddress(code, Galaxy));
        CodeStatus = Here is { } here && S.GalaxyNumber(here.System) != Galaxy
            ? $"This is in {PlanetText.GalaxyLabel(Galaxy)} - you're in {PlanetText.GalaxyLabel(S.GalaxyNumber(here.System))}. Portals can't cross galaxies."
            : code;
    }

    /// <summary>Use my current location: the address (planet 1 in space - portal codes need a planet digit), the galaxy,
    /// and "Where to find it" pre-filled with the planet (or system) name plus the coordinates the game's scanner shows
    /// (2026-10-14) - the spot itself, not just the planet.</summary>
    public void UseHere()
    {
        if (Here is not { } here) return;
        Code = S.PortalAddress(here.System, here.Planet > 0 ? here.Planet : 1);
        SelectGalaxy(S.GalaxyNumber(here.System));
        string current = Location.Trim();
        if (current.Length > 0 && current != _autoLocation) return;
        _autoLocation = here.Planet > 0
            ? $"On {UniverseInfo.PlanetName(here.System, here.Planet)}"
              + (here.Coordinates is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "")
            : $"In {UniverseInfo.SystemName(here.System)}";
        Location = _autoLocation;
    }

    private Dictionary<string, string> PickerNames() => SelectedType == "Fishing spot" ? _fishNames : _itemNames;

    /// <summary>The picker's matches for typed text (two letters or more), names starting with it first.</summary>
    public List<string>? Suggestions(string text)
    {
        text = text.Trim();
        return text.Length < 2 ? null : PickerNames().Values
            .Where(n => n.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(n => n.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1).ThenBy(n => n)
            .Take(12).ToList();
    }

    public async Task AddItemAsync(string name, Func<string, string> nameOf)
    {
        string? id = PickerNames().FirstOrDefault(kv => string.Equals(kv.Value, name, StringComparison.CurrentCultureIgnoreCase)).Key;
        if (id is null || Items.Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))) return;
        var chip = new FindFormItemChip(id, nameOf(id), CatalogService.TryGet(id)?.Icon);
        Items.Add(chip);
        // Sold items aren't pre-warmed (there are thousands); fetch this one's icon.
        if (chip.Icon is null)
        {
            await CatalogService.WarmCacheAsync(new[] { id });
            chip.Icon = CatalogService.TryGet(id)?.Icon;
        }
    }

    public void RemoveItem(string id)
    {
        if (Items.FirstOrDefault(c => c.Id == id) is { } chip) Items.Remove(chip);
    }

    public bool IsEditing(string id) => _editingId == id;

    /// <summary>Fills the form with a find to edit.</summary>
    public void Edit(Find f, Func<string, string> nameOf)
    {
        _editingId = f.Id;
        _editingCreatedAt = f.CreatedAt;
        SelectedType = f.Type;
        Title = f.Title;
        SelectedClass = ClassOptions.FirstOrDefault(c => c.Key == f.Class) ?? ClassOptions[0];
        SelectedKind = Kinds.Contains(f.Subtype) ? f.Subtype : null;
        Location = f.Location;
        Notes = f.Notes;
        Items.Clear();
        foreach (var id in f.ItemIds) Items.Add(new FindFormItemChip(id, nameOf(id), CatalogService.TryGet(id)?.Icon));
        SelectGalaxy(f.Galaxy);
        SystemName = "";
        Code = f.PortalCode;
        FormTitle = "EDIT FIND";
        SaveText = "Save changes";
        SetOpen(true);
    }

    /// <summary>Saves the find; false (with Error set) when the address doesn't read or there's neither title nor address.</summary>
    public bool Save()
    {
        string type = SelectedType ?? "";
        string code = "";
        if (Code.Trim().Length > 0 && !NmsPortalCode.TryParse(Code, out code, out string error))
        {
            Error = error;
            return false;
        }
        if (Title.Trim().Length == 0 && code.Length == 0)
        {
            Error = "Give it a title or an address.";
            return false;
        }

        SaveSystemName(code);
        FindsService.Save(new Find
        {
            Id = _editingId ?? Guid.NewGuid().ToString("N"),
            CreatedAt = _editingCreatedAt,
            Type = type,
            Title = Title.Trim(),
            PortalCode = code,
            Galaxy = Galaxy,
            Class = FindsService.HasClass(type) ? SelectedClass?.Key ?? "" : "",
            Subtype = ShowKinds ? SelectedKind ?? "" : "",
            ItemIds = FindsService.HasItems(type) ? Items.Select(c => c.Id).ToList() : new(),
            Location = Location.Trim(),
            Notes = Notes.Trim(),
        });
        Reset();
        return true;
    }

    /// <summary>Stores a typed system name in the shared system notes (user.db), so the Planets page and every card for
    /// this system pick it up.</summary>
    private void SaveSystemName(string code)
    {
        string name = SystemName.Trim();
        if (code.Length != 12 || name.Length == 0) return;
        ulong system = S.ToSystemAddress(NmsPortalCode.ToAddress(code, Galaxy));
        var note = SystemNotesService.Get(system) ?? new SystemNote();
        if (note.SystemName == name) return;
        note.SystemName = name;
        SystemNotesService.Save(system, note);
    }

    public void Cancel()
    {
        Reset();
        SetOpen(false);
    }

    public void Reset()
    {
        _editingId = null;
        _editingCreatedAt = "";
        Title = "";
        Location = "";
        _autoLocation = "";
        Notes = "";
        Code = "";
        SystemName = "";
        Error = "";
        SelectedClass = ClassOptions[0];
        SelectedKind = null;
        Items.Clear();
        FormTitle = "ADD A FIND";
        SaveText = "Save find";
    }
}
