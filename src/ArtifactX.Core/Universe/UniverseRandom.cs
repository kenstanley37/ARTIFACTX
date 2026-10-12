using System.Numerics;

namespace ArtifactX.Core.Universe;

// Ported to C# from nms_namegen (MIT) by Stuart Coyle & had.sh & GoodGuysFree:
// https://github.com/hadsh/nms_namegen - see THIRD-PARTY-NOTICES.md.
// The Python reference masks every addition back to 64 bits by hand; C#'s
// ulong arithmetic wraps the same way natively, which is why the code below
// has no masks. Verified bit-exact against the reference's golden vectors
// (UniverseGeneratorTests / the verification harness).

/// <summary>The game's multiply-with-carry random stream (nms_namegen prng.py).</summary>
public sealed class UniverseRandom
{
    public const ulong Multiplier = 0x5A76F899;

    public ulong Seed { get; private set; }

    public UniverseRandom(ulong seed) => Seed = seed;

    /// <summary>Advances the stream one step.</summary>
    public void Step() => Seed = (Seed & 0xFFFFFFFF) * Multiplier + (Seed >> 32);

    /// <summary>A number from 0 to range-1.</summary>
    public uint Next(uint range)
    {
        Step();
        return (uint)(((Seed & 0xFFFFFFFF) * range) >> 32);
    }

    /// <summary>The next 32-bit word.</summary>
    public uint NextWord()
    {
        Step();
        return (uint)(Seed & 0xFFFFFFFF);
    }

    /// <summary>The low 32 bits of the current state, read without advancing.</summary>
    public uint CurrentWord => (uint)(Seed & 0xFFFFFFFF);
}

/// <summary>
/// The Threefish/Skein-style hash that turns a universal address into a
/// system seed (nms_namegen iprng.py, indexPrimedPRNG).
/// </summary>
public static class UniverseHash
{
    private static ulong Ror(ulong x, int r) => BitOperations.RotateRight(x, r & 63);

    private static (ulong, ulong, ulong, ulong) Round(ulong a, ulong b, ulong c, ulong d, int rotA, int rotB)
    {
        ulong a1 = Ror(b, rotA) ^ c;
        ulong b1 = Ror(a, rotB) ^ d;
        return (a1, b1, b1 + c, a1 + d);
    }

    private static ulong[] Mix(ulong a, ulong b, ulong c, ulong d, ulong key, ulong seed)
    {
        (a, b, c, d) = Round(a, b, c, d, -0x17, 0x18);
        (a, b, c, d) = Round(a, b, c, d, -0x5, 0x1B);
        a += key + 1;
        d += key + 1;
        (a, b, c, d) = Round(a, b, c, d, -0x19, 0x1F);
        (a, b, c, d) = Round(a, b, c, d, 0x12, -0xC);
        (a, b, c, d) = Round(a, b, c, d, 0x6, -0x16);
        (a, b, c, d) = Round(a, b, c, d, -0x20, -0x20);
        a += seed + 2;
        d += key + seed + 2;
        (a, b, c, d) = Round(a, b, c, d, -0xE, -0x10);
        (a, b, c, d) = Round(b, a, d, c, 0x7, 0xC);
        (a, b, c, d) = Round(b, a, d, c, -0x17, 0x18);
        (a, b, c, d) = Round(a, b, c, d, -0x5, 0x1B);
        a += 3;
        b += key;
        c += key;
        d += 3 + seed;
        (a, b, c, d) = Round(a, b, c, d, -0x19, 0x1F);
        (a, b, c, d) = Round(a, b, c, d, 0x12, -0xC);
        (a, b, c, d) = Round(a, b, c, d, 0x6, -0x16);
        (a, b, c, d) = Round(a, b, c, d, -0x20, -0x20);
        a += 4;
        b += seed;
        c += seed + key;
        d += 4;
        (a, b, c, d) = Round(a, b, c, d, -0xE, -0x10);
        (a, b, c, d) = Round(a, b, c, d, 0xC, 0x7);
        (a, b, c, d) = Round(a, b, c, d, -0x17, 0x18);
        return new[] { c + seed, Ror(a, 0x1B) ^ d, d, (Ror(b, -0x5) ^ c) + 5 };
    }

    /// <summary>System seed word for a universal address (galaxy and system id included).</summary>
    public static ulong IndexPrimed(ulong universalAddress)
    {
        ulong seed = universalAddress & 0xFFFFFFFFFF;
        long systemId = (long)((universalAddress >> 40) & 0xFFF);
        ulong key = seed ^ 0x1BD11BDAA9FC1A22;
        ulong a = seed;
        ulong b = Ror(seed, 7) ^ seed;
        ulong[] o = Mix(a, b, b + a, seed + seed, key, seed);

        long counter;
        if (systemId >= 9)
        {
            long systemIndex = systemId - 1;
            long high = (systemIndex - 8) >> 3;
            counter = (systemIndex - 8) & 7;
            a = (ulong)(high + 1) + seed;
            b = Ror(a, 7) ^ a;
            o = Mix(a, b, b + a, a + a, key, seed);
        }
        else
        {
            counter = systemId - 1;
        }

        // System id 0 gives counter -1; Python's o[-1] is the last word.
        long index = counter >> 1;
        if (index < 0) index += 4;
        counter += 1;
        return (counter & 1) == 0 ? o[index] >> 32 : o[index];
    }
}
