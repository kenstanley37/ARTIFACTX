using ArtifactX.Almanac.Starship;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A pilot race or ship type in the Squadron page's pickers; <see cref="Listed"/> false for a save's ship
/// outside the known list, shown so it isn't silently replaced.</summary>
public sealed record SquadronChoice(string Label, string Value, bool Listed = true)
{
    public override string ToString() => Label;
}

/// <summary>One of the four slot buttons.</summary>
public sealed partial class SquadronSlotViewModel : ObservableObject
{
    public SquadronSlotViewModel(int index) => Index = index;

    public int Index { get; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccessibleName))] public partial string Label { get; set; } = "";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Border), nameof(BorderThickness), nameof(Fill), nameof(Weight))]
    public partial bool IsSelected { get; set; }

    public Brush Border => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Thickness BorderThickness => new(IsSelected ? 2 : 1);
    public Brush Fill => ThemeResources.Brush(IsSelected ? "ThemeSelectedFillBrush" : "ThemePanelAltBrush");
    public Windows.UI.Text.FontWeight Weight => IsSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
    public string AccessibleName => $"Squadron {Label}";
}

/// <summary>
/// The Squadron page (SquadronService): the four pilot slots and the selected pilot's unlock state, race, rank,
/// ship type, seeds and both 3D previews - the pilot from its race's NPC scene and appearance seed, the ship from its
/// model and seed. Each edit is staged as it's made; Reset undoes this pilot's.
/// </summary>
public sealed partial class SquadronViewModel : ObservableObject
{
    private bool _loading;

    public ObservableCollection<SquadronSlotViewModel> Slots { get; } = new();

    /// <summary>GcPlayerSquadronConfig.RandomPilotNPCResources (NmsSquadronPaths.NpcRaceOptions).</summary>
    public IReadOnlyList<SquadronChoice> Races { get; } = NmsSquadronPaths.NpcRaceOptions.Select(o => new SquadronChoice(o.DisplayName, o.Filename)).ToList();

    public ObservableCollection<SquadronChoice> ShipTypes { get; } = new(StarshipTypes.All.Select(t => new SquadronChoice(t.DisplayName, t.ScenePath)));

    /// <summary>The pilot or ship look changed: the page rebuilds the previews.</summary>
    public event Action? PreviewChanged;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelection))] public partial int SelectedIndex { get; private set; } = -1;
    public bool HasSelection => SelectedIndex >= 0;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowPreviews))] public partial bool IsUnlocked { get; set; }
    [ObservableProperty] public partial SquadronChoice? Race { get; set; }
    [ObservableProperty] public partial SquadronChoice? ShipType { get; set; }
    [ObservableProperty] public partial NumberFieldViewModel? Rank { get; private set; }
    [ObservableProperty] public partial string RankLetter { get; private set; } = "";
    [ObservableProperty] public partial string ShipName { get; private set; } = "";
    [ObservableProperty] public partial bool HasEdits { get; private set; }

    public bool ShowPreviews => HasSelection && IsUnlocked;

    /// <summary>The selected pilot's NPC scene and appearance seed, and its ship's model and seed.</summary>
    public string PilotScene => HasSelection ? SquadronService.NpcScene(SelectedIndex) : "";
    public string PilotSeed => HasSelection ? SquadronService.NpcSeed(SelectedIndex) : "";
    public string ShipScene => ShipType?.Value ?? (HasSelection ? SquadronService.ShipScene(SelectedIndex) : "");
    public string ShipSeed => HasSelection ? SquadronService.ShipSeed(SelectedIndex) : "";

    /// <summary>The slots and the selected pilot, from the loaded save.</summary>
    public void Load()
    {
        int count = SaveSessionManager.IsSaveLoaded ? SquadronService.SlotCount() : 0;
        while (Slots.Count > count) Slots.RemoveAt(Slots.Count - 1);
        while (Slots.Count < count) Slots.Add(new SquadronSlotViewModel(Slots.Count));
        Select(count == 0 ? -1 : SelectedIndex >= 0 && SelectedIndex < count ? SelectedIndex : 0);
    }

    /// <summary>Another page or the title bar staged, saved or reset edits: refresh the labels and Reset only.</summary>
    public void RefreshState()
    {
        foreach (var slot in Slots) slot.Label = SlotLabel(slot.Index);
        if (!HasSelection) return;
        bool hasEdits = SquadronService.HasEdits(SelectedIndex);
        // This pilot's edits were saved or discarded elsewhere (the title bar): show the save's values again.
        if (HasEdits && !hasEdits) Select(SelectedIndex);
        else HasEdits = hasEdits;
    }

    public void Select(int index)
    {
        SelectedIndex = index;
        foreach (var slot in Slots)
        {
            slot.Label = SlotLabel(slot.Index);
            slot.IsSelected = slot.Index == index;
        }
        _loading = true;
        if (index < 0)
        {
            IsUnlocked = false;
            Race = null;
            ShipType = null;
            Rank = null;
            RankLetter = "";
            HasEdits = false;
            _loading = false;
            PreviewChanged?.Invoke();
            return;
        }

        IsUnlocked = SquadronService.IsUnlocked(index);
        string npc = SquadronService.NpcScene(index);
        Race = Races.FirstOrDefault(r => string.Equals(r.Value, npc, StringComparison.OrdinalIgnoreCase));

        // Real saves hold ships outside the known types (StarshipTypes' doc): the current one stays visible and selected
        // as a disabled extra entry instead of being silently replaced.
        foreach (var stale in ShipTypes.Where(t => !t.Listed).ToList()) ShipTypes.Remove(stale);
        string ship = SquadronService.ShipScene(index);
        var shipType = ShipTypes.FirstOrDefault(t => string.Equals(t.Value, ship, StringComparison.OrdinalIgnoreCase));
        if (shipType is null)
        {
            shipType = new SquadronChoice("(Current - not in known list)", ship, Listed: false);
            ShipTypes.Insert(0, shipType);
        }
        ShipType = shipType;

        double rank = SquadronService.Rank(index);
        Rank = new NumberFieldViewModel("Pilot rank", "rank", rank, field =>
        {
            RankLetter = RankLetterFor(field.Value);
            if (double.IsNaN(field.Value)) return;
            SquadronService.SetRank(SelectedIndex, (int)field.Value);
            HasEdits = true;
        });
        RankLetter = RankLetterFor(rank);
        HasEdits = SquadronService.HasEdits(index);
        _loading = false;
        PreviewChanged?.Invoke();
    }

    partial void OnIsUnlockedChanged(bool value)
    {
        if (_loading || !HasSelection) return;
        SquadronService.SetUnlocked(SelectedIndex, value);
        HasEdits = true;
        foreach (var slot in Slots) slot.Label = SlotLabel(slot.Index);
        PreviewChanged?.Invoke();
    }

    partial void OnRaceChanged(SquadronChoice? value)
    {
        if (_loading || !HasSelection || value is null) return;
        if (SquadronService.SetNpcScene(SelectedIndex, value.Value)) Edited();
    }

    partial void OnShipTypeChanged(SquadronChoice? value)
    {
        if (_loading || !HasSelection || value is null) return;
        if (SquadronService.SetShipScene(SelectedIndex, value.Value)) Edited();
    }

    /// <summary>A seed box staged a seed (or another field changed the look).</summary>
    public void Edited()
    {
        HasEdits = true;
        PreviewChanged?.Invoke();
    }

    public void Reset()
    {
        if (!HasSelection) return;
        SquadronService.RevertEdits(SelectedIndex);
        Select(SelectedIndex);
    }

    /// <summary>The ship's generated name for the preview heading (StarshipService.ShipNameAsync).</summary>
    public async Task RefreshShipNameAsync()
    {
        string seedText = ShipSeed, scene = ShipScene;
        string name = NmsSeedGenerator.TryParse(seedText, out ulong seed) && seed != 0
            ? await StarshipService.ShipNameAsync(seed, scene) ?? "" : "";
        if (seedText == ShipSeed) ShipName = name.Length > 0 ? name : "Squadron Starship";
    }

    private static string SlotLabel(int slot) => SquadronService.IsUnlocked(slot) ? $"Slot {slot + 1}" : $"Slot {slot + 1} (Locked)";

    /// <summary>Only 0-3 are confirmed (C/B/A/S, GcInventoryClass's raw values) - PilotRank is a plain ushort in
    /// libMBIN, so higher values are accepted without validation. See NmsSquadronPaths.</summary>
    private static string RankLetterFor(double rank) => rank switch
    {
        0 => "Class C",
        1 => "Class B",
        2 => "Class A",
        3 => "Class S",
        _ => "No confirmed class badge",
    };
}
