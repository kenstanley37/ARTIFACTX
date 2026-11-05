using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using libMBIN.NMS.GameComponents;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

/// <summary>Shared shell hosted by all 5 language pages (Gek/Vy'keen/Korvax/
/// Autophage/Atlas - the last isn't a race, just a smaller special vocabulary
/// pool living in the same array) - Initialize(raceName, idPrefix) configures
/// which group's words this instance shows/edits. All 5 share ONE underlying
/// save array (NmsLanguagePaths.KnownWordsArrayPath, "vLc/6f=/MF2") - this
/// control reads the full array but only ever adds/removes entries matching
/// its own idPrefix, re-staging the whole array (including every other
/// group's untouched entries) on each toggle, the same "stage the whole
/// array" pattern CataloguePage/AccountDataPage already use for their own
/// single-scope arrays.</summary>
public sealed partial class LanguageWordsControl : UserControl
{
    private string _raceName = "";
    private string _idPrefix = "";

    private List<LanguageWordRowViewModel>? _allWords;
    private List<LanguageWordRowViewModel> _currentlyVisibleRows = new();

    // MF2's entries are NOT plain id strings like 4kj/B1h - each is an object:
    // {"MYl": "^TRA_ATTACK", "D;o": [one flag per race, see NmsLanguagePaths]}.
    // _fullEntries preserves every entry's own original JObject untouched
    // (including whatever D;o shape it already had) except for ones this
    // control adds/removes itself; _fullKnownIdSet is just the normalized
    // "MYl" values for O(1) lookup.
    private List<JObject> _fullEntries = new();
    private HashSet<string> _fullKnownIdSet = new(StringComparer.Ordinal);

    public LanguageWordsControl()
    {
        InitializeComponent();
    }

    /// <summary>Must be called once right after construction - not a
    /// DependencyProperty, since nothing needs to set this from XAML markup;
    /// each of the 4 thin per-race Pages just calls this from their own
    /// constructor.</summary>
    public void Initialize(string raceName, string idPrefix)
    {
        _raceName = raceName;
        _idPrefix = idPrefix;

        TitleTxt.Text = $"{raceName} Language".ToUpperInvariant();
        InfoTxt.Text = $"The {raceName} words this save knows. Click a word to mark it known or unknown. All five language pages share one list, so Reset undoes unsaved changes on every one of them, not just {raceName}.";

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSaved;
        Unloaded += Control_Unloaded;

        KnownFilterBox.SelectedIndex = 0;

        _ = LoadWordsAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadWordsAsync());

    /// <summary>Deliberately NOT a full reload - see CataloguePage's identical
    /// comment on the same lag bug this avoids (Tile_Tapped's own StageEdit
    /// call fires this event on every click).</summary>
    private void OnPendingEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateResetButton);

    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
        SaveSessionManager.GameSavedLoadedSlot -= OnGameSaved;
    }

    /// <summary>Words learned in game appear once the game saves, ticked in place
    /// (no reload, scroll kept). Unsaved word edits win: LiveFieldsService leaves
    /// the list alone while any are staged.</summary>
    private void OnGameSaved(JObject disk)
    {
        if (_allWords is null) return;
        if (LiveFieldsService.Adopt(disk, [NmsLanguagePaths.KnownWordsArrayPath]).Count == 0) return;
        ReadKnownEntries();
        foreach (var row in _allWords) row.IsKnown = _fullKnownIdSet.Contains(row.GameId);
        string? knownFilter = (KnownFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        if (knownFilter is "Known Only" or "Unknown Only") ApplyFilters();
    }

    private void ReadKnownEntries()
    {
        var knownArray = SaveSessionManager.GetValue(NmsLanguagePaths.KnownWordsArrayPath) as JArray;
        _fullEntries = knownArray?.OfType<JObject>().ToList() ?? new();
        _fullKnownIdSet = new HashSet<string>(
            _fullEntries.Select(e => CatalogService.NormalizeId(e["MYl"]?.Value<string>() ?? "")).Where(s => s.Length > 0),
            StringComparer.Ordinal);
    }

    private async Task LoadWordsAsync()
    {
        ContentPanel.Visibility = Visibility.Collapsed;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        // Forces a real dispatcher yield here, so the spinner actually gets a
        // render pass before the work below runs. Without this, a warm-cache
        // load (the common case - GetAllLanguageWordsAsync's cache hit path
        // never truly awaits) plus building ~1,100-1,170 row view-models can
        // complete in one synchronous burst on the UI thread with zero frames
        // rendered in between, so the spinner is "on" the whole time but never
        // visibly appears - confirmed by the user 2026-08-04 as a residual
        // couple-second stall with no visible loading indicator.
        await Task.Yield();

        var allCatalogWords = await CatalogService.GetAllLanguageWordsAsync();

        ReadKnownEntries();

        // The actual row-building (filter ~4,570 down to one race, construct
        // ~1,100-1,170 ObservableObject instances) is real work, not just a
        // rendering technicality - moved off the UI thread entirely so the
        // spinner keeps animating smoothly for however long it takes, rather
        // than relying on a single yielded frame to cover it.
        string raceName = _raceName;
        var knownIdSet = _fullKnownIdSet;
        _allWords = await Task.Run(() => allCatalogWords
            .Where(w => w.Race == raceName)
            .Select(w => new LanguageWordRowViewModel
            {
                GameId = w.GameId,
                DisplayName = ToDisplayCase(w.DisplayName),
                IsKnown = knownIdSet.Contains(w.GameId)
            })
            // By the word shown, not the group id: TRA_TREASURE reads "Artifact".
            .OrderBy(w => w.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList());

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        ApplyFilters();
        UpdateResetButton();
    }

    /// <summary>The catalog DB's word text is the game's own raw lowercase loc
    /// value ("attack") - title-cased here for display only, same convention
    /// CataloguePage already uses for its own raw ALL-CAPS names.</summary>
    private static string ToDisplayCase(string rawName) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rawName.ToLowerInvariant());

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_allWords is null) return;

        string query = SearchBox.Text?.Trim() ?? "";
        string? knownFilter = (KnownFilterBox.SelectedItem as ComboBoxItem)?.Content as string;

        IEnumerable<LanguageWordRowViewModel> filtered = _allWords;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(w =>
                w.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (knownFilter == "Known Only")
            filtered = filtered.Where(w => w.IsKnown);
        else if (knownFilter == "Unknown Only")
            filtered = filtered.Where(w => !w.IsKnown);

        _currentlyVisibleRows = filtered.ToList();
        ItemsListView.ItemsSource = _currentlyVisibleRows;
        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count:N0} of {_allWords.Count:N0} words";
    }

    /// <summary>Mutates only entries matching this control's own _idPrefix -
    /// every other race's entries in the shared array are left exactly as
    /// they were read, and get re-staged untouched alongside this change.
    /// A newly-added entry's "D;o" is one flag per GcAlienRace value with only
    /// the word's own race set, as the game writes them (NmsLanguagePaths).</summary>
    private void SetRowKnown(LanguageWordRowViewModel row, bool known)
    {
        row.IsKnown = known;

        if (known)
        {
            if (_fullKnownIdSet.Add(row.GameId))
            {
                var flags = new bool[Enum.GetValues<GcAlienRace.AlienRaceEnum>().Length];
                flags[(int)RaceOf(_idPrefix)] = true;
                _fullEntries.Add(new JObject
                {
                    ["MYl"] = "^" + row.GameId,
                    ["D;o"] = new JArray(flags.Cast<object>().ToArray())
                });
            }
        }
        else
        {
            if (_fullKnownIdSet.Remove(row.GameId))
                _fullEntries.RemoveAll(e => CatalogService.NormalizeId(e["MYl"]?.Value<string>() ?? "") == row.GameId);
        }
    }

    /// <summary>The race whose flag a word of this id prefix sets.</summary>
    private static GcAlienRace.AlienRaceEnum RaceOf(string idPrefix) => idPrefix switch
    {
        "TRA" => GcAlienRace.AlienRaceEnum.Traders,
        "WAR" => GcAlienRace.AlienRaceEnum.Warriors,
        "EXP" => GcAlienRace.AlienRaceEnum.Explorers,
        "BUI" => GcAlienRace.AlienRaceEnum.Builders,
        "ATLAS" => GcAlienRace.AlienRaceEnum.Atlas,
        _ => throw new ArgumentOutOfRangeException(nameof(idPrefix), idPrefix, "Not a language word prefix.")
    };

    private void Tile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: LanguageWordRowViewModel row }) return;

        SetRowKnown(row, !row.IsKnown);
        SaveSessionManager.StageEdit(new JArray(_fullEntries), NmsLanguagePaths.KnownWordsArrayPath);
        UpdateResetButton();

        string? knownFilter = (KnownFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        if (knownFilter is "Known Only" or "Unknown Only")
            ApplyFilters();
    }

    private void MarkKnownAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetVisibleRows(known: true);

    private void MarkUnknownAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetVisibleRows(known: false);

    private void BulkSetVisibleRows(bool known)
    {
        if (_currentlyVisibleRows.Count == 0) return;

        foreach (var row in _currentlyVisibleRows)
            SetRowKnown(row, known);

        SaveSessionManager.StageEdit(new JArray(_fullEntries), NmsLanguagePaths.KnownWordsArrayPath);
        UpdateResetButton();
        ApplyFilters();
    }

    private void UpdateResetButton() =>
        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsLanguagePaths.KnownWordsArrayPath)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEditsUnder(NmsLanguagePaths.KnownWordsArrayPath);
        _ = LoadWordsAsync();
    }
}
