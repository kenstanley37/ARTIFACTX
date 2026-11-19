using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The Multi-Tool page: the owned tools and the one on screen, the selector strip's chips, and the selected tool's
/// details (name, class, type, reset state) and tech grid. Reads and edits go through MultiToolService; the page wires
/// the grid and tech list, shows dialogs and draws the 3D preview. A plain edits-changed reload keeps the selection, so
/// work on a non-equipped tool isn't yanked away; the first load picks the tool in hand.
/// </summary>
public sealed partial class MultiToolPageViewModel : ObservableObject
{
    private Dictionary<string, int>? _capacity;
    private bool _labelsPending;
    private bool _loadingType;

    /// <summary>The owned tools (labelled once LabelUnnamedAsync has run).</summary>
    public IReadOnlyList<OwnedMultiTool> Tools { get; private set; } = [];

    /// <summary>The tool picked in the strip, or -1.</summary>
    public int SelectedIndex { get; private set; } = -1;

    /// <summary>The tool whose details are on screen - a live update leaves it there unless it's the one that changed.</summary>
    public int ShownIndex { get; private set; } = -1;

    public OwnedMultiTool? Selected => Tools.FirstOrDefault(t => t.Index == SelectedIndex);

    [ObservableProperty] public partial IReadOnlyList<EquipmentChipViewModel>? Chips { get; private set; }

    /// <summary>The selected tool's tech grid; null with no tool.</summary>
    [ObservableProperty] public partial InventoryGridViewModel? Tech { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FoldedName))]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FoldedName))]
    public partial string NamePlaceholder { get; private set; } = "";

    /// <summary>"Update game data now" under the name: an unnamed tool whose game name needs newer game data.</summary>
    [ObservableProperty] public partial bool ShowNameUpdateLink { get; private set; }

    /// <summary>What the folded Details band says: the name, or the game's name for an unnamed tool.</summary>
    public string FoldedName => Name.Trim() is { Length: > 0 } n ? n : NamePlaceholder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClassBadgeText))]
    [NotifyPropertyChangedFor(nameof(ClassBadgeBrush))]
    [NotifyPropertyChangedFor(nameof(HasClass))]
    public partial string? ClassLetter { get; private set; }

    public bool HasClass => Tech is not null;
    public string ClassBadgeText => ClassLetter ?? "—";

    /// <summary>The game's own tiering as a visual cue (gold S, purple A, blue B, green C) - a local mapping.</summary>
    public Brush ClassBadgeBrush => ClassLetter switch
    {
        "S" => new SolidColorBrush(Color.FromArgb(255, 201, 151, 34)),
        "A" => new SolidColorBrush(Color.FromArgb(255, 130, 80, 200)),
        "B" => new SolidColorBrush(Color.FromArgb(255, 60, 110, 200)),
        "C" => new SolidColorBrush(Color.FromArgb(255, 60, 150, 90)),
        _ => ThemeResources.Brush("ThemePanelAltBrush"),
    };

    [ObservableProperty] public partial IReadOnlyList<MultiToolTypeOption> TypeOptions { get; private set; } = [];
    [ObservableProperty] public partial MultiToolTypeOption? SelectedType { get; set; }

    /// <summary>"Set by the tool's seed" under Type: the main model's kind comes from the seed (MultiToolKinds).</summary>
    [ObservableProperty] public partial bool ShowKindNote { get; private set; }

    [ObservableProperty] public partial bool HasPageEdits { get; private set; }
    [ObservableProperty] public partial string TechTitle { get; private set; } = "TECHNOLOGY";

    /// <summary>Re-reads the owned tools and rebuilds the strip. Returns whether the tool on screen must be (re)loaded:
    /// always, except for a live update (the game added or dropped a tool) that didn't touch it.</summary>
    public bool Reload(bool live)
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            Tools = [];
            SelectedIndex = -1;
            Chips = null;
            Tech = null;
            return false;
        }
        if (SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolArrayPath) is null) return false;
        Tools = MultiToolService.ReadTools();
        if (SelectedIndex < 0 || Tools.All(t => t.Index != SelectedIndex))
            SelectedIndex = (Tools.FirstOrDefault(t => t.IsGameActive) ?? Tools.FirstOrDefault())?.Index ?? -1;
        // Unnamed tools are relabelled a moment later, rebuilding the strip; a new tool's slide-in waits for that.
        _labelsPending = Tools.Any(t => t.IsUnnamed);
        BuildChips();
        return !live || ShownIndex != SelectedIndex || LiveItemsService.IsNew(LiveCollection.MultiTools, SelectedIndex);
    }

    /// <summary>Picks a tool in the strip. False for a new tool: marking it seen makes LiveItemsService reload the list.</summary>
    public bool Select(int index)
    {
        SelectedIndex = index;
        if (LiveItemsService.IsNew(LiveCollection.MultiTools, index))
        {
            LiveItemsService.Seen(LiveCollection.MultiTools, index);
            return false;
        }
        BuildChips();
        return true;
    }

    /// <summary>The selection is gone (a tool deleted, a new one added): pick again on the next reload.</summary>
    public void SelectAfterReload(int index = -1) => SelectedIndex = index;

    private void BuildChips()
    {
        bool IsNew(int index) => LiveItemsService.IsNew(LiveCollection.MultiTools, index);
        Chips = Tools
            .Select(t => EquipmentChipViewModel.Item(t.Index, t.Name, t.Index == SelectedIndex, t.IsGameActive, isNew: IsNew(t.Index),
                animateIn: IsNew(t.Index) && !_labelsPending && LiveItemsService.TakeAnimation(LiveCollection.MultiTools, t.Index)))
            .Concat(LiveItemsService.Gone(LiveCollection.MultiTools).Select(EquipmentChipViewModel.Gone))
            .ToList();
    }

    /// <summary>Names the unnamed tools (MultiToolService.LabelUnnamedAsync) and shows them; dropped if the list was
    /// rebuilt meanwhile.</summary>
    public async Task LabelUnnamedAsync()
    {
        var tools = Tools;
        if (!tools.Any(t => t.IsUnnamed)) return;
        var labelled = await MultiToolService.LabelUnnamedAsync(tools);
        if (!ReferenceEquals(tools, Tools)) return;
        Tools = labelled;
        _labelsPending = false;
        BuildChips();
        if (Selected is { IsUnnamed: true } selected) ShowName(selected);
    }

    /// <summary>Builds the selected tool's grid (rows from its class's slot count, MultiToolCapacity) and details.
    /// Edits go to SuJ[i].OsQ: Kgt, which looked safer for the tool in hand, is a mirror the game rebuilds from it -
    /// edits written there never showed in game.</summary>
    public async Task LoadSelectedAsync()
    {
        ShownIndex = SelectedIndex;
        if (Selected is not { } tool)
        {
            Tech = null;
            Name = "";
            NamePlaceholder = "";
            ShowNameUpdateLink = false;
            ClassLetter = null;
            OnPropertyChanged(nameof(HasClass));
            _loadingType = true;
            SelectedType = null;
            _loadingType = false;
            ShowKindNote = false;
            TechTitle = "TECHNOLOGY";
            HasPageEdits = false;
            return;
        }
        _capacity ??= await CatalogService.GetMultiToolCapacityAsync();
        int rows = MultiToolCapacity.SlotsToRows(MultiToolService.TechSlots(_capacity, MultiToolService.ClassLetter(tool.Index)));
        var tech = new InventoryGridViewModel(NmsInventoryContainer.MultiToolTechnologyPath(tool.Index), MultiToolCapacity.Columns, rows);
        tech.Load();
        await CatalogService.WarmCacheAsync(tech.Cells.Where(c => c.IsOccupied).Select(c => c.ItemId));
        if (tool.Index != SelectedIndex) return; // another tool was picked meanwhile
        Tech = tech;
        ClassLetter = tech.CurrentClass;
        OnPropertyChanged(nameof(HasClass));
        HasPageEdits = MultiToolService.HasEdits(tool.Index);
        // The list may have been relabelled (LabelUnnamedAsync) while this loaded.
        ShowName(Selected ?? tool);

        var (options, isMain) = await MultiToolService.TypeOptionsAsync(tool.Index);
        if (tool.Index != SelectedIndex) return;
        _loadingType = true;
        try
        {
            TypeOptions = options;
            string? scene = MultiToolService.Scene(tool.Index);
            SelectedType = options.FirstOrDefault(o => string.Equals(o.ScenePath, scene, StringComparison.OrdinalIgnoreCase));
        }
        finally { _loadingType = false; }
        ShowKindNote = isMain;
    }

    /// <summary>The name box: the player's name, or the game's for an unnamed tool; with no game name, a placeholder
    /// that says why (game data from before the name words, format 11, offers the update right there).</summary>
    private void ShowName(OwnedMultiTool tool)
    {
        Name = tool.IsUnnamed ? tool.GameName ?? "" : tool.Name;
        bool oldData = GameDataService.Info is { } info && info.Format < ArtifactX.GameData.GameDataBuilder.CatalogFormat;
        ShowNameUpdateLink = tool.IsUnnamed && tool.GameName is null && oldData && !GameDataService.IsBuilding;
        NamePlaceholder = tool.IsUnnamed && tool.GameName is null
            ? (oldData ? "Update your game data to see the game's name" : "The game's own name couldn't be worked out")
            : "";
        TechTitle = $"TECHNOLOGY  —  {tool.Name.ToUpperInvariant()}";
    }

    public void HideNameUpdateLink() => ShowNameUpdateLink = false;

    /// <summary>Stages the typed name when the box loses focus; true when something was staged (the strip needs a reload).</summary>
    public bool CommitName(string? text)
    {
        if (Selected is not { } tool || !MultiToolService.Rename(tool, text)) return false;
        MarkEdited();
        return true;
    }

    /// <summary>Stages a class on the selected tool; the caller rebuilds the grid (the class sets the slot count).</summary>
    public void ChooseClass(string letter)
    {
        Tech?.SetClass(letter);
        ClassLetter = letter;
        MarkEdited();
    }

    partial void OnSelectedTypeChanged(MultiToolTypeOption? value)
    {
        if (_loadingType || value is null || SelectedIndex < 0) return;
        MultiToolService.SetType(SelectedIndex, value.ScenePath);
        MarkEdited(); // staging reloads the page, which redraws the preview
    }

    public void MarkEdited() => HasPageEdits = true;

    /// <summary>Undoes every staged change to the selected tool - tech, name and type.</summary>
    public void ResetSelected()
    {
        MultiToolService.RevertTool(SelectedIndex);
        HasPageEdits = false;
    }
}
