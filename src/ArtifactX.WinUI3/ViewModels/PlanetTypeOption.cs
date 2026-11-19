using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A Planet type choice on Fishing: a planet type, or WhereIAm / AllTypes. The "Where I am" label names the
/// type you're on, so it changes in place.</summary>
public sealed partial class PlanetTypeOption(string label, int biome) : ObservableObject
{
    public const int WhereIAm = -2;
    public const int AllTypes = -1;

    [ObservableProperty] public partial string Label { get; set; } = label;

    public int Biome { get; } = biome;

    public override string ToString() => Label;
}
