using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
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
/// filtering on fH8 silently dropped every unnamed pet from the Companions
/// page's list). The fancy auto-generated name shown in-game (e.g. "Riverpito") is
/// computed client-side for display only and is never written to the save.
/// Only the fields confirmed against real save data (see NmsCompanionPaths) are
/// exposed here - Current Mood, Age, Gender, the fancy species name, and
/// Weight have no confirmed backing field (cross-referenced against
/// NMSCD/Creature-Builder's own reverse-engineered save contract, which
/// doesn't have them either) and aren't exposed. Personality Traits ARE
/// real and stored (see NmsCompanionPaths.TraitsPath) - initially miscategorized
/// as an unrelated position vector until that cross-reference caught it.
/// </summary>
public sealed partial class CompanionsPage : Page
{
    private sealed record CompanionEntry(int Index, string SelectorLabel);

    private int _selectedIndex = -1;
    private List<CompanionEntry> _companions = new();
    private bool _suppressStatChangeEvent;

    // Species archetype (XID, e.g. "RODENT") -> catalog data, loaded once per
    // page lifetime from the CreatureSpecies category - see CatalogService.
    private Dictionary<string, (string DisplayName, string Rarity, string Description)>? _creatureSpecies;

    // Rig id (species XID lowercased, e.g. "trex") -> its full descriptor
    // option tree, fetched once per rig and reused across pets that share
    // one (e.g. multiple tamed Rodents) - see CatalogService.
    // GetCreatureDescriptorTreeAsync and NmsCompanionPaths.DescriptorsPath.
    private readonly Dictionary<string, List<CreatureDescriptorNode>> _descriptorTreeCache = new();

    public CompanionsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        LoadCompanionList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadCompanionList);

    /// <summary>Without this, the constructor's subscriptions above never
    /// get released across page navigation (Frame.Navigate makes a fresh
    /// Page instance every visit, no NavigationCacheMode set anywhere in
    /// this app) - every past visit leaves a dead instance permanently
    /// subscribed, re-running its full reload on every future edit
    /// anywhere in the app. Root cause of a reported slowdown where
    /// editing any page became multi-second after enough navigation.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private void LoadCompanionList()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _companions = new();
            _selectedIndex = -1;
            CompanionSelectorPanel.Children.Clear();
            ClearFields();
            return;
        }

        if (SaveSessionManager.GetValue(NmsCompanionPaths.CompanionArrayPath) is not JArray array)
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
        var companions = new List<CompanionEntry>();
        for (int i = 0; i < array.Count; i++)
        {
            // XID is "^" (a bare, contentless prefix) on a truly empty slot,
            // not "" - IsNullOrEmpty alone doesn't catch it.
            string xid = array[i]?["XID"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(xid.TrimStart('^'))) continue;

            string customName = array[i]?["fH8"]?.Value<string>() ?? "";
            string label = string.IsNullOrEmpty(customName) ? $"{FormatArchetype(xid)} #{i + 1}" : customName;
            companions.Add(new CompanionEntry(i, label));
        }

        _companions = companions;

        if (_selectedIndex < 0 || _companions.All(p => p.Index != _selectedIndex))
            _selectedIndex = _companions.FirstOrDefault()?.Index ?? -1;

        BuildSelectorStrip();
        LoadSelectedCompanion();
    }

    private void BuildSelectorStrip()
    {
        CompanionSelectorPanel.Children.Clear();

        foreach (var companion in _companions)
        {
            bool isSelected = companion.Index == _selectedIndex;

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = companion.SelectorLabel,
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
                _selectedIndex = companion.Index;
                BuildSelectorStrip();
                LoadSelectedCompanion();
            };

            CompanionSelectorPanel.Children.Add(button);
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
        DescriptorsPanel.Children.Clear();
        AdvancedFieldsPanel.Children.Clear();
    }

    private async void LoadSelectedCompanion()
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
        NameEditBox.Text = SaveSessionManager.GetValue(NmsCompanionPaths.NamePath(_selectedIndex))?.Value<string>() ?? "";

        string archetype = SaveSessionManager.GetValue(NmsCompanionPaths.SpeciesArchetypePath(_selectedIndex))?.Value<string>() ?? "";
        string archetypeId = archetype.TrimStart('^');
        SpeciesTxt.Text = FormatArchetype(archetype);

        _creatureSpecies ??= await CatalogService.GetCreatureSpeciesAsync();
        RarityTxt.Text = _creatureSpecies.TryGetValue(archetypeId, out var species) ? species.Rarity : "";

        string rigId = archetypeId.ToLowerInvariant();
        if (!_descriptorTreeCache.TryGetValue(rigId, out var descriptorTree))
        {
            descriptorTree = await CatalogService.GetCreatureDescriptorTreeAsync(rigId);
            _descriptorTreeCache[rigId] = descriptorTree;
        }
        BuildDescriptorsPanel(_selectedIndex, descriptorTree);

        ClimateTxt.Text = SaveSessionManager.GetValue(NmsCompanionPaths.NativeClimatePath(_selectedIndex))?.Value<string>() ?? "";

        SeedEditBox.Text = SaveSessionManager.GetValue(NmsCompanionPaths.SeedPath(_selectedIndex))?.Value<string>() ?? "";
        ColourBaseSeedActiveCheckBox.IsChecked = SaveSessionManager.GetValue(NmsCompanionPaths.ColourBaseSeedActivePath(_selectedIndex))?.Value<bool>() ?? false;
        ColourBaseSeedEditBox.Text = SaveSessionManager.GetValue(NmsCompanionPaths.ColourBaseSeedPath(_selectedIndex))?.Value<string>() ?? "";
        BattleAbilitySeedEditBox.Text = SaveSessionManager.GetValue(NmsCompanionPaths.RollSeedPrimaryPath(_selectedIndex))?.Value<string>() ?? "";
        BattleAbilitySeed2EditBox.Text = SaveSessionManager.GetValue(NmsCompanionPaths.RollSeedSecondaryPath(_selectedIndex))?.Value<string>() ?? "";

        ClassLetterOverrideActiveCheckBox.IsChecked = SaveSessionManager.GetValue(NmsCompanionPaths.ClassLetterOverrideActivePath(_selectedIndex))?.Value<bool>() ?? false;
        SetClassLetterSelection(ClassLetterHealthBox, SaveSessionManager.GetValue(NmsCompanionPaths.ClassLetterPath(_selectedIndex, 0))?.Value<string>());
        SetClassLetterSelection(ClassLetterAgilityBox, SaveSessionManager.GetValue(NmsCompanionPaths.ClassLetterPath(_selectedIndex, 1))?.Value<string>());
        SetClassLetterSelection(ClassLetterCombatBox, SaveSessionManager.GetValue(NmsCompanionPaths.ClassLetterPath(_selectedIndex, 2))?.Value<string>());
        UpdateClassLetterBoxesEnabled();

        double trust = SaveSessionManager.GetValue(NmsCompanionPaths.TrustPath(_selectedIndex))?.Value<double>() ?? 0;
        TrustStatBox.Value = Math.Round(trust * 100, 1);

        var traits = SaveSessionManager.GetValue(NmsCompanionPaths.TraitsPath(_selectedIndex)) as JArray;
        Trait1Box.Value = traits?.Count > 0 ? Math.Round(Math.Abs(traits[0].Value<double>()) * 100, 1) : double.NaN;
        Trait2Box.Value = traits?.Count > 1 ? Math.Round(Math.Abs(traits[1].Value<double>()) * 100, 1) : double.NaN;
        Trait3Box.Value = traits?.Count > 2 ? Math.Round(Math.Abs(traits[2].Value<double>()) * 100, 1) : double.NaN;

        GenesImprovedStatBox.Value = SaveSessionManager.GetValue(NmsCompanionPaths.GenesImprovedPath(_selectedIndex))?.Value<double>() ?? 0;

        double mutationProgress = SaveSessionManager.GetValue(NmsCompanionPaths.MutationProgressPath(_selectedIndex))?.Value<double>() ?? 0;
        MutationProgressStatBox.Value = Math.Round(mutationProgress * 100, 1);

        VictoriesStatBox.Value = SaveSessionManager.GetValue(NmsCompanionPaths.HoloArenaVictoriesPath(_selectedIndex))?.Value<double>() ?? 0;

        var points = SaveSessionManager.GetValue(NmsCompanionPaths.MutationPointsPath(_selectedIndex)) as JArray;
        AgilityPointsBox.Value = points?.Count > 0 ? points[0].Value<double>() : double.NaN;
        HealthPointsBox.Value = points?.Count > 1 ? points[1].Value<double>() : double.NaN;
        CombatPointsBox.Value = points?.Count > 2 ? points[2].Value<double>() : double.NaN;

        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsCompanionPaths.CompanionPath(_selectedIndex))
            ? Visibility.Visible : Visibility.Collapsed;

        BuildAdvancedFieldsPanel(_selectedIndex);

        _suppressStatChangeEvent = false;
    }

    /// <summary>Rebuilds a WHOLE new osl array for a different top-level
    /// archetype choice (e.g. TREX's _TREX_4 -> _TREX_3XRARE) - needed
    /// because the top-level entry determines which child slots even exist
    /// (HEAD/BODY/TAIL for one archetype, TAILB/TOPB for another), so a
    /// same-slot swap doesn't apply here the way it does for every other
    /// row. Walks the tree depth-first from newRootOptionId, and at each
    /// branch, visits sibling CATEGORIES in the game's own original order
    /// (via SortOrder - see CreatureDescriptorNode's doc comment) picking
    /// ONE default option per category (preferring a non-"(Rare)" option
    /// when one exists, then lowest SortOrder) - matching the exact
    /// depth-first shape confirmed against real save data (e.g. TREX's
    /// osl visits HEAD's full subtree, then BODY's, then TAIL's, in that
    /// order). Any entries from the CURRENT array that don't match any
    /// catalog node at all (the trailing per-instance detail seed seen on
    /// every sampled pet) are preserved as-is, appended after the rebuilt
    /// tree portion - there's no way to regenerate those and no evidence
    /// they need to change when the archetype does.
    /// UNTESTED IN-GAME as of 2026-07-29 - unlike the confirmed same-slot
    /// swap, nobody has yet confirmed the game accepts a full archetype
    /// swap built this way.</summary>
    private static JArray BuildDefaultDescriptorArray(List<CreatureDescriptorNode> tree, string newRootOptionId, JArray currentOsl)
    {
        var childrenByParent = tree
            .Where(n => n.ParentOptionId != null)
            .GroupBy(n => n.ParentOptionId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new List<string>();

        void Visit(string optionId)
        {
            result.Add("^" + optionId);

            if (!childrenByParent.TryGetValue(optionId, out var children)) return;

            var categoriesInOrder = children
                .GroupBy(n => n.Category)
                .OrderBy(g => g.Min(n => n.SortOrder));

            foreach (var categoryGroup in categoriesInOrder)
            {
                var defaultChoice = categoryGroup
                    .OrderBy(n => n.OptionId.EndsWith("XRARE", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(n => n.SortOrder)
                    .First();
                Visit(defaultChoice.OptionId);
            }
        }

        Visit(newRootOptionId);

        var byOptionId = new HashSet<string>(tree.Select(n => n.OptionId), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in currentOsl)
        {
            string raw = entry?.Value<string>() ?? "";
            if (!byOptionId.Contains(raw.TrimStart('^')))
                result.Add(raw);
        }

        return new JArray(result.Cast<object>());
    }

    /// <summary>One dropdown per osl (Descriptors) array entry, each
    /// constrained to that entry's real sibling options (same Category,
    /// same parent) from the rig's own catalog tree - see NmsCompanionPaths.
    /// DescriptorsPath and this page's XAML description for the full
    /// picture. CONFIRMED WORKING (2026-07-29, real save+reload test) -
    /// swapping several rows at once and reloading in-game rendered every
    /// new part correctly with no side effects elsewhere.
    ///
    /// Most rows swap ONE entry's value among its own siblings, preserving
    /// array length/order/every other entry - safe and confirmed. The
    /// exception: on a rig whose tree has exactly ONE distinct root-level
    /// category (rigHasSingleRootArchetype - e.g. TREX's single "_TREX_"
    /// category with 2 alternatives, one leading to TAILB/TOPB slots and
    /// the other to a completely different HEAD/BODY/TAIL set), that one
    /// root row is a genuine archetype choice - picking a different
    /// alternative rebuilds the WHOLE array from scratch (see
    /// BuildDefaultDescriptorArray) since it changes which child slots even
    /// apply, and re-renders this whole panel since the row set itself
    /// changes. UNTESTED IN-GAME as of 2026-07-29, unlike the confirmed
    /// same-slot swap.
    ///
    /// Real bug caught and fixed 2026-07-29: originally ANY root-level row
    /// (ParentOptionId == null) got this rebuild treatment, which broke
    /// rigs like rodent that have MULTIPLE independent root categories
    /// (HEAD/BODY/TAIL as separate peers, not alternatives of one choice) -
    /// a user picking a different Body there would have silently discarded
    /// their Head/Tail selections. Checked every cataloged rig: 27 of 71
    /// have this multi-root shape, so it couldn't be assumed away - only
    /// single-root rigs get archetype treatment now.
    ///
    /// A tree.Count == 0 result means no rig data was found for this
    /// species (see CatalogService.GetCreatureDescriptorTreeAsync's own doc
    /// comment for the confirmed exceptions) - shown as a plain message
    /// rather than an empty, silently-broken-looking panel.</summary>
    private void BuildDescriptorsPanel(int companionIndex, List<CreatureDescriptorNode> tree)
    {
        DescriptorsPanel.Children.Clear();

        var path = NmsCompanionPaths.DescriptorsPath(companionIndex);
        if (SaveSessionManager.GetValue(path) is not JArray osl) return;

        if (tree.Count == 0)
        {
            DescriptorsPanel.Children.Add(new TextBlock
            {
                Text = "No catalog data found for this creature's rig - nothing to edit here.",
                FontSize = 11,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var byOptionId = new Dictionary<string, CreatureDescriptorNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in tree)
            byOptionId[node.OptionId] = node;

        // Whether a root-level (ParentOptionId == null) row is a genuine
        // "archetype" choice - where alternatives have fundamentally
        // different downstream slots, so swapping needs the whole array
        // rebuilt (e.g. TREX has ONE root category "_TREX_" with 2 options,
        // one leading to TAILB/TOPB slots and the other to HEAD/BODY/TAIL) -
        // vs an ordinary independent top-level slot that just happens to
        // have no parent (e.g. rodent has THREE separate root categories,
        // HEAD/BODY/TAIL, each just an ordinary same-slot swap like any
        // other row - picking a different Body doesn't touch Head or Tail
        // at all). Confirmed by checking every cataloged rig: 27 of 71 have
        // multiple independent root categories like rodent, so this can't
        // be assumed away - only a rig with EXACTLY ONE distinct root
        // category gets archetype (full-rebuild) treatment.
        bool rigHasSingleRootArchetype = tree
            .Where(n => n.ParentOptionId == null)
            .Select(n => n.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == 1;

        for (int i = 0; i < osl.Count; i++)
        {
            int index = i; // capture for the closure below
            string raw = osl[i]?.Value<string>() ?? "";
            string optionId = raw.TrimStart('^');

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };

            if (!byOptionId.TryGetValue(optionId, out var node))
            {
                // Doesn't match any known tree node (e.g. the trailing
                // per-instance detail seed seen on every sampled pet) -
                // shown read-only so the array stays visible in full.
                row.Children.Add(AdvancedFieldLabel($"osl[{i}]"));
                row.Children.Add(new TextBlock { Text = raw, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6, FontSize = 12 });
                DescriptorsPanel.Children.Add(row);
                continue;
            }

            bool isTopLevel = node.ParentOptionId == null && rigHasSingleRootArchetype;

            var siblings = tree
                .Where(n => n.Category == node.Category && n.ParentOptionId == node.ParentOptionId)
                .OrderBy(n => n.OptionId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string rowLabel = $"{HumanizeDescriptorCategory(node.Category)} (osl[{i}])";
            row.Children.Add(AdvancedFieldLabel(isTopLevel ? rowLabel + " - ARCHETYPE" : rowLabel));

            var box = new ComboBox { Width = 240 };
            foreach (var sibling in siblings)
            {
                var item = new ComboBoxItem { Content = HumanizeDescriptorOption(node.Category, sibling.OptionId), Tag = sibling.OptionId };
                ToolTipService.SetToolTip(item, sibling.OptionId);
                box.Items.Add(item);
            }

            box.SelectedItem = box.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, optionId, StringComparison.OrdinalIgnoreCase));
            ToolTipService.SetToolTip(box, optionId);

            if (isTopLevel)
            {
                // Changing the archetype changes which child slots even
                // apply, so this can't be a same-slot swap like every other
                // row - rebuilds the WHOLE array (see
                // BuildDefaultDescriptorArray) and re-renders this whole
                // panel, since the row set itself will differ.
                box.SelectionChanged += (_, _) =>
                {
                    if (_suppressStatChangeEvent) return;

                    var selected = box.SelectedItem as ComboBoxItem;
                    string newRootOptionId = selected?.Tag as string ?? "";
                    if (string.IsNullOrEmpty(newRootOptionId) || string.Equals(newRootOptionId, optionId, StringComparison.OrdinalIgnoreCase))
                        return;

                    if (SaveSessionManager.GetValue(path) is not JArray currentArray) return;

                    var rebuilt = BuildDefaultDescriptorArray(tree, newRootOptionId, currentArray);
                    SaveSessionManager.StageEdit(rebuilt, path);
                    PageResetBtn.Visibility = Visibility.Visible;

                    BuildDescriptorsPanel(companionIndex, tree);
                };
            }
            else
            {
                box.SelectionChanged += (_, _) =>
                {
                    if (_suppressStatChangeEvent) return;

                    var selected = box.SelectedItem as ComboBoxItem;
                    string newOptionId = selected?.Tag as string ?? "";
                    if (string.IsNullOrEmpty(newOptionId)) return;
                    ToolTipService.SetToolTip(box, newOptionId);

                    if (SaveSessionManager.GetValue(path) is not JArray currentArray || index >= currentArray.Count) return;

                    string existing = currentArray[index]?.Value<string>() ?? "";
                    string newValue = "^" + newOptionId;
                    if (newValue == existing) return;

                    var updated = new JArray(currentArray.Select(v => v.DeepClone()));
                    updated[index] = newValue;

                    SaveSessionManager.StageEdit(updated, path);
                    PageResetBtn.Visibility = Visibility.Visible;
                };
            }
            row.Children.Add(box);

            DescriptorsPanel.Children.Add(row);
        }
    }

    /// <summary>Every remaining raw field on this pet with no confirmed
    /// in-game effect - built dynamically (like BuildSelectorStrip above)
    /// rather than as fixed named XAML controls, since the field set itself
    /// is exploratory and may grow/shrink as fields get confirmed one way or
    /// the other.</summary>
    private void BuildAdvancedFieldsPanel(int companionIndex)
    {
        AdvancedFieldsPanel.Children.Clear();

        AddHexPairFieldRow("Secondary Seed (1p=)", NmsCompanionPaths.SecondarySeedActivePath(companionIndex), NmsCompanionPaths.SecondarySeedPath(companionIndex));
        AddEnumFieldRow("Creature Type (HbY)", NmsCompanionPaths.CreatureTypePath(companionIndex),
            Enum.GetNames<GcCreatureTypes.CreatureTypeEnum>());
        AddCaretPrefixedStringFieldRow("Custom Species Name (HhX)", NmsCompanionPaths.CustomSpeciesNamePath(companionIndex));
        AddHexFieldRow("Unknown Hex (5L6)", NmsCompanionPaths.UnknownHexCPath(companionIndex));
        AddBoolFieldRow("Unknown Bool (Q6I)", NmsCompanionPaths.UnknownBoolAPath(companionIndex));
        AddBoolFieldRow("Unknown Bool (IaE)", NmsCompanionPaths.UnknownBoolBPath(companionIndex));
        AddBoolFieldRow("Unknown Bool (?<V)", NmsCompanionPaths.UnknownBoolCPath(companionIndex));
        AddBoolFieldRow("Unknown Bool (eK9)", NmsCompanionPaths.UnknownBoolDPath(companionIndex));
        AddBoolFieldRow("Unknown Bool (WQX)", NmsCompanionPaths.UnknownBoolEPath(companionIndex));
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

    /// <summary>Turns a raw descriptor slot code like "_HTAZEARS_" into
    /// "Htazears" - title-cased for readability. Rig slot names are the
    /// game's own internal shorthand (not always a recognizable English
    /// word), so this is cosmetic cleanup only, not a translation - see
    /// HumanizeDescriptorOption below for the same caveat on option
    /// values.</summary>
    private static string HumanizeDescriptorCategory(string category)
    {
        string trimmed = category.Trim('_');
        if (trimmed.Length == 0) return category;
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    /// <summary>Turns a raw descriptor option code like "_HEAD_TURTLE" into
    /// "Turtle" for the dropdown label - strips the category's own prefix
    /// (already shown as the row label via HumanizeDescriptorCategory),
    /// splits what's left on underscores, and capitalizes each piece. The
    /// game ships no separate display-name data for these at all -
    /// TkResourceDescriptorData's own "Name" field turned out to just be a
    /// differently-cased copy of the Id, not a real label, confirmed while
    /// decoding the tree - so this is the best available cleanup, not a
    /// translation. The exact raw code stays visible via each item's
    /// tooltip for anyone who wants to report back the literal value.</summary>
    private static string HumanizeDescriptorOption(string category, string rawCode)
    {
        string remainder = rawCode;
        if (remainder.StartsWith(category, StringComparison.OrdinalIgnoreCase))
            remainder = remainder[category.Length..];
        remainder = remainder.Trim('_');
        if (remainder.Length == 0) return rawCode.Trim('_');

        // "XRARE" is a real, recognizable rarity-tier suffix seen across
        // many categories in the game's own data (e.g. "_TREX_3XRARE",
        // "_TAIL_ALIENXRARE", "_HTAZEARS_0XRARE") - preserve it as a clean
        // "(Rare)" suffix instead of letting the generic path below mash it
        // into unreadable noise like "3xrare".
        bool isRare = remainder.EndsWith("XRARE", StringComparison.OrdinalIgnoreCase);
        if (isRare) remainder = remainder[..^5].Trim('_');

        string label;
        if (remainder.Length == 0)
        {
            label = "";
        }
        else if (remainder.All(char.IsDigit))
        {
            // Confirmed against the real catalog data (e.g. rodent's
            // _HTAZEARS_/_HTAZACC_/_TAZBACK_ slots) that some categories
            // are ENTIRELY bare-numbered variants with no descriptive text
            // anywhere in the game's own data - not a gap in this
            // formatting, there's genuinely nothing more to extract. Labeled
            // "Variant N" rather than a bare number so it reads as an
            // intentional unnamed option, not a broken label.
            label = $"Variant {remainder}";
        }
        else
        {
            var parts = remainder.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant());
            label = string.Join(' ', parts);
        }

        if (!isRare) return label;
        return label.Length == 0 ? "(Rare)" : $"{label} (Rare)";
    }

    /// <summary>"^PLANTCAT" -> "Plantcat" - just enough formatting to be
    /// readable; not the fancy in-game Latin species name (unconfirmed/
    /// likely not stored - see NmsCompanionPaths).</summary>
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

        string currentName = SaveSessionManager.GetValue(NmsCompanionPaths.NamePath(_selectedIndex))?.Value<string>() ?? "";
        if (newName == currentName) return;

        SaveSessionManager.StageEdit(newName, NmsCompanionPaths.NamePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
        LoadCompanionList();
    }

    private void TrustStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit(args.NewValue / 100.0, NmsCompanionPaths.TrustPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Redundant safety net alongside ValueChanged - some WinUI3
    /// NumberBox versions don't reliably fire ValueChanged on a plain focus
    /// loss, matching every other stat box in the app.</summary>
    private void TrustStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(TrustStatBox.Value)) return;
        SaveSessionManager.StageEdit(TrustStatBox.Value / 100.0, NmsCompanionPaths.TrustPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenesImprovedStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit((int)args.NewValue, NmsCompanionPaths.GenesImprovedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenesImprovedStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(GenesImprovedStatBox.Value)) return;
        SaveSessionManager.StageEdit((int)GenesImprovedStatBox.Value, NmsCompanionPaths.GenesImprovedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MutationProgressStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit(args.NewValue / 100.0, NmsCompanionPaths.MutationProgressPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MutationProgressStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(MutationProgressStatBox.Value)) return;
        SaveSessionManager.StageEdit(MutationProgressStatBox.Value / 100.0, NmsCompanionPaths.MutationProgressPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void VictoriesStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit((int)args.NewValue, NmsCompanionPaths.HoloArenaVictoriesPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void VictoriesStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(VictoriesStatBox.Value)) return;
        SaveSessionManager.StageEdit((int)VictoriesStatBox.Value, NmsCompanionPaths.HoloArenaVictoriesPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Stages one entry of the whole 3-slot mutation-points array -
    /// rebuilds and stages the WHOLE array at once, same reasoning as every
    /// other @bB-style array edit in the app (a deeper leaf-only stage isn't
    /// seen by SaveSessionManager's staged-edit lookup).</summary>
    private void SetMutationPoints(int statIndex, double newValue)
    {
        if (_selectedIndex < 0 || double.IsNaN(newValue)) return;

        var path = NmsCompanionPaths.MutationPointsPath(_selectedIndex);
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

        var path = NmsCompanionPaths.TraitsPath(_selectedIndex);
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

        var seedPath = NmsCompanionPaths.SeedPath(_selectedIndex);
        string? currentSeed = SaveSessionManager.GetValue(seedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, seedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Rerolls the selected pet's color to a brand new random seed -
    /// same plain-reroll semantics as Ships/Freighter (NmsSeedGenerator), not
    /// a targeted picker. Writes the SAME new value into both SeedPath
    /// (CreatureSeed) and BoneScaleSeedPath (BoneScaleSeed), preserving the
    /// mirror every real sample showed between them - see NmsCompanionPaths for
    /// why the other two seed pairs (CreatureSecondarySeed/ColourBaseSeed)
    /// are deliberately left untouched.</summary>
    private void GenerateSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        SeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsCompanionPaths.SeedPath(_selectedIndex));
        SaveSessionManager.StageEdit(newSeed, NmsCompanionPaths.BoneScaleSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ColourBaseSeedActiveCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        SaveSessionManager.StageEdit(ColourBaseSeedActiveCheckBox.IsChecked ?? false,
            NmsCompanionPaths.ColourBaseSeedActivePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ColourBaseSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newValue = ColourBaseSeedEditBox.Text?.Trim() ?? "";
        var path = NmsCompanionPaths.ColourBaseSeedPath(_selectedIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Activates AND rerolls together - confirmed via real testing
    /// (see NmsCompanionPaths.ColourBaseSeedActivePath) that this is a dormant
    /// secondary color layer, so a randomized hex behind an inactive flag
    /// wouldn't show anything in-game.</summary>
    private void GenerateColourBaseSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        ColourBaseSeedEditBox.Text = newSeed;
        ColourBaseSeedActiveCheckBox.IsChecked = true;
        SaveSessionManager.StageEdit(newSeed, NmsCompanionPaths.ColourBaseSeedPath(_selectedIndex));
        SaveSessionManager.StageEdit(true, NmsCompanionPaths.ColourBaseSeedActivePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void BattleAbilitySeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = BattleAbilitySeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        var rollSeedPath = NmsCompanionPaths.RollSeedPrimaryPath(_selectedIndex);
        string? currentSeed = SaveSessionManager.GetValue(rollSeedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, rollSeedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Rerolls the selected pet's Battle Abilities badges and fancy
    /// species name to a brand new random roll - confirmed via a real
    /// reroll-and-revert round trip (see NmsCompanionPaths.RollSeedPrimaryPath).</summary>
    private void GenerateBattleAbilitySeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        BattleAbilitySeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsCompanionPaths.RollSeedPrimaryPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void BattleAbilitySeed2EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = BattleAbilitySeed2EditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        var rollSeedPath = NmsCompanionPaths.RollSeedSecondaryPath(_selectedIndex);
        string? currentSeed = SaveSessionManager.GetValue(rollSeedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, rollSeedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Second confirmed co-input to the same roll as
    /// GenerateBattleAbilitySeedBtn_Click - see NmsCompanionPaths.RollSeedSecondaryPath.</summary>
    private void GenerateBattleAbilitySeed2Btn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        BattleAbilitySeed2EditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsCompanionPaths.RollSeedSecondaryPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ClassLetterOverrideActiveCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        SaveSessionManager.StageEdit(ClassLetterOverrideActiveCheckBox.IsChecked ?? false,
            NmsCompanionPaths.ClassLetterOverrideActivePath(_selectedIndex));
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
        var path = NmsCompanionPaths.ClassLetterPath(_selectedIndex, statIndex);
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
        if (_selectedIndex >= 0)
        {
            SaveSessionManager.RevertEditsUnder(NmsCompanionPaths.CompanionPath(_selectedIndex));
            LoadSelectedCompanion();
        }

        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
