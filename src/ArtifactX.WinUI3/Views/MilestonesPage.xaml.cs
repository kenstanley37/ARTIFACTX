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
/// tree (Milestones/Lifeforms/Factions, 12 categories total). 6 are wired up
/// to real save data so far (see MappedCategories below) - the rest are
/// real, correctly-named placeholders with no confirmed stat-ID mapping yet.
/// Shipped incrementally, one round of screenshot cross-referencing at a
/// time, rather than guessing at all 12 up front: GLOBAL_STATS has 457 total
/// entries with no decoded name table, and Arena League alone needed several
/// correction rounds to get right for just 5 stats - guessing broadly would
/// very likely ship wrong mappings, worse than shipping nothing since a
/// wrong number silently misleads rather than obviously not existing. See
/// NmsPlayerStatsPaths' doc comment for the exact cross-referencing done for
/// each field below.
/// </summary>
public sealed partial class MilestonesPage : Page
{
    private sealed record CategoryDef(string Tag, string DisplayName, string Group);
    private sealed record MilestoneField(string Label, string StatId);

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

    /// <summary>Every field below is confirmed exact (or near-exact with an
    /// explained gap) against a real Catalog &amp; Guide screenshot - see
    /// NmsPlayerStatsPaths' doc comment for the full cross-referencing per
    /// field, including why a couple of same-category fields (Gek's
    /// "Smuggling Run", Exploration's "Planetary Zoology") are missing -
    /// both show 0 in-game with too many ambiguous 0-valued candidates in
    /// GLOBAL_STATS to confirm from a single screenshot.</summary>
    private static readonly Dictionary<string, List<MilestoneField>> MappedCategories = new()
    {
        ["ArenaLeague"] = new()
        {
            new("Champions Defeated", "PB_BOSS_WINS"),
            new("Holo-Arena Victories", "PB_WINS"),
            new("Apex Companions", "PB_PETS_MAXED"),
            new("Hatch Eggs", "EGGS_HATCHED"),
            new("Iteration: Oceanus", "PB_D_NEXUS"),
        },
        ["ExplorationMilestones"] = new()
        {
            new("Overall Journey", "JM"),
            new("Alien Encounters", "ALIENS_MET"),
            new("Words Collected", "WORDS_LEARNT"),
            new("Space Exploration", "DIST_WARP"),
        },
        ["SurvivalMilestones"] = new()
        {
            new("On-foot Exploration", "DIST_WALKED"),
            new("Extreme Survival", "LONGEST_LIFE_EX"),
            new("Sentinels Destroyed", "SENTINEL_KILLS"),
            new("Units Accrued", "MONEY"),
            new("Ships Destroyed", "ENEMIES_KILLED"),
        },
        ["TheGek"] = new()
        {
            new("Standing", "TRA_STANDING"),
            new("Missions Completed", "TDONE_MISSIONS"),
            new("Words Learned", "TWORDS_LEARNT"),
            new("Systems Visited", "TSEEN_SYSTEMS"),
        },
        ["TheVyKeen"] = new()
        {
            new("Standing", "WAR_STANDING"),
            new("Missions Completed", "WDONE_MISSIONS"),
            new("Words Learned", "WWORDS_LEARNT"),
            new("Systems Visited", "WSEEN_SYSTEMS"),
            new("Walkers Destroyed", "WALKERS_KILLED"),
        },
        ["TheKorvax"] = new()
        {
            new("Standing", "EXP_STANDING"),
            new("Missions Completed", "EDONE_MISSIONS"),
            new("Words Learned", "EWORDS_LEARNT"),
            new("Systems Visited", "ESEEN_SYSTEMS"),
            new("Nanite Clusters", "NANITES_EVER"),
        },
    };

    private const string DefaultCategory = "ArenaLeague";

    private readonly Dictionary<string, Button> _categoryButtons = new();
    private string _selectedCategory = DefaultCategory;
    private bool _suppressChangeEvent;

    public MilestonesPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        BuildCategoryList();
        SelectCategory(DefaultCategory);
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (MappedCategories.ContainsKey(_selectedCategory)) LoadMappedCategoryStats();
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

        if (MappedCategories.TryGetValue(tag, out var fields))
        {
            MappedCategoryPanel.Visibility = Visibility.Visible;
            PlaceholderPanel.Visibility = Visibility.Collapsed;

            var category = Categories.First(c => c.Tag == tag);
            MappedCategoryHeaderTxt.Text = category.DisplayName;
            MappedCategoryNoteTxt.Text = tag == "ArenaLeague"
                ? "Save-wide, not tied to any specific companion (a different number from the per-companion \"Holo-Arena Victories\" field on the Companions page)."
                : "Save-wide - all fields below confirmed exact against a real Catalog & Guide screenshot.";

            BuildMappedCategoryFields(fields);
            LoadMappedCategoryStats();
        }
        else
        {
            MappedCategoryPanel.Visibility = Visibility.Collapsed;
            PlaceholderPanel.Visibility = Visibility.Visible;
            PlaceholderHeaderTxt.Text = Categories.First(c => c.Tag == tag).DisplayName;
            PageResetBtn.Visibility = Visibility.Collapsed;
        }
    }

    private readonly Dictionary<string, NumberBox> _mappedFieldBoxes = new();

    private void BuildMappedCategoryFields(List<MilestoneField> fields)
    {
        MappedCategoryFieldsPanel.Children.Clear();
        _mappedFieldBoxes.Clear();

        foreach (var field in fields)
        {
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = field.Label, Opacity = 0.7, FontSize = 12 });

            var box = new NumberBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Minimum = 0,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden
            };
            box.ValueChanged += (_, args) => SetStat(field.StatId, args.NewValue);
            box.LostFocus += (_, _) => SetStat(field.StatId, box.Value);

            panel.Children.Add(box);
            MappedCategoryFieldsPanel.Children.Add(panel);
            _mappedFieldBoxes[field.StatId] = box;
        }
    }

    // --- Generic GLOBAL_STATS access, shared by every mapped category ---

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

    private void LoadMappedCategoryStats()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            foreach (var box in _mappedFieldBoxes.Values) box.Value = double.NaN;
            PageResetBtn.Visibility = Visibility.Collapsed;
            return;
        }

        _suppressChangeEvent = true;

        int groupIndex = ResolveGlobalStatGroupIndex();
        foreach (var (statId, box) in _mappedFieldBoxes)
            box.Value = ReadStat(groupIndex, statId);

        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsPlayerStatsPaths.StatGroupsArrayPath)
            ? Visibility.Visible : Visibility.Collapsed;

        _suppressChangeEvent = false;
    }

    /// <summary>Most stats are plain ints, but a few (e.g. DIST_WALKED,
    /// LONGEST_LIFE_EX) store a float instead - the two are mutually
    /// exclusive per stat, so check the int path first and only fall back
    /// to the float path if that's null. Rounded to 2 decimals for display;
    /// the ~0.000001-scale precision this loses is irrelevant next to
    /// values in the thousands/millions.</summary>
    private static double ReadStat(int groupIndex, string statId)
    {
        int statIndex = ResolveStatIndex(groupIndex, statId);
        if (statIndex < 0) return 0; // stat not present in this save - treat as untouched/zero

        var intValue = SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatIntValuePath(groupIndex, statIndex));
        if (intValue is not null) return intValue.Value<double>();

        var floatValue = SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatFloatValuePath(groupIndex, statIndex));
        return floatValue is not null ? Math.Round(floatValue.Value<double>(), 2) : 0;
    }

    /// <summary>Stages a new value for one stat by id, re-resolving its
    /// position each time (see ResolveGlobalStatGroupIndex's doc comment on
    /// why this isn't cached). Silently no-ops if the stat can't be found -
    /// this can only happen for a stat that's genuinely never been touched
    /// in this save at all. Writes to whichever value type (int/float) the
    /// stat already used, determined by which one is currently populated,
    /// so an edited float stat stays a float and vice versa.</summary>
    private void SetStat(string statId, double newValue)
    {
        if (_suppressChangeEvent || double.IsNaN(newValue)) return;

        int groupIndex = ResolveGlobalStatGroupIndex();
        int statIndex = ResolveStatIndex(groupIndex, statId);
        if (statIndex < 0) return;

        bool isFloat = SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatIntValuePath(groupIndex, statIndex)) is null;
        if (isFloat)
            SaveSessionManager.StageEdit(newValue, NmsPlayerStatsPaths.StatFloatValuePath(groupIndex, statIndex));
        else
            SaveSessionManager.StageEdit((int)newValue, NmsPlayerStatsPaths.StatIntValuePath(groupIndex, statIndex));

        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEditsUnder(NmsPlayerStatsPaths.StatGroupsArrayPath);
        if (MappedCategories.ContainsKey(_selectedCategory)) LoadMappedCategoryStats();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
