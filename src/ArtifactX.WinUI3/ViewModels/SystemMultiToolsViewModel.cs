using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One multi-tool a system's cabinets can sell: its generated name and type (Pistol, Rifle, Alien,
/// Experimental), resolved from its model's part tree.</summary>
public sealed partial class SystemMultiToolItemViewModel : ObservableObject
{
    public SystemMultiToolItemViewModel(SystemMultiTool tool) => Tool = tool;

    public SystemMultiTool Tool { get; }
    public string Name => Tool.DefaultName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindLabel), nameof(BadgeBackground), nameof(BadgeForeground), nameof(AutomationName))]
    public partial string Kind { get; set; } = "Multi-Tool";

    public string KindLabel => Kind.ToUpperInvariant();
    public string AutomationName => $"{Name}, {Kind}";

    public Brush BadgeBackground => Kind switch
    {
        "Alien" => ThemeResources.Brush("ThemeWarningBrush"),
        "Experimental" => ThemeResources.Brush("ThemeDoneBrush"),
        "Rifle" => ThemeResources.Brush("ThemeAccentAltBrush"),
        "Pistol" => ThemeResources.Brush("ThemePanelBrush"),
        _ => ThemeResources.Brush("ThemeLineBrush"),
    };

    public Brush BadgeForeground => Kind switch
    {
        "Alien" or "Experimental" => ThemeResources.Brush("ThemeOnAccentBrush"),
        "Rifle" => ThemeResources.Brush("ThemeBandTextBrush"),
        _ => ThemeResources.Brush("ThemeTextBrush"),
    };

    /// <summary>Resolves the type from the model's part tree (off the UI thread), keeping "Multi-Tool" when unknown.</summary>
    public async Task ResolveKindAsync(DispatcherQueue? dispatcher)
    {
        try
        {
            if (await MultiToolKinds.KindAsync(Tool.Seed) is { Length: > 0 } kind)
            {
                if (dispatcher is null) Kind = kind;
                else dispatcher.TryEnqueue(() => Kind = kind);
            }
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemMultiTools] Kind of {Tool.DefaultName} unavailable: {error.Message}");
        }
    }
}

/// <summary>A place you can load the game at (the space station or a planet) and the multi-tools every cabinet in
/// the system then sells.</summary>
public sealed partial class MultiToolLoadPlaceViewModel : ObservableObject
{
    public MultiToolLoadPlaceViewModel(int body, string name, IReadOnlyList<SystemMultiToolItemViewModel> tools)
    {
        Body = body;
        Name = name;
        Tools = tools;
    }

    /// <summary>0 for the space station, else the planet index.</summary>
    public int Body { get; }
    public string Name { get; }
    public IReadOnlyList<SystemMultiToolItemViewModel> Tools { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading), nameof(BorderBrush))]
    public partial bool IsSavedLoad { get; set; }

    public string Heading => IsSavedLoad ? $"LOAD AT {Name.ToUpperInvariant()} · YOUR SAVE LOADS HERE" : $"LOAD AT {Name.ToUpperInvariant()}";
    public Brush BorderBrush => ThemeResources.Brush(IsSavedLoad ? "ThemeAccentBrush" : "ThemeLineBrush");
}

/// <summary>
/// The multi-tools a system's cabinets sell, by where the game is loaded. Every standard cabinet in a system - the
/// space station's and the planets' shops alike - sells from the pool of the place the game was last loaded at
/// (SystemMultiToolGenerator.GeneratePool with that body's address); the cabinet's own position decides its class
/// and stats. Confirmed 2026-11-05 (Slot 7, Ihweyf, Kaim 30/19): loaded on the planet, its shop cabinet offered the
/// Parallel Neutron Reflector; after saving and reloading at the station, the same cabinet offered the station pool's
/// Improvised Nano Probe, both C-class 358.1 / 202.9 as predicted (docs/research/MULTITOOL_GENERATION.md). Sentinel
/// pillar tools (other pool types) are left out. Uncharted and abandoned systems have no working cabinets.
/// </summary>
public sealed partial class SystemMultiToolsViewModel : ObservableObject
{
    private ulong? _system;

    [ObservableProperty] public partial bool HasPlaces { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<MultiToolLoadPlaceViewModel> Places { get; private set; } = [];
    [ObservableProperty] public partial string Odds { get; private set; } = "";

    public string Explanation =>
        "Every multi-tool cabinet in this system - the space station's and the planets' shops - sells from one list: "
        + "the list of the place where you last loaded your game. Save and reload at a place to get its multi-tools. "
        + "Each cabinet's class and stats come from where the cabinet stands, so they stay the same whichever list it sells from.";

    /// <summary>Builds the lists once per system (off the UI thread) and marks the place the save loads at.</summary>
    public async Task LoadAsync(ulong system, SystemFacts facts, string stationName,
        IReadOnlyList<(int Index, string Name)> planets, int? savedBody)
    {
        if (facts.Uncharted || facts.Abandoned)
        {
            _system = system;
            Places = [];
            HasPlaces = false;
            return;
        }
        if (_system != system || Places.Count == 0)
        {
            _system = system;
            var dispatcher = DispatcherQueue.GetForCurrentThread();
            var words = await CatalogService.GetProceduralNameWordsAsync();
            var tables = PlanetTypesService.InventoryTables ?? await CatalogService.GetInventoryGenerationTablesAsync();
            if (tables?.MultiToolPools is not { Length: > 0 } pools) return;
            ulong systemAddress = system & ~(0xFUL << 52);
            var places = await Task.Run(() =>
            {
                var bodies = new List<(int Body, string Name)> { (0, stationName) };
                bodies.AddRange(planets.Select(p => (p.Index, p.Name)));
                return bodies.Select(b =>
                {
                    var pool = SystemMultiToolGenerator.GeneratePool(systemAddress | ((ulong)(b.Body & 15) << 52),
                        b.Body == 0 ? MultiToolPoolSource.SpaceStation : MultiToolPoolSource.Planet, b.Name,
                        k => words.GetValueOrDefault(k), pools);
                    var tools = pool.Where(t => t.PoolType == "Standard").Select(t => new SystemMultiToolItemViewModel(t)).ToArray();
                    return new MultiToolLoadPlaceViewModel(b.Body, b.Name, tools);
                }).Where(p => p.Tools.Count > 0).ToArray();
            });
            if (_system != system) return;
            Places = places;
            HasPlaces = places.Length > 0;
            var (c, b, a, s) = EquipmentGeneration.GetClassProbabilities(facts.Wealth, facts.Pirate, tables);
            Odds = $"Cabinet class chances in this system: S {s:0}% · A {a:0}% · B {b:0}% · C {c:0}%";
            foreach (var tool in places.SelectMany(p => p.Tools)) _ = tool.ResolveKindAsync(dispatcher);
        }
        foreach (var place in Places) place.IsSavedLoad = place.Body == savedBody;
    }
}
