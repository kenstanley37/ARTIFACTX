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
/// selected one's fields (CompanionViewModel). Reloads on every staged edit and save
/// switch; Reset undoes every change to the selected pet.
/// </summary>
public sealed partial class CompanionsPage : Page
{
    private List<CompanionEntry> _companions = new();
    private int _selectedIndex = -1;
    private int _loadVersion;

    public CompanionViewModel ViewModel { get; } = new();

    public CompanionsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;
        ViewModel.NameChanged += (_, _) => LoadCompanionList();

        LoadCompanionList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadCompanionList);

    /// <summary>Static events hold their subscribers: without this every past visit
    /// would stay subscribed and reload on every edit anywhere in the app.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private void LoadCompanionList()
    {
        _companions = SaveSessionManager.IsSaveLoaded ? CompanionService.ReadCompanions() : new();
        if (_companions.All(c => c.Index != _selectedIndex))
            _selectedIndex = _companions.FirstOrDefault()?.Index ?? -1;

        ShowStrip();
        _ = LoadSelectedAsync();
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
        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsCompanionPaths.CompanionPath(index))
            ? Visibility.Visible : Visibility.Collapsed;
    }

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
        if (_selectedIndex >= 0) SaveSessionManager.RevertEditsUnder(NmsCompanionPaths.CompanionPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Collapsed;
        LoadCompanionList();
    }
}
