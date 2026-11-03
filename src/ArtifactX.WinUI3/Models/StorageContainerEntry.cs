using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.Models;

public sealed partial class StorageContainerEntry(string key, string name) : ObservableObject
{
    public string Key { get; } = key;
    public string Name { get; } = name;
    [ObservableProperty] public partial bool IsSelected { get; set; }
    public override string ToString() => Name;
}
