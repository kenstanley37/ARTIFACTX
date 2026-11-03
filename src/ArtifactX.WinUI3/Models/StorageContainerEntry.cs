namespace ArtifactX.WinUI3.Models;

public sealed record StorageContainerEntry(string Key, string Name)
{
    public override string ToString() => Name;
}
