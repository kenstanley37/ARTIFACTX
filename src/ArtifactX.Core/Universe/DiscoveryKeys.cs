namespace ArtifactX.Core.Universe;

/// <summary>
/// Pieces of a discovery record's key (fDu/ETO/OsQ/?fB 8P3 bEr) shared by every kind.
/// </summary>
public static class DiscoveryKeys
{
    /// <summary>
    /// The type hash a Flora, Mineral or Animal record keeps in bEr[1]: djb2 (h = h * 33 + c from 5381, 64-bit) of the
    /// discovered node's scene name, upper-cased, cut at its first '.' and ending ".SCENE.MBIN" (NMS.exe 184271: the key
    /// builder 0x14044e1e0 hashes what 0x142c51de0 writes). For a creature it is the creature model's scene. Checked
    /// 2026-11-19 against the owner's Slot 8 save: every type hash of its 3,248 records (234 plant and mineral, 47
    /// creature), e.g. "MODELS/PLANETS/CREATURES/SMALLBIRD/BIRD.SCENE.MBIN" = 0xE7CAD69E2F26FF55.
    /// </summary>
    public static ulong TypeHash(string scenePath)
    {
        string name = scenePath.Replace('\\', '/').ToUpperInvariant();
        int dot = name.IndexOf('.');
        if (dot >= 0) name = name[..dot];
        name += ".SCENE.MBIN";
        ulong hash = 5381;
        foreach (char c in name) hash = hash * 33 + (byte)c;
        return hash;
    }

    /// <summary>The 64-bit mixer (the planet generator's Mix64) an Animal record keeps of its role seed in bEr[4]
    /// (checked on the 2026-11-18 Slot 8 diff's bird).</summary>
    public static ulong Mix64(ulong v)
    {
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }
}
