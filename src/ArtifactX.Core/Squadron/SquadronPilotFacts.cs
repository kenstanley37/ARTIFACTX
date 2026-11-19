namespace ArtifactX.Core.Squadron;

/// <summary>One line of a squadron pilot's stat card: the skill's language key (UI_SQUADRON_FACT_PILOTn / GOODn /
/// ROGUEn) and its grade's (UI_SQUADRON_FACT_GRADEn, 1 to 10).</summary>
public sealed record PilotFact(string Key, int Grade)
{
    public string GradeKey => $"UI_SQUADRON_FACT_GRADE{Grade}";
}

/// <summary>What the game's Squadron Pilots screen shows of a pilot besides its name: two flying skills, a good and a
/// rogue personality trait, each graded; confirmed kills; and the notes line (UI_SQUADRON_SHIP_FACTn).</summary>
public sealed record PilotFacts(PilotFact[] Skills, int Kills, string NoteKey);

/// <summary>
/// A squadron pilot's stat card, as NMS.exe 184271 builds it from the pilot's TraitsSeed and PilotRank (0x140578a40,
/// read 2026-11-19). None of it is in the save. Each pick hashes a short key - the way a TkID hashes, first 15
/// characters upper case - with the traits seed (CityHash Hash128to64) and draws from a generator started on that:
///   - CHOOSEPILOTTRAITS shuffles 1..10 (Fisher-Yates from the end); the first two are the flying skills;
///   - CHOOSEGOODTRAIT one of 15, CHOOSEROGUETRAIT one of 20, CHOOSESHIPFACT one of 25 notes;
///   - a skill's grade (0x14057c0a0): PILOT_TRAITn or GOOD_TRAITn, four draws averaged, scaled into the rank's
///     PilotRankTraitRanges entry (GcPlayerSquadronConfig: C 0-0.7, B 0.2-0.8, A 0.4-0.9, S 0.5-1), x10, taken from 10,
///     kept to 1..10; a rogue trait's grade is ROGUE_TRAITn, one draw of 1..10, whatever the rank;
///   - kills: NUMKILLS, one draw in the rank's range.
/// </summary>
public static class SquadronPilotFacts
{
    private const ulong K = 0x9DDFEA08EB382D69;
    private const ulong Multiplier = 0x5A76F899;

    /// <summary>Confirmed kills per rank, min and max inclusive: a static table in NMS.exe 184271 (0x14518f438, read with
    /// Ghidra 2026-11-19; not in any game data file): C 0-5, B 5-25, A 25-100, S 100-250.</summary>
    private static readonly (int Min, int Max)[] KillRanges = { (0, 5), (5, 25), (25, 100), (100, 250) };

    /// <param name="traitRanges">GcPlayerSquadronConfig.PilotRankTraitRanges (low, high) per rank 0-3.</param>
    public static PilotFacts Generate(ulong traitsSeed, int rank, IReadOnlyList<(float Low, float High)> traitRanges)
    {
        int r = Math.Clamp(rank, 0, 3);
        var (low, high) = r < traitRanges.Count ? traitRanges[r] : (0f, 1f);

        var order = Enumerable.Range(1, 10).ToArray();
        var shuffle = Start(Key("CHOOSEPILOTTRAITS", traitsSeed));
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = (int)(((ulong)Next(ref shuffle) * (ulong)(i + 1)) >> 32);
            (order[i], order[j]) = (order[j], order[i]);
        }
        int good = Pick(Key("CHOOSEGOODTRAIT", traitsSeed), 15) + 1;
        int rogue = Pick(Key("CHOOSEROGUETRAIT", traitsSeed), 20) + 1;
        var (min, max) = KillRanges[r];
        int kills = Pick(Key("NUMKILLS", traitsSeed), max - min + 1) + min;
        int note = Pick(Key("CHOOSESHIPFACT", traitsSeed), 25) + 1;

        var skills = new[]
        {
            new PilotFact($"UI_SQUADRON_FACT_PILOT{order[0]}", Grade(Key($"PILOT_TRAIT{order[0]}", traitsSeed), low, high)),
            new PilotFact($"UI_SQUADRON_FACT_PILOT{order[1]}", Grade(Key($"PILOT_TRAIT{order[1]}", traitsSeed), low, high)),
            new PilotFact($"UI_SQUADRON_FACT_GOOD{good}", Grade(Key($"GOOD_TRAIT{good}", traitsSeed), low, high)),
            new PilotFact($"UI_SQUADRON_FACT_ROGUE{rogue}", Pick(Key($"ROGUE_TRAIT{rogue}", traitsSeed), 10) + 1),
        };
        return new PilotFacts(skills, kills, $"UI_SQUADRON_SHIP_FACT{note}");
    }

    /// <summary>A skill's grade (0x14057c0a0): 10 minus the scaled mean of four draws, kept to 1..10.</summary>
    private static int Grade(ulong key, float low, float high)
    {
        var rng = Start(key);
        const double Fraction = 2.3283064370807974e-10;
        float f1 = (float)(Next(ref rng) * Fraction), f2 = (float)(Next(ref rng) * Fraction);
        float f3 = (float)(Next(ref rng) * Fraction), f4 = (float)(Next(ref rng) * Fraction);
        float mean = (f2 + f1 + f3 + f4) * 0.25f;
        int grade = 10 - (int)((mean * (high - low) + low) * 10f);
        return grade < 2 ? 1 : Math.Min(grade, 10);
    }

    private static int Pick(ulong key, int count)
    {
        var rng = Start(key);
        return (int)(((ulong)Next(ref rng) * (ulong)count) >> 32);
    }

    /// <summary>The traits seed combined with a TkID's hash: Hash128to64(low = seed, high = Hash128to64(low = bytes 8-15,
    /// high = bytes 0-7)) of the first 15 characters, upper case, zero padded.</summary>
    private static ulong Key(string text, ulong seed)
    {
        Span<byte> id = stackalloc byte[16];
        for (int i = 0; i < 15 && i < text.Length; i++) id[i] = (byte)char.ToUpperInvariant(text[i]);
        ulong idHash = Hash128(BitConverter.ToUInt64(id[8..]), BitConverter.ToUInt64(id[..8]));
        return Hash128(seed, idHash);
    }

    private static ulong Hash128(ulong low, ulong high)
    {
        ulong a = (low ^ high) * K;
        a ^= a >> 47;
        ulong b = (high ^ a) * K;
        b ^= b >> 47;
        return b * K;
    }

    private static (uint S0, uint S1) Start(ulong seed)
    {
        uint lo = (uint)seed, hi = (uint)(seed >> 32);
        return (lo == 0 ? 1u : lo, ((lo << 16) | (lo >> 16)) ^ hi ^ lo);
    }

    private static uint Next(ref (uint S0, uint S1) rng)
    {
        ulong t = rng.S0 * Multiplier + rng.S1;
        rng = ((uint)t, (uint)(t >> 32));
        return rng.S0;
    }
}
