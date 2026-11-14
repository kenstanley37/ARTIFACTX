namespace ArtifactX.Core.Universe;

/// <summary>The language keys of the words the game shows for a system's economy, wealth and
/// conflict ("UI_ECON_CLASS_MINING_4", "UI_ECON_LEVEL_LOW_1", "UI_CONFLICT_LEVEL_MED_8").</summary>
public sealed record SystemWordKeys(string Economy, string Wealth, string Conflict)
{
    /// <summary>The economy's plain word ("UI_ECON_CLASS_MINING_1", "Mining"), which the screen that
    /// passes a count of 1 (0x141388160) always shows.</summary>
    public string PlainEconomy => Economy[..(Economy.LastIndexOf('_') + 1)] + "1";
}

/// <summary>
/// Which of its words the game picks for a system's economy, wealth and conflict: "Prospecting //
/// Struggling" rather than "Mining // Low" (issue #66).
/// <para>
/// NMS.exe 184271, 0x141687890 (the galaxy map and system info lines; 0x141688310 builds the economy
/// key). The seed is the system's universal address ([system:12][galaxy:8][Y:8][Z:12][X:12], as the
/// caller 0x1405225b0 packs it). It goes through a 64-bit finalizer (xor-shift 33, multiply
/// 0x64DD81482CBD31D7, xor-shift 33, multiply 0xE36AA5C613612997), which seeds the game's
/// multiply-with-carry stream: low word = (hi &gt;&gt; 1) ^ lo (1 if that is 0), high word = rol16(that)
/// ^ hi ^ that. One step gives the draw. The function puts the stream back before each line, so
/// conflict, economy and wealth all use that same first draw: word = (count * draw &gt;&gt; 32) + 1,
/// key = prefix + that number ("%s%d", 0x144b17008).
/// </para>
/// <para>
/// The counts are constants in the executable, not the number of words in the language files:
/// {8, 8, 8, 1} for low/medium/high/pirate at 0x144bfdab0 (wealth and conflict), and 4 for the
/// economy. Conflict words 9 and 10 ("Unthreatening", "Mild", "Belligerent", "Rowdy",
/// "Destructive", "Aggressive") exist in the language files but the game never picks them. One
/// caller (0x141388160) passes an economy count of 1 and so shows the plain word ("Mining"); the
/// system info lines use 4.
/// </para>
/// <para>
/// Checked 2026-11-14 against the 363 systems with recorded words in user.db: every system read from
/// the save matches (wealth 61/61, conflict 58/58), and so do the player's typed notes (wealth 77/78)
/// and screen captures (139/164). 18 of the 25 capture misses come from one region (5C6DC00C)
/// recorded in October, where the economy and wealth levels themselves don't match either.
/// </para>
/// </summary>
public static class SystemWords
{
    /// <summary>Words per wealth or conflict level the game picks from (0x144bfdab0).</summary>
    public const int LevelWordCount = 8;

    /// <summary>Words per economy the system info lines pick from (0x141687890's default of 4).</summary>
    public const int EconomyWordCount = 4;

    /// <summary>The universal address the system generator and the word pick are seeded from.</summary>
    public static ulong UniversalAddress(ulong portalCode, int galaxy)
    {
        ulong systemId = (portalCode >> 32) & 0xFFF;
        return (((systemId << 8) | ((ulong)galaxy & 0xFF)) << 32) | (portalCode & 0xFFFFFFFF);
    }

    /// <summary>The one random draw all three words come from.</summary>
    public static uint Draw(ulong universalAddress)
    {
        ulong x = universalAddress;
        x = ((x >> 33) ^ x) * 0x64DD81482CBD31D7UL;
        x = ((x >> 33) ^ x) * 0xE36AA5C613612997UL;
        uint hi = (uint)(x >> 32), lo = (uint)x;
        uint mixed = (hi >> 1) ^ lo;
        uint low = mixed == 0 ? 1u : mixed;
        uint high = ((mixed >> 16) | (mixed << 16)) ^ hi ^ mixed;
        return (uint)((ulong)low * UniverseRandom.Multiplier + high);
    }

    /// <summary>The word number (1 to count) the game picks for a draw.</summary>
    public static int WordNumber(uint draw, int count) => (int)(((ulong)count * draw) >> 32) + 1;

    /// <summary>
    /// The keys of the words the game shows for a system, or null when it shows none (uncharted and
    /// abandoned systems read "Data Unavailable"). Pirate systems' wealth and conflict are their own
    /// single words ("Black Market", "Pirate Controlled"); their economy is picked as usual.
    /// </summary>
    public static SystemWordKeys? Keys(ulong portalCode, int galaxy, SystemAttributes attributes)
    {
        if (attributes.Uncharted || attributes.Abandoned) return null;
        string? economy = attributes.Economy switch
        {
            SystemEconomy.Trading => "TRADE",
            SystemEconomy.AdvancedMaterials => "ALLOY",
            SystemEconomy.Scientific => "SCIENCE",
            SystemEconomy.Mining => "MINING",
            SystemEconomy.Manufacturing => "MANUFACT",
            SystemEconomy.Technology => "TECH",
            SystemEconomy.PowerGeneration => "POWER",
            _ => null,
        };
        if (economy is null) return null;
        uint draw = Draw(UniversalAddress(portalCode, galaxy));
        int level = WordNumber(draw, LevelWordCount);
        return new SystemWordKeys(
            $"UI_ECON_CLASS_{economy}_{WordNumber(draw, EconomyWordCount)}",
            $"UI_ECON_LEVEL_{Level(attributes.Wealth)}_{level}",
            $"UI_CONFLICT_LEVEL_{Level(attributes.Conflict)}_{level}");
    }

    private static string Level(int level) => level switch { 3 => "HIGH", 2 => "MED", _ => "LOW" };
}
