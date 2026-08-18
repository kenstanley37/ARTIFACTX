using ArtifactX.Tools.DataCataloger.Models;

namespace ArtifactX.Tools.DataCataloger.Services;

/// <summary>
/// Flat lookup of every file across every PAK, keyed by path, so an icon/texture path
/// discovered in one PAK (e.g. a table inside NMSARC.MetadataEtc.pak) can be resolved to
/// its actual bytes in a completely different PAK (e.g. NMSARC.TexUI.pak). Build this once
/// up front by feeding it every PAK's entry list before running the catalog/icon passes.
/// </summary>
public class GlobalFileIndexService
{
    private readonly Dictionary<string, (string PakPath, PakEntry Entry, PakHeader Header)> _index
        = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string pakPath, PakHeader header, IReadOnlyList<PakEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.FileName)) continue;

            // Last PAK wins on collisions - fine in practice since a given texture path
            // is only ever packed into one PAK per platform build.
            _index[entry.FileName] = (pakPath, entry, header);
        }
    }

    public bool TryFind(string relativePath, out string pakPath, out PakEntry entry, out PakHeader header)
    {
        if (_index.TryGetValue(relativePath, out var found))
        {
            (pakPath, entry, header) = found;
            return true;
        }

        pakPath = "";
        entry = null!;
        header = null!;
        return false;
    }

    public int Count => _index.Count;
}