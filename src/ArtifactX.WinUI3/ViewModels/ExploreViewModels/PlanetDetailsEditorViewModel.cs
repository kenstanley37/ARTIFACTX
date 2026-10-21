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
    IReadOnlyList<string> Sentinels,
    IReadOnlyList<string> Life,
    IReadOnlyList<string> Weather,
    IReadOnlyList<string> Resources,
    IReadOnlyList<FishInfo> Fish,
    IReadOnlyDictionary<string, string> ItemNames)
{
    public static async Task<PlanetVocabulary> LoadAsync()
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
            Group("Sentinels"), Group("Life"), Group("Weather"),
            (substancesTask.Result ?? new()).Concat(Group("Resource"))
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList(),
            (fishTask.Result ?? new()).Where(f => CatalogService.IsFishSpecies(f.Id) && !f.NeedsMission).ToList(),
            namesTask.Result ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> Matching(IReadOnlyList<string> words, string text) =>
        string.IsNullOrWhiteSpace(text) ? [] : words.Where(w => w.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).Take(15).ToList();
}

/// <summary>"Edit details" on a planet's page: what the player records about a planet
/// that the save doesn't hold (PlanetDetailsService, PlanetWaterService) and its name in
/// game (SystemNotesService).</summary>
public sealed partial class PlanetDetailsEditorViewModel : ObservableObject
{
    private readonly KnownPlanet _planet;

    public PlanetDetailsEditorViewModel(KnownPlanet planet, PlanetVocabulary vocabulary)
    {
        _planet = planet;
        Vocabulary = vocabulary;
        var d = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.Copy() ?? new PlanetDetails();
        string generated = UniverseInfo.GeneratedPlanetName(planet.SystemAddress, planet.PlanetIndex);
        string custom = SystemNotesService.Get(planet.SystemAddress)?.PlanetNames.GetValueOrDefault(IndexKey) ?? "";
        NamePlaceholder = generated;
        Name = custom == generated ? "" : custom;
        Descriptor = d.Descriptor;
        Weather = d.Weather;
        Sentinels = d.Sentinels;
        Flora = d.Flora;
        Fauna = d.Fauna;
        WaterIndex = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex) switch { true => 1, false => 2, _ => 0 };
        Resources = new ObservableCollection<string>(d.Resources);
        Notes = new ObservableCollection<string>(d.Notes);
    }

    public PlanetVocabulary Vocabulary { get; }
    public string NamePlaceholder { get; }
    public IReadOnlyList<string> WaterChoices { get; } = ["Not recorded", "Yes", "No"];
    public ObservableCollection<string> Resources { get; }
    public ObservableCollection<string> Notes { get; }

    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Descriptor { get; set; }
    [ObservableProperty] public partial string Weather { get; set; }
    [ObservableProperty] public partial string Sentinels { get; set; }
    [ObservableProperty] public partial string Flora { get; set; }
    [ObservableProperty] public partial string Fauna { get; set; }
    [ObservableProperty] public partial int WaterIndex { get; set; }
    [ObservableProperty] public partial string NewNote { get; set; } = "";
    [ObservableProperty] public partial string Status { get; private set; } = "";

    private string IndexKey => _planet.PlanetIndex.ToString(CultureInfo.InvariantCulture);

    public void AddResource(string name)
    {
        name = name.Trim();
        if (name.Length > 0 && !Resources.Contains(name, StringComparer.OrdinalIgnoreCase)) Resources.Add(name);
    }

    public void AddNote()
    {
        if (NewNote.Trim() is { Length: > 0 } note) Notes.Add(note);
        NewNote = "";
    }

    public void Save()
    {
        PlanetDetailsService.Save(_planet.SystemAddress, _planet.PlanetIndex, new PlanetDetails
        {
            Weather = Weather.Trim(),
            Sentinels = Sentinels.Trim(),
            Flora = Flora.Trim(),
            Fauna = Fauna.Trim(),
            Descriptor = Descriptor.Trim(),
            FaunaTotal = PlanetDetailsService.Get(_planet.SystemAddress, _planet.PlanetIndex)?.FaunaTotal,
            Resources = Resources.ToList(),
            Notes = Notes.ToList(),
        });
        PlanetWaterService.Set(_planet.SystemAddress, _planet.PlanetIndex, WaterIndex switch { 1 => true, 2 => false, _ => null });

        var note = SystemNotesService.Get(_planet.SystemAddress) ?? new SystemNote();
        string name = Name.Trim();
        if (name.Length > 0 && !string.Equals(name, NamePlaceholder, StringComparison.OrdinalIgnoreCase))
            note.PlanetNames[IndexKey] = name;
        else
            note.PlanetNames.Remove(IndexKey);
        SystemNotesService.Save(_planet.SystemAddress, note);
        Status = $"Saved at {DateTime.Now:t}";
    }
}
