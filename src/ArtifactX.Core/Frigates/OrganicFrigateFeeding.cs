using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Frigates;

/// <summary>A frigatetraittable.mbin row: the stat it changes, its strength tier
/// (GcFrigateTraitStrength order, 0-2 negative) and its ChanceOfBeingOffered
/// per frigate class (GcFrigateClass order).</summary>
public sealed record FrigateTraitRule(string Id, int Stat, int Strength, int[] Chance);

/// <summary>
/// What frigate feeding needs from the player's game files (catalog row
/// FRIGATEFEEDING, CatalogSteps.AddFrigateFeeding): the trait table in its
/// order, each class's primary trait (GcFleetGlobals FrigateInitialStats
/// InitialTrait), DeepSpaceCommonPrimaryTraits, the stat change of each
/// strength per stat (FrigateTraitStrengths StatAlteration) and the foods
/// (products and substances flagged CookingIngredient).
/// </summary>
public sealed record FrigateFeedingRules(
    IReadOnlyList<FrigateTraitRule> Traits,
    IReadOnlyList<string> ClassPrimaryTraits,
    IReadOnlyList<string> CommonPrimaryTraits,
    IReadOnlyList<int[]> StatAlteration,
    IReadOnlyList<string> Foods);

/// <summary>A feed's outcome: the frigate's new traits and the change to each
/// stat (FrigateService.StatNames order). Refused means the game rejects the
/// food ("Insufficient nutrient variety") because the traits would not change.</summary>
public sealed record FrigateFeedResult(IReadOnlyList<string> Traits, int[] StatChange, bool Refused);

/// <summary>
/// "Feed Vessel" on an organic frigate, read off NMS.exe build 184271 on
/// 2026-11-04 (static disassembly) and checked against the owner's Slot 1: the
/// fed organic frigate at index 24 (ResourceSeed 0x3EBB7D4425806A04) has
/// ForcedTraitsSeed 0x6BA1EE1F81C91529. Of 19,779 catalog ids, only
/// FOOD_P_ALL1/2/3 (Heptaploid Wheat, Sweetroot, Pulpy Roots - the same first
/// 8 bytes) give that seed. Rerolling from it gives the frigate's first three
/// traits exactly (LIVING_MIN_PRI, LIVING_COM_T_1, LIVING_SPE_T_3). Its other
/// two differ, which fits two traits gained from expeditions after the feed.
///
/// The feed (0x140533070, called from the fleet screen at 0x1407e1d9f):
///   1. seed = CityHash Hash128to64(itemHash, ResourceSeed), where itemHash is
///      the first 8 bytes of the item id times 0x9ddfea08eb382d69, x ^= x &gt;&gt; 47,
///      times the constant again (as the Egg Sequencer). It is stored as
///      ForcedTraitsSeed when the feed is taken.
///   2. StartState(seed) and one step. Negative traits may be drawn when that
///      word is odd and the class is not DeepSpaceCommon (7).
///   3. Every trait is removed; the count n is kept. The primary trait is the
///      class's InitialTrait, or for class 7 one step picks
///      DeepSpaceCommonPrimaryTraits[(w * count) &gt;&gt; 32].
///   4. Until n traits: two steps give words a, b; the picker (0x14052e3b0)
///      seeds StartState(Mix64((b &lt;&lt; 32) | a)), takes one word and does a
///      weighted pick over the trait table by ChanceOfBeingOffered[class],
///      skipping traits held and (unless allowed) negative ones.
///   5. If the new set equals the old one, the feed is refused and nothing
///      changes. Stats move by each trait's StatAlteration[strength].
/// </summary>
public static class OrganicFrigateFeeding
{
    /// <summary>GcFrigateClass DeepSpaceCommon: the organic frigates.</summary>
    public const int OrganicClass = 7;

    private const ulong HashMultiplier = 0x9DDFEA08EB382D69;

    public static FrigateFeedResult Feed(string foodId, ulong resourceSeed, int frigateClass,
        IReadOnlyList<string> currentTraits, FrigateFeedingRules rules)
    {
        var current = currentTraits.Where(t => t.Length > 0).ToList();
        var byId = rules.Traits.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
        var rng = new UniverseRandom(StartState(Hash128(ItemHash(foodId.TrimStart('^')), resourceSeed)));
        bool allowNegative = (rng.NextWord() & 1) == 1 && frigateClass != OrganicClass;

        var picked = new List<FrigateTraitRule>();
        if (frigateClass == OrganicClass && rules.CommonPrimaryTraits.Count > 0)
            picked.Add(byId[rules.CommonPrimaryTraits[(int)rng.Next((uint)rules.CommonPrimaryTraits.Count)]]);
        else if (frigateClass < rules.ClassPrimaryTraits.Count && byId.TryGetValue(rules.ClassPrimaryTraits[frigateClass], out var primary))
            picked.Add(primary);

        bool Allowed(FrigateTraitRule t) => t.Strength > 2 || allowNegative;
        int Chance(FrigateTraitRule t) => frigateClass < t.Chance.Length ? t.Chance[frigateClass] : 0;
        long total = rules.Traits.Where(Allowed).Sum(t => (long)Chance(t));
        for (int guard = 0; picked.Count < current.Count && guard < 1000; guard++)
        {
            ulong a = rng.NextWord(), b = rng.NextWord();
            uint word = new UniverseRandom(StartState(Mix64((b << 32) | a))).NextWord();
            long remaining = total - picked.Where(Allowed).Sum(t => (long)Chance(t));
            if (remaining <= 0) break;
            long index = (long)(((ulong)word * (ulong)remaining) >> 32);
            foreach (var trait in rules.Traits)
            {
                if (picked.Contains(trait) || !Allowed(trait)) continue;
                if ((index -= Chance(trait)) < 0) { picked.Add(trait); break; }
            }
        }

        var ids = picked.Select(t => t.Id).ToList();
        bool refused = ids.Count == current.Count
            && current.All(c => ids.Contains(c, StringComparer.OrdinalIgnoreCase));
        var change = new int[rules.StatAlteration.Count];
        if (!refused)
        {
            foreach (var t in current) Apply(t, -1);
            foreach (var t in picked) Apply(t.Id, 1);
        }
        return new FrigateFeedResult(refused ? current : ids, change, refused);

        void Apply(string id, int sign)
        {
            if (!byId.TryGetValue(id, out var t) || t.Stat >= change.Length) return;
            var tiers = rules.StatAlteration[t.Stat];
            if (t.Strength < tiers.Length) change[t.Stat] += sign * tiers[t.Strength];
        }
    }

    /// <summary>Foods whose ids share their first 8 characters have the same
    /// effect; this is the key they share.</summary>
    public static string EffectKey(string foodId)
    {
        string id = foodId.TrimStart('^');
        return id.Length > 8 ? id[..8] : id;
    }

    private static ulong ItemHash(string id)
    {
        Span<byte> bytes = stackalloc byte[8];
        for (int i = 0; i < 8 && i < id.Length; i++) bytes[i] = (byte)id[i];
        ulong x = BitConverter.ToUInt64(bytes) * HashMultiplier;
        x ^= x >> 47;
        return x * HashMultiplier;
    }

    private static ulong Hash128(ulong low, ulong high)
    {
        ulong a = (low ^ high) * HashMultiplier;
        a ^= a >> 47;
        ulong b = (high ^ a) * HashMultiplier;
        b ^= b >> 47;
        return b * HashMultiplier;
    }

    private static ulong Mix64(ulong v)
    {
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }

    private static ulong StartState(ulong seed)
    {
        ulong lo = seed & 0xFFFFFFFF, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo;
        ulong s1 = (((lo << 16) | (lo >> 16)) & 0xFFFFFFFF) ^ hi ^ lo;
        return (s1 << 32) | s0;
    }
}
