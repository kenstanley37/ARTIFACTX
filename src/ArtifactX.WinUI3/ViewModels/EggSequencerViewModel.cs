using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.Companions;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>An egg to sequence, or none (Egg null, Slot -1); Slot is its place in the save's egg array.</summary>
public sealed record EggChoice(string Label, EggCreature? Egg, int Slot = -1)
{
    public override string ToString() => Label;
}

/// <summary>An egg's creature as it is now, for the 3D preview: model rig, body parts and look seeds.</summary>
public sealed record EggPreviewLook(string Rig, IReadOnlyList<string> Parts, ulong Seed, ulong ColourSeed);

/// <summary>
/// The Egg Sequencer page: a copy of the game's sequencer screen. Pick a
/// catalyst slot to list only the items it takes, filtered by the effect
/// wanted; put one in and the slot shows the game's dose and, in plain words,
/// what will happen (EggSlotViewModel). Rules from Core EggSequencer and the
/// catalog's EGGSEQUENCER row, values from ItemValues. Nothing touches the save.
/// </summary>
public sealed partial class EggSequencerViewModel : ObservableObject
{
    private EggSequencerRules? _rules;
    private List<EggCatalyst> _items = [];
    private List<EggCatalystRowViewModel> _slotRows = [];
    private Dictionary<string, long> _owned = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<EggSlotViewModel> Slots { get; } = [];
    public ObservableCollection<EggCatalystRowViewModel> Items { get; } = [];

    [ObservableProperty] public partial EggSlotViewModel? SelectedSlot { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<EggCatalystFilter> FilterOptions { get; private set; } = [];
    [ObservableProperty] public partial EggCatalystFilter? Filter { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string Message { get; private set; } = "Loading the Egg Sequencer rules...";
    [ObservableProperty] public partial string Title { get; private set; } = "";
    [ObservableProperty] public partial string CatalystsHeading { get; private set; } = "";
    [ObservableProperty] public partial string StatusHeading { get; private set; } = "";
    [ObservableProperty] public partial string DoseNote { get; private set; } = "";
    [ObservableProperty] public partial bool IsReady { get; private set; }
    [ObservableProperty] public partial bool OnlyOwned { get; set; }
    /// <summary>Only items that give the slot's full effect (100%); with OnlyOwned, ones you carry enough of.</summary>
    [ObservableProperty] public partial bool OnlyFullDose { get; set; }
    [ObservableProperty] public partial bool HasSave { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<EggChoice> Eggs { get; private set; } = [];
    [ObservableProperty] public partial EggChoice? SelectedEgg { get; set; }
    /// <summary>The picked egg's look, or null for "Any egg" or a creature with no model.</summary>
    [ObservableProperty] public partial EggPreviewLook? Look { get; private set; }
    [ObservableProperty] public partial bool HasLook { get; private set; }
    /// <summary>0 = the egg now, 1 = after the chosen catalysts.</summary>
    [ObservableProperty] public partial int PreviewIndex { get; set; } = 1;
    /// <summary>True when a catalyst changes the chosen egg, so "After sequencing" means something.</summary>
    [ObservableProperty] public partial bool HasChanges { get; private set; }
    [ObservableProperty] public partial string PreviewCaption { get; private set; } = "";
    /// <summary>The egg's Battle Abilities (Helpers.Fauna.CreatureTraitText.Classes); sequencing and hatching keep them.</summary>
    [ObservableProperty] public partial ExploreViewModels.CreatureBattleViewModel? Battle { get; private set; }
    private EggPreviewLook? _nowLook;
    private int _lookVersion;

    public async Task LoadAsync()
    {
        try { await LoadRulesAsync(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EggSequencer] Load failed: {ex}");
            IsReady = false;
            Message = "The Egg Sequencer rules could not be read. Read the game data again in Settings.";
        }
    }

    private async Task LoadRulesAsync()
    {
        var rules = await CatalogService.GetEggSequencerRulesAsync();
        var values = await CatalogService.GetItemValuesAsync();
        if (rules is null || values.Count == 0)
        {
            Message = "Read the game data again in Settings to use the Egg Sequencer.";
            return;
        }
        _rules = rules;
        var full = values.FirstOrDefault(v => string.Equals(v.Id, rules.FullDoseProductId, StringComparison.OrdinalIgnoreCase));
        long fullDose = full?.Value ?? 0;
        _items = await Task.Run(() => EggSequencer.Catalysts(rules, values.Where(v => v.Kind is "Product" or "Substance")
            .Select(v => (v.Id, v.Name, v.Kind == "Substance", v.Value)), fullDose));
        await CatalogService.WarmCacheAsync(_items.Select(i => i.Id));
        _owned = InventoryTotalsService.Count();
        HasSave = _owned.Count > 0;
        OnlyOwned = HasSave;

        Title = Text("UI_EGG_MODIFIER_NAME_L");
        CatalystsHeading = Text("UI_EGG_MODIFIER_INPUT_HEADING");
        StatusHeading = Text("UI_EGG_MODIFIER_OUTPUT_HEADING");
        DoseNote = EggSequencerText.DoseNote(rules, fullDose, full?.Name);
        Slots.Clear();
        foreach (var slot in Enum.GetValues<EggCatalystSlot>())
        {
            var vm = new EggSlotViewModel(slot, rules, fullDose);
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(EggSlotViewModel.Result)) UpdateLook(); };
            Slots.Add(vm);
        }
        // The egg's name as the game shows it ("Mecha-Mouse", "Beary-Beary") and the inventory
        // holding it; a number only tells same-named eggs apart.
        var saved = CompanionService.ReadEggs(await CreatureNameService.LoadAsync()).ToList();
        var places = await CompanionService.EggPlacesAsync();
        string Label(int i)
        {
            var (slot, egg) = saved[i];
            string name = saved.Count(o => o.Egg.Name == egg.Name) > 1
                ? $"{egg.Name} {saved.Take(i + 1).Count(o => o.Egg.Name == egg.Name)}" : egg.Name;
            return places.TryGetValue(slot, out var place) ? $"{name} (in {place})" : name;
        }
        Eggs = (EggChoice[])[new EggChoice("Any egg (no exact numbers)", null),
            .. saved.Select((e, i) => new EggChoice(Label(i), e.Egg, e.Slot))];
        SelectedEgg = Eggs.Count > 1 ? Eggs[1] : Eggs[0];
        IsReady = true;
        Select(Slots[^1]);
    }

    /// <summary>Show the items for a slot.</summary>
    public void Select(EggSlotViewModel slot)
    {
        if (_rules is null) return;
        foreach (var s in Slots) s.IsSelected = s == slot;
        SelectedSlot = slot;
        _slotRows = _items.Select(i => new EggCatalystRowViewModel(i, slot.Slot, _rules, _owned.GetValueOrDefault(i.Id))
                { Icon = CatalogService.TryGet(i.Id)?.Icon })
            .ToList();
        FilterOptions = Options(slot.Slot);
        Filter = FilterOptions[0];
        Apply();
    }

    /// <summary>Put an item in the selected slot.</summary>
    public void Pick(EggCatalystRowViewModel row) => SelectedSlot?.SetItem(row);

    public void ClearSelectedSlot() => SelectedSlot?.SetItem(null);

    partial void OnFilterChanged(EggCatalystFilter? value) => Apply();
    partial void OnSelectedEggChanged(EggChoice? value)
    {
        foreach (var slot in Slots) slot.SetEgg(value?.Egg);
        _ = LoadLookAsync(value);
    }

    /// <summary>The picked egg's creature as it is now (CompanionService.EggLook), so a
    /// player who forgot the parent sees what they are sequencing.</summary>
    private async Task LoadLookAsync(EggChoice? choice)
    {
        int version = ++_lookVersion;
        EggPreviewLook? look = null;
        Helpers.Fauna.CreatureBattle? battle = null;
        if (choice is { Slot: >= 0 } && CompanionService.EggLook(choice.Slot) is { } egg)
        {
            if (await CreatureModelService.ResolveRigAsync(egg.Species, egg.Parts) is { Length: > 0 } rig)
                look = new EggPreviewLook(rig, egg.Parts, egg.Seed, egg.ColourSeed);
            await PlanetTypesService.EnsureLoadedAsync();
            battle = Helpers.Fauna.CreatureTraitText.Classes(egg.SpeciesSeed, egg.GenusSeed, PlanetTypesService.CreatureTables);
        }
        if (version != _lookVersion) return;
        Battle = battle is null ? null : new ExploreViewModels.CreatureBattleViewModel(battle);
        _nowLook = look;
        HasLook = look is not null;   // shown first, so the panel draws while visible
        UpdateLook();
    }

    partial void OnPreviewIndexChanged(int value) => UpdateLook();

    /// <summary>The look to draw and its caption: the egg now, or after the catalysts. A Dye Injector
    /// change gives the item's colour seed (EggSequencer.DyeColourSeed); the body seed and parts stay.
    /// Confirmed by Slot 8 save diffs 2026-11-15 (issue #71).</summary>
    private void UpdateLook()
    {
        if (_nowLook is not { } now) { Look = null; HasChanges = false; PreviewCaption = ""; return; }
        EggSlotViewModel? Slot(EggCatalystSlot s) => Slots.FirstOrDefault(x => x.Slot == s && x.Result is not null);
        var dye = Slot(EggCatalystSlot.DyeInjector);
        var growth = Slot(EggCatalystSlot.GrowthHormone);
        var splitter = Slot(EggCatalystSlot.GeneSplitter);
        HasChanges = Slots.Any(s => s.Result is not null);
        if (!HasChanges || PreviewIndex == 0)
        {
            Look = now;
            PreviewCaption = "The creature in this egg as it is now, before sequencing.";
            return;
        }

        var notes = new List<string>();
        var look = now;
        if (dye is { Item: { } dyeItem, Result: { } dyeResult })
        {
            look = now with { ColourSeed = EggSequencer.DyeColourSeed(dyeItem.Id) };
            notes.Add(dyeResult.ChangeChance >= 1 ? $"Colours from {dyeItem.Name}."
                : $"{dyeResult.ChangeChance:P0} chance of these colours from {dyeItem.Name}; otherwise it keeps its own.");
        }
        if (growth?.SizeAfter is float size && SelectedEgg?.Egg is { } egg) notes.Add($"Size {egg.Scale:0.##} becomes {size:0.##}.");
        if (splitter is not null) notes.Add("The Gene Splitter changes some body parts at random, so the parts shown are the ones it has now.");
        if (Slot(EggCatalystSlot.NeuralCalibrator) is not null) notes.Add("Personality changes are under What will happen.");
        Look = look;
        PreviewCaption = "After sequencing: " + string.Join(" ", notes);
    }
    partial void OnSearchTextChanged(string value) => Apply();
    partial void OnOnlyOwnedChanged(bool value) => Apply();
    partial void OnOnlyFullDoseChanged(bool value) => Apply();

    private IReadOnlyList<EggCatalystFilter> Options(EggCatalystSlot slot) =>
        EggSequencerText.Filters(slot, _slotRows.Select(r => r.Label));

    private void Apply()
    {
        if (_slotRows.Count == 0) return;
        string text = SearchText.Trim();
        var rows = _slotRows.Where(r => r.HasEffect)
            .Where(r => Filter?.Matches(r.Item, r.Label) ?? true)
            .Where(r => text.Length == 0 || r.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Where(r => !OnlyOwned || r.HasOwned)
            // A full dose (100%): with your own items, enough of them; otherwise one a slot can hold enough of.
            .Where(r => !OnlyFullDose || (r.ReachesFull && (!OnlyOwned || r.OwnsEnough)))
            .OrderByDescending(r => OnlyOwned && r.OwnsEnough)
            .ThenBy(r => r.Needed)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Items.Clear();
        foreach (var row in rows) Items.Add(row);
        Message = OnlyOwned
            ? $"{rows.Count:N0} of your items work in the {SelectedSlot?.Name}; ones you have enough of come first. Click one to put it in."
            : $"{rows.Count:N0} items work in the {SelectedSlot?.Name}, fewest needed first. Click one to put it in.";
    }

    private string Text(string key) => _rules is null ? key : EggSequencerText.Text(_rules, key);
}
