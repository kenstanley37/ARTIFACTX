using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Animal Companions ("Pets") - a flat 30-slot array (Mcl, empty slots have
/// fH8 == ""), unlike every other page's :No/hl? inventory-grid shape. Only
/// the fields confirmed against real save data (see NmsPetPaths) are
/// exposed here - Current Mood, Age, Gender, the fancy species name, and
/// Weight have no confirmed backing field (cross-referenced against
/// NMSCD/Creature-Builder's own reverse-engineered save contract, which
/// doesn't have them either) and aren't exposed. Personality Traits ARE
/// real and stored (see NmsPetPaths.TraitsPath) - initially miscategorized
/// as an unrelated position vector until that cross-reference caught it.
/// </summary>
public sealed partial class PetsPage : Page
{
    private sealed record PetEntry(int Index, string Name);

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

        var pets = new List<PetEntry>();
        for (int i = 0; i < array.Count; i++)
        {
            string name = array[i]?["fH8"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(name)) continue;
            pets.Add(new PetEntry(i, name));
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
                    Text = pet.Name,
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
    }

    private async void LoadSelectedPet()
    {
        if (_selectedIndex < 0)
        {
            ClearFields();
            return;
        }

        _suppressStatChangeEvent = true;

        var pet = _pets.First(p => p.Index == _selectedIndex);
        NameEditBox.Text = pet.Name;

        string archetype = SaveSessionManager.GetValue(NmsPetPaths.SpeciesArchetypePath(_selectedIndex))?.Value<string>() ?? "";
        string archetypeId = archetype.TrimStart('^');
        SpeciesTxt.Text = FormatArchetype(archetype);

        _creatureSpecies ??= await CatalogService.GetCreatureSpeciesAsync();
        RarityTxt.Text = _creatureSpecies.TryGetValue(archetypeId, out var species) ? species.Rarity : "";

        ClimateTxt.Text = SaveSessionManager.GetValue(NmsPetPaths.NativeClimatePath(_selectedIndex))?.Value<string>() ?? "";

        SeedEditBox.Text = SaveSessionManager.GetValue(NmsPetPaths.SeedPath(_selectedIndex))?.Value<string>() ?? "";

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

        _suppressStatChangeEvent = false;
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

        var currentPet = _pets.FirstOrDefault(p => p.Index == _selectedIndex);
        if (currentPet != null && newName == currentPet.Name) return;

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
