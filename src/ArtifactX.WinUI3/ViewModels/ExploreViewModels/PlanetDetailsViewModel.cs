using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

public partial class PlanetDetailsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _planetName = "Pundieg Prime";

    [ObservableProperty]
    private string _systemName = "Akare Colony System";

    [ObservableProperty]
    private string _biomeClassification = "Normal - Lush - Humid";

    [ObservableProperty]
    private string _weatherType = "Superheated Rain";

    [ObservableProperty]
    private string _weatherDescription = "Intermittent thermal storms";

    [ObservableProperty]
    private string _sentinelActivity = "Low Security";

    [ObservableProperty]
    private string _floraDensity = "Full";

    [ObservableProperty]
    private string _faunaAbundance = "Generous (14 species)";

    public ObservableCollection<ResourceItemViewModel> PlanetResources { get; } = new()
        {
            new("Star Bulb", "Abundant Flora Element"),
            new("Copper", "Mineral Deposit"),
            new("Paraffinium", "Subterranean Resource"),
            new("Magnetised Ferrite", "Condensed Deposit")
        };

    public ObservableCollection<BaseRecordViewModel> PlanetBases { get; } = new()
        {
            new("Main Lush Fishing Outpost", "Portal Hex: 0x0021060002cb95f5"),
            new("Secondary Hydroponics Bay", "Portal Hex: 0x00203e09e8f83732")
        };
}

public record ResourceItemViewModel(string Name, string AbundanceOrType);
public record BaseRecordViewModel(string BaseName, string CoordinatesString);