using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Animal Companions ("Pets"): the tamed pets in the save (CompanionService) and the
/// selected one's fields (CompanionViewModel). Reloads on save switches and edits made
/// elsewhere; the editor's own edits (staged while typing) only refresh the pet strip
/// and Reset, so the box being typed in isn't rewritten. Reset undoes every change to
/// the selected pet.
/// </summary>
public sealed partial class CompanionsPage : Page
{
    private List<CompanionEntry> _companions = new();
    private CreatureNameService? _names;
    private int _selectedIndex = -1;
    private int _loadVersion;

    public CompanionViewModel ViewModel { get; } = new();

    public CompanionsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;
        ViewModel.NameChanged += (_, _) => ShowCompanionLabels();

        LoadCompanionList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (ViewModel.IsStagingField) DispatcherQueue.TryEnqueue(ShowResetButton);
        else DispatcherQueue.TryEnqueue(LoadCompanionList);
    }

    /// <summary>Static events hold their subscribers: without this every past visit
    /// would stay subscribed and reload on every edit anywhere in the app.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private void LoadCompanionList()
    {
        _companions = SaveSessionManager.IsSaveLoaded ? CompanionService.ReadCompanions(_names) : new();
        if (_companions.All(c => c.Index != _selectedIndex))
            _selectedIndex = _companions.FirstOrDefault()?.Index ?? -1;

        ShowStrip();
        _ = LoadSelectedAsync();
        if (_names is null) _ = LoadNamesAsync();
    }

    /// <summary>The words for the names the game gives companions come from the
    /// catalog; the chips show "Species #N" until they're read.</summary>
    private async Task LoadNamesAsync()
    {
        _names = await CreatureNameService.LoadAsync();
        ShowCompanionLabels();
    }

    /// <summary>A renamed pet's chip label, without reloading the editor.</summary>
    private void ShowCompanionLabels()
    {
        _companions = SaveSessionManager.IsSaveLoaded ? CompanionService.ReadCompanions(_names) : new();
        ShowStrip();
    }

    private void ShowStrip() =>
        CompanionStrip.Items = _companions
            .Select(c => EquipmentChipViewModel.Item(c.Index, c.Label, c.Index == _selectedIndex, isActive: false, isNew: false, animateIn: false))
            .ToList();

    private void CompanionStrip_ChipClicked(object? sender, EquipmentChipViewModel chip)
    {
        _selectedIndex = chip.Index;
        ShowStrip();
        _ = LoadSelectedAsync();
    }

    private async Task LoadSelectedAsync()
    {
        int version = ++_loadVersion;
        int index = _selectedIndex;
        if (index < 0)
        {
            ViewModel.Clear();
            ShowSeeds(-1);
            PageResetBtn.Visibility = Visibility.Collapsed;
            return;
        }

        await ViewModel.LoadAsync(index);
        if (version != _loadVersion) return;
        ShowSeeds(index);
        ShowResetButton();
        _ = ShowCompanionPreviewAsync(index, version);
    }

    private string? _previewKey;

    private async Task ShowCompanionPreviewAsync(int index, int version)
    {
        if (index < 0)
        {
            CompanionPreviewGroup.Visibility = Visibility.Collapsed;
            _previewKey = null;
            return;
        }

        string? archetype = (string?)SaveSessionManager.GetValue(NmsCompanionPaths.SpeciesArchetypePath(index));
        var oslToken = SaveSessionManager.GetValue(NmsCompanionPaths.DescriptorsPath(index));
        var descriptors = new List<string>();
        if (oslToken is Newtonsoft.Json.Linq.JArray osl)
        {
            foreach (var item in osl)
            {
                var val = (string?)item;
                if (!string.IsNullOrEmpty(val)) descriptors.Add(val);
            }
        }

        string? rig = await CreatureModelService.ResolveRigAsync(archetype, descriptors);
        if (version != _loadVersion) return;

        if (string.IsNullOrEmpty(rig))
        {
            CompanionPreviewGroup.Visibility = Visibility.Collapsed;
            _previewKey = null;
            return;
        }

        string key = $"{index}|{rig}|{string.Join(",", descriptors)}";
        if (key == _previewKey) return;
        _previewKey = key;

        CompanionPreviewGroup.Visibility = Visibility.Visible;
        if (!string.IsNullOrEmpty(ViewModel.SpeciesName))
            CompanionPreviewTitle.Text = ViewModel.SpeciesName.ToUpperInvariant();
        else if (!string.IsNullOrEmpty(ViewModel.Species))
            CompanionPreviewTitle.Text = ViewModel.Species.ToUpperInvariant();
        else
            CompanionPreviewTitle.Text = "COMPANION 3D PREVIEW";

        await CompanionPreview.ShowCreatureAsync(rig, descriptors);
    }

    private void ShowResetButton() =>
        PageResetBtn.Visibility = _selectedIndex >= 0 && SaveSessionManager.HasStagedEditsUnder(NmsCompanionPaths.CompanionPath(_selectedIndex))
            ? Visibility.Visible : Visibility.Collapsed;

    private void ShowSeeds(int index)
    {
        SeedBox.Show(index < 0 ? null : NmsCompanionPaths.SeedPath(index));
        BattleAbilitySeedBox.Show(index < 0 ? null : NmsCompanionPaths.RollSeedPrimaryPath(index));
        BattleAbilitySeed2Box.Show(index < 0 ? null : NmsCompanionPaths.RollSeedSecondaryPath(index));
    }

    private void Seed_Changed(object? sender, SeedChange change)
    {
        // Generate rolls both of the pet's colour-seed slots together (they were
        // always found mirrored on real saves); a pasted seed sets just the primary.
        if (sender == SeedBox && change.Generated && _selectedIndex >= 0)
            SaveSessionManager.StageEdit(change.Seed, NmsCompanionPaths.BoneScaleSeedPath(_selectedIndex));
    }

    private void GenerateColourOverrideSeed_Click(object sender, RoutedEventArgs e) => ViewModel.GenerateColourOverrideSeed();

    /// <summary>Undoes every staged change to the selected pet (and only that pet).</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _previewKey = null;
        if (_selectedIndex >= 0) SaveSessionManager.RevertEditsUnder(NmsCompanionPaths.CompanionPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Collapsed;
        LoadCompanionList();
    }
}
