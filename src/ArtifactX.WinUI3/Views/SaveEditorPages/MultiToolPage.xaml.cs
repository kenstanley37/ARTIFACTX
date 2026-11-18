using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views.SaveEditorPages;

/// <summary>
/// The Multi-Tool page: wiring only. The strip, details band and tech card bind to <see cref="ViewModel"/>
/// (MultiToolPageViewModel); save reads and edits are MultiToolService's. This page sets up the tech grid and list,
/// follows the game (live fields and tools), shows dialogs and draws the 3D preview.
/// </summary>
public sealed partial class MultiToolPage : Page, ILoadoutTemplateHost
{
    public MultiToolPageViewModel ViewModel { get; } = new();

    public MultiToolPage()
    {
        InitializeComponent();
        TechCard.GroupOf = TechListPanel.TypeOf;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        LiveItemsService.Changed += OnLiveItemsChanged;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSaved;
        Unloaded += Page_Unloaded;

        LoadToolList();
        TemplatesPanel.Host = this;
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (StatsBoxes.IsStaging) return; // own stat edit: nothing else changes
        DispatcherQueue.TryEnqueue(() => LoadToolList());
    }

    /// <summary>The game added or dropped a tool while it played: only the strip changes, the tool on screen stays.</summary>
    private void OnLiveItemsChanged(LiveCollection list)
    {
        if (list == LiveCollection.MultiTools) DispatcherQueue.TryEnqueue(() => LoadToolList(live: true));
    }

    /// <summary>Releases the session subscriptions: every visit makes a new page, and a dead one left subscribed re-ran
    /// its full reload on every edit anywhere (a reported multi-second slowdown).</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
        LiveItemsService.Changed -= OnLiveItemsChanged;
        SaveSessionManager.GameSavedLoadedSlot -= OnGameSaved;
    }

    /// <summary>What follows the game for the tool on screen (LiveFieldsService), with the field that shows it; the
    /// class and stats before the inventory holding them.</summary>
    private (string[] Path, Func<UIElement> Field)[] LiveFields(int tool)
    {
        string i = tool.ToString(CultureInfo.InvariantCulture);
        var tech = NmsInventoryContainer.MultiToolTechnologyPath(tool);
        return new (string[], Func<UIElement>)[]
        {
            (new[] { "vLc", "6f=", "SuJ", i, "NKm" }, () => NameEditBox),
            (new[] { "vLc", "6f=", "SuJ", i, "NTx" }, () => TypeComboBox),
            (tech.Append("B@N").Append("1o6").ToArray(), () => ClassPicker),
            (tech.Append("@bB").ToArray(), () => StatsBoxes),
            (tech, () => TechGrid),
        };
    }

    /// <summary>The game saved: the tool on screen takes its values where nothing is edited here, and each changed field
    /// pulses once - no reload (2026-10-22).</summary>
    private void OnGameSaved(JObject disk)
    {
        if (ViewModel.SelectedIndex < 0) return;
        var fields = LiveFields(ViewModel.SelectedIndex);
        var changed = LiveFieldsService.Adopt(disk, fields.Select(f => f.Path),
            fields.Where(f => f.Field() == TechGrid).Select(f => f.Path).ToList());
        if (changed.Count == 0) return;
        LoadToolList();
        foreach (var field in fields.Where(f => changed.Contains(f.Path)).Select(f => f.Field()).Distinct())
            FieldHighlight.Flash(field);
    }

    /// <summary>Re-reads the owned tools (MultiToolPageViewModel.Reload) and, unless a live update left it alone, the
    /// tool on screen; then names the unnamed ones.</summary>
    private void LoadToolList(bool live = false)
    {
        if (ViewModel.Reload(live)) LoadSelectedTool();
        else if (!SaveSessionManager.IsSaveLoaded) { TechGrid.ViewModel = null; TechGrid.Refresh(); }
        _ = ViewModel.LabelUnnamedAsync();
    }

    private void ToolStrip_ChipClicked(object? sender, EquipmentChipViewModel chip)
    {
        if (ViewModel.Select(chip.Index)) LoadSelectedTool();
    }

    private async void LoadSelectedTool() => await LoadSelectedToolAsync();

    private async Task LoadSelectedToolAsync()
    {
        ShowToolPreview();
        await ViewModel.LoadSelectedAsync();
        TechGrid.ViewModel = ViewModel.Tech;
        if (ViewModel.Tech is null)
        {
            TechGrid.Refresh();
            TechLayout.ShowList = false;
            StatsBoxes.Clear();
            return;
        }

        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        // Every tech on the 94 multi-tools in the real saves is "Weapon" (2026-10-13).
        TechGrid.AllowedUsageCategories = new[] { "Weapon" };
        // Reloads run on every strip click: unsubscribe first so one edit fires the handler once.
        TechGrid.CellChanged -= TechGrid_CellChanged;
        TechGrid.CellChanged += TechGrid_CellChanged;
        TechGrid.Refresh();

        // The tech a multi-tool can fit, to click in - the same list limits the right-click search.
        TechLayout.ShowList = true;
        TechListPanel.IsBestClass = () => ViewModel.Tech?.CurrentClass == "S";
        TechListPanel.SwitchToBestClassAsync = async () =>
        {
            ViewModel.ChooseClass("S");
            await LoadSelectedToolAsync();
        };
        TechListPanel.MoreRoomHint = () => ViewModel.Tech?.CurrentClass == "S" ? null
            : "S class has more slots - change the class above to fit more.";
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            var tech = await CatalogService.GetTechForAsync(new[] { "Weapon" });
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modules = await CatalogService.GetUpgradeModulesAsync(MultiToolService.ModuleFamilies);
            return Controls.ItemListPanel.SortByGroups(tech.Concat(modules), MultiToolService.TechGroup, MultiToolService.TechGroupOrder);
        }, "technology", "technologies", "Technology", MultiToolService.TechGroup);

        UpdateStatDisplay();
    }

    // ------------------------------------------------------------ details band

    private void TechGrid_CellChanged(object? sender, EventArgs e) => ViewModel.MarkEdited();

    private void Inventory_Edited(object? sender, EventArgs e) => ViewModel.MarkEdited();

    /// <summary>Stages the rename when the box loses focus - not on every keystroke, which would rebuild the strip
    /// mid-typing and fight the cursor.</summary>
    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CommitName(NameEditBox.Text)) LoadToolList();
    }

    private void NameUpdateLink_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.HideNameUpdateLink();
        App.MainWindowInstance?.StartGameDataUpdate();
    }

    /// <summary>A class changes the slot count (MultiToolCapacity), so the grid is rebuilt. B@N.1o6 is a plain string,
    /// independent of the tool's seed, so it's a safe single write.</summary>
    private void ClassPicker_ClassChosen(object? sender, string letter)
    {
        ViewModel.ChooseClass(letter);
        LoadSelectedTool();
    }

    private void UpdateStatDisplay()
    {
        StatsBoxes.Show(MultiToolService.StatsPath(ViewModel.SelectedIndex), StatBonusService.MultiToolStats);
        ShowInGameTotals();
    }

    /// <summary>The game's Damage Potential and Scanner Range under the base stats (#17); dropped if another tool was
    /// picked meanwhile.</summary>
    private async void ShowInGameTotals()
    {
        int index = ViewModel.SelectedIndex;
        var totals = await OwnedMultiToolDisplayStatsService.EvaluateAsync(index);
        if (index == ViewModel.SelectedIndex) StatsBoxes.ShowInGameTotals(totals);
    }

    private void StatsBoxes_Edited(object? sender, EventArgs e)
    {
        ViewModel.MarkEdited();
        ShowInGameTotals();
    }

    /// <summary>Undoes every staged change to the selected tool - tech, name and type.</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetSelected();
        LoadToolList();
    }

    // ------------------------------------------------------------ copy / delete / add

    /// <summary>Copies the selected tool's whole loadout onto another owned tool, after showing what else that needs
    /// (slots to unlock, a class difference with an opt-in to match it). Only :No, hl? and (if chosen) the class change.</summary>
    private async void CopyTechStackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is not { } source || ViewModel.Tech is not { } sourceTech) return;
        var others = ViewModel.Tools.Where(t => t.Index != source.Index).ToList();
        if (others.Count == 0)
        {
            await new ContentDialog
            {
                Title = "No other tools",
                Content = "You only own one multi-tool, so there's nothing to copy this stack onto.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }

        var pickerList = new ListView { SelectionMode = ListViewSelectionMode.Single, ItemsSource = others };
        var pickerDialog = new ContentDialog
        {
            Title = $"Copy {source.Name}'s tech stack to...",
            Content = pickerList,
            PrimaryButtonText = "Next",
            IsPrimaryButtonEnabled = false,
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        pickerList.SelectionChanged += (_, _) => pickerDialog.IsPrimaryButtonEnabled = pickerList.SelectedIndex >= 0;
        if (await pickerDialog.ShowAsync() != ContentDialogResult.Primary || pickerList.SelectedItem is not OwnedMultiTool target) return;

        var capacity = await CatalogService.GetMultiToolCapacityAsync();
        var targetTech = new InventoryGridViewModel(NmsInventoryContainer.MultiToolTechnologyPath(target.Index), MultiToolCapacity.Columns,
            MultiToolCapacity.SlotsToRows(MultiToolService.TechSlots(capacity, MultiToolService.ClassLetter(target.Index))));
        targetTech.Load();
        var (confirmed, alsoMatchClass) = await TechStackCopyConfirmation.ShowAsync(XamlRoot, $"a copy of {source.Name}'s",
            sourceTech.CurrentClass, sourceTech.Cells.Where(c => c.IsOccupied).Select(c => (c.X, c.Y)), target.Name, targetTech, targetTech.CurrentClass);
        if (!confirmed) return;
        targetTech.CopyTechStackFrom(sourceTech, alsoMatchClass);
        ViewModel.MarkEdited();
        LoadToolList();
    }

    /// <summary>Deletes the selected tool after a strict confirmation that can keep it as a template first. The tool in
    /// hand can't go - the game needs one.</summary>
    private async void DeleteToolBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is not { } tool || ViewModel.Tech is not { } tech) return;
        if (tool.IsGameActive)
        {
            await DeleteConfirmation.ShowBlockedAsync(XamlRoot, "That's the Multi-Tool you're holding",
                $"{tool.Name} is your equipped Multi-Tool, and the game always needs one. Switch to another in game, save, then delete this one here.");
            return;
        }
        var choice = await DeleteConfirmation.ShowAsync(XamlRoot, tool.Name, "its tech, its stats and its look");
        if (!choice.Confirmed) return;
        if (choice.SaveTemplateFirst)
        {
            await LoadoutTemplateService.SaveAsync(MultiToolService.CaptureFullBuild(tech, $"{tool.Name} (saved before deleting)", tool));
            await TemplatesPanel.RefreshAsync();
        }
        NewFromTemplateService.RemoveMultiTool(tool.Index);
        ViewModel.SelectAfterReload();
        LoadToolList();
    }

    /// <summary>Full Build templates add a whole new tool in the first empty slot (NewFromTemplateService) - the tools
    /// you have are never changed. Tech Stack templates Apply onto the selected one.</summary>
    private async Task AddAsNewAsync(NmsLoadoutTemplate template)
    {
        int skeleton = ViewModel.Selected?.Index ?? ViewModel.Tools.FirstOrDefault()?.Index ?? -1;
        if (skeleton < 0 || NewFromTemplateService.FirstEmptyMultiToolSlot() is not int slot)
        {
            await new ContentDialog
            {
                Title = "No room for another Multi-Tool",
                Content = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = MultiToolService.NoRoomText(hasTool: skeleton >= 0) },
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }
        var (title, body) = MultiToolService.AddAsNewText(template, slot);
        var confirm = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = body },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        if (NewFromTemplateService.AddMultiTool(template, skeleton) is not int added) return;
        ViewModel.SelectAfterReload(added);
        LoadToolList();
    }

    /// <summary>Applies a template to the tool on screen - the target is whatever you're looking at. A class change
    /// (alsoMatchClass) can change the slot count, so the grid is rebuilt.</summary>
    private async Task ApplyTemplateAsync(NmsLoadoutTemplate template)
    {
        if (ViewModel.Selected is not { } target || ViewModel.Tech is not { } tech) return;
        var (confirmed, alsoMatchClass) = await TechStackCopyConfirmation.ShowAsync(XamlRoot, $"the \"{template.Name}\" template",
            template.SourceClass, template.UnlockedPositions.Select(p => (p.X, p.Y)), target.Name, tech, tech.CurrentClass);
        if (!confirmed) return;
        tech.ApplyTemplate(template, alsoMatchClass);
        ViewModel.MarkEdited();
        LoadSelectedTool();
    }

    string? ILoadoutTemplateHost.CurrentName => ViewModel.Tech is null ? null : ViewModel.Selected?.Name;

    bool ILoadoutTemplateHost.HasTech => ViewModel.Tech?.Cells.Any(c => c.IsOccupied) == true;

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureTechStack(string name) =>
        MultiToolService.CaptureTechStack(ViewModel.Tech!, name, ViewModel.Selected!);

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureFullBuild(string name) =>
        MultiToolService.CaptureFullBuild(ViewModel.Tech!, name, ViewModel.Selected!);

    Task ILoadoutTemplateHost.ApplyTechStackAsync(NmsLoadoutTemplate template) => ApplyTemplateAsync(template);

    Task ILoadoutTemplateHost.UseFullBuildAsync(NmsLoadoutTemplate template) => AddAsNewAsync(template);

    // ------------------------------------------------------------ 3D preview

    private int _previewVersion;

    /// <summary>The selected tool's model. Cleared first: the previous tool stayed on screen for a few seconds while the
    /// next one built (owner, 2026-11-14).</summary>
    private async void ShowToolPreview()
    {
        int version = ++_previewVersion;
        int index = ViewModel.SelectedIndex;
        if (string.IsNullOrEmpty(MultiToolService.Scene(index)) || GameDataService.Game is null)
        {
            ToolPreviewGroup.Visibility = Visibility.Collapsed;
            return;
        }
        ToolPreviewGroup.Visibility = Visibility.Visible;
        ToolPreviewView.ShowMessage("Building this tool's model from your game files...");
        ToolPreviewNote.Text = "";
        var preview = await MultiToolService.BuildOwnedPreviewAsync(index, () => version != _previewVersion);
        if (preview is null || version != _previewVersion) return;
        ToolPreviewView.Show(preview.Mesh, MultiToolService.MaterialColour, preview.Painted);
        ToolPreviewNote.Text = preview.Note;
    }
}
