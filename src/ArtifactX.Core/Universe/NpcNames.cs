namespace ArtifactX.Core.Universe;

/// <summary>A generated NPC name: the title's language key ("WAR_TITLE_20" - "Liquidator") and the name ("Uduoh").</summary>
public sealed record NpcName(string TitleKey, string Name)
{
    /// <summary>The full name in the game's NPC_NAME_LAYOUT ("%TITLE% %NAME%").</summary>
    public string Format(string layout, string title) =>
        layout.Replace("%TITLE%", title).Replace("%NAME%", Name).Trim();
}

/// <summary>
/// The game's NPC name generator: a title from the race's title list and a word from the race's letter set, both from
/// one 64-bit seed. Read from NMS.exe 184271 (0x140ea9a10, 2026-11-19); squadron pilots call it with the pilot-and-ship
/// name seed and the race of their NPC scene (GcNPCComponentData.Race). Checked against the owner's four Vy'keen pilots
/// (Liquidator Uduoh, Captain Yongomord, Scout Ribxi, Recruit Gangolag).
///   - Title: the seed's own generator (no mixing), one draw of the race's title count, + 1: "{prefix}_TITLE_{n}".
///   - Name: a generator started on Mix64(seed), NameGenerator with the race's letter set, 3 to 9 letters, first
///     letter upper case.
/// </summary>
public static class NpcNames
{
    /// <summary>Per GcAlienRace (Traders, Warriors, Explorers, Robots, Atlas, Diplomats, Exotics, None): static tables in
    /// NMS.exe 184271, not in any game data file (read with Ghidra 2026-11-19). Title prefixes from the pointer table at
    /// 0x144aef2f0 (an empty prefix means the race has no title list); title counts from 0x144bfdaf0/0x144bfda50; letter
    /// sets from 0x144aef2c8.</summary>
    private static readonly (string Prefix, int Titles, int LetterSet)[] Races =
    {
        ("TRA", 40, 2), ("WAR", 40, 4), ("EXP", 40, 3), ("", 1, 0), ("", 1, 0), ("DIP", 10, 7), ("FIF", 1, 0), ("", 1, 3),
    };

    /// <summary>The title list prefixes ("TRA", "WAR"...), for picking the title words out of the language files.</summary>
    public static IEnumerable<string> TitlePrefixes => Races.Select(r => r.Prefix).Where(p => p.Length > 0).Distinct();

    /// <summary>The name for a seed and a GcAlienRace raw value; null for a race outside the game's tables.</summary>
    public static NpcName? Generate(ulong seed, int race)
    {
        if (race < 0 || race >= Races.Length) return null;
        var (prefix, titles, letterSet) = Races[race];
        int title = (int)Start(seed).Next((uint)titles) + 1;
        string name = NameGenerator.Generate(Start(DiscoveryKeys.Mix64(seed)), letterSet, 3, 9);
        if (name.Length > 0) name = char.ToUpperInvariant(name[0]) + name[1..];
        return new NpcName(prefix.Length > 0 ? $"{prefix}_TITLE_{title}" : "", name);
    }

    /// <summary>The game's generator started on a 64-bit value: S0 = low (1 when 0), S1 = rot16(low) ^ high ^ low.</summary>
    private static UniverseRandom Start(ulong v)
    {
        uint lo = (uint)v, hi = (uint)(v >> 32);
        uint s1 = ((lo >> 16) | (lo << 16)) ^ hi ^ lo;
        return new UniverseRandom(((ulong)s1 << 32) | (lo == 0 ? 1u : lo));
    }
}
