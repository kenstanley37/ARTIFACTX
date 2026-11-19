using ArtifactX.Almanac.Starship;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Helpers.Text;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The Starships page: the owned ships and the one on screen, the selector strip's chips (empty slots included), and
/// the selected ship's details (name, class, type, corvette note, reset state) and its tech and cargo grids. Reads and
/// edits go through StarshipService; the page wires the grids and tech list, the parts panel and 3D preview, and the
/// dialogs. A plain edits-changed reload keeps the selection; the first load picks the ship being flown.
/// </summary>
public sealed partial class ShipsPageViewModel : ObservableObject
{
    // Ship type/class -> slot counts (GameId -> CapacityValue), from the catalog once per page (StarshipCapacity).
    private Dictionary<string, int>? _capacity;
    private bool _loadingType;

    /// <summary>The owned ships (unnamed ones labelled once their game names are worked out).</summary>
    public IReadOnlyList<OwnedShip> Ships { get; private set; } = [];

    /// <summary>The ship picked in the strip, or -1.</summary>
    public int SelectedIndex { get; private set; } = -1;

    /// <summary>The ship whose details are on screen - a live update leaves it there unless it's the one that changed.</summary>
    public int ShownIndex { get; private set; } = -1;

    public OwnedShip? Selected => Ships.FirstOrDefault(s => s.Index == SelectedIndex);

    [ObservableProperty] public partial IReadOnlyList<EquipmentChipViewModel>? Chips { get; private set; }

    /// <summary>"N OF M SLOTS USED" over the strip.</summary>
    [ObservableProperty] public partial string SlotsText { get; private set; } = "";

    /// <summary>The selected ship's grids; null with no ship.</summary>
    public InventoryGridViewModel? Tech { get; private set; }
    public InventoryGridViewModel? Cargo { get; private set; }

    /// <summary>A corvette's upgrade modules show the part that gives them (ShipBaseService.ModulePartsAsync).</summary>
    public IReadOnlyDictionary<string, string>? ModulePartIcons { get; private set; }

    /// <summary>The tech this kind of ship takes, and the list's heading (StarshipService.TechRuleFor).</summary>
    public (string[] UsageCategories, string Header) TechRule { get; private set; } = ([], "");

    /// <summary>The name box, two-way: what it shows while the player types, staged by CommitName.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FoldedName))]
    public partial string Name { get; set; } = "";

    [ObservableProperty] public partial string NamePlaceholder { get; private set; } = "";

    /// <summary>"Update game data now" under the name: an unnamed ship whose game name needs newer game data.</summary>
    [ObservableProperty] public partial bool ShowNameUpdateLink { get; private set; }

    /// <summary>What the folded Details band says: the ship's name and type.</summary>
    public string FoldedName
    {
        get
        {
            string name = Name.Trim(), type = SelectedType?.DisplayName ?? "";
            return name.Length > 0 && type.Length > 0 ? $"{name}  ·  {type}" : name;
        }
    }

    /// <summary>The class the game shows: the Cargo copy (StarshipService.ReadClass).</summary>
    [ObservableProperty] public partial string? ClassLetter { get; private set; }

    [ObservableProperty] public partial bool HasClass { get; private set; }

    [ObservableProperty] public partial IReadOnlyList<StarshipTypeInfo> TypeOptions { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FoldedName))]
    public partial StarshipTypeInfo? SelectedType { get; set; }

    [ObservableProperty] public partial bool ShowCorvetteNote { get; private set; }
    [ObservableProperty] public partial string CorvetteNoteText { get; private set; } = "";

    [ObservableProperty] public partial bool HasPageEdits { get; private set; }
    [ObservableProperty] public partial string TechTitle { get; private set; } = "TECHNOLOGY";

    /// <summary>Set around a rename staged while typing, so the page skips the full reload for it (that would put the
    /// text back and move the cursor).</summary>
    public bool IsStagingName { get; private set; }

    /// <summary>Re-reads the owned ships and rebuilds the strip. Returns whether the ship on screen must be (re)loaded:
    /// always, except for a live update (the game added or dropped a ship) that didn't touch it.</summary>
    public bool Reload(bool live)
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            Ships = [];
            SelectedIndex = -1;
            Chips = null;
            SlotsText = "";
            Tech = null;
            Cargo = null;
            return false;
        }
        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath) is null) return false;
        Ships = StarshipService.ReadShips();
        if (SelectedIndex < 0 || Ships.All(s => s.Index != SelectedIndex))
            SelectedIndex = (Ships.FirstOrDefault(s => s.IsGameActive) ?? Ships.FirstOrDefault())?.Index ?? -1;
        BuildChips();
        return !live || ShownIndex != SelectedIndex || LiveItemsService.IsNew(LiveCollection.Ships, SelectedIndex);
    }

    /// <summary>Picks a ship in the strip. False for a new ship: marking it seen makes LiveItemsService reload the list.</summary>
    public bool Select(int index)
    {
        SelectedIndex = index;
        if (LiveItemsService.IsNew(LiveCollection.Ships, index))
        {
            LiveItemsService.Seen(LiveCollection.Ships, index);
            return false;
        }
        BuildChips();
        return true;
    }

    /// <summary>The selection is gone (a ship deleted, a new one added): pick again on the next reload.</summary>
    public void SelectAfterReload(int index = -1) => SelectedIndex = index;

    /// <summary>The ships, then the empty slots as faint placeholders, so how much room is left shows at a glance.</summary>
    private void BuildChips()
    {
        bool IsNew(int index) => LiveItemsService.IsNew(LiveCollection.Ships, index);
        var chips = Ships
            .Select(s => EquipmentChipViewModel.Item(s.Index, s.Name, s.Index == SelectedIndex, s.IsGameActive, isNew: IsNew(s.Index),
                animateIn: IsNew(s.Index) && LiveItemsService.TakeAnimation(LiveCollection.Ships, s.Index), isCorvette: s.IsCorvette))
            .Concat(LiveItemsService.Gone(LiveCollection.Ships).Select(EquipmentChipViewModel.Gone))
            .ToList();
        int free = NewFromTemplateService.FreeShipSlots;
        for (int i = 0; i < free; i++)
            chips.Add(EquipmentChipViewModel.EmptySlot("An empty starship slot. Design a ship for it in the Ship Designer, or use a Full Build template's Add as new.", i + 1, free));
        SlotsText = StarshipService.SlotsUsedText();
        Chips = chips;
    }

    /// <summary>Works out the game's names for unnamed ships in the background. True when one was found: the caller
    /// reloads the strip, then calls ShowGameName.</summary>
    public Task<bool> WorkOutGameNamesAsync() => StarshipService.WorkOutGameNamesAsync(Ships);

    /// <summary>After the game's names arrived: fills the name box when it shows an unnamed ship the player hasn't typed
    /// into meanwhile, and relabels the tech heading.</summary>
    public void ShowGameName(OwnedShip? before)
    {
        if (Selected is not { } after) return;
        if (after.IsUnnamed && before is { IsUnnamed: true } && string.IsNullOrEmpty(Name)) ShowName(after);
        TechTitle = Heading(after.Name);
    }

    private static string Heading(string name) => $"TECHNOLOGY  —  {HeadingText.Upper(name)}";

    /// <summary>Builds the selected ship's grids (rows from its type and class, StarshipCapacity) and details. False
    /// when another ship was picked meanwhile - that load fills the page.</summary>
    public async Task<bool> LoadSelectedAsync()
    {
        ShownIndex = SelectedIndex;
        ShowType();
        if (Selected is not { } ship)
        {
            Tech = null;
            Cargo = null;
            ModulePartIcons = null;
            TechTitle = "TECHNOLOGY";
            HasClass = false;
            Name = "";
            NamePlaceholder = "";
            ShowNameUpdateLink = false;
            ShowCorvetteNote = false;
            HasPageEdits = false;
            return true;
        }

        var (type, classLetter) = StarshipService.TypeAndClass(ship.Index);
        _capacity ??= await CatalogService.GetShipCapacityAsync();
        var tech = new InventoryGridViewModel(NmsInventoryContainer.ShipTechnologyPath(ship.Index), StarshipCapacity.Columns,
            StarshipCapacity.SlotsToRows(StarshipService.TechSlots(_capacity, type, classLetter)));
        var cargo = new InventoryGridViewModel(NmsInventoryContainer.ShipCargoPath(ship.Index), StarshipCapacity.Columns,
            StarshipCapacity.SlotsToRows(StarshipService.CargoSlots(_capacity, type, classLetter)));
        tech.Load();
        cargo.Load();
        var icons = ship.IsCorvette ? await ShipBaseService.ModulePartsAsync(ship.Index) : null;
        await CatalogService.WarmCacheAsync(tech.Cells.Concat(cargo.Cells).Where(c => c.IsOccupied).Select(c => c.ItemId)
            .Concat(icons?.Values ?? []));
        if (ship.Index != SelectedIndex) return false;

        Tech = tech;
        Cargo = cargo;
        ModulePartIcons = icons;
        TechRule = StarshipService.TechRuleFor(StarshipService.Scene(ship.Index));
        // The list may have been relabelled (game names) while this loaded.
        var current = Selected ?? ship;
        TechTitle = Heading(current.Name);
        HasPageEdits = StarshipService.HasEdits(ship.Index);
        ClassLetter = StarshipService.ReadClass(ship.Index);
        HasClass = true;
        ShowName(current);
        ShowCorvetteNote = current.IsCorvette;
        CorvetteNoteText = StarshipService.CorvetteNote(current);
        return true;
    }

    private void ShowType()
    {
        var (options, selected) = SelectedIndex < 0 ? (StarshipTypes.All, null) : StarshipService.TypeOptions(SelectedIndex);
        _loadingType = true;
        try
        {
            TypeOptions = options;
            SelectedType = selected;
        }
        finally { _loadingType = false; }
    }

    /// <summary>The name box: the player's name, or the one the game made up for an unnamed ship; with no game name, a
    /// placeholder that says why (game data from before the name words offers the update right there).</summary>
    private void ShowName(OwnedShip ship)
    {
        Name = ship.IsUnnamed ? ship.GameName ?? "" : ship.Name;
        bool oldData = GameDataService.Info is { } info && info.Format < ArtifactX.GameData.GameDataBuilder.CatalogFormat;
        ShowNameUpdateLink = ship.IsUnnamed && ship.GameName is null && oldData && !GameDataService.IsBuilding;
        NamePlaceholder = ship.IsUnnamed && ship.GameName is null
            ? (oldData ? "Update your game data to see the game's name" : "No name yet - type one to name this ship")
            : "";
    }

    public void HideNameUpdateLink() => ShowNameUpdateLink = false;

    /// <summary>Stages the name in the box; true when something was staged. While typing (typing: true) the page only
    /// relabels the strip, so this relabels the tech heading itself.</summary>
    public bool CommitName(bool typing)
    {
        if (Selected is not { } ship) return false;
        IsStagingName = typing;
        bool staged;
        try { staged = StarshipService.Rename(ship, Name); }
        finally { IsStagingName = false; }
        if (!staged) return false;
        MarkEdited();
        if (typing) TechTitle = Heading(Name.Trim());
        return true;
    }

    /// <summary>Stages a class on the selected ship - on Technology via its grid, then copied onto Cargo, which the game
    /// reads (StarshipService.SyncCargoClass). The caller rebuilds the grids: the class sets the slot counts.</summary>
    public void ChooseClass(string letter)
    {
        Tech?.SetClass(letter);
        StarshipService.SyncCargoClass(SelectedIndex);
        ClassLetter = letter;
        MarkEdited();
    }

    partial void OnSelectedTypeChanged(StarshipTypeInfo? value)
    {
        if (_loadingType || value is null || SelectedIndex < 0) return;
        StarshipService.SetType(SelectedIndex, value.ScenePath);
        MarkEdited(); // staging reloads the page, which redraws the parts and preview
    }

    public void MarkEdited() => HasPageEdits = true;

    /// <summary>Undoes every staged change to the selected ship - inventories, name, seed and stats.</summary>
    public void ResetSelected()
    {
        StarshipService.RevertShip(SelectedIndex);
        HasPageEdits = false;
    }
}
