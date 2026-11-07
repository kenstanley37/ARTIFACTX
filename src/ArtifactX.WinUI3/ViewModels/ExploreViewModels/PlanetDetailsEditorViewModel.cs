using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>The game's own words for planets (catalog "planet descriptions" step) and the
/// substance names, for the editor's pickers and suggestions; and the fish.</summary>
public sealed record PlanetVocabulary(
    IReadOnlyList<(string Word, string Group, string Tier)> Words,
    IReadOnlyList<string> Weather,
    IReadOnlyList<string> Resources,
    IReadOnlyList<FishInfo> Fish,
    IReadOnlyDictionary<string, string> ItemNames)
{
    private static Task<PlanetVocabulary>? _load;

    /// <summary>Read once; the catalog doesn't change while the app runs (a rebuild restarts the pages).</summary>
    public static Task<PlanetVocabulary> LoadAsync() => _load ??= ReadAsync();

    private static async Task<PlanetVocabulary> ReadAsync()
    {
        var wordsTask = CatalogService.GetPlanetDescriptorsAsync();
        var substancesTask = CatalogService.GetSubstanceNamesAsync();
        var fishTask = CatalogService.GetFishAsync();
        var namesTask = CatalogService.GetItemNamesAsync();
        await Task.WhenAll(wordsTask, substancesTask, fishTask, namesTask);
        var words = wordsTask.Result ?? new();

        List<string> Group(string group) => words.Where(w => w.Group == group).Select(w => w.Word)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        return new PlanetVocabulary(
            words,
            Group("Weather"),
            // Plus the special finds a planet lists among its resources (Ancient Bones...).
            (substancesTask.Result ?? new()).Concat(Group("Resource"))
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList(),
            // The 210 real species, as on the Fishing page (no junk or mission-only fish).
            (fishTask.Result ?? new()).Where(f => CatalogService.IsFishSpecies(f.Id) && !f.NeedsMission).ToList(),
            namesTask.Result ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>"Frenzied (High)": a group's words with their level, lowest level first.</summary>
    public IReadOnlyList<string> Labelled(string group) => Words.Where(w => w.Group == group)
        .GroupBy(w => w.Word, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
        .OrderBy(w => Array.IndexOf(PlanetText.LevelOrder, PlanetText.LevelName(w.Tier)) is var i && i < 0 ? 99 : i)
        .ThenBy(w => w.Word, StringComparer.CurrentCultureIgnoreCase)
        .Select(w => $"{w.Word} ({PlanetText.LevelName(w.Tier)})").ToList();

    /// <summary>The word in a "Word (Level)" label.</summary>
    public static string WordOf(string label) =>
        label.LastIndexOf(" (", StringComparison.Ordinal) is int cut and > 0 ? label[..cut].Trim() : label.Trim();

    public static IReadOnlyList<string> Matching(IReadOnlyList<string> words, string text) =>
        string.IsNullOrWhiteSpace(text) ? [] : words.Where(w => w.Contains(text.Trim(), StringComparison.CurrentCultureIgnoreCase)).Take(20).ToList();
}

/// <summary>A resource typed with a typo, and the game's word it's probably meant to be.</summary>
public sealed record ResourceFix(string Typed, string Better)
{
    public string Text => $"\"{Typed}\" isn't one of the game's resources - did you mean {Better}?";
    public string Action => $"Use {Better}";
}

/// <summary>"Edit details" on a planet's page: what the player records about a planet
/// that the save doesn't hold (PlanetDetailsService, PlanetWaterService) and its name in
/// game (SystemNotesService). Words come from the game; anything typed is kept too.</summary>
public sealed partial class PlanetDetailsEditorViewModel : ObservableObject
{
    private readonly KnownPlanet _planet;

    public PlanetDetailsEditorViewModel(KnownPlanet planet, PlanetVocabulary vocabulary)
    {
        _planet = planet;
        Vocabulary = vocabulary;
        SentinelChoices = vocabulary.Labelled("Sentinels");
        LifeChoices = vocabulary.Labelled("Life");
        var d = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.Copy() ?? new PlanetDetails();
        string generated = UniverseInfo.GeneratedPlanetName(planet.SystemAddress, planet.PlanetIndex);
        string custom = SystemNotesService.Get(planet.SystemAddress)?.PlanetNames.GetValueOrDefault(IndexKey) ?? "";
        NamePlaceholder = generated;
        NameHint = $"Original name: {generated}. If whoever discovered it renamed it, type the name the game shows - ArtifactX also learns it when you land there.";
        Name = custom == generated ? "" : custom;
        Descriptor = d.Descriptor;
        Weather = d.Weather;
        Sentinels = LabelFor(SentinelChoices, d.Sentinels);
        Flora = LabelFor(LifeChoices, d.Flora);
        Fauna = LabelFor(LifeChoices, d.Fauna);
        WaterIndex = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex) switch { true => 1, false => 2, _ => 0 };
        Resources = new ObservableCollection<string>(d.Resources);
        Notes = new ObservableCollection<string>(d.Notes);
        Resources.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ResourceFixes));
    }

    public PlanetVocabulary Vocabulary { get; }
    public IReadOnlyList<string> SentinelChoices { get; }
    public IReadOnlyList<string> LifeChoices { get; }
    public string NamePlaceholder { get; }
    public string NameHint { get; }
    public IReadOnlyList<string> WaterChoices { get; } = ["Not recorded", "Yes", "No"];
    public ObservableCollection<string> Resources { get; }
    public ObservableCollection<string> Notes { get; }

    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Descriptor { get; set; }
    [ObservableProperty] public partial string Sentinels { get; set; }
    [ObservableProperty] public partial string Flora { get; set; }
    [ObservableProperty] public partial string Fauna { get; set; }
    [ObservableProperty] public partial int WaterIndex { get; set; }
    [ObservableProperty] public partial string NewNote { get; set; } = "";
    [ObservableProperty] public partial string NewResource { get; set; } = "";
    [ObservableProperty] public partial string Status { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeatherFix), nameof(WeatherFixText), nameof(WeatherFixAction), nameof(HasWeatherFix))]
    public partial string Weather { get; set; }

    /// <summary>Checked when the box is left or a suggestion picked, not on every key.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeatherFixText), nameof(WeatherFixAction), nameof(HasWeatherFix))]
    public partial string? WeatherFix { get; private set; }

    public bool HasWeatherFix => WeatherFix is not null;
    public string WeatherFixText => WeatherFix is null ? "" : $"\"{Weather.Trim()}\" isn't one of the game's weathers - did you mean {WeatherFix}?";
    public string WeatherFixAction => WeatherFix is null ? "" : $"Use {WeatherFix}";

    public IReadOnlyList<ResourceFix> ResourceFixes => Resources
        .Select(r => WordSuggestion.For(r, Vocabulary.Resources) is string better ? new ResourceFix(r, better) : null)
        .OfType<ResourceFix>().ToList();

    private string IndexKey => _planet.PlanetIndex.ToString(CultureInfo.InvariantCulture);

    private static string LabelFor(IReadOnlyList<string> labels, string word) =>
        word.Length == 0 ? "" : labels.FirstOrDefault(l => string.Equals(PlanetVocabulary.WordOf(l), word, StringComparison.OrdinalIgnoreCase)) ?? word;

    public void CheckWeather() => WeatherFix = WordSuggestion.For(Weather, Vocabulary.Weather);

    public void UseWeatherFix()
    {
        if (WeatherFix is not string better) return;
        Weather = better;
        CheckWeather();
    }

    /// <summary>Adds a resource in the game's spelling when it is one (duplicate-safe).</summary>
    public void AddResource(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        string name = Vocabulary.Resources.FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase)) ?? text;
        if (!Resources.Contains(name, StringComparer.OrdinalIgnoreCase)) Resources.Add(name);
        NewResource = "";
    }

    public void UseResourceFix(ResourceFix fix)
    {
        int at = Resources.IndexOf(fix.Typed);
        if (at < 0) return;
        if (Resources.Contains(fix.Better, StringComparer.OrdinalIgnoreCase)) Resources.RemoveAt(at);
        else Resources[at] = fix.Better;
    }

    public void AddNote()
    {
        if (NewNote.Trim() is { Length: > 0 } note) Notes.Add(note);
        NewNote = "";
    }

    public void Save()
    {
        // Something typed but not added yet still counts.
        AddNote();
        AddResource(NewResource);
        PlanetDetailsService.Save(_planet.SystemAddress, _planet.PlanetIndex, new PlanetDetails
        {
            Weather = Weather.Trim(),
            Sentinels = PlanetVocabulary.WordOf(Sentinels),
            Flora = PlanetVocabulary.WordOf(Flora),
            Fauna = PlanetVocabulary.WordOf(Fauna),
            Descriptor = Descriptor.Trim(),
            FaunaTotal = PlanetDetailsService.Get(_planet.SystemAddress, _planet.PlanetIndex)?.FaunaTotal,
            Resources = Resources.ToList(),
            Notes = Notes.ToList(),
        });
        bool? water = WaterIndex switch { 1 => true, 2 => false, _ => null };
        if (water != PlanetWaterService.Get(_planet.SystemAddress, _planet.PlanetIndex))
            PlanetWaterService.Set(_planet.SystemAddress, _planet.PlanetIndex, water);

        var note = SystemNotesService.Get(_planet.SystemAddress) ?? new SystemNote();
        note.PlanetNames.TryGetValue(IndexKey, out var oldName);
        string name = Name.Trim();
        if (name.Length > 0 && !string.Equals(name, NamePlaceholder, StringComparison.OrdinalIgnoreCase) && name != oldName)
        {
            note.PlanetNames[IndexKey] = name;
            SystemNotesService.Save(_planet.SystemAddress, note);
        }
        else if ((name.Length == 0 || string.Equals(name, NamePlaceholder, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(oldName))
        {
            note.PlanetNames.Remove(IndexKey);
            SystemNotesService.Save(_planet.SystemAddress, note);
        }
        Status = $"Saved at {DateTime.Now:t}";
    }
}
