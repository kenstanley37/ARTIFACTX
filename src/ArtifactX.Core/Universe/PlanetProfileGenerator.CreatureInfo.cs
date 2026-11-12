namespace ArtifactX.Core.Universe;

/// <summary>
/// A predicted creature's species info, as the game's Discoveries and analysis visor show it. A species comes in two
/// forms: gender 1 with height/weight 1, gender 2 with height/weight 2; the Discoveries page shows the form you
/// scanned (the owner's 446 captured creatures with traits: 407 match one form exactly, the rest are captures without
/// a size or spelling, "Vectorized"). Gender1/Gender2 are 1-based SEX&lt;n&gt; numbers, or UI_ROBOT_GENDER_&lt;n&gt;
/// ones when RobotGenders (0 when not predicted). Rarity is the discovery list's (spawn data, see RarityFor).
/// </summary>
public sealed record PredictedCreatureInfo(float Height1, float Weight1, float Height2, float Weight2, int Rarity,
    int Gender1, int Gender2, bool AgeFlag, bool RobotGenders = false, string Behaviour = "", string Diet = "", string Notes = "",
    int AdultAge = 0, int BabyAge = 0, int Element = -1)
{
    /// <summary>"121.5kg / 3.0m", the game's own format ("%0.1fkg / %0.1fm"), for the first form.</summary>
    public string Size => SizeText(Weight1, Height1);

    /// <summary>The second form's weight and height, the same way.</summary>
    public string Size2 => SizeText(Weight2, Height2);

    /// <summary>printf rounds the float's exact value (146.9499... -> 146.9); .NET would round its shortest form
    /// (146.95 -> 147.0), so format through double.</summary>
    private static string SizeText(float weight, float height) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{(double)weight:0.0}kg / {(double)height:0.0}m");
}

public static partial class PlanetProfileGenerator
{
    private const float MaxInfoScale = 10.0f;      // 0x144bf6064
    private const float WeightPerScale = 40.0f;    // 0x144bf612c
    private const float AgeChance = 0.3f;          // 0x144bf51b0
    private const float RareBelow = 0.33f;         // 0x144bf52c8
    private const float UncommonBelow = 0.66f;     // 0x144bf5920
    private const float MinRarityScale = 0.75f;    // 0x144bf5a30

    /// <summary>
    /// NMS.exe 184271 0x1416cfcc0, run once per creature in GenerateCreatureRoles order, drawing from the planet
    /// generator's own stream (+0xba4, <see cref="RngStack.Root"/>; Ghidra 2026-11-12). Every text pick there
    /// (0x1416cdfb0) makes one draw to choose PREFIX&lt;n&gt;; picks on the creature's own seed don't touch this stream.
    /// In order, for a normal creature:
    ///   1. behaviour (by role) and diet text: one draw each;
    ///   2. Height1 = min(scale, 10) + f * 0.4 - 0.2, Weight1 = min(scale, 10) * 40 + f * 50,
    ///      Height2 = Height1 + f * 0.2, Weight2 = Weight1 + f * 15 (scale: <see cref="SpeciesScale"/>);
    ///   3. notes: one draw; gender 1: one draw over the SEX&lt;n&gt; words; gender 2: draws until it differs;
    ///      age: one draw, the flag set at 0.3 or below.
    /// Robots (diet Robot, ROBOT worlds) pick behaviour and diet from their own seed, and with ROBOTANTELOPE and
    /// WALKINGBUILDING also notes and genders; their age flag is always set. (This routine also sets GcCreatureInfo
    /// .Rarity from Height1 - above 2.0, above 1.1 - but the discovery list shows the spawn data's, RarityFor.)
    /// </summary>
    private static PredictedCreatureInfo? CreatureInfoFor(ref Rng root, PredictedCreature creature, string? ground, int biome, int subType,
        CreatureTables tables)
    {
        if (tables.Sizes is not { } sizes || tables.GenderWords is not { Length: > 0 } genders) return null;
        var entry = Array.Find(tables.Creatures, c => c.Id == creature.Id);
        bool robot = creature.Diet == 3 || ground == "ROBOT";
        string behaviour = "", diet = "";
        if (!robot)
        {
            // Behaviour by role, then diet by diet (water creatures: the water list), each one draw when the list has
            // words (0x1416cdfb0 draws only then). Diet text may name the biome's plant or meat. The water test is
            // the role data's TileType 4 in the game, taken here as "lives in water" (unconfirmed).
            var text = tables.Text;
            string[] behaviours = text is not null && creature.Role >= 0 && creature.Role < text.Behaviours.Length ? text.Behaviours[creature.Role] : [];
            behaviour = Pick(ref root, behaviours);
            var dietLists = creature.Domain == "water" ? text?.WaterDiets : text?.Diets;
            string[] diets = dietLists is not null && creature.Diet >= 0 && creature.Diet < dietLists.Length ? dietLists[creature.Diet] : [];
            diet = Pick(ref root, diets);
            int b = subType == SwampSub ? Swamp : subType == LavaSub ? Lava : biome;
            if (text is not null && b >= 0)
            {
                if (b < text.Plants.Length) diet = diet.Replace("%BIOME_PLANT%", text.Plants[b]);
                if (b < text.Meats.Length) diet = diet.Replace("%BIOME_MEAT%", text.Meats[b]);
            }
            if (text is null)
            {
                root.Next(); // behaviour
                root.Next(); // diet text
            }
        }

        float scale = entry is null ? 0 : SpeciesScale(creature.Seed, entry, creature.MinSize, creature.MaxSize, sizes).Scale;
        float s = Math.Min(scale, MaxInfoScale);
        float weightBase = s * WeightPerScale;
        float height1 = root.NextFraction() * 0.4f - 0.2f + s;
        float weight1 = root.NextFraction() * 50.0f + 0.0f + weightBase;
        float height2 = root.NextFraction() * 0.2f + 0.0f + height1;
        float weight2 = root.NextFraction() * 15.0f + 0.0f + weight1;

        int gender1 = 0, gender2 = 0;
        bool age = true;
        string notes = "";
        bool robotGenders = robot || creature.Id is "ROBOTANTELOPE" or "WALKINGBUILDING";
        if (robotGenders && tables.RobotGenderWords is { Length: > 0 } robotWords)
        {
            // On the creature's own stream: robots make five text picks (behaviour, three descriptions, diet), a
            // robot antelope or walking building on a normal world three (the normal path's descriptions); then the
            // robot note and its description, then gender 1 and gender 2 (no "must differ" here). Checked on the
            // owner's four robots from robot worlds (gender 1 = draw 8, all four); the normal-world case is the same
            // code but not yet confirmed by a capture.
            // Behaviour (draw 1) and diet (draw 4) here only for robots; the note is draw 6, or 4 on a normal world.
            // Robot behaviour, diet and notes follow the confirmed draw order but aren't checked against a scan yet.
            var own = Rng.Start(creature.Seed);
            var t = tables.Text;
            if (robot)
            {
                behaviour = Pick(ref own, t?.RobotBehaviours ?? []);
                own.Next(); own.Next(); // descriptions
                diet = Pick(ref own, t?.RobotDiets ?? []);
                own.Next(); // description
            }
            else
            {
                own.Next(); own.Next(); own.Next(); // the normal path's three descriptions
            }
            notes = Pick(ref own, t?.RobotNotes ?? []);
            own.Next(); // the note's description
            gender1 = (int)(((ulong)own.Next() * (ulong)robotWords.Length) >> 32) + 1;
            gender2 = (int)(((ulong)own.Next() * (ulong)robotWords.Length) >> 32) + 1;
        }
        else if (!robotGenders)
        {
            if (tables.Text?.Notes is { Length: > 0 } noteWords) notes = Pick(ref root, noteWords);
            else root.Next(); // notes
            int count = genders.Length;
            gender1 = (int)(((ulong)root.Next() * (ulong)count) >> 32) + 1;
            if (count > 1)
                do gender2 = (int)(((ulong)root.Next() * (ulong)count) >> 32) + 1;
                while (gender2 == gender1);
            age = root.NextFraction() <= AgeChance;
        }

        // The analysis visor's age word (NMS.exe 184271 0x1412e3f40): one draw w from the role seed (StartState, one
        // step); a baby is AGE (w * 3 >> 32) + 1 (Infant, Young, Juvenile), an adult of a regular species (w * 3 >> 32)
        // + 4 (Adult, Mature, Elderly), of a weird-age species (w >> 30) + 7 (Indeterminate ... Perpetual).
        var ageStream = Rng.Start(creature.Seed);
        uint w = ageStream.Next();
        int babyAge = (int)(((ulong)w * 3) >> 32) + 1;
        int adultAge = age ? (int)(w >> 30) + 7 : (int)(((ulong)w * 3) >> 32) + 4;
        return new PredictedCreatureInfo(height1, weight1, height2, weight2, RarityFor(creature, scale, tables.Globals),
            gender1, gender2, age, robotGenders, behaviour, diet, notes, adultAge, babyAge, ElementFor(creature, biome, subType, tables));
    }

    /// <summary>The pet-battle element the analysis visor shows (0x1412da090): the spawned creature data's
    /// PetBattlerForcedAffinity when set (1-8), else GcGameTableGlobals PetBiomeAffinities for the planet's biome as
    /// the adoption routine 0x1405110b0 maps it (swamp 12, lava 13, the anomaly biomes 8-10 as 7). -1 without the
    /// tables. The owner's three scans on a scorched planet: Fire.</summary>
    private static int ElementFor(PredictedCreature creature, int biome, int subType, CreatureTables tables)
    {
        if (tables.PetBattle is not { } battle) return -1;
        string id = string.IsNullOrEmpty(creature.SpeciesId) ? creature.Id : creature.SpeciesId;
        var data = Array.Find(tables.Creatures, c => c.Id == id) ?? Array.Find(tables.Creatures, c => c.Id == creature.Id);
        if (data is { ForcedAffinity: >= 1 and <= 8 }) return data.ForcedAffinity;
        int b = subType == SwampSub ? Swamp : subType == LavaSub ? Lava : biome is >= 8 and <= 10 ? 7 : biome;
        return b >= 0 && b < battle.BiomeAffinities.Length ? battle.BiomeAffinities[b] : -1;
    }

    /// <summary>One numbered word, as 0x1416cdfb0 picks it: a draw only when there are words, n = (w * count) &gt;&gt; 32.</summary>
    private static string Pick(ref Rng rng, string[] words)
    {
        if (words.Length == 0) return "";
        return words[(int)(((ulong)rng.Next() * (ulong)words.Length) >> 32)];
    }

    /// <summary>
    /// The rarity the discovery list shows (NMS.exe 184271 0x1416d9650, the role's spawn data; Ghidra 2026-11-12):
    /// groups per km divided by max(scale, 0.75), over the domain's groups per km for "very dense" (birds: air, then
    /// cave, water, ground); below 0.33 Rare, below 0.66 Uncommon, else Common.
    /// </summary>
    private static int RarityFor(PredictedCreature creature, float scale, CreatureGlobals g)
    {
        float[] reference = creature.Role == 5 ? g.AirGroups
            : creature.Domain switch { "cave" => g.CaveGroups, "water" => g.WaterGroups, _ => g.GroundGroups };
        if (reference.Length < 4 || reference[3] <= 0) return 0;
        float r = creature.GroupsPerKm / Math.Max(MinRarityScale, scale) / reference[3];
        return r < RareBelow ? 2 : r < UncommonBelow ? 1 : 0;
    }
}
