using Microsoft.UI;
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
/// Mirrors the in-game Catalog &amp; Guide -&gt; Milestones screen's own category
/// tree (Milestones/Lifeforms/Factions, 12 categories total). Only "Arena
/// League" is wired up to real save data so far (moved here from the
/// Companions page 2026-08-01 - it's a Factions milestone, not really a
/// per-companion concern) - the other 11 are real, correctly-named
/// placeholders with no confirmed stat-ID mapping yet. Deliberately shipped
/// this way rather than guessing at stat IDs for all 12 at once: every other
/// page in this app got its field mappings confirmed one at a time against
/// real screenshots (Arena League itself needed several correction rounds
/// even for just 5 stats), and GLOBAL_STATS has 457 total entries with no
/// decoded name table to cross-reference against - guessing broadly here
/// would very likely ship wrong mappings, the same mistake already made and
/// caught twice on Arena League alone.
/// </summary>
public sealed partial class MilestonesPage : Page
{
    private sealed record CategoryDef(string Tag, string DisplayName, string Group);

    private static readonly CategoryDef[] Categories =
    {
        new("ExplorationMilestones", "Exploration Milestones", "Milestones"),
        new("SurvivalMilestones", "Survival Milestones", "Milestones"),
        new("PreviousExpeditions", "Previous Expeditions", "Milestones"),
        new("TheGek", "The Gek", "Lifeforms"),
        new("TheVyKeen", "The Vy'keen", "Lifeforms"),
        new("TheKorvax", "The Korvax", "Lifeforms"),
        new("MerchantsGuild", "Merchants Guild", "Factions"),
        new("MercenariesGuild", "Mercenaries Guild", "Factions"),
        new("ExplorersGuild", "Explorers Guild", "Factions"),
        new("ArenaLeague", "Arena League", "Factions"),
        new("Outlaws", "Outlaws", "Factions"),
        new("TheAutophage", "The Autophage", "Factions"),
    };

    private const string MappedCategory = "ArenaLeague";

    private readonly Dictionary<string, Button> _categoryButtons = new();
    private string _selectedCategory = MappedCategory;
    private bool _suppressChangeEvent;

    public MilestonesPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        BuildCategoryList();
        SelectCategory(MappedCategory);
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_selectedCategory == MappedCategory) LoadArenaLeagueStats();
        });

    /// <summary>Without this, the constructor's subscriptions above never
    /// get released across page navigation (Frame.Navigate makes a fresh
    /// Page instance every visit, no NavigationCacheMode set anywhere in
    /// this app) - see ShipsPage.Page_Unloaded for the full history of the
    /// bug this pattern fixes.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private void BuildCategoryList()
    {
        foreach (var category in Categories)
        {
            var button = new Button
            {
                Content = category.DisplayName,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 6, 10, 6),
                BorderThickness = new Thickness(1)
            };
            button.Click += (_, _) => SelectCategory(category.Tag);
            _categoryButtons[category.Tag] = button;

            var targetPanel = category.Group switch
            {
                "Milestones" => MilestonesGroupPanel,
                "Lifeforms" => LifeformsGroupPanel,
                _ => FactionsGroupPanel
            };
            targetPanel.Children.Add(button);
        }

        ApplyCategoryButtonStyles();
    }

    private void ApplyCategoryButtonStyles()
    {
        foreach (var (tag, button) in _categoryButtons)
        {
            bool isSelected = tag == _selectedCategory;
            button.BorderBrush = new SolidColorBrush(isSelected
                ? Color.FromArgb(255, 255, 157, 0)
                : Color.FromArgb(90, 90, 98, 112));
            button.BorderThickness = new Thickness(isSelected ? 2 : 1);
            button.Background = new SolidColorBrush(isSelected
                ? Color.FromArgb(60, 255, 157, 0)
                : Color.FromArgb(20, 255, 255, 255));
        }
    }

    private void SelectCategory(string tag)
    {
        _selectedCategory = tag;
        ApplyCategoryButtonStyles();

        bool isArenaLeague = tag == MappedCategory;
        ArenaLeaguePanel.Visibility = isArenaLeague ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderPanel.Visibility = isArenaLeague ? Visibility.Collapsed : Visibility.Visible;

        if (isArenaLeague)
        {
            LoadArenaLeagueStats();
        }
        else
        {
            PlaceholderHeaderTxt.Text = Categories.First(c => c.Tag == tag).DisplayName;
            PageResetBtn.Visibility = Visibility.Collapsed;
        }
    }

    // --- Arena League: moved from CompanionsPage.xaml.cs (2026-08-01) ---

    /// <summary>GcPlayerStateData.Stats (NmsPlayerStatsPaths) is a "find by
    /// id" structure, not a fixed-index array - the GLOBAL_STATS group's
    /// index and a target stat's index within it are resolved by scanning
    /// the live JSON at read/write time, not cached (a ~457-entry linear
    /// scan is cheap enough that avoiding staleness across save switches
    /// wins over the trivial saved cost of caching). Returns -1 if not
    /// found (e.g. no save loaded).</summary>
    private static int ResolveGlobalStatGroupIndex()
    {
        if (SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) is not JArray groups)
            return -1;

        for (int i = 0; i < groups.Count; i++)
        {
            string groupId = (groups[i]?[":rc"]?.Value<string>() ?? "").TrimStart('^');
            if (string.Equals(groupId, "GLOBAL_STATS", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>Finds a stat's index within the given group's own Stats
    /// list by its raw id (e.g. "PB_WINS", no "^" prefix needed). Returns
    /// -1 if not found - some stats simply don't exist in every save (e.g.
    /// never-touched counters may be omitted entirely rather than zeroed).</summary>
    private static int ResolveStatIndex(int groupIndex, string statId)
    {
        if (groupIndex < 0) return -1;
        if (SaveSessionManager.GetValue(NmsPlayerStatsPaths.GroupStatsArrayPath(groupIndex)) is not JArray stats)
            return -1;

        for (int i = 0; i < stats.Count; i++)
        {
            string id = (stats[i]?["b2n"]?.Value<string>() ?? "").TrimStart('^');
            if (string.Equals(id, statId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private void LoadArenaLeagueStats()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            ArenaChampionsBox.Value = double.NaN;
            ArenaVictoriesBox.Value = double.NaN;
            ArenaCompanionsBox.Value = double.NaN;
            ArenaEggsBox.Value = double.NaN;
            ArenaOceanusBox.Value = double.NaN;
            PageResetBtn.Visibility = Visibility.Collapsed;
            return;
        }

        _suppressChangeEvent = true;

        int groupIndex = ResolveGlobalStatGroupIndex();
        ArenaChampionsBox.Value = ReadArenaStat(groupIndex, "PB_BOSS_WINS");
        ArenaVictoriesBox.Value = ReadArenaStat(groupIndex, "PB_WINS");
        ArenaCompanionsBox.Value = ReadArenaStat(groupIndex, "PB_PETS_MAXED");
        ArenaEggsBox.Value = ReadArenaStat(groupIndex, "EGGS_HATCHED");
        ArenaOceanusBox.Value = ReadArenaStat(groupIndex, "PB_D_NEXUS");

        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsPlayerStatsPaths.StatGroupsArrayPath)
            ? Visibility.Visible : Visibility.Collapsed;

        _suppressChangeEvent = false;
    }

    private static double ReadArenaStat(int groupIndex, string statId)
    {
        int statIndex = ResolveStatIndex(groupIndex, statId);
        if (statIndex < 0) return 0; // stat not present in this save - treat as untouched/zero

        return SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatIntValuePath(groupIndex, statIndex))?.Value<double>() ?? 0;
    }

    /// <summary>Stages a new value for one Arena League stat by id, re-
    /// resolving its position each time (see ResolveGlobalStatGroupIndex's
    /// doc comment on why this isn't cached). Silently no-ops if the stat
    /// can't be found - this can only happen for a stat that's genuinely
    /// never been touched in this save at all, which none of the 5 exposed
    /// here should be for an account with real Arena League progress.</summary>
    private void SetArenaStat(string statId, double newValue)
    {
        if (_suppressChangeEvent || double.IsNaN(newValue)) return;

        int groupIndex = ResolveGlobalStatGroupIndex();
        int statIndex = ResolveStatIndex(groupIndex, statId);
        if (statIndex < 0) return;

        SaveSessionManager.StageEdit((int)newValue, NmsPlayerStatsPaths.StatIntValuePath(groupIndex, statIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ArenaChampionsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SetArenaStat("PB_BOSS_WINS", args.NewValue);
    private void ArenaChampionsBox_LostFocus(object sender, RoutedEventArgs e) => SetArenaStat("PB_BOSS_WINS", ArenaChampionsBox.Value);

    private void ArenaVictoriesBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SetArenaStat("PB_WINS", args.NewValue);
    private void ArenaVictoriesBox_LostFocus(object sender, RoutedEventArgs e) => SetArenaStat("PB_WINS", ArenaVictoriesBox.Value);

    private void ArenaCompanionsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SetArenaStat("PB_PETS_MAXED", args.NewValue);
    private void ArenaCompanionsBox_LostFocus(object sender, RoutedEventArgs e) => SetArenaStat("PB_PETS_MAXED", ArenaCompanionsBox.Value);

    private void ArenaEggsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SetArenaStat("EGGS_HATCHED", args.NewValue);
    private void ArenaEggsBox_LostFocus(object sender, RoutedEventArgs e) => SetArenaStat("EGGS_HATCHED", ArenaEggsBox.Value);

    private void ArenaOceanusBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SetArenaStat("PB_D_NEXUS", args.NewValue);
    private void ArenaOceanusBox_LostFocus(object sender, RoutedEventArgs e) => SetArenaStat("PB_D_NEXUS", ArenaOceanusBox.Value);

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEditsUnder(NmsPlayerStatsPaths.StatGroupsArrayPath);
        LoadArenaLeagueStats();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
