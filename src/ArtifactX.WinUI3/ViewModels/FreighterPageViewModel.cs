using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A Type or Crew Race choice: the model scene the save stores and the name shown.</summary>
public sealed record FreighterSceneOption(string DisplayName, string ScenePath)
{
    public override string ToString() => DisplayName;
}

/// <summary>One of the game's seven engine flares (NmsInventoryContainer.FreighterEngineEffects).</summary>
public sealed record FreighterEngineFlare(string Name, string Value)
{
    public override string ToString() => Name;
}

/// <summary>
/// The Freighter page's details: name, class, type, crew race, engine flare, the Home Seed note and whether the page
/// has unsaved edits. Reads and edits go through FreighterService; the page wires the grids, dialogs and 3D previews.
/// Choices made by loading (not by the player) don't stage anything: <see cref="_loading"/> covers them.
/// </summary>
public sealed partial class FreighterPageViewModel : ObservableObject
{
    private bool _loading;

    /// <summary>The save has a freighter (else the page offers to add one).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoFreighter))]
    [NotifyPropertyChangedFor(nameof(ShowDetails))]
    public partial bool HasFreighter { get; private set; }

    private bool _loaded;

    /// <summary>The "add a freighter" panel: only once a load has found none.</summary>
    public bool ShowNoFreighter => _loaded && !HasFreighter;

    /// <summary>The details and grids: until a load finds no freighter.</summary>
    public bool ShowDetails => !_loaded || HasFreighter;

    /// <summary>Anything on the page is staged (shows "Reset Freighter Changes").</summary>
    [ObservableProperty] public partial bool HasPageEdits { get; private set; }

    [ObservableProperty] public partial string Name { get; private set; } = "";
    [ObservableProperty] public partial string? ClassLetter { get; private set; }

    [ObservableProperty] public partial IReadOnlyList<FreighterSceneOption> TypeOptions { get; private set; } = [];
    [ObservableProperty] public partial FreighterSceneOption? SelectedType { get; set; }

    [ObservableProperty] public partial IReadOnlyList<FreighterSceneOption> CrewRaceOptions { get; private set; } = [];
    [ObservableProperty] public partial FreighterSceneOption? SelectedCrewRace { get; set; }

    public IReadOnlyList<FreighterEngineFlare> EngineFlares { get; } =
        NmsInventoryContainer.FreighterEngineEffects.Select(f => new FreighterEngineFlare(f.Name, f.Value)).ToList();
    [ObservableProperty] public partial FreighterEngineFlare? SelectedFlare { get; set; }

    /// <summary>Under the Home Seed while the save has picked colours: they win, or (with a new Home Seed) saving
    /// removes them so the game shows the seed's paint (#69).</summary>
    [ObservableProperty] public partial bool HasHomeSeedNote { get; private set; }
    [ObservableProperty] public partial string HomeSeedNote { get; private set; } = "";
    [ObservableProperty] public partial Brush? HomeSeedNoteBrush { get; private set; }

    /// <summary>The hull's look changed (type, seeds, flare, paint): redraw its preview.</summary>
    public event EventHandler? HullLookChanged;

    /// <summary>The captain changed (crew race or seed): redraw the captain.</summary>
    public event EventHandler? CaptainChanged;

    /// <summary>Reads everything the details band shows. Type and Crew Race options load once (they don't change).</summary>
    public async Task LoadAsync()
    {
        _loaded = true;
        HasFreighter = FreighterService.HasFreighter();
        OnPropertyChanged(nameof(ShowNoFreighter));
        OnPropertyChanged(nameof(ShowDetails));
        if (!HasFreighter) return;
        _loading = true;
        try
        {
            if (TypeOptions.Count == 0)
                TypeOptions = (await CatalogService.GetFreighterTypesAsync()).Select(t => new FreighterSceneOption(t.DisplayName, t.ScenePath)).ToList();
            if (CrewRaceOptions.Count == 0)
                CrewRaceOptions = (await CatalogService.GetFreighterCrewRacesAsync()).Select(t => new FreighterSceneOption(t.DisplayName, t.ScenePath)).ToList();
            Name = FreighterService.Name();
            ClassLetter = FreighterService.ClassLetter();
            SelectedType = Match(TypeOptions, FreighterService.TypeScene());
            SelectedCrewRace = Match(CrewRaceOptions, FreighterService.CrewRaceScene());
            SelectedFlare = EngineFlares.FirstOrDefault(f => string.Equals(f.Value, FreighterService.EngineFlare(), StringComparison.OrdinalIgnoreCase));
            HasPageEdits = FreighterService.HasPageEdits();
            RefreshHomeSeedNote();
        }
        finally { _loading = false; }
    }

    private static FreighterSceneOption? Match(IReadOnlyList<FreighterSceneOption> options, string? scene) =>
        options.FirstOrDefault(o => string.Equals(o.ScenePath, scene, StringComparison.OrdinalIgnoreCase));

    /// <summary>Something on the page was edited outside these fields (grids, stats, seeds, templates).</summary>
    public void MarkEdited() => HasPageEdits = true;

    /// <summary>Stages the typed name when the box loses focus (not on every keystroke).</summary>
    public void CommitName(string? text)
    {
        if (!FreighterService.Rename(text)) return;
        Name = FreighterService.Name();
        MarkEdited();
    }

    /// <summary>Stages a class; the caller rebuilds the grids (the class sets the slot counts).</summary>
    public void ChooseClass(string letter)
    {
        FreighterService.SetClass(letter);
        ClassLetter = letter;
        MarkEdited();
    }

    partial void OnSelectedTypeChanged(FreighterSceneOption? value)
    {
        if (_loading || value is null) return;
        FreighterService.SetType(value.ScenePath);
        MarkEdited();
        HullLookChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedCrewRaceChanged(FreighterSceneOption? value)
    {
        if (_loading || value is null) return;
        FreighterService.SetCrewRace(value.ScenePath);
        MarkEdited();
        CaptainChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedFlareChanged(FreighterEngineFlare? value)
    {
        if (_loading || value is null || !FreighterService.SetEngineFlare(value.Value)) return;
        MarkEdited();
        HullLookChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A seed box staged a new seed. A new Home Seed also clears picked colours (#69).</summary>
    public void SeedChanged(bool homeSeed, bool modelSeed, bool crewSeed)
    {
        MarkEdited();
        if (homeSeed) FreighterService.SyncCustomColoursWithHomeSeed();
        if (homeSeed || modelSeed) { RefreshHomeSeedNote(); HullLookChanged?.Invoke(this, EventArgs.Empty); }
        if (crewSeed) CaptainChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshHomeSeedNote()
    {
        HasHomeSeedNote = FreighterService.HasSavedCustomColours();
        if (!HasHomeSeedNote) return;
        bool clearing = FreighterService.HomeSeedChanged();
        HomeSeedNote = clearing
            ? "Saving removes the colours you picked in game, so the game paints this Home Seed's pattern. Undo the Home Seed to keep your colours."
            : "Your freighter has colours you picked in game, and those win. Pick a new Home Seed to see its paint - saving it then removes your colours.";
        HomeSeedNoteBrush = ThemeResources.Brush(clearing ? "ThemeWarningBrush" : "ThemeTextDimBrush");
    }

    /// <summary>Undoes every change made on this page (details, seeds, grids, interior).</summary>
    public void ResetAll()
    {
        FreighterService.RevertPageEdits();
        HasPageEdits = false;
    }
}
