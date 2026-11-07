using ArtifactX.Core.Collections;
using System.Collections.ObjectModel;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.Universe;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One installed technology on a compact cabinet card: its icon and, for upgrade modules, class badge.</summary>
public sealed class StationToolTechChip(string name, Microsoft.UI.Xaml.Media.ImageSource? icon, string? classLetter)
{
    public string Name { get; } = name;
    public Microsoft.UI.Xaml.Media.ImageSource? Icon { get; } = icon;
    public string ClassLetter { get; } = classLetter ?? "";
    public bool HasClass { get; } = classLetter is not null;
    public Microsoft.UI.Xaml.Media.Brush ClassBrush { get; } = ThemeResources.ClassBrush(classLetter);
    public string Label { get; } = classLetter switch
    {
        null => name,
        "!" => $"{name} - Rusted (!)-class upgrade",
        "?" => $"{name} - (?)-class upgrade",
        _ => $"{name} - {classLetter}-class upgrade",
    };
}

public sealed partial class StationReloadToolCard(string name, string rating, string damage, string scanner, string position,
    string slotSummary, InventoryGridSnapshot inventory, string modelPath, ulong modelSeed, ulong siteSeed) : ObservableObject
{
    public IReadOnlyList<StationToolTechChip> Technologies { get; } = inventory.Cells
        .Where(c => c.IsOccupied && c.ItemId is not null)
        .Select(c => new StationToolTechChip(c.DisplayName ?? CatalogService.TryGet(c.ItemId)?.DisplayName ?? c.ShortLabel,
            CatalogService.TryGet(c.ItemId)?.Icon, CatalogService.ModuleClassLetter(c.ItemId)))
        .ToArray();
    public string Name { get; } = name;
    public string Rating { get; } = rating;
    /// <summary>Empty when damage is unsupported (pirate cabinets without a live preview state).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasDamage))] public partial string Damage { get; set; } = damage;
    public bool HasDamage => Damage.Length > 0;
    [ObservableProperty] public partial string Scanner { get; set; } = scanner;
    public string Position { get; } = position;
    public string SlotSummary { get; } = slotSummary;
    public InventoryGridSnapshot Inventory { get; } = inventory;
    public string ModelPath { get; } = modelPath;
    public ulong ModelSeed { get; } = modelSeed;
    public ulong SiteSeed { get; } = siteSeed;
    public (ulong Site, ulong Model, string Path) Identity => (SiteSeed, ModelSeed, ModelPath);
    public string DisplayKey => $"{ModelPath}|{ModelSeed}|{Name}|{Rating}|{Position}|{SlotSummary}|"
        + string.Join(";", Inventory.Cells.Select(c => $"{c.X},{c.Y},{c.State},{c.ItemId},{c.IsSupercharged},{c.DisplayName}"));

    [ObservableProperty] public partial bool IsSelected { get; set; }
    public Microsoft.UI.Xaml.Media.Brush CardBorder => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Microsoft.UI.Xaml.Thickness CardBorderThickness => new(IsSelected ? 2 : 1);
    public Microsoft.UI.Xaml.Media.Brush CardBackground => ThemeResources.Brush(IsSelected ? "ThemeSelectedFillBrush" : "ThemeVoidBrush");

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardBorder));
        OnPropertyChanged(nameof(CardBorderThickness));
        OnPropertyChanged(nameof(CardBackground));
    }
}

/// <summary>One choice of the station reload pool: Space Station (native) or a planet in the system.</summary>
public sealed record StationReloadPoolOption(string Label, int Body)
{
    public override string ToString() => Label;
}

/// <summary>Supported station cabinet records for the current system across any reload location in the system.</summary>
public sealed partial class StationMultiToolsViewModel : ObservableObject
{
    private int _stationLoadVersion;
    private ulong? _displaySystem;
    private object? _displaySession;
    private Newtonsoft.Json.Linq.JObject? _cachedSaveInputs;
    private bool _rebuildingPoolOptions;

    public ObservableCollection<StationReloadToolCard> StationReloadTools { get; } = new();
    [ObservableProperty] public partial bool HasStationReloadTools { get; private set; }
    [ObservableProperty] public partial string StationReloadHint { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<StationReloadPoolOption> PoolOptions { get; private set; } = [];
    [ObservableProperty] public partial StationReloadPoolOption? SelectedPoolOption { get; set; }

    partial void OnSelectedPoolOptionChanged(StationReloadPoolOption? value)
    {
        if (_rebuildingPoolOptions || value is null || _displaySystem is null || _cachedSaveInputs is null) return;
        _ = LoadForPoolAsync(_displaySystem.Value, value.Body, _cachedSaveInputs);
    }

    public async Task UpdateAsync(ulong? system, PlanetSnapshot snapshot)
    {
        int version = ++_stationLoadVersion;
        var session = SaveSessionManager.SessionIdentity;
        bool systemChanged = _displaySystem != system || !ReferenceEquals(session, _displaySession);
        if (systemChanged)
        {
            StationReloadTools.Clear();
            HasStationReloadTools = false;
            _displaySystem = system;
            _displaySession = session;
        }
        if (system is null) return;
        try
        {
            var diskSnapshot = snapshot.EquipmentSaveInputs is null ? await LiveSaveWatcher.GetLatestAsync() : snapshot;
            if (version != _stationLoadVersion || !ReferenceEquals(session, SaveSessionManager.SessionIdentity)
                || diskSnapshot.EquipmentSaveInputs is null) return;

            _cachedSaveInputs = diskSnapshot.EquipmentSaveInputs;

            int savedBody = CabinetPoolService.SavedLoadBody(system.Value, diskSnapshot.EquipmentSaveInputs) ?? 0;

            if (systemChanged || PoolOptions.Count == 0)
            {
                var (code, galaxy) = UniverseInfo.Generator(system.Value);
                var attributes = SystemGenerator.Attributes(code, galaxy);
                int count = attributes.PlanetCount + attributes.PrimePlanetCount;
                var options = new List<StationReloadPoolOption>
                {
                    new("Space Station (native pool)", 0)
                };
                for (int i = 1; i <= count; i++)
                {
                    string planetName = UniverseInfo.PlanetName(system.Value, i);
                    options.Add(new($"{planetName} (Planet {i})", i));
                }
                _rebuildingPoolOptions = true;
                PoolOptions = options;
                SelectedPoolOption = options.FirstOrDefault(o => o.Body == savedBody) ?? options[0];
                _rebuildingPoolOptions = false;
            }

            int activeBody = SelectedPoolOption?.Body ?? savedBody;
            await LoadForPoolAsync(system.Value, activeBody, diskSnapshot.EquipmentSaveInputs);
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationMultiTools] Saved reload station unavailable: {error.Message}");
        }
    }

    private async Task LoadForPoolAsync(ulong system, int body, Newtonsoft.Json.Linq.JObject saveInputs)
    {
        int version = ++_stationLoadVersion;
        var session = _displaySession;
        try
        {
            var tools = await LocatedMultiToolService.GenerateStationForBodyAsync(system, body, saveInputs);
            if (version != _stationLoadVersion || !ReferenceEquals(session, SaveSessionManager.SessionIdentity)) return;
            await CatalogService.WarmCacheAsync(tools.SelectMany(t => t.Inventory.Technologies).Select(t => t.Id));
            if (version != _stationLoadVersion || !ReferenceEquals(session, SaveSessionManager.SessionIdentity)) return;

            var cards = tools.Select(tool => new StationReloadToolCard(tool.Contents.Tool.DefaultName,
                tool.Contents.RatingIndex switch { 0 => "C-Class", 1 => "B-Class", 2 => "A-Class", 3 => "S-Class", _ => "" },
                tool.HasSupportedDamagePotential ? tool.DamagePotential.ToString("0.0", CultureInfo.InvariantCulture) : "",
                tool.ScannerRange.ToString("0.0", CultureInfo.InvariantCulture),
                string.Create(CultureInfo.InvariantCulture,
                    $"System position: X {tool.Contents.Location.WorldPosition.X:0.00}, Y {tool.Contents.Location.WorldPosition.Y:0.00}, Z {tool.Contents.Location.WorldPosition.Z:0.00}"),
                $"{tool.Inventory.Layout.UnlockedSlots.Length} unlocked slots, {tool.Inventory.Technologies.Count} installed technologies, {tool.Inventory.SuperchargedSlots.Count} supercharged slots",
                StationToolInventory.Create(tool.Inventory), tool.Contents.Tool.ModelPath, tool.Contents.Tool.Seed, tool.Contents.Location.SiteSeed)).ToArray();

            string sourceName = body == 0 ? "the space station" : UniverseInfo.PlanetName(system, body);
            StationReloadHint = body == 0
                ? "Showing tools from the Space Station's native pool (what the cabinets sell after saving and clicking 'Reload Restore Point' at the station). Only supported cabinets are shown."
                : $"Showing tools from {sourceName}'s pool (what the station cabinets sell when flying directly to the station from this planet without reloading).";

            if (tools.Any(t => t.UsesLivePreviewState))
                StationReloadHint += " Damage reflects the game's current preview state.";

            ObservableCollectionSync.Apply(StationReloadTools, cards, c => c.Identity,
                (current, next) => current.DisplayKey == next.DisplayKey,
                (current, next) => { current.Damage = next.Damage; current.Scanner = next.Scanner; });
            HasStationReloadTools = cards.Length > 0;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationMultiTools] Station tools for body {body} unavailable: {error.Message}");
        }
    }
}
