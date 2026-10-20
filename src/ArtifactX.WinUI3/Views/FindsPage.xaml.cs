using ArtifactX.Almanac.Starship;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Tools > Finds: player-recorded finds with portal addresses (FindsService,
/// user.db). Follows the game (LiveSaveWatcher) so "Use my current location"
/// and the distances stay current. Never writes to the save.
/// </summary>
public sealed partial class FindsPage : Page
{
    private readonly LiveSaveWatcher _live;
    private ulong? _hereSystem;
    private int _herePlanet;
    // The player's coordinates on that planet (NmsPlanetCoordinates), and the
    // last "Where to find it" text Use my current location wrote, so pressing
    // it again after moving updates it without overwriting the player's own.
    private (double Latitude, double Longitude)? _hereCoords;
    private string _autoLocation = "";
    private List<KnownPlanet> _herePlanets = new();
    private Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _shipKinds = new();
    private List<string> _toolKinds = new();
    private List<string> _freighterKinds = new();
    private Dictionary<string, string> _fishNames = new(StringComparer.OrdinalIgnoreCase);

    // Form state
    private string? _editingId;
    private string _editingCreatedAt = "";
    private readonly List<string> _formItems = new();
    private bool _formOpen;
    private bool _ready;

    public FindsPage()
    {
        InitializeComponent();
        _live = new LiveSaveWatcher(DispatcherQueue);
        _live.SnapshotRead += OnLiveSnapshot;
        JumpPlanner.RangeChanged += Apply;
        FindsService.Changed += OnFindsChanged;
        PlanetDetailsService.Captured += OnPlanetDetailsCaptured;
        PlanetDetailsService.Changed += OnPlanetDetailsCaptured;
        SystemNotesService.Changed += OnSystemNotesChanged;
        SystemPageReader.Captured += OnSystemNotesChanged;
        Unloaded += (_, _) =>
        {
            JumpPlanner.RangeChanged -= Apply;
            FindsService.Changed -= OnFindsChanged;
            PlanetDetailsService.Captured -= OnPlanetDetailsCaptured;
            PlanetDetailsService.Changed -= OnPlanetDetailsCaptured;
            SystemNotesService.Changed -= OnSystemNotesChanged;
            SystemPageReader.Captured -= OnSystemNotesChanged;
            _live.SnapshotRead -= OnLiveSnapshot;
            _live.Dispose();
        };
        _ = LoadAsync();
    }

    private void OnFindsChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        RefreshTypeFilter();
        Apply();
    });

    private void OnPlanetDetailsCaptured(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        Apply();
    });

    private void OnSystemNotesChanged(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        UpdateHere();
        Apply();
    });

    private async Task LoadAsync()
    {
        await CatalogService.WarmCacheAsync(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));
        _itemNames = await CatalogService.GetItemNamesAsync();
        // Kinds come from game data / the app's own type lists, not hand-typed here.
        // Reward ships can't be found in the wild, so they're left out.
        _shipKinds = StarshipTypes.All.Select(t => t.DisplayName).Where(n => !n.Contains("(Reward")).ToList();
        // The main model (MULTITOOL.SCENE, listed as "Rifle") makes pistols, rifles,
        // experimentals and alien tools - the seed picks which - so a find needs
        // those names, not the one model name (an Alien tool had no entry,
        // 2026-10-15). The game has no names for them; these are the players'.
        _toolKinds = (await CatalogService.GetMultiToolTypesAsync())
            .SelectMany(t => t.ScenePath.EndsWith("/MULTITOOL.SCENE.MBIN", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Pistol", "Rifle", "Experimental", "Alien", "Starter" }
                : new[] { MultiToolKinds.NameFor(t.ScenePath, t.DisplayName) })
            .Distinct().ToList();
        _freighterKinds = (await CatalogService.GetFreighterTypesAsync()).Select(t => t.DisplayName)
            .Where(n => n is "Normal" or "Capital" or "Dreadnought").ToList();
        // Same fish list as the Fishing Records page (game ids, not hand-typed).
        var fish = CatalogService.FilterFishSpecies(await CatalogService.GetAllUnlockableItemsAsync());
        _fishNames = fish.GroupBy(f => f.GameId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => NameOf(g.Key), StringComparer.OrdinalIgnoreCase);
        // Icons for the fish picker and for every item already on a card.
        await CatalogService.WarmCacheAsync(_fishNames.Keys.Concat(FindsService.All.SelectMany(f => f.ItemIds)));

        foreach (var type in FindsService.Types) FormTypeBox.Items.Add(type);
        FormTypeBox.SelectedIndex = 0;
        foreach (var c in new[] { "", "S", "A", "B", "C" }) FormClassBox.Items.Add(new ComboBoxItem { Content = c.Length == 0 ? "-" : c, Tag = c });
        FormClassBox.SelectedIndex = 0;
        for (int g = 1; g <= 256; g++)
            FormGalaxyBox.Items.Add(new ComboBoxItem { Content = GalaxyLabel(g), Tag = g });

        ClassFilterBox.Items.Add(new ComboBoxItem { Content = "Any class", Tag = "" });
        foreach (var c in new[] { "S", "A", "B", "C" }) ClassFilterBox.Items.Add(new ComboBoxItem { Content = c, Tag = c });
        ClassFilterBox.SelectedIndex = 0;
        SortBox.SelectedIndex = 0;

        if (SaveSessionManager.IsSaveLoaded)
        {
            var universe = SaveSessionManager.GetValue(S.CurrentUniverseAddressPath);
            _hereSystem = S.ReadSystemAddress(universe);
            _herePlanet = universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
            _hereCoords = LiveSaveWatcher.FromSession().Coordinates;
            _herePlanets = NmsPlanets.ReadAll(SaveSessionManager.GetValue(S.DiscoveriesPath) as JArray);
            _live.Start();
        }
        SelectGalaxy(_hereSystem is ulong h ? S.GalaxyNumber(h) : 1);
        UseHereBtn.IsEnabled = _hereSystem is not null;
        UpdateHere();

        _ready = true;
        RefreshTypeFilter();
        Apply();
    }

    private void OnLiveSnapshot(PlanetSnapshot snapshot)
    {
        _hereSystem = S.ReadSystemAddress(snapshot.Universe);
        _herePlanet = snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        _hereCoords = snapshot.Coordinates;
        _herePlanets = NmsPlanets.ReadAll(snapshot.Discoveries);
        UseHereBtn.IsEnabled = _hereSystem is not null;
        UpdateHere();
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        Apply();
    }

    /// <summary>The "You are here" strip: learned system/planet names plus the
    /// planet's type from its discovery record.</summary>
    private void UpdateHere()
    {
        if (_hereSystem is not ulong system)
        {
            HerePanel.Visibility = Visibility.Collapsed;
            return;
        }
        HerePanel.Visibility = Visibility.Visible;
        HereSystemTxt.Text = UniverseInfo.SystemName(system);
        // Names now come from the game's generator, so the "save in space to learn it" hint isn't needed.
        HereHintTxt.Visibility = Visibility.Collapsed;

        var parts = new List<string>();
        if (_herePlanet > 0)
        {
            parts.Add($"On {UniverseInfo.PlanetName(system, _herePlanet)} (planet {_herePlanet})");
            var planet = _herePlanets.FirstOrDefault(p => p.SystemAddress == system && p.PlanetIndex == _herePlanet);
            if (planet is not null) parts.Add(planet.TypeName);
        }
        else parts.Add("In space");
        parts.Add(GalaxyLabel(S.GalaxyNumber(system)));
        parts.Add(S.PortalAddress(system, _herePlanet > 0 ? _herePlanet : 1));
        HerePlaceTxt.Text = string.Join(" · ", parts) + "\n" + UniverseInfo.Summary(system);
    }

    // ---------------------------------------------------------------- list

    private void RefreshTypeFilter()
    {
        string previous = (TypeFilterBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var finds = FindsService.All;
        TypeFilterBox.Items.Clear();
        TypeFilterBox.Items.Add(new ComboBoxItem { Content = $"All types ({finds.Count})", Tag = "" });
        foreach (var type in FindsService.Types)
        {
            int count = finds.Count(f => f.Type == type);
            if (count > 0) TypeFilterBox.Items.Add(new ComboBoxItem { Content = $"{type} ({count})", Tag = type });
        }
        TypeFilterBox.SelectedItem = TypeFilterBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == previous) ?? TypeFilterBox.Items[0];
    }

    private void Apply()
    {
        if (!_ready) return;
        var finds = FindsService.All;
        string text = SearchBox.Text.Trim();
        string type = (TypeFilterBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        string cls = (ClassFilterBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

        bool Matches(Find f) => text.Length == 0
            || f.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || f.Notes.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || f.Location.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || f.Subtype.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || f.ItemIds.Any(i => NameOf(i).Contains(text, StringComparison.CurrentCultureIgnoreCase))
            || SystemName(f).Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || PlanetName(f, PlanetOf(f)).Contains(text, StringComparison.CurrentCultureIgnoreCase);

        var filtered = finds.Where(f => type.Length == 0 || f.Type == type)
            .Where(f => cls.Length == 0 || f.Class == cls)
            .Where(Matches);

        var ordered = SortBox.SelectedIndex switch
        {
            1 => filtered.OrderBy(f => DistanceFrom(f) ?? double.MaxValue),
            2 => filtered.OrderBy(f => f.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => filtered.OrderByDescending(f => f.CreatedAt, StringComparer.Ordinal),
        };

        var cards = ordered.Select(ToCard).ToList();
        FindsList.ItemsSource = cards;
        CountTxt.Text = $"{cards.Count} of {finds.Count} finds";
        EmptyTxt.Visibility = finds.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string NameOf(string id) => _itemNames.TryGetValue(id, out var n) ? n : CatalogService.TryGet(id)?.DisplayName ?? id;

    private static ulong? AddressOf(Find f) =>
        f.PortalCode.Length == 12 ? NmsPortalCode.ToAddress(f.PortalCode, f.Galaxy) : null;

    private static string SystemName(Find f) =>
        AddressOf(f) is ulong a ? UniverseInfo.SystemName(a) : "";

    private static int PlanetOf(Find f) =>
        f.PortalCode.Length == 12 ? int.Parse(f.PortalCode[..1], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : 0;

    /// <summary>The planet's name: learned from a save, else the game's generated name.</summary>
    private static string PlanetName(Find f, int planet) =>
        AddressOf(f) is ulong a && planet > 0 ? UniverseInfo.PlanetName(a, planet) : "";

    private double? DistanceFrom(Find f) =>
        _hereSystem is ulong h && AddressOf(f) is ulong a ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(a)) : null;

    private FindCardViewModel ToCard(Find f)
    {
        var detail = new List<string>();
        if (f.Subtype.Length > 0) detail.Add(f.Subtype);
        if (f.Location.Length > 0) detail.Add(f.Location);

        string system = SystemName(f);
        int planet = PlanetOf(f);
        string planetName = PlanetName(f, planet);
        string planetText = planetName.Length > 0 ? $"{planetName} (planet {planet})" : $"planet {planet}";
        string place = f.PortalCode.Length == 12
            ? $"{(system.Length > 0 ? system : "Unnamed system")} · {planetText} · {GalaxyLabel(f.Galaxy)}"
            : "No address recorded";

        string distance = AddressOf(f) is not ulong address ? ""
            : _hereSystem is ulong here && S.ToSystemAddress(address) == here ? "In the system you're in"
            : DistanceFrom(f) switch
            {
                null => "In a different galaxy from you - portals can't cross galaxies",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };

        return new FindCardViewModel
        {
            Id = f.Id,
            TypeLabel = f.Type.ToUpperInvariant(),
            Title = f.Title.Length > 0 ? f.Title : f.Type,
            Detail = string.Join(" · ", detail),
            DetailVisibility = detail.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
            ClassLetter = f.Class,
            ClassVisibility = f.Class.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
            ClassBrush = new SolidColorBrush(ClassColour(f.Class)),
            Items = f.ItemIds.Select(id => new FindItemChip { Label = NameOf(id), Icon = CatalogService.TryGet(id)?.Icon }).ToList(),
            ItemsVisibility = f.ItemIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
            Place = place,
            Facts = AddressOf(f) is ulong factsAddress ? UniverseInfo.Compact(factsAddress) : "",
            Distance = distance,
            PortalCode = f.PortalCode,
            Glyphs = GlyphsFor(f.PortalCode),
            Notes = f.Notes,
            NotesVisibility = f.Notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
    }

    // Game class-tier colours (same as the Multi-Tool page's badge) - fixed, not themed.
    private static Windows.UI.Color ClassColour(string cls) => cls switch
    {
        "S" => Windows.UI.Color.FromArgb(255, 201, 151, 34),
        "A" => Windows.UI.Color.FromArgb(255, 130, 80, 200),
        "B" => Windows.UI.Color.FromArgb(255, 60, 110, 200),
        "C" => Windows.UI.Color.FromArgb(255, 60, 150, 90),
        _ => Colors.Transparent,
    };

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var cards = FindsList.ItemsSource as List<FindCardViewModel> ?? new();
        var finds = FindsService.All.ToDictionary(f => f.Id);
        var rows = new List<string[]>();
        foreach (var card in cards)
        {
            if (!finds.TryGetValue(card.Id, out var f)) continue;
            rows.Add(new[]
            {
                f.Type, f.Title, f.Class, f.Subtype, string.Join(", ", f.ItemIds.Select(NameOf)), f.Location,
                f.PortalCode, f.PortalCode.Length == 12 ? GalaxyLabel(f.Galaxy) : "", SystemName(f), PlanetName(f, PlanetOf(f)),
                card.Distance, f.Notes, LocalTime(f.CreatedAt),
            });
        }
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX finds",
            new[] { "Type", "Title", "Class", "Kind", "Items or fish", "Where", "Portal code", "Galaxy", "System", "Planet", "Distance", "Notes", "Added" },
            rows);
    }

    /// <summary>user.db timestamps are UTC (ISO 8601); show them in local time.</summary>
    private static string LocalTime(string utc) =>
        DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
            ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : utc;

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Apply();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Apply();

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code || code.Length == 0) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var find = FindsService.All.FirstOrDefault(f => f.Id == id);
        var confirm = new ContentDialog
        {
            Title = "Delete this find?",
            Content = find?.Title is { Length: > 0 } t ? $"\"{t}\" will be removed." : "This find will be removed.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        FindsService.Delete(id);
        if (_editingId == id) ResetForm();
        RefreshTypeFilter();
        Apply();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var f = FindsService.All.FirstOrDefault(x => x.Id == id);
        if (f is null) return;

        _editingId = f.Id;
        _editingCreatedAt = f.CreatedAt;
        FormTypeBox.SelectedItem = f.Type;
        FormTitleBox.Text = f.Title;
        SelectByTag(FormClassBox, f.Class);
        FormSubtypeBox.SelectedItem = FormSubtypeBox.Items.Contains(f.Subtype) ? f.Subtype : null;
        FormLocationBox.Text = f.Location;
        FormNotesBox.Text = f.Notes;
        _formItems.Clear();
        _formItems.AddRange(f.ItemIds);
        RenderFormItems();
        SelectGalaxy(f.Galaxy);
        FormSystemNameBox.Text = "";
        FormCodeBox.Text = f.PortalCode;
        FormTitleTxt.Text = "EDIT FIND";
        FormSaveBtn.Content = "Save changes";
        SetFormOpen(true);
    }

    // ---------------------------------------------------------------- form

    private void FormToggle_Click(object sender, RoutedEventArgs e) => SetFormOpen(!_formOpen);

    private void SetFormOpen(bool open)
    {
        _formOpen = open;
        FormPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        FormToggleIcon.Glyph = open ? "" : "";
    }

    private void FormType_Changed(object sender, SelectionChangedEventArgs e)
    {
        string type = FormTypeBox.SelectedItem as string ?? "";
        bool hasClass = FindsService.HasClass(type);
        FormClassBox.Visibility = hasClass ? Visibility.Visible : Visibility.Collapsed;

        var kinds = type switch
        {
            "Starship" => _shipKinds,
            "Multi-Tool" => _toolKinds,
            "Freighter" => _freighterKinds,
            _ => new List<string>(),
        };
        FormSubtypeBox.Items.Clear();
        foreach (var k in kinds) FormSubtypeBox.Items.Add(k);
        FormSubtypeBox.Visibility = kinds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        FormItemsPanel.Visibility = FindsService.HasItems(type) ? Visibility.Visible : Visibility.Collapsed;
        bool fishing = type == "Fishing spot";
        FormItemSearch.Header = fishing ? "Fish caught here" : "Items sold here";
        FormItemSearch.PlaceholderText = fishing ? "Search a fish, e.g. Mud Crab" : "Search an item, e.g. Pugneum";
        // Switching between the item and fish lists drops chips from the other list.
        if (_formItems.RemoveAll(id => fishing != _fishNames.ContainsKey(id)) > 0) RenderFormItems();
    }

    private void UseHere_Click(object sender, RoutedEventArgs e)
    {
        if (_hereSystem is not ulong system) return;
        // Portal codes need a planet digit; in space, planet 1 lands you in the system.
        FormCodeBox.Text = S.PortalAddress(system, _herePlanet > 0 ? _herePlanet : 1);
        SelectGalaxy(S.GalaxyNumber(system));
        // Pre-fill "Where to find it" with the planet (or system) name, plus the
        // coordinates the game's scanner shows (2026-10-14) - the spot itself,
        // not just the planet.
        string current = FormLocationBox.Text.Trim();
        if (current.Length == 0 || current == _autoLocation)
        {
            _autoLocation = _herePlanet > 0
                ? $"On {UniverseInfo.PlanetName(system, _herePlanet)}" + (_hereCoords is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "")
                : $"In {UniverseInfo.SystemName(system)}";
            FormLocationBox.Text = _autoLocation;
        }
    }

    private void FormCode_TextChanged(object sender, TextChangedEventArgs e) => UpdateFormGlyphs();
    private void FormGalaxy_Changed(object sender, SelectionChangedEventArgs e) => UpdateFormGlyphs();

    /// <summary>Shows the glyphs for the code being entered, and flags a code
    /// for a different galaxy from the player's.</summary>
    private void UpdateFormGlyphs()
    {
        FormGlyphsPanel.Children.Clear();
        FormGlyphStrip.Code = NmsPortalCode.TryParse(FormCodeBox.Text.Trim(), out string stripCode, out _) ? stripCode : "";
        string input = FormCodeBox.Text.Trim();
        if (input.Length == 0)
        {
            FormCodeStatusTxt.Text = "";
            return;
        }
        if (!NmsPortalCode.TryParse(input, out string code, out string error))
        {
            FormCodeStatusTxt.Text = error;
            return;
        }
        // Show the system's name (recorded, else the game's own); a typed name is only
        // needed when a player renamed the system. Never overwrite what the user typed.
        ulong codeAddress = NmsPortalCode.ToAddress(code, SelectedGalaxy());
        FormSystemNameBox.PlaceholderText = UniverseInfo.SystemName(codeAddress);
        foreach (var glyph in GlyphsFor(code))
            FormGlyphsPanel.Children.Add(new Border
            {
                Width = 24, Height = 24, Background = ThemeResources.Brush("ThemeVoidBrush"),
                Child = new Image { Source = glyph, Width = 20, Height = 20 },
            });
        int galaxy = SelectedGalaxy();
        FormCodeStatusTxt.Text = _hereSystem is ulong h && S.GalaxyNumber(h) != galaxy
            ? $"This is in {GalaxyLabel(galaxy)} - you're in {GalaxyLabel(S.GalaxyNumber(h))}. Portals can't cross galaxies."
            : code;
    }

    private void FormItemSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        string text = sender.Text.Trim();
        sender.ItemsSource = text.Length < 2 ? null : PickerNames().Values
            .Where(n => n.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(n => n.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1).ThenBy(n => n)
            .Take(12).ToList();
    }

    private async void FormItemSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        string name = args.SelectedItem as string ?? "";
        string? id = PickerNames().FirstOrDefault(kv => string.Equals(kv.Value, name, StringComparison.CurrentCultureIgnoreCase)).Key;
        sender.Text = "";
        if (id is null || _formItems.Contains(id, StringComparer.OrdinalIgnoreCase)) return;
        _formItems.Add(id);
        RenderFormItems();
        // Sold items aren't pre-warmed (there are thousands); fetch this one's icon.
        if (CatalogService.TryGet(id)?.Icon is null)
        {
            await CatalogService.WarmCacheAsync(new[] { id });
            RenderFormItems();
        }
    }

    private Dictionary<string, string> PickerNames() =>
        FormTypeBox.SelectedItem as string == "Fishing spot" ? _fishNames : _itemNames;

    private void RenderFormItems()
    {
        FormItemChips.Children.Clear();
        foreach (var id in _formItems.ToList())
        {
            var chip = new Button { Padding = new Thickness(6, 2, 6, 2) };
            ToolTipService.SetToolTip(chip, "Remove");
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new Image { Source = CatalogService.TryGet(id)?.Icon, Width = 18, Height = 18 });
            row.Children.Add(new TextBlock { Text = NameOf(id), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new FontIcon { Glyph = "", FontSize = 10 });
            chip.Content = row;
            chip.Click += (_, _) =>
            {
                _formItems.Remove(id);
                RenderFormItems();
            };
            FormItemChips.Children.Add(chip);
        }
    }

    private void FormSave_Click(object sender, RoutedEventArgs e)
    {
        string type = FormTypeBox.SelectedItem as string ?? "";
        string code = "";
        if (FormCodeBox.Text.Trim().Length > 0 && !NmsPortalCode.TryParse(FormCodeBox.Text, out code, out string error))
        {
            FormErrorTxt.Text = error;
            return;
        }
        if (FormTitleBox.Text.Trim().Length == 0 && code.Length == 0)
        {
            FormErrorTxt.Text = "Give it a title or an address.";
            return;
        }

        SaveSystemName(code);
        FindsService.Save(new Find
        {
            Id = _editingId ?? Guid.NewGuid().ToString("N"),
            CreatedAt = _editingCreatedAt,
            Type = type,
            Title = FormTitleBox.Text.Trim(),
            PortalCode = code,
            Galaxy = SelectedGalaxy(),
            Class = FindsService.HasClass(type) ? (FormClassBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "" : "",
            Subtype = FormSubtypeBox.Visibility == Visibility.Visible ? FormSubtypeBox.SelectedItem as string ?? "" : "",
            ItemIds = FindsService.HasItems(type) ? _formItems.ToList() : new(),
            Location = FormLocationBox.Text.Trim(),
            Notes = FormNotesBox.Text.Trim(),
        });

        ResetForm();
        RefreshTypeFilter();
        Apply();
    }

    /// <summary>Stores a typed system name in the shared system notes (user.db),
    /// so the Planets page and every card for this system pick it up.</summary>
    private void SaveSystemName(string code)
    {
        string name = FormSystemNameBox.Text.Trim();
        if (code.Length != 12 || name.Length == 0) return;
        ulong system = S.ToSystemAddress(NmsPortalCode.ToAddress(code, SelectedGalaxy()));
        var note = SystemNotesService.Get(system) ?? new SystemNote();
        if (note.SystemName == name) return;
        note.SystemName = name;
        SystemNotesService.Save(system, note);
        UpdateHere();
    }

    private void FormCancel_Click(object sender, RoutedEventArgs e)
    {
        ResetForm();
        SetFormOpen(false);
    }

    private void ResetForm()
    {
        _editingId = null;
        _editingCreatedAt = "";
        FormTitleBox.Text = "";
        FormLocationBox.Text = "";
        _autoLocation = "";
        FormNotesBox.Text = "";
        FormCodeBox.Text = "";
        FormSystemNameBox.Text = "";
        FormErrorTxt.Text = "";
        FormClassBox.SelectedIndex = 0;
        FormSubtypeBox.SelectedItem = null;
        _formItems.Clear();
        RenderFormItems();
        FormTitleTxt.Text = "ADD A FIND";
        FormSaveBtn.Content = "Save find";
    }

    // ---------------------------------------------------------------- helpers

    private int SelectedGalaxy() => (FormGalaxyBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? 1;

    private void SelectGalaxy(int galaxy) =>
        FormGalaxyBox.SelectedItem = FormGalaxyBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == galaxy) ?? FormGalaxyBox.Items.FirstOrDefault();

    private static void SelectByTag(ComboBox box, string tag) =>
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag) ?? box.Items.FirstOrDefault();

    private static string GalaxyLabel(int galaxy) =>
        NmsGalaxyNames.ForNumber(galaxy) is string name ? $"{name} ({galaxy})" : $"Galaxy {galaxy}";

    private static List<ImageSource> GlyphsFor(string code)
    {
        if (code.Length != 12) return new();
        var images = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }
}
