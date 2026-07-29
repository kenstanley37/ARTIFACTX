using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using libMBIN.NMS.GameComponents;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Animal Companions ("Pets") - a flat 30-slot array (Mcl), unlike every
/// other page's :No/hl? inventory-grid shape. A slot's occupancy is XID
/// (species), not fH8 (custom name) - fH8 is only set once the player
/// manually renames a pet, so a freshly tamed, never-renamed pet has fH8 ==
/// "" while still very much occupying the slot (real bug hit 2026-07-28:
/// filtering on fH8 silently dropped every unnamed pet from the Pets page's
/// list). The fancy auto-generated name shown in-game (e.g. "Riverpito") is
/// computed client-side for display only and is never written to the save.
/// Only the fields confirmed against real save data (see NmsPetPaths) are
/// exposed here - Current Mood, Age, Gender, the fancy species name, and
/// Weight have no confirmed backing field (cross-referenced against
/// NMSCD/Creature-Builder's own reverse-engineered save contract, which
/// doesn't have them either) and aren't exposed. Personality Traits ARE
/// real and stored (see NmsPetPaths.TraitsPath) - initially miscategorized
/// as an unrelated position vector until that cross-reference caught it.
/// </summary>
public sealed partial class PetsPage : Page
{
    private sealed record PetEntry(int Index, string SelectorLabel);

    private int _selectedIndex = -1;
    private List<PetEntry> _pets = new();
    private bool _suppressStatChangeEvent;

    // Species archetype (XID, e.g. "RODENT") -> catalog data, loaded once per
    // page lifetime from the CreatureSpecies category - see CatalogService.
    private Dictionary<string, (string DisplayName, string Rarity, string Description)>? _creatureSpecies;

    public PetsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;

        LoadPetList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadPetList);

    private void LoadPetList()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _pets = new();
            _selectedIndex = -1;
            PetSelectorPanel.Children.Clear();
            ClearFields();
            return;
        }

        if (SaveSessionManager.GetValue(NmsPetPaths.PetArrayPath) is not JArray array)
            return;

        // Occupancy is XID (species), NOT fH8 (custom name) - a freshly tamed
        // pet the player never manually renamed has fH8 == "" while still
        // occupying a real slot. The fancy auto-generated name shown in-game
        // (e.g. "Riverpito") is computed client-side for display and is
        // never written to the save at all - confirmed 2026-07-28 by
        // decrypting a real save with 7 such pets and full-text-searching
        // the raw JSON for their names, which found nothing anywhere in the
        // file. Filtering on fH8 (the original assumption) silently dropped
        // every one of them from this list.
        var pets = new List<PetEntry>();
        for (int i = 0; i < array.Count; i++)
        {
            // XID is "^" (a bare, contentless prefix) on a truly empty slot,
            // not "" - IsNullOrEmpty alone doesn't catch it.
            string xid = array[i]?["XID"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(xid.TrimStart('^'))) continue;

            string customName = array[i]?["fH8"]?.Value<string>() ?? "";
            string label = string.IsNullOrEmpty(customName) ? $"{FormatArchetype(xid)} #{i + 1}" : customName;
            pets.Add(new PetEntry(i, label));
        }

        _pets = pets;

        if (_selectedIndex < 0 || _pets.All(p => p.Index != _selectedIndex))
            _selectedIndex = _pets.FirstOrDefault()?.Index ?? -1;

        BuildSelectorStrip();
        LoadSelectedPet();
    }

    private void BuildSelectorStrip()
    {
        PetSelectorPanel.Children.Clear();

        foreach (var pet in _pets)
        {
            bool isSelected = pet.Index == _selectedIndex;

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = pet.SelectorLabel,
                    FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal
                },
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                BorderBrush = new SolidColorBrush(isSelected
                    ? Color.FromArgb(255, 255, 157, 0)
                    : Color.FromArgb(255, 90, 98, 112)),
                Background = new SolidColorBrush(isSelected
                    ? Color.FromArgb(60, 255, 157, 0)
                    : Color.FromArgb(20, 255, 255, 255))
            };

            button.Click += (_, _) =>
            {
                _selectedIndex = pet.Index;
                BuildSelectorStrip();
                LoadSelectedPet();
            };

            PetSelectorPanel.Children.Add(button);
        }
    }

    private void ClearFields()
    {
        NameEditBox.Text = "";
        SpeciesTxt.Text = "";
        ClimateTxt.Text = "";
        RarityTxt.Text = "";
        SeedEditBox.Text = "";
        ColourBaseSeedActiveCheckBox.IsChecked = false;
        ColourBaseSeedEditBox.Text = "";
        BattleAbilitySeedEditBox.Text = "";
        BattleAbilitySeed2EditBox.Text = "";
        ClassLetterOverrideActiveCheckBox.IsChecked = false;
        ClassLetterHealthBox.SelectedIndex = -1;
        ClassLetterAgilityBox.SelectedIndex = -1;
        ClassLetterCombatBox.SelectedIndex = -1;
        UpdateClassLetterBoxesEnabled();
        TrustStatBox.Value = double.NaN;
        Trait1Box.Value = double.NaN;
        Trait2Box.Value = double.NaN;
        Trait3Box.Value = double.NaN;
        GenesImprovedStatBox.Value = double.NaN;
        MutationProgressStatBox.Value = double.NaN;
        VictoriesStatBox.Value = double.NaN;
        AgilityPointsBox.Value = double.NaN;
        HealthPointsBox.Value = double.NaN;
        CombatPointsBox.Value = double.NaN;
        AdvancedFieldsPanel.Children.Clear();
    }

    private async void LoadSelectedPet()
    {
        if (_selectedIndex < 0)
        {
            ClearFields();
            return;
        }

        _suppressStatChangeEvent = true;

        // The real stored value, not the selector strip's fallback label -
        // shows genuinely empty when the player never manually renamed this
        // pet, rather than the synthetic "Species #N" placeholder.
        NameEditBox.Text = SaveSessionManager.GetValue(NmsPetPaths.NamePath(_selectedIndex))?.Value<string>() ?? "";

        string archetype = SaveSessionManager.GetValue(NmsPetPaths.SpeciesArchetypePath(_selectedIndex))?.Value<string>() ?? "";
        string archetypeId = archetype.TrimStart('^');
        SpeciesTxt.Text = FormatArchetype(archetype);

        _creatureSpecies ??= await CatalogService.GetCreatureSpeciesAsync();
        RarityTxt.Text = _creatureSpecies.TryGetValue(archetypeId, out var species) ? species.Rarity : "";

        ClimateTxt.Text = SaveSessionManager.GetValue(NmsPetPaths.NativeClimatePath(_selectedIndex))?.Value<string>() ?? "";

        SeedEditBox.Text = SaveSessionManager.GetValue(NmsPetPaths.SeedPath(_selectedIndex))?.Value<string>() ?? "";
        ColourBaseSeedActiveCheckBox.IsChecked = SaveSessionManager.GetValue(NmsPetPaths.ColourBaseSeedActivePath(_selectedIndex))?.Value<bool>() ?? false;
        ColourBaseSeedEditBox.Text = SaveSessionManager.GetValue(NmsPetPaths.ColourBaseSeedPath(_selectedIndex))?.Value<string>() ?? "";
        BattleAbilitySeedEditBox.Text = SaveSessionManager.GetValue(NmsPetPaths.RollSeedPrimaryPath(_selectedIndex))?.Value<string>() ?? "";
        BattleAbilitySeed2EditBox.Text = SaveSessionManager.GetValue(NmsPetPaths.RollSeedSecondaryPath(_selectedIndex))?.Value<string>() ?? "";

        ClassLetterOverrideActiveCheckBox.IsChecked = SaveSessionManager.GetValue(NmsPetPaths.ClassLetterOverrideActivePath(_selectedIndex))?.Value<bool>() ?? false;
        SetClassLetterSelection(ClassLetterHealthBox, SaveSessionManager.GetValue(NmsPetPaths.ClassLetterPath(_selectedIndex, 0))?.Value<string>());
        SetClassLetterSelection(ClassLetterAgilityBox, SaveSessionManager.GetValue(NmsPetPaths.ClassLetterPath(_selectedIndex, 1))?.Value<string>());
        SetClassLetterSelection(ClassLetterCombatBox, SaveSessionManager.GetValue(NmsPetPaths.ClassLetterPath(_selectedIndex, 2))?.Value<string>());
        UpdateClassLetterBoxesEnabled();

        double trust = SaveSessionManager.GetValue(NmsPetPaths.TrustPath(_selectedIndex))?.Value<double>() ?? 0;
        TrustStatBox.Value = Math.Round(trust * 100, 1);

        var traits = SaveSessionManager.GetValue(NmsPetPaths.TraitsPath(_selectedIndex)) as JArray;
        Trait1Box.Value = traits?.Count > 0 ? Math.Round(Math.Abs(traits[0].Value<double>()) * 100, 1) : double.NaN;
        Trait2Box.Value = traits?.Count > 1 ? Math.Round(Math.Abs(traits[1].Value<double>()) * 100, 1) : double.NaN;
        Trait3Box.Value = traits?.Count > 2 ? Math.Round(Math.Abs(traits[2].Value<double>()) * 100, 1) : double.NaN;

        GenesImprovedStatBox.Value = SaveSessionManager.GetValue(NmsPetPaths.GenesImprovedPath(_selectedIndex))?.Value<double>() ?? 0;

        double mutationProgress = SaveSessionManager.GetValue(NmsPetPaths.MutationProgressPath(_selectedIndex))?.Value<double>() ?? 0;
        MutationProgressStatBox.Value = Math.Round(mutationProgress * 100, 1);

        VictoriesStatBox.Value = SaveSessionManager.GetValue(NmsPetPaths.HoloArenaVictoriesPath(_selectedIndex))?.Value<double>() ?? 0;

        var points = SaveSessionManager.GetValue(NmsPetPaths.MutationPointsPath(_selectedIndex)) as JArray;
        AgilityPointsBox.Value = points?.Count > 0 ? points[0].Value<double>() : double.NaN;
        HealthPointsBox.Value = points?.Count > 1 ? points[1].Value<double>() : double.NaN;
        CombatPointsBox.Value = points?.Count > 2 ? points[2].Value<double>() : double.NaN;

        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsPetPaths.PetPath(_selectedIndex))
            ? Visibility.Visible : Visibility.Collapsed;

        BuildAdvancedFieldsPanel(_selectedIndex);

        _suppressStatChangeEvent = false;
    }

    /// <summary>Every remaining raw field on this pet with no confirmed
    /// in-game effect - built dynamically (like BuildSelectorStrip above)
    /// rather than as fixed named XAML controls, since the field set itself
    /// is exploratory and may grow/shrink as fields get confirmed one way or
    /// the other.</summary>
    private void BuildAdvancedFieldsPanel(int petIndex)
    {
        AdvancedFieldsPanel.Children.Clear();

        AddHexPairFieldRow("Secondary Seed (1p=)", NmsPetPaths.SecondarySeedActivePath(petIndex), NmsPetPaths.SecondarySeedPath(petIndex));
        AddEnumFieldRow("Creature Type (HbY)", NmsPetPaths.CreatureTypePath(petIndex),
            Enum.GetNames<GcCreatureTypes.CreatureTypeEnum>());
        AddCaretPrefixedStringFieldRow("Custom Species Name (HhX)", NmsPetPaths.CustomSpeciesNamePath(petIndex));
        AddHexFieldRow("Unknown Hex (5L6)", NmsPetPaths.UnknownHexCPath(petIndex));
        AddBoolFieldRow("Unknown Bool (Q6I)", NmsPetPaths.UnknownBoolAPath(petIndex));
        AddBoolFieldRow("Unknown Bool (IaE)", NmsPetPaths.UnknownBoolBPath(petIndex));
        AddBoolFieldRow("Unknown Bool (?<V)", NmsPetPaths.UnknownBoolCPath(petIndex));
        AddBoolFieldRow("Unknown Bool (eK9)", NmsPetPaths.UnknownBoolDPath(petIndex));
        AddBoolFieldRow("Unknown Bool (WQX)", NmsPetPaths.UnknownBoolEPath(petIndex));
    }

    private static TextBlock AdvancedFieldLabel(string text) => new()
    {
        Text = text,
        Width = 210,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.8,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap
    };

    /// <summary>For a field that uses the same "^" prefix convention as
    /// XID/archetype fields - CONFIRMED real test, 2026-07-28: typing plain
    /// text with no caret broke something in-game; retyping with a leading
    /// caret worked fine and only changed the in-game Species display text,
    /// no effect on stats/Battle Abilities. Displays and accepts the value
    /// WITHOUT the caret for a cleaner box, and adds exactly one back
    /// before staging so the raw prefix can't be forgotten.</summary>
    private void AddCaretPrefixedStringFieldRow(string label, string[] path)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(AdvancedFieldLabel(label));

        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        var box = new TextBox { Width = 260, Text = current.TrimStart('^') };
        box.LostFocus += (_, _) =>
        {
            string newValue = "^" + (box.Text ?? "").TrimStart('^');
            string currentValue = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
            if (newValue == currentValue) return;
            SaveSessionManager.StageEdit(newValue, path);
            PageResetBtn.Visibility = Visibility.Visible;
        };
        row.Children.Add(box);

        AdvancedFieldsPanel.Children.Add(row);
    }

    /// <summary>Like AddStringFieldRow, but constrained to a known,
    /// game-defined set of valid values (e.g. libMBIN's own
    /// GcCreatureTypes.CreatureTypeEnum via Enum.GetNames) via a dropdown
    /// instead of free text - safe to let the user pick from even when the
    /// field's in-game effect is still unconfirmed, since the value set
    /// itself is a real constraint, not a guess.</summary>
    private void AddEnumFieldRow(string label, string[] path, IReadOnlyList<string> values)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(AdvancedFieldLabel(label));

        var box = new ComboBox { Width = 220 };
        foreach (var value in values)
            box.Items.Add(new ComboBoxItem { Content = value });

        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        box.SelectedItem = box.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content as string, current, StringComparison.OrdinalIgnoreCase));

        box.SelectionChanged += (_, _) =>
        {
            if (_suppressStatChangeEvent) return;

            string newValue = (box.SelectedItem as ComboBoxItem)?.Content as string ?? "";
            string currentValue = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
            if (newValue == currentValue) return;

            SaveSessionManager.StageEdit(newValue, path);
            PageResetBtn.Visibility = Visibility.Visible;
        };
        row.Children.Add(box);

        AdvancedFieldsPanel.Children.Add(row);
    }

    /// <summary>Like AddStringFieldRow, but for a raw hex-string field with
    /// no separate active flag - adds a Generate button using the same
    /// NmsSeedGenerator the confirmed Seed field uses.</summary>
    private void AddHexFieldRow(string label, string[] path)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(AdvancedFieldLabel(label));

        var box = new TextBox { Width = 220, Text = SaveSessionManager.GetValue(path)?.Value<string>() ?? "" };
        box.LostFocus += (_, _) =>
        {
            string newValue = box.Text ?? "";
            string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
            if (newValue == current) return;
            SaveSessionManager.StageEdit(newValue, path);
            PageResetBtn.Visibility = Visibility.Visible;
        };
        row.Children.Add(box);

        var generateBtn = new Button { Content = "Generate" };
        generateBtn.Click += (_, _) =>
        {
            string newValue = NmsSeedGenerator.GenerateRandom();
            box.Text = newValue;
            SaveSessionManager.StageEdit(newValue, path);
            PageResetBtn.Visibility = Visibility.Visible;
        };
        row.Children.Add(generateBtn);

        AdvancedFieldsPanel.Children.Add(row);
    }

    private void AddBoolFieldRow(string label, string[] path)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(AdvancedFieldLabel(label));

        var box = new CheckBox { IsChecked = SaveSessionManager.GetValue(path)?.Value<bool>() ?? false };
        box.Click += (_, _) =>
        {
            SaveSessionManager.StageEdit(box.IsChecked ?? false, path);
            PageResetBtn.Visibility = Visibility.Visible;
        };
        row.Children.Add(box);

        AdvancedFieldsPanel.Children.Add(row);
    }

    private void AddHexPairFieldRow(string label, string[] activePath, string[] hexPath)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(AdvancedFieldLabel(label));

        var activeBox = new CheckBox
        {
            Content = "Active",
            IsChecked = SaveSessionManager.GetValue(activePath)?.Value<bool>() ?? false
        };
        activeBox.Click += (_, _) =>
        {
            SaveSessionManager.StageEdit(activeBox.IsChecked ?? false, activePath);
            PageResetBtn.Visibility = Visibility.Visible;
        };
        row.Children.Add(activeBox);

        var hexBox = new TextBox { Width = 220, Text = SaveSessionManager.GetValue(hexPath)?.Value<string>() ?? "" };
        hexBox.LostFocus += (_, _) =>
        {
            string newValue = hexBox.Text ?? "";
            string current = SaveSessionManager.GetValue(hexPath)?.Value<string>() ?? "";
            if (newValue == current) return;
            SaveSessionManager.StageEdit(newValue, hexPath);
            PageResetBtn.Visibility = Visibility.Visible;
        };
        row.Children.Add(hexBox);

        // Generate also flips Active on - a randomized hex behind a false
        // flag is an untested combination, not a meaningful test on its own.
        var generateBtn = new Button { Content = "Generate" };
        generateBtn.Click += (_, _) =>
        {
            string newValue = NmsSeedGenerator.GenerateRandom();
            hexBox.Text = newValue;
            activeBox.IsChecked = true;
            SaveSessionManager.StageEdit(newValue, hexPath);
            SaveSessionManager.StageEdit(true, activePath);
            PageResetBtn.Visibility = Visibility.Visible;
        };
        row.Children.Add(generateBtn);

        AdvancedFieldsPanel.Children.Add(row);
    }

    /// <summary>"^PLANTCAT" -> "Plantcat" - just enough formatting to be
    /// readable; not the fancy in-game Latin species name (unconfirmed/
    /// likely not stored - see NmsPetPaths).</summary>
    private static string FormatArchetype(string raw)
    {
        string trimmed = raw.TrimStart('^');
        if (trimmed.Length == 0) return "";
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        string currentName = SaveSessionManager.GetValue(NmsPetPaths.NamePath(_selectedIndex))?.Value<string>() ?? "";
        if (newName == currentName) return;

        SaveSessionManager.StageEdit(newName, NmsPetPaths.NamePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
        LoadPetList();
    }

    private void TrustStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit(args.NewValue / 100.0, NmsPetPaths.TrustPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Redundant safety net alongside ValueChanged - some WinUI3
    /// NumberBox versions don't reliably fire ValueChanged on a plain focus
    /// loss, matching every other stat box in the app.</summary>
    private void TrustStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(TrustStatBox.Value)) return;
        SaveSessionManager.StageEdit(TrustStatBox.Value / 100.0, NmsPetPaths.TrustPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenesImprovedStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit((int)args.NewValue, NmsPetPaths.GenesImprovedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenesImprovedStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(GenesImprovedStatBox.Value)) return;
        SaveSessionManager.StageEdit((int)GenesImprovedStatBox.Value, NmsPetPaths.GenesImprovedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MutationProgressStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit(args.NewValue / 100.0, NmsPetPaths.MutationProgressPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MutationProgressStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(MutationProgressStatBox.Value)) return;
        SaveSessionManager.StageEdit(MutationProgressStatBox.Value / 100.0, NmsPetPaths.MutationProgressPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void VictoriesStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit((int)args.NewValue, NmsPetPaths.HoloArenaVictoriesPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void VictoriesStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(VictoriesStatBox.Value)) return;
        SaveSessionManager.StageEdit((int)VictoriesStatBox.Value, NmsPetPaths.HoloArenaVictoriesPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Stages one entry of the whole 3-slot mutation-points array -
    /// rebuilds and stages the WHOLE array at once, same reasoning as every
    /// other @bB-style array edit in the app (a deeper leaf-only stage isn't
    /// seen by SaveSessionManager's staged-edit lookup).</summary>
    private void SetMutationPoints(int statIndex, double newValue)
    {
        if (_selectedIndex < 0 || double.IsNaN(newValue)) return;

        var path = NmsPetPaths.MutationPointsPath(_selectedIndex);
        if (SaveSessionManager.GetValue(path) is not JArray points || points.Count != 3) return;

        var updated = new JArray(points.Select(p => p.DeepClone()));
        updated[statIndex] = (int)newValue;

        SaveSessionManager.StageEdit(updated, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void AgilityPointsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetMutationPoints(0, args.NewValue);
    }

    private void HealthPointsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetMutationPoints(1, args.NewValue);
    }

    private void CombatPointsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetMutationPoints(2, args.NewValue);
    }

    /// <summary>Stages one entry of the whole 3-slot Traits array - rebuilds
    /// and stages the whole array at once, same reasoning as
    /// SetMutationPoints above. newMagnitude is 0-100 (matching the
    /// NumberBox); the stored value's existing sign is preserved since its
    /// meaning (which descriptor-word "pole" it picks) isn't confirmed - only
    /// the magnitude the user typed is applied.</summary>
    private void SetTrait(int traitIndex, double newMagnitude)
    {
        if (_selectedIndex < 0 || double.IsNaN(newMagnitude)) return;

        var path = NmsPetPaths.TraitsPath(_selectedIndex);
        if (SaveSessionManager.GetValue(path) is not JArray traits || traits.Count != 3) return;

        double currentValue = traits[traitIndex].Value<double>();
        double sign = currentValue < 0 ? -1.0 : 1.0;

        var updated = new JArray(traits.Select(t => t.DeepClone()));
        updated[traitIndex] = sign * (newMagnitude / 100.0);

        SaveSessionManager.StageEdit(updated, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void Trait1Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetTrait(0, args.NewValue);
    }

    private void Trait2Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetTrait(1, args.NewValue);
    }

    private void Trait3Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetTrait(2, args.NewValue);
    }

    /// <summary>Stages the seed when the field loses focus - same plain-text
    /// staging pattern as Ships/Freighter's own seed fields.</summary>
    private void SeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = SeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        var seedPath = NmsPetPaths.SeedPath(_selectedIndex);
        string? currentSeed = SaveSessionManager.GetValue(seedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, seedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Rerolls the selected pet's color to a brand new random seed -
    /// same plain-reroll semantics as Ships/Freighter (NmsSeedGenerator), not
    /// a targeted picker. Writes the SAME new value into both SeedPath
    /// (CreatureSeed) and BoneScaleSeedPath (BoneScaleSeed), preserving the
    /// mirror every real sample showed between them - see NmsPetPaths for
    /// why the other two seed pairs (CreatureSecondarySeed/ColourBaseSeed)
    /// are deliberately left untouched.</summary>
    private void GenerateSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        SeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsPetPaths.SeedPath(_selectedIndex));
        SaveSessionManager.StageEdit(newSeed, NmsPetPaths.BoneScaleSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ColourBaseSeedActiveCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        SaveSessionManager.StageEdit(ColourBaseSeedActiveCheckBox.IsChecked ?? false,
            NmsPetPaths.ColourBaseSeedActivePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ColourBaseSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newValue = ColourBaseSeedEditBox.Text?.Trim() ?? "";
        var path = NmsPetPaths.ColourBaseSeedPath(_selectedIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Activates AND rerolls together - confirmed via real testing
    /// (see NmsPetPaths.ColourBaseSeedActivePath) that this is a dormant
    /// secondary color layer, so a randomized hex behind an inactive flag
    /// wouldn't show anything in-game.</summary>
    private void GenerateColourBaseSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        ColourBaseSeedEditBox.Text = newSeed;
        ColourBaseSeedActiveCheckBox.IsChecked = true;
        SaveSessionManager.StageEdit(newSeed, NmsPetPaths.ColourBaseSeedPath(_selectedIndex));
        SaveSessionManager.StageEdit(true, NmsPetPaths.ColourBaseSeedActivePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void BattleAbilitySeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = BattleAbilitySeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        var rollSeedPath = NmsPetPaths.RollSeedPrimaryPath(_selectedIndex);
        string? currentSeed = SaveSessionManager.GetValue(rollSeedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, rollSeedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Rerolls the selected pet's Battle Abilities badges and fancy
    /// species name to a brand new random roll - confirmed via a real
    /// reroll-and-revert round trip (see NmsPetPaths.RollSeedPrimaryPath).</summary>
    private void GenerateBattleAbilitySeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        BattleAbilitySeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsPetPaths.RollSeedPrimaryPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void BattleAbilitySeed2EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = BattleAbilitySeed2EditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        var rollSeedPath = NmsPetPaths.RollSeedSecondaryPath(_selectedIndex);
        string? currentSeed = SaveSessionManager.GetValue(rollSeedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, rollSeedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Second confirmed co-input to the same roll as
    /// GenerateBattleAbilitySeedBtn_Click - see NmsPetPaths.RollSeedSecondaryPath.</summary>
    private void GenerateBattleAbilitySeed2Btn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        BattleAbilitySeed2EditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsPetPaths.RollSeedSecondaryPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ClassLetterOverrideActiveCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        SaveSessionManager.StageEdit(ClassLetterOverrideActiveCheckBox.IsChecked ?? false,
            NmsPetPaths.ClassLetterOverrideActivePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
        UpdateClassLetterBoxesEnabled();
    }

    /// <summary>The Health/Agility/Combat letter boxes only do anything
    /// in-game while Override Active is checked - disabled otherwise so
    /// their text can't be mistaken for something currently in effect.</summary>
    private void UpdateClassLetterBoxesEnabled()
    {
        bool active = ClassLetterOverrideActiveCheckBox.IsChecked ?? false;
        ClassLetterHealthBox.IsEnabled = active;
        ClassLetterAgilityBox.IsEnabled = active;
        ClassLetterCombatBox.IsEnabled = active;
    }

    /// <summary>Selects the ComboBoxItem matching the stored letter (S/A/B/C
    /// - the same fixed set Ships/Freighter/Multi-Tool use for Class),
    /// clearing selection if the value doesn't match any of them (e.g. a
    /// pet that's never had this field touched).</summary>
    private static void SetClassLetterSelection(ComboBox box, string? letter)
    {
        foreach (ComboBoxItem item in box.Items.Cast<ComboBoxItem>())
        {
            if (string.Equals(item.Content as string, letter, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = -1;
    }

    private void StageClassLetterEdit(ComboBox box, int statIndex)
    {
        if (_selectedIndex < 0 || _suppressStatChangeEvent) return;

        string newValue = (box.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        var path = NmsPetPaths.ClassLetterPath(_selectedIndex, statIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ClassLetterHealthBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => StageClassLetterEdit(ClassLetterHealthBox, 0);
    private void ClassLetterAgilityBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => StageClassLetterEdit(ClassLetterAgilityBox, 1);
    private void ClassLetterCombatBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => StageClassLetterEdit(ClassLetterCombatBox, 2);

    /// <summary>Scoped to just the currently selected pet's own subtree (via
    /// RevertEditsUnder) - same "current item only" scope as every other
    /// multi-item page's Reset button (e.g. Ships checks only the currently
    /// loaded ship's ViewModels, not other owned ships).</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        SaveSessionManager.RevertEditsUnder(NmsPetPaths.PetPath(_selectedIndex));
        LoadSelectedPet();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
