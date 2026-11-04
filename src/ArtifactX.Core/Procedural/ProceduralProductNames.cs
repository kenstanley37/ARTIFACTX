using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Procedural;

/// <summary>One placeholder of a procedural product's name ("%LOOTADJ%") and the
/// words for it at each rarity, in the game's order. An empty uncommon or rare
/// list means the game falls back to the common one.</summary>
public sealed record ProceduralNameWord(string ReplaceKey, IReadOnlyList<string> Common,
    IReadOnlyList<string> Uncommon, IReadOnlyList<string> Rare);

/// <summary>How one procedural product (PROC_LOOT, PROC_BIO...) is named:
/// GcProceduralProductData's NameGeneratorBase variants (the templates),
/// NameGeneratorWordList, NameGeneratorLegacyRolls and the DropWeight of its
/// Common/Uncommon/Rare ProceduralData.</summary>
public sealed record ProceduralProductNaming(string ProductId, int LegacyRolls, int[] RarityWeights,
    IReadOnlyList<string> Formats, IReadOnlyList<ProceduralNameWord> Words);

/// <summary>
/// The name the game gives a procedural product from the seed after its '#'
/// ("PROC_LOOT#06686" -> "Shiny Bones"). Read off NMS.exe build 184271 on
/// 2026-11-04 (static disassembly, nothing run) and checked against the owner's
/// Slot 8 Treasures screen: PROC_LOOT#06686 "Shiny Bones", PROC_BIO#15819
/// "Precious Hatchlings", PROC_FOSS#41022 "Carbonized Beetle Antennae" (British
/// "Carbonised" in the catalog's English text) and PROC_PLNT#00902 "Pulpoak
/// Cultivar" - 4 of 4. The description builder 0x140edee70:
///
///   1. parses the seed with atoi (0x140ee7050) and hashes it (0x140edf14f):
///      h = (s ^ 61) ^ (s &gt;&gt; 16); h *= 9; h ^= h &gt;&gt; 4; h *= 0x1b873593; h ^= h &gt;&gt; 15;
///   2. seeds the generator with h (StartState) and draws two words, the value
///      and age rolls;
///   3. draws a third: the rarity, weighted by DropWeight (0x140ee6a20, on a
///      copy of the state), and the description variant (0x140ea23b0, on the
///      state itself) - the same word, so one step;
///   4. for every category except FreighterPassword/CaptLog/CrewList,
///      FreighterTech*, DismantleData and MessageInBottle (their own branches),
///      draws two more words w1, w2 and calls the name routine 0x140eb1d10 with
///      Mix64((w2 &lt;&lt; 32) | w1) and the rarity;
///   5. the name routine mixes that seed again, StartState, skips
///      NameGeneratorLegacyRolls steps, then one step per word (the rarity's
///      word, the common one when that is empty; variant `(word * n) &gt;&gt; 32`
///      of n, key "%s_%i" from 1, the bare word when it has none) and one for
///      the template, and replaces each ReplaceKey in it.
/// </summary>
public static class ProceduralProductNames
{
    private const ulong M32 = 0xFFFFFFFF;

    /// <summary>The name for the seed in an item id ("PROC_LOOT#06686"), or
    /// null when the id has no seed or the naming has no templates.</summary>
    public static string? Generate(string itemId, ProceduralProductNaming naming)
    {
        int hash = itemId.IndexOf('#');
        if (hash < 0 || !uint.TryParse(itemId.AsSpan(hash + 1), out uint seed)) return null;
        return Generate(seed, naming);
    }

    /// <summary>The name for a product's seed (the number after '#').</summary>
    public static string? Generate(uint seed, ProceduralProductNaming naming)
    {
        if (naming.Formats.Count == 0) return null;
        var item = new UniverseRandom(StartState(ItemHash(seed)));
        item.Step(); // value
        item.Step(); // age
        int rarity = Rarity(item.NextWord(), naming.RarityWeights);
        ulong w1 = item.NextWord(), w2 = item.NextWord();

        var rng = new UniverseRandom(StartState(Mix64(Mix64((w2 << 32) | w1))));
        for (int i = 0; i < naming.LegacyRolls; i++) rng.Step();
        var picks = new List<(string Key, string Word)>(naming.Words.Count);
        foreach (var word in naming.Words)
        {
            var list = rarity switch
            {
                2 when word.Rare.Count > 0 => word.Rare,
                1 when word.Uncommon.Count > 0 => word.Uncommon,
                _ => word.Common,
            };
            // The game steps even for a word with no variants.
            uint index = rng.Next((uint)Math.Max(list.Count, 1));
            picks.Add((word.ReplaceKey, list.Count > 0 ? list[(int)index] : ""));
        }
        string name = naming.Formats[(int)rng.Next((uint)naming.Formats.Count)];
        foreach (var (key, word) in picks) name = name.Replace(key, word);
        return name;
    }

    /// <summary>Common 0, uncommon 1, rare 2: the first whose running total of
    /// DropWeight reaches ((word * total) &gt;&gt; 32) + 1.</summary>
    private static int Rarity(uint word, int[] weights)
    {
        long total = weights.Sum(w => (long)w);
        if (total <= 0) return 0;
        long roll = (long)(((ulong)word * (ulong)total) >> 32) + 1, running = 0;
        for (int i = 0; i < weights.Length; i++)
            if (roll <= (running += weights[i])) return i;
        return weights.Length - 1;
    }

    private static ulong ItemHash(uint s)
    {
        uint h = (s ^ 61) ^ (s >> 16);
        h *= 9;
        h ^= h >> 4;
        h *= 0x1b873593;
        return h ^ (h >> 15);
    }

    /// <summary>The game's 64-bit mixer.</summary>
    private static ulong Mix64(ulong v)
    {
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }

    /// <summary>The game's multiply-with-carry seeding (as MultiToolNames).</summary>
    private static ulong StartState(ulong seed)
    {
        ulong lo = seed & M32, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo;
        ulong s1 = (((lo << 16) | (lo >> 16)) & M32) ^ hi ^ lo;
        return (s1 << 32) | s0;
    }
}
