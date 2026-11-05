using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The Catalog page, laid out like the game's Catalog screen: its sections, each
/// section's subcategories in the game's order, and the items as tiles that are
/// coloured when known. Known = the save's list + the section's always-known items
/// (NmsGameCatalogPaths; exact against the owner's Slot 1 and Slot 8 screens,
/// 2026-11-03). Technology edits the save's known technology (4kj), Building Parts
/// its known products (eZ&lt;), Recipes its known recipes (Ddk); every change is
/// staged until the player saves.
/// </summary>
public sealed partial class GameCatalogViewModel : ObservableObject
{
    private bool _staging;
    private int _iconVersion;

    public ObservableCollection<GameCatalogSectionViewModel> Sections { get; } = new();

    [ObservableProperty] public partial GameCatalogSectionViewModel? SelectedSection { get; set; }

    /// <summary>The selected section's subcategories. Its own property, set before
    /// SelectedCategory, so the list has its items before it is told which to select
    /// (bound through SelectedSection.Categories the first one showed unselected,
    /// owner report 2026-11-03).</summary>
    [ObservableProperty] public partial IReadOnlyList<GameCatalogCategoryViewModel> Categories { get; private set; } = [];
    [ObservableProperty] public partial GameCatalogCategoryViewModel? SelectedCategory { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool ShowLockedOnly { get; set; }
    [ObservableProperty] public partial IReadOnlyList<GameCatalogItemViewModel> VisibleItems { get; private set; } = [];
    [ObservableProperty] public partial bool? AllUnlocked { get; private set; }
    [ObservableProperty] public partial string HeaderText { get; private set; } = "";
    [ObservableProperty] public partial string ScopeCountText { get; private set; } = "";
    [ObservableProperty] public partial bool IsLoading { get; private set; } = true;
    [ObservableProperty] public partial bool NeedsGameData { get; private set; }
    [ObservableProperty] public partial bool HasChanges { get; private set; }

    public bool CanToggleAll => SelectedSection?.TracksKnown == true && Scope().Any(i => i.CanEdit);

    /// <summary>True while this page is staging, so its own edits don't trigger a reload.</summary>
    public bool IsStaging => _staging;

    public async Task LoadAsync()
    {
        IsLoading = true;
        string? keepSection = SelectedSection?.Key;
        string? keepCategory = SelectedCategory?.Name;
        var catalogue = await CatalogueSectionsService.LoadAsync();
        Sections.Clear();
        NeedsGameData = catalogue.Sections.Length == 0;
        if (!SaveSessionManager.IsSaveLoaded || NeedsGameData)
        {
            IsLoading = false;
            return;
        }

        var tech = new UnlockListViewModel(UnlockListStore.ForSave(NmsGameCatalogPaths.KnownTechPath));
        var products = new UnlockListViewModel(UnlockListStore.ForSave(NmsGameCatalogPaths.KnownProductsPath));
        var recipes = new UnlockListViewModel(UnlockListStore.ForSave(NmsGameCatalogPaths.KnownRecipesPath));
        tech.Load();
        products.Load();
        recipes.Load();
        var specials = (SaveSessionManager.GetValue(NmsGameCatalogPaths.KnownSpecialsPath) as JArray)?
            .Select(t => CatalogService.NormalizeId(t.Value<string>() ?? "")).ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var fish = await WonderFishAsync(catalogue.FishOrder);
        var species = await CreatureNameService.LoadAsync();
        foreach (var section in catalogue.Sections)
        {
            if (section.Key == "UI_PORTAL_CAT_COLLECTION")
            {
                Sections.Add(new GameCatalogSectionViewModel(section,
                    section.Categories.Select(c => WonderCategory(c, catalogue, fish, species)).ToList(), null, tracksKnown: true));
                continue;
            }
            UnlockListViewModel? list = section.Key switch
            {
                "UI_PORTAL_CAT_CRAFTING" => tech,
                "UI_PORTAL_CAT_BUILDING" => products,
                "UI_PORTAL_CAT_RECIPES" => recipes,
                _ => null,
            };
            var always = section.AlwaysKnown.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var categories = section.Categories.Select(c => new GameCatalogCategoryViewModel(c.Name, c.Items.Select(id =>
            {
                // Three building parts have no name in the game's text (U_SWITCHWALL...);
                // they still count, so they're kept under a plain label, never their id.
                string name = catalogue.Names.GetValueOrDefault(id, "Unnamed part");
                if (list is null) return new GameCatalogItemViewModel(id, name, known: true, canEdit: false, showState: false, null);
                // A Recipes tile is a result: learning it learns every recipe that makes it.
                if (c.ItemRecipes?.GetValueOrDefault(id) is { } made)
                    return new GameCatalogItemViewModel(id, name, list.AnyUnlocked(made), true, true, null, made);
                if (always.Contains(id)) return new GameCatalogItemViewModel(id, name, true, false, true, "always known in the game");
                if (!list.IsUnlocked(id) && specials.Contains(id))
                    return new GameCatalogItemViewModel(id, name, true, false, true, "unlocked as a special reward");
                return new GameCatalogItemViewModel(id, name, list.IsUnlocked(id), true, true, null);
            }).ToList())).ToList();
            Sections.Add(new GameCatalogSectionViewModel(section, categories, list, tracksKnown: list is not null));
        }

        IsLoading = false;
        SelectedSection = Sections.FirstOrDefault(s => s.Key == keepSection) ?? Sections.FirstOrDefault(s => s.TracksKnown) ?? Sections.FirstOrDefault();
        if (keepCategory is not null && SelectedSection?.Categories.FirstOrDefault(c => c.Name == keepCategory) is { } category)
            SelectedCategory = category;
        UpdateChanges();
    }

    /// <summary>Fishing Records: the 210 species and 9 flotsam the game lists (owner's
    /// screenshots, 219 tiles), known once caught (count above 0 in the fishing record).</summary>
    private static async Task<(List<(string Id, string Name)> Items, HashSet<string> Caught)> WonderFishAsync(Dictionary<string, int> order)
    {
        var all = await CatalogService.GetAllUnlockableItemsAsync();
        var items = CatalogService.FilterFishSpecies(all).Concat(CatalogService.FilterFlotsam(all))
            .OrderBy(i => order.GetValueOrDefault(i.GameId, int.MaxValue))
            .Select(i => (i.GameId, DisplayCase(i.DisplayName))).ToList();
        var ids = (SaveSessionManager.GetValue(NmsFishingPaths.ProductListPath) as JArray)?.Select(t => CatalogService.NormalizeId(t.Value<string>() ?? "")).ToList() ?? new();
        var counts = (SaveSessionManager.GetValue(NmsFishingPaths.ProductCountListPath) as JArray)?.Select(t => t.Value<int>()).ToList() ?? new();
        var caught = ids.Zip(counts).Where(p => p.First.Length > 0 && p.Second > 0).Select(p => p.First).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (items, caught);
    }

    /// <summary>The catalog's capitals ("ABERRANT DUSKFIN") in the game's mixed case.</summary>
    private static string DisplayCase(string name) => name.Any(char.IsLower) ? name
        : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());

    /// <summary>A Wonders category, read-only: the game records these as you play.</summary>
    private static GameCatalogCategoryViewModel WonderCategory(Core.Unlocks.CatalogueCategory category, CatalogueSectionsService.Catalogue catalogue,
        (List<(string Id, string Name)> Items, HashSet<string> Caught) fish, CreatureNameService species)
    {
        var names = catalogue.Names;
        var nameUses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? SlotItem(string name)
        {
            // Repeated slot names ("Terrifying Sample" twice) take that name's items in order.
            int use = nameUses.GetValueOrDefault(name);
            nameUses[name] = use + 1;
            return catalogue.IdsByName.TryGetValue(name, out var ids) && ids.Count > 0 ? ids[Math.Min(use, ids.Count - 1)] : null;
        }
        List<GameCatalogItemViewModel> items;
        if (category.Type == "Fish")
            items = fish.Items.Select(f => new GameCatalogItemViewModel(f.Id, f.Name, fish.Caught.Contains(f.Id), false, true,
                "caught in the game; edit catches on Fishing Records")).ToList();
        else if (category.Type == "ExhibitBones")
            items = category.Items.Select(id => new GameCatalogItemViewModel(id, names.GetValueOrDefault(id, "Unnamed fossil"), true, false, true,
                "always known in the game")).ToList();
        else
        {
            var records = NmsGameCatalogPaths.WonderRecordPaths.TryGetValue(category.Type, out var path)
                ? SaveSessionManager.GetValue(path) as JArray : null;
            var personal = category.Type == "CustomWonders" ? SaveSessionManager.GetValue(NmsGameCatalogPaths.PersonalRecordNamesPath) as JArray : null;
            items = category.Items.Select((id, i) =>
            {
                // A Personal Record carries the name the player gave it in game.
                string name = category.ItemNames?.GetValueOrDefault(id) is { Length: > 0 } n ? n
                    : personal?.ElementAtOrDefault(i)?["fH8"]?.Value<string>() is { Length: > 0 } own ? own
                    : $"Personal Record {i + 1}";
                var generation = records?.ElementAtOrDefault(i)?["UqY"] as JArray;
                var (recorded, detail, iconId) = ReadRecord(category.Type, generation);
                if (category.Type is "TreasureWonders" or "WeirdBasePartWonders") iconId ??= SlotItem(name);
                return new GameCatalogItemViewModel(id, name, recorded, false, true, "recorded by the game as you explore",
                    detail: detail, iconId: iconId, species: recorded ? SpeciesOf(category.Type, generation, species) : null);
            }).ToList();
        }
        return new GameCatalogCategoryViewModel(category.Name, items, category.Counted);
    }

    /// <summary>The species name the game shows on a Fauna or Flora record.</summary>
    private static string? SpeciesOf(string type, JArray? generation, CreatureNameService species)
    {
        ulong? first = NmsSpaceStationPaths.ParsePackedAddress(generation?.ElementAtOrDefault(0));
        ulong? second = NmsSpaceStationPaths.ParsePackedAddress(generation?.ElementAtOrDefault(1));
        return type switch
        {
            "CreatureWonders" when first is ulong address && second is ulong seed => species.FaunaRecord(address, seed),
            "FloraWonders" when second is ulong seed => species.FloraRecord(seed),
            _ => null,
        };
    }

    /// <summary>A record's GenerationID pair (UqY). Treasures and Glitches hold the
    /// recorded item's id as 16 ASCII bytes ("PROC_LOOT#06686", Slot 8 2026-11-03), so
    /// the tile shows that item's picture (and a glitch its name). Planet, fauna, flora
    /// and mineral records hold the planet's packed universe address first (planet
    /// number in bits 52-55, as NmsSpaceStationPaths.ToSystemAddress clears), so the
    /// tile names the planet. A treasure's line is the name the game generates from
    /// the item's seed ("Shiny Bones", CatalogService.ProceduralProductName).</summary>
    private static (bool Recorded, string? Detail, string? IconId) ReadRecord(string type, JArray? generation)
    {
        static ulong Value(JToken? token) => token is null ? 0
            : token.Type == JTokenType.Integer ? (ulong)token.Value<long>()
            : ulong.TryParse((token.Value<string>() ?? "").Replace("0x", "", StringComparison.OrdinalIgnoreCase),
                System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;
        ulong first = Value(generation?.ElementAtOrDefault(0)), second = Value(generation?.ElementAtOrDefault(1));
        if (first == 0 && second == 0) return (false, null, null);
        if (type is "TreasureWonders" or "WeirdBasePartWonders")
        {
            var bytes = BitConverter.GetBytes(first).Concat(BitConverter.GetBytes(second)).TakeWhile(b => b != 0).ToArray();
            string itemId = System.Text.Encoding.ASCII.GetString(bytes);
            string baseId = itemId.Split('#')[0];
            string? detail = type == "WeirdBasePartWonders" ? CatalogService.TryGet(baseId)?.DisplayName
                : CatalogService.ProceduralProductName(itemId);
            return (true, detail, CatalogService.TryGet(baseId) is null ? null : baseId);
        }
        int planet = (int)((first >> 52) & 0xF);
        if (planet == 0) return (true, null, null);
        return (true, $"{UniverseInfo.PlanetName(NmsSpaceStationPaths.ToSystemAddress(first), planet)} · {UniverseInfo.SystemName(first)}", null);
    }

    partial void OnSelectedSectionChanged(GameCatalogSectionViewModel? value)
    {
        foreach (var section in Sections) section.IsSelected = section == value;
        Categories = value?.Categories ?? [];
        SelectedCategory = Categories.FirstOrDefault(c => c.Items.Count > 0);
        ApplyFilter();
    }

    /// <summary>A section tile was clicked. The tile is a ToggleButton that toggles
    /// itself, so every tile's state is restored from IsSelected afterwards.</summary>
    public void SelectSection(GameCatalogSectionViewModel section)
    {
        SelectedSection = section;
        foreach (var each in Sections) each.NotifySelected();
    }

    partial void OnSelectedCategoryChanged(GameCatalogCategoryViewModel? value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnShowLockedOnlyChanged(bool value) => ApplyFilter();

    /// <summary>The items the tick-all and search act on: the selected subcategory, or
    /// every subcategory of the section while searching.</summary>
    private IEnumerable<GameCatalogItemViewModel> Scope()
    {
        string search = SearchText.Trim();
        if (search.Length > 0 && SelectedSection is { } section)
            return section.AllItems.Where(i => i.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        return SelectedCategory?.Items ?? Enumerable.Empty<GameCatalogItemViewModel>();
    }

    private void ApplyFilter()
    {
        var scope = Scope().ToList();
        HeaderText = SearchText.Trim().Length > 0 ? $"Results in {SelectedSection?.Title}" : SelectedCategory?.Name ?? "";
        VisibleItems = ShowLockedOnly ? scope.Where(i => !i.IsUnlocked).ToList() : scope;
        RefreshScope(scope);
        _ = LoadIconsAsync(VisibleItems);
    }

    private void RefreshScope(List<GameCatalogItemViewModel> scope)
    {
        var editable = scope.Where(i => i.CanEdit).ToList();
        int known = editable.Count(i => i.IsUnlocked);
        AllUnlocked = editable.Count == 0 ? null : known == editable.Count ? true : known == 0 ? false : null;
        ScopeCountText = SelectedSection?.TracksKnown == true
            ? $"{scope.Count(i => i.IsUnlocked):N0} of {scope.Count:N0} known"
            : $"{scope.Count:N0} items";
        OnPropertyChanged(nameof(CanToggleAll));
    }

    private async Task LoadIconsAsync(IReadOnlyList<GameCatalogItemViewModel> items)
    {
        int version = ++_iconVersion;
        var missing = items.Where(i => i.Icon is null).ToList();
        if (missing.Count == 0) return;
        await CatalogService.WarmCacheAsync(missing.Select(i => (string?)i.IconId));
        if (version != _iconVersion) return;
        foreach (var item in missing)
        {
            var entry = CatalogService.TryGet(item.IconId);
            item.Icon = entry?.Icon;
            if (item.ShowState && entry?.IconPng is { } png) item.Silhouette = await Helpers.Catalog.SilhouetteImages.FromPngAsync(png);
            if (version != _iconVersion) return;
        }
    }

    public void SetItem(GameCatalogItemViewModel item, bool known)
    {
        if (!item.CanEdit || SelectedSection?.List is not { } list) return;
        Stage(() => list.Set([item], known));
    }

    /// <summary>A mixed or empty scope learns everything; only a fully known one forgets it.</summary>
    public void ToggleAll()
    {
        if (SelectedSection?.List is not { } list) return;
        bool learn = AllUnlocked != true;
        Stage(() => list.Set(Scope().Where(i => i.CanEdit).ToList(), learn));
        OnPropertyChanged(nameof(AllUnlocked)); // the checkbox toggled itself; restore the real state
    }

    private void Stage(Action stage)
    {
        _staging = true;
        try { stage(); }
        finally { _staging = false; }
        SelectedSection?.Refresh();
        RefreshScope(Scope().ToList());
        UpdateChanges();
    }

    private static readonly string[][] EditedPaths =
        [NmsGameCatalogPaths.KnownTechPath, NmsGameCatalogPaths.KnownProductsPath, NmsGameCatalogPaths.KnownRecipesPath];

    public void UpdateChanges() => HasChanges = EditedPaths.Any(SaveSessionManager.HasStagedEditsUnder);

    /// <summary>Undoes this page's staged edits (known technology, products and recipes) only.</summary>
    public async Task ResetAsync()
    {
        foreach (var path in EditedPaths) SaveSessionManager.RevertEdit(path);
        await LoadAsync();
    }
}
